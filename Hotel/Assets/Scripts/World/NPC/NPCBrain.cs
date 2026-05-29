// Assets/Scripts/World/NPC/NPCBrain.cs
// INTEGRATION PATCH v2 — інтеграція NPCStatsTicker та стану Resting.
//
// ═══════════════════════════════════════════════════════════════════
//  ЩО ЗМІНИТИ в існуючому NPCBrain.cs:
// ═══════════════════════════════════════════════════════════════════
//
// [1] Додати поле _ticker (Awake або як private field)
// [2] Видалити _stayTimer — замінено на Patience в NPCStats
// [3] Initialize(): додати _ticker.Initialize(Personality)
// [4] Update(): видалити _stayTimer countdown — тепер в NPCStatsTicker
// [5] CompletePurchase(): додати _ticker.RegisterPurchase(...)
// [6] Додати case NPCState.Resting в UpdateCurrentState()
// [7] Додати метод TryRest() що перевіряє SeatRegistry
// [8] Додати ChangeState(NPCState.Resting) в UpdateBrowsing() коли WantsRest
// [9] OnDestroy / DestroyNPC(): звільнити місце в SeatRegistry
//
// ═══════════════════════════════════════════════════════════════════
//  ПОВНИЙ КОД ЗМІН (вставити в NPCBrain.cs):
// ═══════════════════════════════════════════════════════════════════

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NPCStatsTicker))]   // [NEW]
public class NPCBrain : MonoBehaviour
{
    // ── Public State ────────────────────────────────────────────
    public NPCData       Data         { get; private set; }
    public NPCState      CurrentState { get; private set; }
    public BookGenre     DesiredGenre { get; private set; }
    public BookTemplate  FoundBook    { get; private set; }
    public NPCPersonality Personality { get; private set; }

    // [NEW] Доступ до живих показників ззовні (UI, Debug)
    public NPCStatsTicker Ticker      { get; private set; }

    // ── Events ──────────────────────────────────────────────────
    public event Action<NPCState>     OnStateChanged;
    public event Action<BookTemplate> OnBookFound;
    public event Action               OnPurchaseComplete;
    public event Action               OnNPCLeft;

    // ── Private ─────────────────────────────────────────────────
    private NavMeshAgent  _agent;
    private NPCWorldUI    _worldUI;
    private ShelfScanner  _scanner;
    private CashRegister  _cashRegister;

    // [REMOVED] private float _stayTimer; — замінено на Patience
    private float       _enteringTimer;
    private bool        _isDestroyPending;
    private List<Shelf> _visitedShelves   = new List<Shelf>();
    private Shelf       _currentTargetShelf;
    private bool        _isInitialized;
    private bool        _inspectStarted;
    private int         _playerOfferAttempts;

    private Shelf[] _cachedShelves;

    private Shelf  _reservedShelf;
    private int    _reservedBookIndex = -1;
    private string _reservedNpcID;

    // [NEW] Resting state
    private bool    _restingArrived;        // NPC дійшов до місця і сидить
    private float   _restingTimer;          // скільки часу сидить
    private const float MAX_REST_DURATION = 30f; // максимум 30с відпочинку

    // ── Init ────────────────────────────────────────────────────
    public void Initialize(NPCData data, CashRegister cashRegister)
    {
        Data          = data;
        _cashRegister = cashRegister;
        _agent        = GetComponent<NavMeshAgent>();
        _worldUI      = GetComponent<NPCWorldUI>();
        _scanner      = GetComponent<ShelfScanner>();
        Ticker        = GetComponent<NPCStatsTicker>(); // [NEW]

        Personality  = NPCPersonality.Generate(data);
        DesiredGenre = Personality.DesiredGenre;

        // [NEW] Ініціалізуємо живі показники
        Ticker.Initialize(Personality);

        _playerOfferAttempts = 0;
        _inspectStarted      = false;
        _enteringTimer       = 0f;
        _isDestroyPending    = false;
        _reservedBookIndex   = -1;
        _reservedShelf       = null;
        _isInitialized       = true;

        int level = NPCLevelCalculator.Calculate(Personality);
        GetComponent<NPCLevelBadge>()?.SetLevel(level);
        Debug.Log($"[NPC] {data.npcName} Lv{level}: {Personality.DebugString()}");

        _cachedShelves = FindObjectsByType<Shelf>(FindObjectsSortMode.None);

        Vector3 entryTarget = transform.position;
        if (_cachedShelves != null && _cachedShelves.Length > 0)
            entryTarget = _cachedShelves[0].transform.position
                        + _cachedShelves[0].transform.forward * 2f;
        TrySetDestination(entryTarget);
        ChangeState(NPCState.Entering);
    }

