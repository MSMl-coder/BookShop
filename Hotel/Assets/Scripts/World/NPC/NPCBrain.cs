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
    private float _inspectTargetTime;  // ← кешований час огляду (один раз при _inspectStarted)

    private float _waitingTimer;
    [SerializeField] private float waitingForPlayerTimeout = 30f;

    // [STAY DURATION] Час перебування в крамниці — по закінченню бот іде незалежно від всього
    private float _stayTimer;
    private bool  _stayTimerStarted;

    private float _leavingTimer;

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
        DesiredGenre  = Personality.CurrentDesiredGenre;

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
        if (newState == NPCState.Leaving)
        {
            _leavingTimer = 0f;
            if (CurrentState != NPCState.Buying  // не перериваємо оплату
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
            _leavingTimer += Time.deltaTime;
            // ✅ ФІКС: мінімум 0.5с перед перевіркою arrival
            // Без цього — NPC миттєво зникає якщо ExitPoint = null
            if (_leavingTimer >= 0.5f && AgentArrived()) DestroyNPC();
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
            // ✅ [v10] Всі полиці для поточного слоту переглянуто без успіху.
            // Переходимо до наступного слоту (з іншим жанром) якщо є.
            // Тільки якщо всі слоти вичерпані — йдемо на касу.
            bool moreSlots = Personality.AdvanceToNextBook();
            if (moreSlots)
            {
                ResetForNextBook(); // свіжі полиці + новий жанр
                // Лишаємось у поточному стані (Browsing або CollectingBooks)
                // — наступний кадр UpdateBrowsing() почне новий пошук
            }
            else
            {
                GoToCashier();
            }
        }
    }

    // ── Inspecting ────────────────────────────────────────────────
       private void UpdateInspecting()
    {
        if (!AgentArrived()) return;
 
        if (!_inspectStarted)
        {
            _inspectStarted    = true;
            _inspectingTimer   = 0f;
            // ✅ Обчислюємо цільовий час огляду ОДИН РАЗ
            _inspectTargetTime = Personality.GetInspectTime();
 
            Debug.Log($"[NPCBrain] {Data?.npcName}: Inspecting {_currentTargetShelf?.name}, " +
                      $"genre={DesiredGenre}, time={_inspectTargetTime:F1}s");
 
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
        // ✅ Порівнюємо з кешованим значенням, не з новим рандомом
        if (_inspectingTimer < _inspectTargetTime) return;
 
        // ── Час огляду вичерпано — шукаємо книгу ─────────────────────
        var result = _scanner?.FindBook(_currentTargetShelf, Personality);
        ReleaseCurrentShelf();
 
        if (result.HasValue)
        {
            FoundBook = result.Value.template;
            OnBookFound?.Invoke(FoundBook);
 
            float chance = Ticker.GetAdjustedBuyChance(Personality.BuyChance);
            bool  buy    = Personality.BuyChance >= 1f
                        || UnityEngine.Random.value <= chance
                        || Ticker.CanImpulseBuy;
 
            if (buy)
            {
                _currentTargetShelf?.TakeBookAt(result.Value.index);
                Personality.AddToBasket(result.Value.template);
                OnBookPickedUp?.Invoke(result.Value.template);
 
                Debug.Log($"[NPCBrain] {Data?.npcName} взяв «{result.Value.template.title}» " +
                           $"(кошик: {Personality.Basket.Count}/{Personality.WantsToBuy})");
 
                // ✅ [v10] Переходимо до наступного слоту покупки
                bool moreSlots = Personality.AdvanceToNextBook();
                if (moreSlots && !Ticker.IsBudgetLow)
                {
                    ResetForNextBook(); // нові полиці + новий жанр
                    ChangeState(NPCState.CollectingBooks);
                }
                else
                {
                    GoToCashier();
                }
            }
            else
            {
                // Не хоче купувати → пропускаємо і йдемо до наступного слоту
                bool moreSlots = Personality.AdvanceToNextBook();
                if (moreSlots)
                {
                    ResetForNextBook();
                    ChangeState(CurrentState == NPCState.CollectingBooks
                        ? NPCState.CollectingBooks
                        : NPCState.Browsing);
                }
                else
                {
                    GoToCashier();
                }
            }
        }
        else
        {
            // Книга не знайдена на цій полиці — додаємо в "порожні" і продовжуємо
            if (_currentTargetShelf != null)
                _fullShelves.Add(_currentTargetShelf);
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
            Debug.Log($"[NPCBrain] {Data?.npcName}: кошик порожній → Leaving");
            ChangeState(NPCState.Leaving);
 
            // ✅ ФІКС: якщо ExitPoint null — не встановлюємо destination взагалі,
            // щоб уникнути миттєвого "arrival" до поточної позиції.
            var exitPos = NPCSpawner.Instance?.ExitPoint?.position;
            if (exitPos.HasValue)
                TrySetDestination(SampleNavMesh(exitPos.Value));
            else
            {
                // Немає exit point — затримуємо знищення через корутину
                Debug.LogWarning($"[NPCBrain] {Data?.npcName}: ExitPoint не призначений! " +
                                 "Додайте ExitPoint у NPCSpawner. Бот зникне через 3с.");
                StartCoroutine(DelayedDestroy(3f));
            }
            return;
        }
 
        Debug.Log($"[NPCBrain] {Data?.npcName}: йде на касу з {Personality.Basket.Count} книгами");
        if (_cashRegister != null)
            TrySetDestination(SampleNavMesh(_cashRegister.transform.position));
        else
            Debug.LogWarning($"[NPCBrain] {Data?.npcName}: _cashRegister не призначений!");
 
        ChangeState(NPCState.Buying);
    }
 
    /// Знищити NPC після затримки (fallback коли ExitPoint відсутній).
    private IEnumerator DelayedDestroy(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_isDestroyPending) DestroyNPC();
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
        var exitPoint = NPCSpawner.Instance?.ExitPoint?.position;
        if (exitPoint.HasValue)
            TrySetDestination(SampleNavMesh(exitPoint.Value));
        else
            StartCoroutine(DelayedDestroy(3f));
    }

    // ── Player interaction ────────────────────────────────────────
    public void ReceiveBookOffer(BookTemplate offeredBook)
    {
        if (CurrentState != NPCState.WaitingForPlayer) return;
        _playerOfferAttempts++;

        if (offeredBook != null
            && Personality.AcceptsRarity(offeredBook.rarity)
            && offeredBook.genre == DesiredGenre
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
                : offeredBook.genre != Personality.CurrentDesiredGenre  ? "Не мій жанр."
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



 private void ResetForNextBook()
    {
        // Скидаємо список відвіданих полиць — кожна книга отримує свіжі спроби
        _visitedShelves.Clear();
        _fullShelves.Clear();  // полиці "без потрібного жанру" — теж скидаємо (жанр змінився)
 
        // Переперемішуємо порядок полиць — NPC не ходить тим самим маршрутом
        if (_cachedShelves != null)
            _cachedShelves = Shuffle(_cachedShelves);
 
        // Скидаємо стан інспекції
        _inspectStarted    = false;
        _inspectingTimer   = 0f;
        _inspectTargetTime = 0f;
 
        // Оновлюємо DesiredGenre з нового слоту ShoppingList
        DesiredGenre = Personality.CurrentDesiredGenre;
 
        Debug.Log($"[NPCBrain] {Data?.npcName}: → книга {Personality.CurrentBookIndex + 1}/" +
                  $"{Personality.WantsToBuy} ({DesiredGenre}). " +
                  $"Кошик: {Personality.Basket.Count}");
    }



    private void DestroyNPC()
    {
        if (_isDestroyPending) return;
        _isDestroyPending = true;
        Ticker.Deactivate();
        OnNPCLeft?.Invoke();
        Destroy(gameObject, 0.1f);
    }
}