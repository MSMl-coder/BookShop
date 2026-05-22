// NPCBrain.cs  [Фаза 1 — оновлено під новий NPCWorldUI]
// ЗМІНИ:
//   - Видалено NPCInteractionUI (_ui) — UI тепер через NPCWorldUI
//   - ShowingHint → видалено, замінено напряму WaitingForPlayer
//   - Додано _playerOfferAttempts лічильник
//   - ReceiveBookOffer: відмова тепер викликає ShowRejectionFeedback з причиною
//   - inspectTime тепер береться з NPCData (inspectTimeMin/Max)

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCBrain : MonoBehaviour
{
    // ── Public State ────────────────────────────────────────────
    public NPCData      Data         { get; private set; }
    public NPCState     CurrentState { get; private set; }
    public BookGenre    DesiredGenre { get; private set; }
    public BookTemplate FoundBook    { get; private set; }

    // ── Events ──────────────────────────────────────────────────
    public event Action<NPCState>     OnStateChanged;
    public event Action<BookTemplate> OnBookFound;
    public event Action               OnPurchaseComplete;
    public event Action               OnNPCLeft;

    // ── Private ─────────────────────────────────────────────────
    private NavMeshAgent  _agent;
    private NPCWorldUI    _worldUI;       // НОВЕ: тільки NPCWorldUI, без NPCInteractionUI
    private ShelfScanner  _scanner;
    private CashRegister  _cashRegister;

    private float       _stayTimer;
    private float       _enteringTimer;     // захист від миттєвого переходу з Entering
    private bool        _isDestroyPending;
    private List<Shelf> _visitedShelves     = new List<Shelf>();
    private Shelf       _currentTargetShelf;
    private bool        _isInitialized;
    private bool        _inspectStarted;    // захист від повторного запуску InspectShelf
    private int         _playerOfferAttempts; // скільки разів гравець вже пропонував книгу

    private Shelf[] _cachedShelves;

    // Резервування через новий Shelf data layer
    private Shelf _reservedShelf;
    private int   _reservedBookIndex = -1;   
     private string _reservedNpcID;      // ID для Unreserve
 
    public  NPCPersonality Personality        { get; private set; }

    // ── Init ────────────────────────────────────────────────────
    public void Initialize(NPCData data, CashRegister cashRegister)
    {
        Data          = data;
        _cashRegister = cashRegister;
        _agent        = GetComponent<NavMeshAgent>();
        _worldUI      = GetComponent<NPCWorldUI>();   // NPCWorldUI замість NPCInteractionUI
        _scanner      = GetComponent<ShelfScanner>();

        if (data.preferredGenres != null && data.preferredGenres.Length > 0)
            DesiredGenre = data.preferredGenres[UnityEngine.Random.Range(0, data.preferredGenres.Length)];
        Personality  = NPCPersonality.Generate(data);
        DesiredGenre = Personality.DesiredGenre; // для сумісності з UI
        _stayTimer   = Personality.StayDuration;
        _playerOfferAttempts  = 0;
        _inspectStarted       = false;
        _enteringTimer        = 0f;
        _isDestroyPending     = false;
        _reservedBookIndex    = -1;
        _reservedShelf        = null;
        _isInitialized        = true;

        // Unity 6: обов'язковий FindObjectsSortMode
        _cachedShelves = FindObjectsByType<Shelf>(FindObjectsSortMode.None);

        Debug.Log($"[NPC] {data.npcName} initialized. {Personality.DebugString()}. Shelves cached: {_cachedShelves.Length}");

        // ФІКС БАГ 2: встановити destination до зміни стану,
        // інакше AgentArrived() = true з першого кадру → миттєво пропускає Entering
        Vector3 entryTarget = transform.position;
        if (_cachedShelves != null && _cachedShelves.Length > 0)
            entryTarget = _cachedShelves[0].transform.position
                        + _cachedShelves[0].transform.forward * 2f;
        // Агент ще не має destination — він стоїть, тому спочатку ставимо ціль
        TrySetDestination(entryTarget);

        ChangeState(NPCState.Entering);
    }

    // ── Unity ───────────────────────────────────────────────────
    private void Update()
    {
        if (!_isInitialized) return;

        _stayTimer -= Time.deltaTime;

        if (_stayTimer <= 0f && CurrentState != NPCState.Leaving && CurrentState != NPCState.Buying)
        {
            Debug.Log($"[NPC] {Data.npcName} time expired → Leaving.");
            ChangeState(NPCState.Leaving);
            return;
        }

        UpdateCurrentState();
    }

    // ── State Machine ───────────────────────────────────────────
    private void UpdateCurrentState()
    {
        switch (CurrentState)
        {
            case NPCState.Entering:
                // Чекаємо поки агент отримає path і дійде до першої точки
                // _enteringTimer дає кадр щоб NavMesh встановив pathPending = true
                _enteringTimer += Time.deltaTime;
                if (_enteringTimer > 0.3f && AgentArrived())
                    ChangeState(NPCState.Browsing);
                break;

            case NPCState.Browsing:
                UpdateBrowsing();
                break;

            case NPCState.Inspecting:
                if (AgentArrived() && !_inspectStarted)
                {
                    _inspectStarted = true;
                    StartCoroutine(InspectShelf());
                }
                break;

            case NPCState.Buying:
                if (AgentArrived()) CompletePurchase();
                break;

            case NPCState.Leaving:
              if (_reservedShelf != null && _reservedBookIndex >= 0)
                {
                    _reservedShelf.UnreserveBook(_reservedBookIndex);
                    _reservedShelf = null;
                    _reservedBookIndex = -1;
                }
                if (AgentArrived()) DestroyNPC();
                break;

            // WaitingForPlayer: нічого в Update не робимо,
            // чекаємо виклику ReceiveBookOffer() або закінчення таймера
        }
    }

    private void UpdateBrowsing()
    {
        // Якщо агент ще рухається до поточної полиці — чекаємо
        if (!AgentArrived() && _currentTargetShelf != null) return;

        // Всі ліміт полиць вичерпано?
        if (_visitedShelves.Count >= Data.shelvesToInspect)
        {
            Debug.Log($"[NPC] {Data.npcName} checked {Data.shelvesToInspect} shelves. Waiting for player.");
            ChangeState(NPCState.WaitingForPlayer);
            return;
        }

        Shelf nextShelf = FindUnvisitedShelf();
        if (nextShelf != null)
        {
            _currentTargetShelf = nextShelf;
            _visitedShelves.Add(nextShelf);
            _inspectStarted = false;   // скидаємо перед новою полицею
            TrySetDestination(nextShelf.transform.position + nextShelf.transform.forward * 1.2f);
            ChangeState(NPCState.Inspecting);
        }
        else
        {
            // Більше нема невідвіданих полиць
            Debug.Log($"[NPC] {Data.npcName}: no more shelves → WaitingForPlayer.");
            ChangeState(NPCState.WaitingForPlayer);
        }
    }

   private IEnumerator InspectShelf()
{
    if (_currentTargetShelf == null) yield break;

    yield return StartCoroutine(LookAtTarget(_currentTargetShelf.transform.position));

    float inspectTime = UnityEngine.Random.Range(2f, 5f);
    yield return new WaitForSeconds(inspectTime);

  BookTemplate found = _scanner.FindBookOnShelf(_currentTargetShelf, Personality);


    if (found != null)
    {
        FoundBook = found;
        OnBookFound?.Invoke(found);

        // v4.2: резервуємо через індекс полиці, не BookWorldItem
        _reservedShelf = _currentTargetShelf;
        _reservedBookIndex = _currentTargetShelf.FindAvailableBookIndex(found.bookID);
        if (_reservedBookIndex >= 0)
            _currentTargetShelf.ReserveBook(_reservedBookIndex, gameObject.GetInstanceID().ToString());

        Debug.Log($"[NPC] {Data.npcName} found: {found.title}!");

        if (UnityEngine.Random.value <= Data.buyChance)
            ChangeState(NPCState.Buying);
        else
            ChangeState(NPCState.Leaving);
    }
    else
    {
        ChangeState(NPCState.Browsing);
    }
}

    // ── Book Offer (від гравця) ─────────────────────────────────

    public void ReceiveBookOffer(BookTemplate offeredBook)
    {
        if (offeredBook == null || CurrentState != NPCState.WaitingForPlayer)
        {
            Debug.LogWarning($"[NPC] ReceiveBookOffer: некоректний стан або null книга.");
            return;
        }

        // Перевіряємо ліміт спроб
        if (_playerOfferAttempts >= Data.maxPlayerOfferAttempts)
        {
            Debug.Log($"[NPC] {Data.npcName}: ліміт спроб вичерпано → Leaving.");
            ChangeState(NPCState.Leaving);
            return;
        }

        _playerOfferAttempts++;

        bool genreOk  = offeredBook.genre == DesiredGenre;
        bool priceOk  = offeredBook.sellPrice <= Data.maxBudget;
        bool chanceOk = UnityEngine.Random.value <= Data.buyChance;

        if (genreOk && priceOk && chanceOk)
        {
            FoundBook = offeredBook;
            Debug.Log($"[NPC] {Data.npcName} прийняв: {offeredBook.title}");
            ChangeState(NPCState.Buying);
        }
        else
        {
            // Причина відмови
            string reason = "";
            if (!genreOk)  reason = "Не мій жанр...";
            else if (!priceOk) reason = "Трохи дорогувато.";
            else           reason = "Може щось інше?";

            Debug.Log($"[NPC] {Data.npcName} відмовив: {reason} (спроба {_playerOfferAttempts}/{Data.maxPlayerOfferAttempts})");

            _worldUI?.ShowRejectionFeedback(reason);

            // Якщо спроби вичерпані — йде
            if (_playerOfferAttempts >= Data.maxPlayerOfferAttempts)
            {
                StartCoroutine(LeaveAfterDelay(2f));
            }
        }
    }

    private IEnumerator LeaveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (CurrentState == NPCState.WaitingForPlayer)
            ChangeState(NPCState.Leaving);
    }

    // ── Purchase ────────────────────────────────────────────────

  private void CompletePurchase()
{
    if (FoundBook == null) return;

    // v4.2: забираємо книгу по індексу
    if (_reservedShelf != null && _reservedBookIndex >= 0)
    {
        _reservedShelf.TakeBookAt(_reservedBookIndex);
        _reservedShelf = null;
        _reservedBookIndex = -1;
    }

    EconomyManager.Instance?.RecordBookSold(FoundBook.sellPrice);
    Personality.BooksBought++;
    OnPurchaseComplete?.Invoke();
    Debug.Log($"[NPC] {Data.npcName} bought {FoundBook.title} for ${FoundBook.sellPrice}");

    if (Personality.WantsMoreBooks)
    {
        _visitedShelves.Clear(); // обходить знову
        ChangeState(NPCState.Browsing);
    }
    else
    {
        ChangeState(NPCState.Leaving);
    }
}

    private void ClearReservation()
    {
        if (_reservedShelf != null && _reservedBookIndex >= 0)
            _reservedShelf.UnreserveBook(_reservedBookIndex);
        _reservedShelf     = null;
        _reservedBookIndex = -1;
        _reservedNpcID     = null;
    }

    // ── Change State ────────────────────────────────────────────

    public void ChangeState(NPCState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        Debug.Log($"[NPC] {Data.npcName} → {newState}");

        switch (newState)
        {
            case NPCState.Buying:
                if (_cashRegister != null)
                {
                    Vector3 queuePos = _cashRegister.GetQueuePosition();
                    TrySetDestination(queuePos);
                    Debug.Log($"[NPC] {Data.npcName}: → каса, destination={queuePos}");
                }
                else
                {
                    Debug.LogError($"[NPC] {Data.npcName}: _cashRegister == null! " +
                                   "Перевір що NPCSpawner.cashRegister призначено в Inspector.");
                    // Немає каси — просто йдемо
                    ChangeState(NPCState.Leaving);
                }
                break;

            case NPCState.Leaving:
                ClearReservation();
                var spawner = NPCSpawner.Instance;
                if (spawner != null)
                    TrySetDestination(spawner.ExitPoint.position);
                break;
        }
    }

    // ── EndDay API ──────────────────────────────────────────────

   public BookInstance ForceLeaveAndTakeBook()
{
    BookInstance result = null;

    if (_reservedShelf != null && _reservedBookIndex >= 0)
    {
        var entry = _reservedShelf.GetBookData(_reservedBookIndex);
        result = new BookInstance(entry.templateID);
        result.instanceID = entry.instanceID;

        _reservedShelf.TakeBookAt(_reservedBookIndex);
        _reservedShelf = null;
        _reservedBookIndex = -1;
        FoundBook = null;

        Debug.Log($"[NPC] {Data?.npcName}: EndDay → '{result.templateID}' → стіл каси.");
    }

    if (_isInitialized && CurrentState != NPCState.Leaving)
        ChangeState(NPCState.Leaving);

    return result;
}

    // ── Public Timer API ────────────────────────────────────────

    public float GetRemainingTimeNormalized() => Mathf.Clamp01(_stayTimer / Data.stayDuration);
    public float GetRemainingTime()           => Mathf.Max(0f, _stayTimer);

    // ── Helpers ─────────────────────────────────────────────────



    private Shelf FindUnvisitedShelf()
    {
        if (_cachedShelves == null || _cachedShelves.Length == 0) return null;

        var withGenre    = new List<Shelf>();
        var withoutGenre = new List<Shelf>();

        foreach (var s in _cachedShelves)
        {
            if (s == null || _visitedShelves.Contains(s)) continue;
            if (s.ZoneType != ShopZoneType.Storefront) continue;
            if (s.GetBookCount() == 0) continue;

            // ShelfGenreInfo.HasGenre — легка перевірка без виклику FindBookOnShelf
            if (ShelfGenreInfo.HasGenre(s, DesiredGenre))
                withGenre.Add(s);
            else
                withoutGenre.Add(s);
        }

        if (withGenre.Count > 0)
            return withGenre[UnityEngine.Random.Range(0, withGenre.Count)];

        if (withoutGenre.Count > 0)
            return withoutGenre[UnityEngine.Random.Range(0, withoutGenre.Count)];

        return null;
    }

    private bool AgentArrived()
    {
        if (_agent == null || !_agent.isOnNavMesh || !_agent.enabled) return false;
        if (_agent.pathPending) return false;
        return _agent.remainingDistance <= _agent.stoppingDistance
            && _agent.velocity.sqrMagnitude < 0.04f;
    }

    private bool TrySetDestination(Vector3 destination)
    {
        if (_agent == null || !_agent.isOnNavMesh || !_agent.enabled)
        {
            Debug.LogWarning($"[NPC] {Data?.npcName}: не на NavMesh → SetDestination пропущено.");
            return false;
        }
        _agent.SetDestination(destination);
        return true;
    }

    private IEnumerator LookAtTarget(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;
        if (dir == Vector3.zero) yield break;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 4f;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
            yield return null;
        }
    }

    private void DestroyNPC()
    {
        if (_isDestroyPending) return; // захист від подвійного виклику
        _isDestroyPending = true;
        OnNPCLeft?.Invoke();
        Destroy(gameObject, 0.5f);
    }
}