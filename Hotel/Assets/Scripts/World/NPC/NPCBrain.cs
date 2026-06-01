// Assets/Scripts/World/NPC/NPCBrain.cs  v9
// ЗМІНИ v9 (нова механіка збору книг):
//   [1] Новий стан CollectingBooks — NPC ходить по полицях збираючи книги
//   [2] UpdateInspecting: книга одразу в Basket (TakeBookAt без резерву)
//   [3] GoToCashier(): якщо Basket порожній → Leaving, інакше → Buying
//   [4] CompletePurchase(): оплачує ВСІ книги з Basket
//   [5] OnPatienceDepleted (через NPCStatsTicker): GoToCashier() замість Leaving
//   [6] Новий event OnBookPickedUp для UI
//   [7] WaitingForPlayer: якщо гравець не допоміг → GoToCashier() (не Leaving)
//      виняток: якщо WantsToBuy==1 і Basket порожній → Leaving

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NPCStatsTicker))]
public class NPCBrain : MonoBehaviour
{
    // ── Public properties ──────────────────────────────────────────
    public NPCData        Data         { get; private set; }
    public NPCState       CurrentState { get; private set; }
    public BookGenre      DesiredGenre { get; private set; }
    public BookTemplate   FoundBook    { get; private set; }
    public NPCPersonality Personality  { get; private set; }
    public NPCStatsTicker Ticker       { get; private set; }

    // ── Events ────────────────────────────────────────────────────
    public event Action<NPCState>     OnStateChanged;
    public event Action<BookTemplate> OnBookFound;
    public event Action<BookTemplate> OnBookPickedUp;   // [NEW] книга взята в кошик
    public event Action               OnPurchaseComplete;
    public event Action               OnNPCLeft;

    // ── Private refs ──────────────────────────────────────────────
    private NavMeshAgent  _agent;
    private NPCWorldUI    _worldUI;
    private ShelfScanner  _scanner;
    private CashRegister  _cashRegister;

    // ── State data ────────────────────────────────────────────────
    private float          _enteringTimer;
    private bool           _isDestroyPending;
    private List<Shelf>    _visitedShelves    = new List<Shelf>();
    private HashSet<Shelf> _fullShelves       = new HashSet<Shelf>();
    private Shelf          _currentTargetShelf;
    private bool           _isInitialized;
    private bool           _inspectStarted;
    private int            _playerOfferAttempts;
    private Shelf[]        _cachedShelves;

    // [v9] Прибрано _reservedShelf/_reservedBookIndex —
    // книга одразу забирається з полиці в Basket без резервування
    private bool        _restingArrived;
    private float       _restingTimer;
    private const float MAX_REST_DURATION  = 30f;

    private float       _inspectingTimer;
    private const float INSPECTING_TIMEOUT = 5f;

    private float _waitingTimer;
    [SerializeField] private float waitingForPlayerTimeout = 30f;

    // [STAY DURATION] Час перебування в крамниці — по закінченню бот іде незалежно від всього
    private float _stayTimer;
    private bool  _stayTimerStarted;

    // ── Computed (для UI) ─────────────────────────────────────────
    public float PatientNormalized =>
        Ticker?.Stats == null ? 1f
        : Mathf.InverseLerp(NPCStats.MIN, NPCStats.MAX, Ticker.Stats.Patience);

    // ── Init ──────────────────────────────────────────────────────
    public void Initialize(NPCData data, CashRegister cashRegister)
    {
        Data          = data;
        _cashRegister = cashRegister;
        _agent        = GetComponent<NavMeshAgent>();
        _worldUI      = GetComponent<NPCWorldUI>();
        _scanner      = GetComponent<ShelfScanner>();
        Ticker        = GetComponent<NPCStatsTicker>();

        Personality   = NPCPersonality.Generate(data);
        DesiredGenre  = Personality.DesiredGenre;

        Ticker.Initialize(Personality);

        // [v9] NPCStatsTicker.Initialize підписується на Stats.OnPatienceDepleted
        // і викликає _brain.ChangeState(Leaving) — перехоплюємо через OnStateChanged
        // щоб замінити поведінку: якщо є книги → каса замість виходу.

        // Використовуємо ShelfRegistry якщо є, інакше FindObjectsByType
        var allShelves = ShelfRegistry.Instance != null
            ? new List<Shelf>(ShelfRegistry.Instance.GetAll()).ToArray()
            : FindObjectsByType<Shelf>(FindObjectsSortMode.None);
        _cachedShelves = Shuffle(allShelves);

        _isInitialized = true;
        ChangeState(NPCState.Entering);
        TrySetDestination(SampleNavMesh(transform.position));
    }