    // ── Unity ───────────────────────────────────────────────────
    private void Update()
    {
        if (!_isInitialized) return;

        // [REMOVED] _stayTimer countdown — тепер в NPCStatsTicker.Update()

        UpdateCurrentState();
    }

    // ── State Machine ───────────────────────────────────────────
    private void UpdateCurrentState()
    {
        switch (CurrentState)
        {
            case NPCState.Entering:
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

            // [NEW] Resting state
            case NPCState.Resting:
                UpdateResting();
                break;

            case NPCState.Buying:
                if (AgentArrived()) CompletePurchase();
                break;

            case NPCState.Leaving:
                if (_reservedShelf != null && _reservedBookIndex >= 0)
                {
                    _reservedShelf.UnreserveBook(_reservedBookIndex);
                    _reservedShelf     = null;
                    _reservedBookIndex = -1;
                }
                if (AgentArrived()) DestroyNPC();
                break;
        }
    }

    // ── Browsing ────────────────────────────────────────────────
    private void UpdateBrowsing()
    {
        if (!AgentArrived() && _currentTargetShelf != null) return;

        // [NEW] Перевіряємо чи хоче відпочити до пошуку наступної полиці
        if (Ticker.WantsRest && TryBeginResting()) return;

        // [NEW] Extra shelf від Comfort
        int shelvesLimit = Ticker.ExtraShelf
            ? Data.shelvesToInspect + 1
            : Data.shelvesToInspect;

        if (_visitedShelves.Count >= shelvesLimit)
        {
            Debug.Log($"[NPC] {Data.npcName} checked {shelvesLimit} shelves → WaitingForPlayer");
            ChangeState(NPCState.WaitingForPlayer);
            return;
        }

        Shelf nextShelf = FindUnvisitedShelf();
        if (nextShelf != null)
        {
            _currentTargetShelf = nextShelf;
            _visitedShelves.Add(nextShelf);
            _inspectStarted = false;
            TrySetDestination(nextShelf.transform.position + nextShelf.transform.forward * 1.2f);
            ChangeState(NPCState.Inspecting);
        }
        else
        {
            Debug.Log($"[NPC] {Data.npcName}: no more shelves → WaitingForPlayer");
            ChangeState(NPCState.WaitingForPlayer);
        }
    }

    // ── [NEW] Resting ────────────────────────────────────────────
    private bool TryBeginResting()
    {
        if (SeatRegistry.Instance == null) return false;

        if (SeatRegistry.Instance.TryClaim(
            Personality.UniqueID,
            out PropTemplate prop,
            out Vector3 seatPos))
        {
            TrySetDestination(seatPos);
            _restingArrived = false;
            _restingTimer   = 0f;
            ChangeState(NPCState.Resting);
            return true;
        }

        return false;
    }

    private void UpdateResting()
    {
        // Чекаємо поки дійде до місця
        if (!_restingArrived)
        {
            if (!AgentArrived()) return;
            _restingArrived = true;

            // Повідомляємо Ticker що почався відпочинок
            if (SeatRegistry.Instance.TryClaim(
                Personality.UniqueID,
                out PropTemplate prop,
                out Vector3 _))
            {
                Ticker.BeginResting(prop.comfortForce, prop.patienceRestoreBonus);
                Debug.Log($"[NPC] {Data.npcName} is resting on {prop.propName}");
            }
        }

        // Таймер відпочинку
        _restingTimer += Time.deltaTime;

        // Умова виходу:
        // 1. Набрали достатньо Patience → повертаємось до Browsing
        // 2. Час вийшов
        bool patienceRestored = !Ticker.WantsRest;
        bool timedOut         = _restingTimer >= MAX_REST_DURATION;

        if (patienceRestored || timedOut)
        {
            Ticker.StopResting();
            SeatRegistry.Instance?.Release(Personality.UniqueID);
            Debug.Log($"[NPC] {Data.npcName} done resting " +
                      $"(restored={patienceRestored} timeout={timedOut})");
            ChangeState(NPCState.Browsing);
        }
    }

    // ── Purchase ────────────────────────────────────────────────
    private void CompletePurchase()
    {
        if (FoundBook == null) return;

        if (_reservedShelf != null && _reservedBookIndex >= 0)
        {
            _reservedShelf.TakeBookAt(_reservedBookIndex);
            _reservedShelf     = null;
            _reservedBookIndex = -1;
        }

        EconomyManager.Instance?.RecordBookSold(FoundBook.sellPrice);
        Personality.BooksBought++;

        // [NEW] Витрачаємо бюджет у Ticker
        Ticker.RegisterPurchase(FoundBook.sellPrice, Personality.MaxBudget);

        OnPurchaseComplete?.Invoke();
        Debug.Log($"[NPC] {Data.npcName} bought {FoundBook.title} " +
                  $"| Stats: {Ticker.Stats}");

        if (Personality.WantsMoreBooks && !Ticker.IsBudgetLow)
        {
            _visitedShelves.Clear();
            ChangeState(NPCState.Browsing);
        }
        else
        {
            ChangeState(NPCState.Leaving);
        }
    }

