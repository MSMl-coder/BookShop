// Assets/Scripts/World/NPC/NPCBrain.cs  v6
// FIXES:
//   [1] Перша полиця — shuffle _cachedShelves при ініціалізації
//   [2] ShelfAccessRegistry deny — не додає до _visitedShelves (окремий skip-pass)
//   [3] WaitingForPlayer — hard timeout (30с за замовчуванням)
//   [4] Вихід при кінці WorkDay — підписка на GameLoopManager.OnStateChanged

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NPCStatsTicker))]
public class NPCBrain : MonoBehaviour
{
    // ── Public State ────────────────────────────────────────────
    public NPCData        Data         { get; private set; }
    public NPCState       CurrentState { get; private set; }
    public BookGenre      DesiredGenre { get; private set; }
    public BookTemplate   FoundBook    { get; private set; }
    public NPCPersonality Personality  { get; private set; }
    public NPCStatsTicker Ticker       { get; private set; }

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

    private float       _enteringTimer;
    private bool        _isDestroyPending;
    private List<Shelf> _visitedShelves = new List<Shelf>();
    // [FIX 2] Полиці що зараз переповнені — тимчасовий skip, очищається при кожному проході
    private HashSet<Shelf> _fullShelvesThisPass = new HashSet<Shelf>();
    private Shelf       _currentTargetShelf;
    private bool        _isInitialized;
    private bool        _inspectStarted;
    private int         _playerOfferAttempts;
    private Shelf[]     _cachedShelves;

    private Shelf  _reservedShelf;
    private int    _reservedBookIndex = -1;
    private string _reservedNpcID;

    // Resting
    private bool        _restingArrived;
    private float       _restingTimer;
    private const float MAX_REST_DURATION = 30f;

    // Inspecting timeout
    private float       _inspectingTimer;
    private const float INSPECTING_TIMEOUT = 5f;

    // [FIX 3] WaitingForPlayer hard timeout
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

        // Кешуємо Storefront-полиці
        var all = FindObjectsByType<Shelf>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var storefront = System.Array.FindAll(all, s => s.ZoneType == ShopZoneType.Storefront);

        // [FIX 1] Перемішуємо порядок унікально для кожного NPC
        _cachedShelves = Shuffle(storefront);

        Debug.Log($"[NPC] {data.npcName} Lv{level}: {Personality.DebugString()} | shelves={_cachedShelves.Length}");

        Vector3 entry = _cachedShelves.Length > 0
            ? _cachedShelves[0].transform.position + _cachedShelves[0].transform.forward * 1.5f
            : transform.position;

