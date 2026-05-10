// ═══════════════════════════════════════════════════════════════════════════
// ПАТЧ до Assets/Scripts/World/NPC/NPCBrain.cs  — Фаза 1 / EndDay підтримка
// ═══════════════════════════════════════════════════════════════════════════
//
// Додай наступне в клас NPCBrain:
//
// 1. Нове приватне поле (в секції Private Fields):
//
//    private BookWorldItem _reservedBookWorldItem;
//
//
// 2. В методі InspectShelf() — після рядка "FoundBook = found;" додай:
//
//    // Резервуємо книгу в 3D-сцені
//    var bwi = _currentTargetShelf
//        .GetComponentsInChildren<BookWorldItem>()
//        .FirstOrDefault(b => b.instance?.templateID == found.templateID);
//    if (bwi != null && bwi.Reserve(this))
//        _reservedBookWorldItem = bwi;
//
//    (додай using System.Linq; в початок файлу якщо ще немає)
//
//
// 3. В методі CompletePurchase() — на початку методу додай:
//
//    // Знімаємо резервацію після успішної покупки
//    _reservedBookWorldItem?.Unreserve();
//    _reservedBookWorldItem = null;
//
//
// 4. В методі ChangeState() — у case NPCState.Leaving додай:
//
//    // Знімаємо резервацію якщо NPC йде без покупки
//    _reservedBookWorldItem?.Unreserve();
//    _reservedBookWorldItem = null;
//
//
// 5. НОВИЙ PUBLIC МЕТОД — додай у клас NPCBrain:

/*
/// Викликається CashDeskBuffer при EndDay.
/// NPC примусово переходить у стан Leaving та повертає BookInstance
/// зарезервованої/знайденої книги (без нарахування грошей).
/// Повертає null якщо NPC не тримав книгу.
public BookInstance ForceLeaveAndTakeBook()
{
    BookInstance result = null;

    // Якщо NPC знайшов/зарезервував книгу — забираємо її
    if (_reservedBookWorldItem != null)
    {
        result = _reservedBookWorldItem.instance;

        // Знімаємо резервацію та прибираємо книгу з полиці
        _reservedBookWorldItem.Unreserve();
        if (_reservedBookWorldItem.parentShelf != null)
            _reservedBookWorldItem.parentShelf.RemoveBook(_reservedBookWorldItem.gameObject);

        _reservedBookWorldItem = null;
        FoundBook = null;

        Debug.Log($"[NPCBrain] {Data?.npcName}: EndDay — книга '{result?.templateID}' вилучена.");
    }

    // Примусово відправляємо NPC до виходу (якщо він ще активний)
    if (CurrentState != NPCState.Leaving && _isInitialized)
        ChangeState(NPCState.Leaving);

    return result;
}
*/

