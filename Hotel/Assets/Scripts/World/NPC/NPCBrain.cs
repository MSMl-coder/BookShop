// Assets/Scripts/World/NPC/NPCBrain.cs  v8
// ЗМІНИ:
//   [1] Порожня полиця: InspectShelf виходить миттєво (0.3с) якщо shelf.GetBookCount() == 0
//   [2] Shelf re-add: підписуємось на Shelf.OnBookPlaced → видаляємо з _visitedShelves
//   [3] Shelf.OnBookPlaced патч (додати в Shelf.cs) описано в коментарях

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NPCStatsTicker))]
public class NPCBrain : MonoBehaviour
{
    public NPCData        Data         { get; private set; }
    public NPCState       CurrentState { get; private set; }
    public BookGenre      DesiredGenre { get; private set; }
    public BookTemplate   FoundBook    { get; private set; }
    public NPCPersonality Personality  { get; private set; }
    public NPCStatsTicker Ticker       { get; private set; }

    public event Action<NPCState>     OnStateChanged;
    public event Action<BookTemplate> OnBookFound;
    public event Action               OnPurchaseComplete;
    public event Action               OnNPCLeft;

    private NavMeshAgent  _agent;
    private NPCWorldUI    _worldUI;
    private ShelfScanner  _scanner;
    private CashRegister  _cashRegister;

    private float       _enteringTimer;
    private bool        _isDestroyPending;
    private List<Shelf> _visitedShelves = new List<Shelf>();
    private HashSet<Shelf> _fullShelves = new HashSet<Shelf>();
    private Shelf       _currentTargetShelf;
    private bool        _isInitialized;
    private bool        _inspectStarted;
    private int         _playerOfferAttempts;
    private Shelf[]     _cachedShelves;

    private Shelf  _reservedShelf;
    private int    _reservedBookIndex = -1;

    private bool        _restingArrived;
    private float       _restingTimer;
    private const float MAX_REST_DURATION = 30f;

    private float       _inspectingTimer;
    private const float INSPECTING_TIMEOUT = 5f;

    private float _waitingTimer;
    [SerializeField] private float waitingForPlayerTimeout = 30f;

    // ── Init ────────────────────────────────────────────────────
    public void Initialize(NPCData data, CashRegister cashRegister)
    {
        Data          = data;
        _cashRegister = cashRegister;
        _agent        = GetComponent<NavMeshAgent>();
        _worldUI      = GetComponent<NPCWorldUI>();
        _scanner      = GetComponent<ShelfScanner>();
        Ticker        = GetComponent<NPCStatsTicker>();

        Personality  = NPCPersonality.Generate(data);
        DesiredGenre = Personality.DesiredGenre;
        Ticker.Initialize(Personality);

        _playerOfferAttempts = 0;
        _inspectStarted      = false;
        _enteringTimer       = 0f;
        _inspectingTimer     = 0f;
        _waitingTimer        = 0f;
        _isDestroyPending    = false;
        _reservedBookIndex   = -1;
        _reservedShelf       = null;
        _isInitialized       = true;

        int level = NPCLevelCalculator.Calculate(Personality);
        GetComponent<NPCLevelBadge>()?.SetLevel(level);

        var all = FindObjectsByType<Shelf>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        _cachedShelves = Shuffle(System.Array.FindAll(all, s => s.ZoneType == ShopZoneType.Storefront));

        // [FIX 2] Підписуємось на OnBookPlaced для кожної полиці
        // Це дозволяє переглянути полицю знову якщо гравець поставив на неї книгу
        foreach (var shelf in _cachedShelves)
            if (shelf != null) shelf.OnBookPlaced += OnShelfBookAdded;

        Debug.Log($"[NPC] {data.npcName} Lv{level}: {Personality.DebugString()} | shelves={_cachedShelves.Length}");

        Vector3 entry = _cachedShelves.Length > 0
            ? SampleNavMesh(_cachedShelves[0].transform.position + _cachedShelves[0].transform.forward * 1.5f)
            : transform.position;
        TrySetDestination(entry);
        ChangeState(NPCState.Entering);
    }

    private void OnEnable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= OnGameStateChanged;