        TrySetDestination(entry);
        ChangeState(NPCState.Entering);
    }

    private void OnEnable()
    {
        // [FIX 4] Підписуємось на зміну фази гри
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= OnGameStateChanged;
    }

    private void Update()
    {
        if (!_isInitialized) return;
        UpdateCurrentState();
    }

    // ── [FIX 4] Вихід при кінці дня ────────────────────────────
    private void OnGameStateChanged(GameState state)
    {
        // При переході в DayStats або LootPhase — всі NPC мають піти
        if (state == GameState.DayStats || state == GameState.LootPhase)
        {
            if (CurrentState != NPCState.Leaving && !_isDestroyPending)
            {
                Debug.Log($"[NPC] {Data?.npcName}: WorkDay ended → ForceLeave");
                ForceLeaveAndTakeBook();
            }
        }
    }

    // ── Public API ───────────────────────────────────────────────

    public float GetRemainingTimeNormalized() =>
        Ticker?.Stats == null ? 0f
        : Mathf.InverseLerp(NPCStats.MIN, NPCStats.MAX, Ticker.Stats.Patience);

    public void OnNPCClicked() =>
        NPCInspectorPanel.Instance?.Show(this);

    public BookInstance ForceLeaveAndTakeBook()
    {
        BookInstance held = null;
        if (_reservedShelf != null && _reservedBookIndex >= 0)
        {
            var e = _reservedShelf.GetBookData(_reservedBookIndex);
            if (!string.IsNullOrEmpty(e.templateID))
                held = new BookInstance(e.templateID);
            _reservedShelf.TakeBookAt(_reservedBookIndex);
            _reservedShelf = null; _reservedBookIndex = -1;
        }
        if (CurrentState == NPCState.Resting && _restingArrived) Ticker.StopResting();
        SeatRegistry.Instance?.Release(Personality.UniqueID);
        ReleaseCurrentShelfSlot();
        ChangeState(NPCState.Leaving);
        return held;
    }

    // ── State Machine ────────────────────────────────────────────
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
                UpdateInspecting();
                break;

            case NPCState.Resting:
                UpdateResting();
                break;

            case NPCState.WaitingForPlayer:
                // [FIX 3] Hard timeout
                _waitingTimer += Time.deltaTime;
                if (_waitingTimer >= waitingForPlayerTimeout)
                {
                    Debug.Log($"[NPC] {Data.npcName}: WaitingForPlayer timeout → Leaving");
                    ChangeState(NPCState.Leaving);
                }
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
        // Ще рухаємось до попередньої цілі
        if (!AgentArrived() && _currentTargetShelf != null) return;

        if (Ticker.WantsRest && TryBeginResting()) return;

        int limit = Data.shelvesToInspect + (Ticker.ExtraShelf ? 1 : 0);

        // Перевірка чи вичерпали ліміт по відвіданих (не лічимо fullShelves)
        if (_visitedShelves.Count >= limit)
        {
            _currentTargetShelf = null;
            Debug.Log($"[NPC] {Data.npcName}: visited {_visitedShelves.Count}/{limit} → WaitingForPlayer");
            ChangeState(NPCState.WaitingForPlayer);
            return;
        }

        // Шукаємо наступну полицю: невідвідана + не в поточному fullPass + має слот
        Shelf next = FindNextShelf(limit);

        if (next == null)
        {
            // [FIX 2] Якщо всі залишкові повні — очищаємо _fullShelvesThisPass і чекаємо
            if (_fullShelvesThisPass.Count > 0)
            {
                _fullShelvesThisPass.Clear();
                return; // наступний кадр спробуємо знову
            }

            // Дійсно нема невідвіданих — виходимо
            _currentTargetShelf = null;
            Debug.Log($"[NPC] {Data.npcName}: no shelves left → WaitingForPlayer");
            ChangeState(NPCState.WaitingForPlayer);
            return;
        }

        // [FIX 2] ShelfAccessRegistry перевірка — не додаємо до _visitedShelves при deny
        if (!ShelfAccessRegistry.Instance.TryClaim(next, Personality.UniqueID))
        {
            _fullShelvesThisPass.Add(next); // тимчасовий skip
            return; // наступний кадр спробуємо інший
        }

        // Клеймили успішно — ідемо до полиці
        _currentTargetShelf = next;
        _visitedShelves.Add(next);
        _fullShelvesThisPass.Clear();    // скидаємо full-pass при кожному новому русі
        _inspectStarted  = false;
        _inspectingTimer = 0f;

        TrySetDestination(SampleNavMesh(next.transform.position + next.transform.forward * 1.2f));
        ChangeState(NPCState.Inspecting);
    }

    // ── Inspecting ────────────────────────────────────────────────
    private void UpdateInspecting()
    {
        _inspectingTimer += Time.deltaTime;
        bool arrived = AgentArrived();
        bool timeout = _inspectingTimer >= INSPECTING_TIMEOUT;

        if ((arrived || timeout) && !_inspectStarted)
        {
            _inspectStarted = true;
            if (timeout && !arrived)
                Debug.LogWarning($"[NPC] {Data.npcName}: Inspecting timeout '{_currentTargetShelf?.name}'");
            StartCoroutine(InspectShelf());
        }
    }

    // ── Resting ───────────────────────────────────────────────────
    private bool TryBeginResting()
    {
        if (SeatRegistry.Instance == null) return false;
        if (!SeatRegistry.Instance.TryClaim(Personality.UniqueID, out _, out Vector3 seatPos))
            return false;
        TrySetDestination(seatPos);
        _restingArrived = false;
        _restingTimer   = 0f;
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
            // [FIX 2] При поверненні з Resting скидаємо full-pass щоб спробувати всі полиці знову
            _fullShelvesThisPass.Clear();
            ChangeState(NPCState.Browsing);
        }
    }

    // ── InspectShelf ─────────────────────────────────────────────
    private IEnumerator InspectShelf()
    {
        yield return new WaitForSeconds(Personality.GetInspectTime());
        if (CurrentState != NPCState.Inspecting) yield break;

        // [FIX 2] Полиця пуста або немає відповідних книг — просто продовжуємо
        var result = _scanner?.FindBook(_currentTargetShelf, Personality);

        // Звільняємо слот доступу одразу після огляду
        ReleaseCurrentShelfSlot();

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
            else
            {
                ChangeState(NPCState.Browsing);
            }
        }
        else
        {
            // Полиця пуста — продовжуємо
            Debug.Log($"[NPC] {Data.npcName}: empty/no-match on shelf → continue");
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

        // Перевіряємо ліміт книг: WantsToBuy задано в NPCData
        if (Personality.WantsMoreBooks && !Ticker.IsBudgetLow)
        {
            _visitedShelves.Clear();
            _fullShelvesThisPass.Clear();
            ChangeState(NPCState.Browsing);
        }
        else
        {
            ChangeState(NPCState.Leaving);
        }
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
            ReleaseCurrentShelfSlot();
        if (newState == NPCState.WaitingForPlayer)
            _waitingTimer = 0f;

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);

        switch (newState)
        {
            case NPCState.Inspecting:
                _inspectingTimer = 0f;
                _inspectStarted  = false;
                break;
            case NPCState.Buying:
                if (_cashRegister != null)
                    TrySetDestination(_cashRegister.GetQueuePosition());
                break;
            case NPCState.Leaving:
                TrySetDestination(NPCSpawner.Instance?.ExitPoint?.position ?? transform.position);
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private void ReleaseCurrentShelfSlot()
    {
        if (_currentTargetShelf == null) return;
        ShelfAccessRegistry.Instance?.Release(_currentTargetShelf, Personality.UniqueID);
        _currentTargetShelf = null;
    }

    // [FIX 1+2] Шукає наступну: не відвідану, не в fullPass, не помічену ShelfAccess
    private Shelf FindNextShelf(int limit)
    {
        if (_cachedShelves == null) return null;
        if (_visitedShelves.Count >= limit) return null;

        foreach (var shelf in _cachedShelves)
        {
            if (shelf == null)                          continue;
            if (_visitedShelves.Contains(shelf))        continue;
            if (_fullShelvesThisPass.Contains(shelf))   continue;
            return shelf;
        }
        return null;
    }

    // [FIX 1] Fisher-Yates shuffle
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
        if (_agent != null && _agent.isOnNavMesh)
            _agent.SetDestination(pos);
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
            string reason = offeredBook == null                          ? "Це не книга..."
                : offeredBook.genre != Personality.DesiredGenre          ? "Не мій жанр."
                : !Personality.AcceptsRarity(offeredBook.rarity)         ? "Не та якість."
                :                                                           "Задорого.";
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
}