    // ── InspectShelf ─────────────────────────────────────────────
    private IEnumerator InspectShelf()
    {
        yield return new WaitForSeconds(Personality.GetInspectTime());

        if (CurrentState != NPCState.Inspecting) yield break;

        var book = _scanner?.FindBook(_currentTargetShelf, Personality);
        if (book != null)
        {
            FoundBook = book.template;
            OnBookFound?.Invoke(FoundBook);

            // [NEW] Скоригований BuyChance враховує поточний Mood
            float adjustedChance = Ticker.GetAdjustedBuyChance(Personality.BuyChance);

            if (UnityEngine.Random.value <= adjustedChance)
            {
                _reservedShelf     = _currentTargetShelf;
                _reservedBookIndex = book.index;
                _currentTargetShelf.ReserveBook(book.index, Personality.UniqueID);
                ChangeState(NPCState.Buying);
            }
            else
            {
                // [NEW] Перевірка імпульсної покупки
                if (Ticker.CanImpulseBuy)
                {
                    Debug.Log($"[NPC] {Data.npcName} impulse buy triggered!");
                    _reservedShelf     = _currentTargetShelf;
                    _reservedBookIndex = book.index;
                    _currentTargetShelf.ReserveBook(book.index, Personality.UniqueID);
                    ChangeState(NPCState.Buying);
                }
                else
                {
                    ChangeState(NPCState.Browsing);
                }
            }
        }
        else
        {
            ChangeState(NPCState.Browsing);
        }
    }

    // ── Destroy ──────────────────────────────────────────────────
    private void DestroyNPC()
    {
        if (_isDestroyPending) return;
        _isDestroyPending = true;

        // [NEW] Звільняємо місце якщо NPC раптово виходить під час Resting
        SeatRegistry.Instance?.Release(Personality.UniqueID);
        Ticker.Deactivate();

        OnNPCLeft?.Invoke();
        Destroy(gameObject);
    }

    // ── ChangeState ──────────────────────────────────────────────
    public void ChangeState(NPCState newState)
    {
        // Якщо виходимо з Resting не через UpdateResting — чистимо
        if (CurrentState == NPCState.Resting && newState != NPCState.Resting)
        {
            if (_restingArrived) Ticker.StopResting();
            SeatRegistry.Instance?.Release(Personality.UniqueID);
        }

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        Debug.Log($"[NPC] {Data.npcName} → {newState} | {Ticker?.Stats}");

        switch (newState)
        {
            case NPCState.Buying:
                if (_cashRegister != null)
                {
                    Vector3 queuePos = _cashRegister.GetQueuePosition();
                    TrySetDestination(queuePos);
                }
                break;

            case NPCState.Leaving:
                var exit = FindObjectsByType<NPCExitPoint>(FindObjectsSortMode.None);
                if (exit != null && exit.Length > 0)
                    TrySetDestination(exit[0].transform.position);
                break;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────
    private bool AgentArrived() =>
        _agent != null
        && !_agent.pathPending
        && _agent.remainingDistance <= _agent.stoppingDistance + 0.05f;

    private void TrySetDestination(Vector3 pos)
    {
        if (_agent != null && _agent.isOnNavMesh)
            _agent.SetDestination(pos);
    }

    private Shelf FindUnvisitedShelf()
    {
        if (_cachedShelves == null) return null;
        foreach (var shelf in _cachedShelves)
            if (shelf != null && !_visitedShelves.Contains(shelf)) return shelf;
        return null;
    }

    // ── Player interaction ───────────────────────────────────────
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
            string reason;
            if (offeredBook == null)                                  reason = "Це не книга...";
            else if (offeredBook.genre != Personality.DesiredGenre)   reason = "Не мій жанр.";
            else if (!Personality.AcceptsRarity(offeredBook.rarity))  reason = "Не та якість.";
            else                                                       reason = "Задорого.";

            _worldUI?.ShowRejectionFeedback(reason);

            if (_playerOfferAttempts >= Data.maxPlayerOfferAttempts)
                StartCoroutine(LeaveAfterDelay(2f));
        }
    }

    private IEnumerator LeaveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (CurrentState == NPCState.WaitingForPlayer)
            ChangeState(NPCState.Leaving);
    }

    private void ClearReservation()
    {
        if (_reservedShelf != null && _reservedBookIndex >= 0)
            _reservedShelf.UnreserveBook(_reservedBookIndex);
        _reservedShelf     = null;
        _reservedBookIndex = -1;
        _reservedNpcID     = null;
    }
}