        // Відписуємось від всіх полиць при знищенні
        if (_cachedShelves != null)
            foreach (var shelf in _cachedShelves)
                if (shelf != null) shelf.OnBookPlaced -= OnShelfBookAdded;
    }

    private void Update()
    {
        if (!_isInitialized) return;
        UpdateCurrentState();
    }

    // ── [FIX 2] Коли гравець поставив книгу на полицю ────────────
    // Shelf.cs має мати: public event Action OnBookPlaced;
    // Викликати в PlaceBook(): OnBookPlaced?.Invoke();
    private void OnShelfBookAdded(Shelf shelf)
    {
        if (shelf == null) return;
        if (!_visitedShelves.Contains(shelf)) return;

        // Видаляємо з відвіданих — бот зможе переглянути знову
        _visitedShelves.Remove(shelf);
        Debug.Log($"[NPC] {Data?.npcName}: shelf '{shelf.name}' got a book → re-added to visit list");
    }

    private void OnGameStateChanged(GameState state)
    {
        if ((state == GameState.DayStats || state == GameState.LootPhase)
            && CurrentState != NPCState.Leaving && !_isDestroyPending)
            ForceLeaveAndTakeBook();
    }

    // ── Public API ───────────────────────────────────────────────
    public float GetRemainingTimeNormalized() =>
        Ticker?.Stats == null ? 0f
        : Mathf.InverseLerp(NPCStats.MIN, NPCStats.MAX, Ticker.Stats.Patience);

    public void OnNPCClicked()
    {
           Debug.Log($"[NPCBrain] OnNPCClicked: {Data?.npcName}");
 
    // Новий UI v2 — NPCInspectorMount
    if (NPCInspectorMount.Instance != null)
    {
        NPCInspectorMount.Instance.Show(this);
        return;
    }
 
    // Fallback до старого UI якщо v2 не змонтований
    NPCInspectorPanel.Instance?.Show(this);
        }


    public BookInstance ForceLeaveAndTakeBook()
    {
        BookInstance held = null;
        if (_reservedShelf != null && _reservedBookIndex >= 0)
        {
            var e = _reservedShelf.GetBookData(_reservedBookIndex);
            if (!string.IsNullOrEmpty(e.templateID)) held = new BookInstance(e.templateID);
            _reservedShelf.TakeBookAt(_reservedBookIndex);
            _reservedShelf = null; _reservedBookIndex = -1;
        }
        if (CurrentState == NPCState.Resting && _restingArrived) Ticker.StopResting();
        SeatRegistry.Instance?.Release(Personality.UniqueID);
        ReleaseCurrentShelf();
        ChangeState(NPCState.Leaving);
        return held;
    }

    // ── State Machine ─────────────────────────────────────────────
    private void UpdateCurrentState()
    {
        switch (CurrentState)
        {
            case NPCState.Entering:
                _enteringTimer += Time.deltaTime;
                if (_enteringTimer > 0.3f && AgentArrived()) ChangeState(NPCState.Browsing);
                break;
            case NPCState.Browsing:   UpdateBrowsing();   break;
            case NPCState.Inspecting: UpdateInspecting(); break;
            case NPCState.Resting:    UpdateResting();    break;
            case NPCState.WaitingForPlayer:
                _waitingTimer += Time.deltaTime;
                if (_waitingTimer >= waitingForPlayerTimeout) ChangeState(NPCState.Leaving);
                break;
            case NPCState.Buying:
                if (AgentArrived()) CompletePurchase();
                break;
            case NPCState.Leaving:
                if (_reservedShelf != null && _reservedBookIndex >= 0)
                {
                    _reservedShelf.UnreserveBook(_reservedBookIndex);
                    _reservedShelf = null; _reservedBookIndex = -1;
                }
                if (AgentArrived()) DestroyNPC();
                break;
        }
    }

    // ── Browsing ─────────────────────────────────────────────────
    private void UpdateBrowsing()
    {
        if (!AgentArrived() && _currentTargetShelf != null) return;
        if (Ticker.WantsRest && TryBeginResting()) return;

        int limit = Data.shelvesToInspect + (Ticker.ExtraShelf ? 1 : 0);
        if (_visitedShelves.Count >= limit)
        {
            _currentTargetShelf = null;
            ChangeState(NPCState.WaitingForPlayer);
            return;
        }

        Shelf next = FindNextShelf(limit);
        if (next == null)
        {
            if (_fullShelves.Count > 0) { _fullShelves.Clear(); return; }
            _currentTargetShelf = null;
            ChangeState(NPCState.WaitingForPlayer);
            return;
        }

        if (!ShelfAccessRegistry.Instance.TryClaim(next, Personality.UniqueID, out Vector3 standPos))
        {
            _fullShelves.Add(next);
            return;
        }

        _currentTargetShelf = next;
        _visitedShelves.Add(next);
        _fullShelves.Clear();
        _inspectStarted  = false;
        _inspectingTimer = 0f;

        TrySetDestination(SampleNavMesh(standPos));
        ChangeState(NPCState.Inspecting);
    }

    // ── Inspecting ────────────────────────────────────────────────
    private void UpdateInspecting()
    {
        _inspectingTimer += Time.deltaTime;
        if ((_inspectingTimer >= INSPECTING_TIMEOUT || AgentArrived()) && !_inspectStarted)
        {
            _inspectStarted = true;
            StartCoroutine(InspectShelf());
        }
    }

    // ── Resting ───────────────────────────────────────────────────
    private bool TryBeginResting()
    {
        if (SeatRegistry.Instance == null) return false;
        if (!SeatRegistry.Instance.TryClaim(Personality.UniqueID, out _, out Vector3 seatPos)) return false;
        TrySetDestination(seatPos);
        _restingArrived = false; _restingTimer = 0f;
        ChangeState(NPCState.Resting);
        return true;
    }

    private void UpdateResting()
    {
        if (!_restingArrived)
        {
            if (!AgentArrived()) return;
            _restingArrived = true;
            if (SeatRegistry.Instance.TryClaim(Personality.UniqueID, out PropTemplate prop, out _))
                Ticker.BeginResting(prop.comfortForce, prop.patienceRestoreBonus);
        }
        _restingTimer += Time.deltaTime;
        if (!Ticker.WantsRest || _restingTimer >= MAX_REST_DURATION)
        {
            Ticker.StopResting();
            SeatRegistry.Instance?.Release(Personality.UniqueID);
            _fullShelves.Clear();
            ChangeState(NPCState.Browsing);
        }
    }

    // ── InspectShelf ─────────────────────────────────────────────
    private IEnumerator InspectShelf()
    {
        // [FIX 1] Якщо полиця ПОРОЖНЯ — не чекаємо, виходимо за ~0.3с
        bool shelfIsEmpty = _currentTargetShelf == null
                            || _currentTargetShelf.GetBookCount() == 0;

        if (shelfIsEmpty)
        {
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[NPC] {Data.npcName}: shelf '{_currentTargetShelf?.name}' is empty → skip");
            ReleaseCurrentShelf();
            ChangeState(NPCState.Browsing);
            yield break;
        }

        // Полиця не порожня — стандартний огляд
        yield return new WaitForSeconds(Personality.GetInspectTime());
        if (CurrentState != NPCState.Inspecting) yield break;

        var result = _scanner?.FindBook(_currentTargetShelf, Personality);
        ReleaseCurrentShelf();

        if (result.HasValue)
        {
            FoundBook = result.Value.template;
            OnBookFound?.Invoke(FoundBook);

            float chance = Ticker.GetAdjustedBuyChance(Personality.BuyChance);
            bool  buy    = UnityEngine.Random.value <= chance || Ticker.CanImpulseBuy;

            if (buy)
            {
                _reservedShelf     = _currentTargetShelf;
                _reservedBookIndex = result.Value.index;
                _currentTargetShelf?.ReserveBook(result.Value.index, Personality.UniqueID);
                ChangeState(NPCState.Buying);
            }
            else { ChangeState(NPCState.Browsing); }
        }
        else
        {
            // Є книги, але не підходять по жанру/рарності/ціні
            ChangeState(NPCState.Browsing);
        }
    }

    // ── Purchase ─────────────────────────────────────────────────
    private void CompletePurchase()
    {
        if (FoundBook == null) return;
        if (_reservedShelf != null && _reservedBookIndex >= 0)
        {
            _reservedShelf.TakeBookAt(_reservedBookIndex);
            _reservedShelf = null; _reservedBookIndex = -1;
        }
        EconomyManager.Instance?.RecordBookSold(FoundBook.sellPrice);
        Personality.BooksBought++;
        Ticker.RegisterPurchase(FoundBook.sellPrice, Personality.MaxBudget);
        OnPurchaseComplete?.Invoke();

        if (Personality.WantsMoreBooks && !Ticker.IsBudgetLow)
        {
            _visitedShelves.Clear(); _fullShelves.Clear();
            ChangeState(NPCState.Browsing);
        }
        else { ChangeState(NPCState.Leaving); }
    }

    // ── Destroy ───────────────────────────────────────────────────
    private void DestroyNPC()
    {
        if (_isDestroyPending) return;
        _isDestroyPending = true;
        NPCInspectorPanel.Instance?.HideIfShowing(this);
        SeatRegistry.Instance?.Release(Personality.UniqueID);
        ShelfAccessRegistry.Instance?.ReleaseAll(Personality.UniqueID);
        Ticker.Deactivate();
        OnNPCLeft?.Invoke();
        Destroy(gameObject);
    }

    // ── ChangeState ───────────────────────────────────────────────
    public void ChangeState(NPCState newState)
    {
        if (CurrentState == NPCState.Resting && newState != NPCState.Resting)
        {
            if (_restingArrived) Ticker.StopResting();
            SeatRegistry.Instance?.Release(Personality.UniqueID);
        }
        if (CurrentState == NPCState.Inspecting && newState != NPCState.Inspecting)
            ReleaseCurrentShelf();
        if (newState == NPCState.WaitingForPlayer)
            _waitingTimer = 0f;

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);

        switch (newState)
        {
            case NPCState.Inspecting: _inspectingTimer = 0f; _inspectStarted = false; break;
            case NPCState.Buying:
                if (_cashRegister != null) TrySetDestination(_cashRegister.GetQueuePosition());
                break;
            case NPCState.Leaving:
                TrySetDestination(NPCSpawner.Instance?.ExitPoint?.position ?? transform.position);
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────
    private void ReleaseCurrentShelf()
    {
        if (_currentTargetShelf == null) return;
        ShelfAccessRegistry.Instance?.Release(_currentTargetShelf, Personality.UniqueID);
        _currentTargetShelf = null;
    }

    private Shelf FindNextShelf(int limit)
    {
        if (_cachedShelves == null || _visitedShelves.Count >= limit) return null;
        foreach (var s in _cachedShelves)
        {
            if (s == null) continue;
            if (_visitedShelves.Contains(s)) continue;
            if (_fullShelves.Contains(s))    continue;
            return s;
        }
        return null;
    }

    private static Shelf[] Shuffle(Shelf[] arr)
    {
        var list = new List<Shelf>(arr);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list.ToArray();
    }

    private bool AgentArrived() =>
        _agent != null && !_agent.pathPending
        && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f;

    private void TrySetDestination(Vector3 pos)
    {
        if (_agent != null && _agent.isOnNavMesh) _agent.SetDestination(pos);
    }

    private Vector3 SampleNavMesh(Vector3 pos) =>
        NavMesh.SamplePosition(pos, out NavMeshHit h, 2f, NavMesh.AllAreas) ? h.position : pos;

    // ── Player interaction ─────────────────────────────────────────
    public void ReceiveBookOffer(BookTemplate offeredBook)
    {
        if (CurrentState != NPCState.WaitingForPlayer) return;
        _playerOfferAttempts++;
        if (offeredBook != null
            && Personality.AcceptsRarity(offeredBook.rarity)
            && offeredBook.genre == Personality.DesiredGenre
            && offeredBook.sellPrice <= Personality.MaxBudget * (Ticker.Stats.Wallet / 100f))
        {
            FoundBook = offeredBook;
            OnBookFound?.Invoke(FoundBook);
            ChangeState(NPCState.Buying);
        }
        else
        {
            string reason = offeredBook == null ? "Це не книга..."
                : offeredBook.genre != Personality.DesiredGenre ? "Не мій жанр."
                : !Personality.AcceptsRarity(offeredBook.rarity) ? "Не та якість."
                : "Задорого.";
            _worldUI?.ShowRejectionFeedback(reason);
            if (_playerOfferAttempts >= Data.maxPlayerOfferAttempts)
                StartCoroutine(LeaveAfterDelay(2f));
        }
    }

    private IEnumerator LeaveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (CurrentState == NPCState.WaitingForPlayer) ChangeState(NPCState.Leaving);
    }
}