    private void Update()
    {
        if (!_isInitialized || _isDestroyPending) return;
        UpdateStayTimer();
        UpdateCurrentState();
    }

    private void UpdateStayTimer()
    {
        if (!_stayTimerStarted) return;
        // Не рахуємо час поки бот на касі або вже йде
        if (CurrentState == NPCState.Buying || CurrentState == NPCState.Leaving) return;

        _stayTimer += Time.deltaTime;
        float limit = Personality.StayDuration;
        if (limit <= 0f) return;

        if (_stayTimer >= limit)
        {
            _stayTimerStarted = false;
            Debug.Log($"[NPCBrain] {Data?.npcName}: stayDuration {limit}s → GoToCashier");

            // Повідомлення через UI
            NPCInspectorMount.Instance?.Controller?.ShowSpeechBubble(
                "У вас дуже цікаво, але мені вже час!", 4f);

            // Невелика затримка щоб гравець побачив повідомлення
            StartCoroutine(LeaveAfterSpeech(3f));
        }
    }

    private IEnumerator LeaveAfterSpeech(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_isDestroyPending) GoToCashier();
    }

    private void OnDestroy()
    {
        // NPCStatsTicker.Deactivate() знімає підписки сам
    }

    // ── Public API ────────────────────────────────────────────────
    public void ChangeState(NPCState newState)
    {
        // [v9] Перехоплюємо Leaving що ініціюється NPCStatsTicker (patience=0):
        // якщо є книги в кошику — замінюємо на GoToCashier
        if (newState == NPCState.Leaving
            && CurrentState != NPCState.Buying  // не перериваємо оплату
            && Personality != null
            && Personality.BasketNotEmpty
            && !_isDestroyPending)
        {
            Debug.Log($"[NPCBrain] {Data?.npcName}: Leaving intercepted → GoToCashier (basket={Personality.Basket.Count})");
            GoToCashier();
            return;
        }

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        _inspectStarted = false;
        _waitingTimer   = 0f;
        Debug.Log($"[NPCBrain] {Data?.npcName}: → {newState}");
    }

    public void OnNPCClicked()
    {
        Debug.Log($"[NPCBrain] OnNPCClicked: {Data?.npcName}");
        if (NPCInspectorMount.Instance != null)
        {
            NPCInspectorMount.Instance.Show(this);
            return;
        }
        NPCInspectorPanel.Instance?.Show(this);
    }

    // ── State Machine ─────────────────────────────────────────────
    private void UpdateCurrentState()
    {
        switch (CurrentState)
        {
            case NPCState.Entering:
                _enteringTimer += Time.deltaTime;
                if (_enteringTimer > 0.3f && AgentArrived())
                {
                    _stayTimerStarted = true;
                    ChangeState(NPCState.Browsing);
                }
                break;

            case NPCState.Browsing:
            case NPCState.CollectingBooks:  // [NEW v9] — аналогічний Browsing
                UpdateBrowsing();
                break;

            case NPCState.Inspecting:
                UpdateInspecting();
                break;

            case NPCState.Resting:
                UpdateResting();
                break;

            case NPCState.WaitingForPlayer:
                _waitingTimer += Time.deltaTime;
                if (_waitingTimer >= waitingForPlayerTimeout)
                {
                    // [v9] Час очікування вичерпано:
                    // якщо вже є книги в кошику → йде на касу
                    // якщо WantsToBuy==1 і кошик порожній → йде з магазину
                    if (Personality.BasketNotEmpty)
                        GoToCashier();
                    else
                        ChangeState(NPCState.Leaving);
                }
                break;

            case NPCState.Buying:
                if (AgentArrived()) CompletePurchase();
                break;

            case NPCState.Leaving:
                if (AgentArrived()) DestroyNPC();
                break;
        }
    }

    // ── Browsing ──────────────────────────────────────────────────
    private void UpdateBrowsing()
    {
        if (!AgentArrived() && _currentTargetShelf != null) return;
        if (Ticker.WantsRest && TryBeginResting()) return;

        int limit = Data.shelvesToInspect + (Ticker.ExtraShelf ? 1 : 0);
        Shelf next = FindNextShelf(limit);

        if (next != null)
        {
            _currentTargetShelf = next;
            if (ShelfAccessRegistry.Instance != null)
                ShelfAccessRegistry.Instance.TryClaim(_currentTargetShelf, Personality.UniqueID, out _);
            _visitedShelves.Add(next);
            TrySetDestination(SampleNavMesh(next.transform.position));
            ChangeState(NPCState.Inspecting);
        }
        else
        {
            // [v9] Всі полиці переглянуто — йдемо на касу або з магазину
            GoToCashier();
        }
    }

    // ── Inspecting ────────────────────────────────────────────────
    private void UpdateInspecting()
    {
        if (!AgentArrived()) return;

        if (!_inspectStarted)
        {
            _inspectStarted  = true;
            _inspectingTimer = 0f;

            if (_currentTargetShelf != null && _currentTargetShelf.GetBookCount() == 0)
            {
                _fullShelves.Add(_currentTargetShelf);
                ReleaseCurrentShelf();
                ChangeState(CurrentState == NPCState.CollectingBooks
                    ? NPCState.CollectingBooks
                    : NPCState.Browsing);
                return;
            }
        }

        _inspectingTimer += Time.deltaTime;
        if (_inspectingTimer < Personality.GetInspectTime()) return;

        // Час огляду вичерпано — шукаємо книгу
        var result = _scanner?.FindBook(_currentTargetShelf, Personality);
        ReleaseCurrentShelf();

        if (result.HasValue)
        {
            FoundBook = result.Value.template;
            OnBookFound?.Invoke(FoundBook);

            float chance = Ticker.GetAdjustedBuyChance(Personality.BuyChance);
            // Якщо BuyChance >= 1 — гарантована покупка незалежно від Mood multiplier
            bool  buy    = Personality.BuyChance >= 1f
                        || UnityEngine.Random.value <= chance
                        || Ticker.CanImpulseBuy;

            if (buy)
            {
                // [v9] НОВА ЛОГІКА: книга одразу в кошик, без резервування
                _currentTargetShelf?.TakeBookAt(result.Value.index);
                Personality.AddToBasket(result.Value.template);
                OnBookPickedUp?.Invoke(result.Value.template);

                Debug.Log($"[NPCBrain] {Data?.npcName} взяв «{result.Value.template.title}» " +
                           $"(кошик: {Personality.Basket.Count}/{Personality.WantsToBuy})");

                // Хоче ще і бюджет є?
                if (Personality.WantsMoreBooks && !Ticker.IsBudgetLow)
                {
                    // [FIX] НЕ скидаємо відвідані полиці — бот не повертається туди де вже був
                    // Повернення можливе тільки якщо гравець поклав туди книгу (OnBookPlaced event)
                    ChangeState(NPCState.CollectingBooks);
                }
                else
                {
                    // Все зібрав → каса
                    GoToCashier();
                }
            }
            else
            {
                // Не хоче купувати цю книгу
                ChangeState(CurrentState == NPCState.CollectingBooks
                    ? NPCState.CollectingBooks
                    : NPCState.Browsing);
            }
        }
        else
        {
            // Книг потрібного жанру/рарності не знайшло
            _fullShelves.Add(_currentTargetShelf ?? FindNextShelf(99));
            ChangeState(CurrentState == NPCState.CollectingBooks
                ? NPCState.CollectingBooks
                : NPCState.Browsing);
        }
    }

    // ── Resting ───────────────────────────────────────────────────
    private void UpdateResting()
    {
        if (!_restingArrived)
        {
            if (!AgentArrived()) return;
            _restingArrived = true;
            _restingTimer   = 0f;
            Ticker.BeginResting(0f, 0f);
        }
        _restingTimer += Time.deltaTime;
        if (_restingTimer >= MAX_REST_DURATION || !Ticker.WantsRest)
        {
            Ticker.StopResting();
            SeatRegistry.Instance?.Release(Personality.UniqueID);
            _restingArrived = false;
            ChangeState(Personality.BasketNotEmpty
                ? NPCState.CollectingBooks
                : NPCState.Browsing);
        }
    }

    // ── GoToCashier (НОВА ЛОГІКА) ─────────────────────────────────
    /// Вирішує: іти на касу чи покинути магазин.
    private void GoToCashier()
    {
        if (!Personality.BasketNotEmpty)
        {
            // Нічого не знайшов взагалі → йде з магазину
            Debug.Log($"[NPCBrain] {Data?.npcName}: кошик порожній → Leaving");
            ChangeState(NPCState.Leaving);
            TrySetDestination(SampleNavMesh(NPCSpawner.Instance?.ExitPoint?.position ?? transform.position));
            return;
        }

        // Є книги → йде до каси
        Debug.Log($"[NPCBrain] {Data?.npcName}: йде на касу з {Personality.Basket.Count} книгами");
        if (_cashRegister != null)
            TrySetDestination(SampleNavMesh(_cashRegister != null ? _cashRegister.transform.position : transform.position));
        ChangeState(NPCState.Buying);
    }

    // ── CompletePurchase (оновлено) ───────────────────────────────
    private void CompletePurchase()
    {
        // [v9] Оплачуємо ВСІ книги з кошика
        float totalPrice = 0f;
        foreach (var book in Personality.Basket)
        {
            EconomyManager.Instance?.RecordBookSold(book.sellPrice);
            totalPrice += book.sellPrice;
        }

        Ticker.RegisterPurchase(totalPrice, Personality.MaxBudget);
        OnPurchaseComplete?.Invoke();

        Debug.Log($"[NPCBrain] {Data?.npcName}: оплатив {Personality.Basket.Count} книги " +
                  $"на суму ${totalPrice:F0}");

        Personality.ClearBasket();
        ChangeState(NPCState.Leaving);
        TrySetDestination(SampleNavMesh(NPCSpawner.Instance?.ExitPoint?.position ?? transform.position));
    }

    // ── Player interaction ────────────────────────────────────────
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

            // Книга від гравця → в кошик
            Personality.AddToBasket(offeredBook);
            OnBookPickedUp?.Invoke(offeredBook);

            if (Personality.WantsMoreBooks && !Ticker.IsBudgetLow)
                ChangeState(NPCState.CollectingBooks);
            else
                GoToCashier();
        }
        else
        {
            string reason = offeredBook == null          ? "Це не книга..."
                : offeredBook.genre != Personality.DesiredGenre  ? "Не мій жанр."
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
        if (CurrentState == NPCState.WaitingForPlayer)
        {
            // [v9] Якщо є книги → каса; якщо ні → йде
            if (Personality.BasketNotEmpty) GoToCashier();
            else ChangeState(NPCState.Leaving);
        }
    }

    // ── ForceLeave ────────────────────────────────────────────────
    public BookInstance ForceLeaveAndTakeBook()
    {
        if (CurrentState == NPCState.Resting && _restingArrived) Ticker.StopResting();
        SeatRegistry.Instance?.Release(Personality.UniqueID);
        ReleaseCurrentShelf();
        _stayTimerStarted = false;

        // [FIX] Якщо є книги в кошику — йде на касу, а не просто йде
        if (Personality.BasketNotEmpty)
        {
            Debug.Log($"[NPCBrain] {Data?.npcName}: ForceLeave → GoToCashier (basket={Personality.Basket.Count})");
            GoToCashier();
        }
        else
        {
            Personality.ClearBasket();
            ChangeState(NPCState.Leaving);
        }
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────
    private void ReleaseCurrentShelf()
    {
        if (_currentTargetShelf == null) return;
        ShelfAccessRegistry.Instance?.Release(_currentTargetShelf, Personality.UniqueID);
        _currentTargetShelf = null;
    }

    private bool TryBeginResting()
    {
        if (SeatRegistry.Instance == null) return false;
        if (!SeatRegistry.Instance.TryClaim(Personality.UniqueID, out _, out Vector3 seatPos)) 
            return false;
        _restingArrived = false;
        TrySetDestination(SampleNavMesh(seatPos));
        ChangeState(NPCState.Resting);
        return true;
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

    private void DestroyNPC()
    {
        if (_isDestroyPending) return;
        _isDestroyPending = true;
        Ticker.Deactivate();
        OnNPCLeft?.Invoke();
        Destroy(gameObject, 0.1f);
    }
}