// ═══════════════════════════════════════════════════════════════════════════
// Повний оновлений NPCBrain.cs з усіма змінами вбудованими:
// ═══════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCBrain : MonoBehaviour
{
    // ── Public State ────────────────────────────────────────────
    public NPCData      Data         { get; private set; }
    public NPCState     CurrentState { get; private set; }
    public BookGenre    DesiredGenre { get; private set; }
    public BookTemplate FoundBook    { get; private set; }

    // ── Events ──────────────────────────────────────────────────
    public event Action<NPCState>    OnStateChanged;
    public event Action<BookTemplate> OnBookFound;
    public event Action              OnPurchaseComplete;
    public event Action              OnNPCLeft;

    // ── Private ─────────────────────────────────────────────────
    private NavMeshAgent   _agent;
    private NPCInteractionUI _ui;
    private ShelfScanner   _scanner;
    private CashRegister   _cashRegister;

    private float         _stayTimer;
    private float         _browseTimer;
    private List<Shelf>   _visitedShelves   = new List<Shelf>();
    private Shelf         _currentTargetShelf;
    private bool          _isInitialized;
    private Shelf[]       _cachedShelves;

    // НОВЕ — Фаза 1: зарезервована книга в 3D-сцені
    private BookWorldItem _reservedBookWorldItem;

    // ── Init ────────────────────────────────────────────────────
    public void Initialize(NPCData data, CashRegister cashRegister)
    {
        Data          = data;
        _cashRegister = cashRegister;
        _agent        = GetComponent<NavMeshAgent>();
        _ui           = GetComponent<NPCInteractionUI>();
        _scanner      = GetComponent<ShelfScanner>();

        if (data.preferredGenres != null && data.preferredGenres.Length > 0)
            DesiredGenre = data.preferredGenres[UnityEngine.Random.Range(0, data.preferredGenres.Length)];

        _stayTimer     = data.stayDuration;
        _isInitialized = true;
        _cachedShelves = FindObjectsByType<Shelf>(FindObjectsSortMode.None);

        Debug.Log($"[NPC] {data.npcName} initialized. Wants: {DesiredGenre}. Shelves: {_cachedShelves.Length}");
        ChangeState(NPCState.Entering);
    }

    // ── Unity ───────────────────────────────────────────────────
    private void Update()
    {
        if (!_isInitialized) return;

        _stayTimer -= Time.deltaTime;

        if (_stayTimer <= 0f && CurrentState != NPCState.Leaving && CurrentState != NPCState.Buying)
        {
            Debug.Log($"[NPC] {Data.npcName} time expired. Leaving.");
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
                if (AgentArrived()) ChangeState(NPCState.Browsing);
                break;
            case NPCState.Browsing:
                UpdateBrowsing();
                break;
            case NPCState.Inspecting:
                if (AgentArrived()) StartCoroutine(InspectShelf());
                break;
            case NPCState.Buying:
                if (AgentArrived()) CompletePurchase();
                break;
            case NPCState.Leaving:
                if (AgentArrived()) DestroyNPC();
                break;
        }
    }

    private void UpdateBrowsing()
    {
        _browseTimer += Time.deltaTime;

        if (AgentArrived() || _currentTargetShelf == null)
        {
            Shelf next = FindUnvisitedShelf();
            if (next != null)
            {
                _currentTargetShelf = next;
                _visitedShelves.Add(next);
                _agent.SetDestination(next.transform.position + next.transform.forward * 1.2f);
                ChangeState(NPCState.Inspecting);
            }
            else
            {
                ChangeState(NPCState.ShowingHint);
            }
        }

        if (_browseTimer >= Data.browseTimeBeforeHint && CurrentState == NPCState.Browsing)
            ChangeState(NPCState.ShowingHint);
    }

    private IEnumerator InspectShelf()
    {
        if (_currentTargetShelf == null) yield break;

        yield return StartCoroutine(LookAtTarget(_currentTargetShelf.transform.position));

        float inspectTime = UnityEngine.Random.Range(2f, 5f);
        yield return new WaitForSeconds(inspectTime);

        BookTemplate found = _scanner.FindBookOnShelf(_currentTargetShelf, DesiredGenre, Data.maxBudget);

        if (found != null)
        {
            FoundBook = found;
            OnBookFound?.Invoke(found);

            // НОВЕ — Фаза 1: резервуємо BookWorldItem
            var bwi = _currentTargetShelf
                .GetComponentsInChildren<BookWorldItem>()
                .FirstOrDefault(b => b.instance?.templateID == found.templateID && !b.IsReserved);

            if (bwi != null && bwi.Reserve(this))
                _reservedBookWorldItem = bwi;

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

    public void ReceiveBookOffer(BookTemplate offeredBook)
    {
        if (offeredBook == null ||
            (CurrentState != NPCState.ShowingHint && CurrentState != NPCState.WaitingForPlayer))
            return;

        if (offeredBook.genre == DesiredGenre && offeredBook.sellPrice <= Data.maxBudget)
        {
            FoundBook = offeredBook;
            Debug.Log($"[NPC] {Data.npcName} accepted offer: {offeredBook.title}");
            ChangeState(NPCState.Buying);
        }
        else
        {
            Debug.Log($"[NPC] {Data.npcName} rejected offer.");
            _ui?.ShowRejection();
        }
    }

    private void CompletePurchase()
    {
        if (FoundBook == null) return;

        // НОВЕ — Фаза 1: знімаємо резервацію після покупки
        _reservedBookWorldItem?.Unreserve();
        _reservedBookWorldItem = null;

        EconomyManager.Instance?.RecordBookSold(FoundBook.sellPrice);
        OnPurchaseComplete?.Invoke();
        Debug.Log($"[NPC] {Data.npcName} bought {FoundBook.title} for ${FoundBook.sellPrice}");

        ChangeState(NPCState.Leaving);
    }

    public void ChangeState(NPCState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        Debug.Log($"[NPC] {Data.npcName} → {newState}");

        switch (newState)
        {
            case NPCState.Buying:
                if (_cashRegister != null)
                    _agent.SetDestination(_cashRegister.GetQueuePosition());
                break;

            case NPCState.Leaving:
                // НОВЕ — Фаза 1: знімаємо резервацію якщо NPC йде без покупки
                _reservedBookWorldItem?.Unreserve();
                _reservedBookWorldItem = null;

                var spawner = NPCSpawner.Instance;
                if (spawner != null)
                    _agent.SetDestination(spawner.ExitPoint.position);
                break;

            case NPCState.ShowingHint:
                CurrentState = NPCState.WaitingForPlayer;
                OnStateChanged?.Invoke(NPCState.WaitingForPlayer);
                break;
        }
    }

    // ── НОВЕ — Фаза 1: EndDay API ───────────────────────────────

    /// Викликається CashDeskBuffer при EndDay.
    /// Примусово відправляє NPC до виходу і повертає BookInstance
    /// яку NPC тримав/зарезервував (без нарахування грошей гравцю).
    /// Повертає null якщо NPC не мав книги.
    public BookInstance ForceLeaveAndTakeBook()
    {
        BookInstance result = null;

        if (_reservedBookWorldItem != null)
        {
            result = _reservedBookWorldItem.instance;

            // Знімаємо резервацію
            _reservedBookWorldItem.Unreserve();

            // Прибираємо книгу з полиці (вона переходить на стіл каси)
            if (_reservedBookWorldItem.parentShelf != null)
                _reservedBookWorldItem.parentShelf.RemoveBook(_reservedBookWorldItem.gameObject);

            _reservedBookWorldItem = null;
            FoundBook = null;

            Debug.Log($"[NPC] {Data?.npcName}: EndDay → книга '{result?.templateID}' вилучена.");
        }

        // Відправляємо до виходу якщо ще активний
        if (_isInitialized && CurrentState != NPCState.Leaving)
            ChangeState(NPCState.Leaving);

        return result;
    }

    // ── Helpers ─────────────────────────────────────────────────
    private Shelf FindUnvisitedShelf()
    {
        if (_cachedShelves == null) return null;
        foreach (var s in _cachedShelves)
            if (s != null && !_visitedShelves.Contains(s)) return s;
        return null;
    }

    private bool AgentArrived()
    {
        if (!_agent.isOnNavMesh) return false;
        return !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance;
    }

    private IEnumerator LookAtTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0f;
        if (dir == Vector3.zero) yield break;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
            yield return null;
        }
    }

    private void DestroyNPC()
    {
        OnNPCLeft?.Invoke();
        Destroy(gameObject);
    }
}