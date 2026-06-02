// Assets/Scripts/World/NPC/NPCBrain.cs  v10
// ЗМІНИ v10 (всі попередні патчі в одному файлі):
//   [FIX-COMPILE] DesiredGenre = Personality.DesiredGenre
//                 → Personality.CurrentDesiredGenre (NPCPersonality v4 API)
//   [FIX-INSPECT] _inspectTargetTime — кешується один раз при _inspectStarted
//                 (GetInspectTime більше НЕ викликається кожен кадр)
//   [FIX-MULTIBOOK] ResetForNextBook() — скидає _visitedShelves + новий жанр
//                   після кожної знайденої/не знайденої книги
//   [FIX-LEAVING] _leavingTimer — 0.5s guard проти миттєвого DestroyNPC
//   [FIX-EXIT] null ExitPoint — DelayedDestroy(3f) замість тихого зникнення
//   [FIX-OFFER] ReceiveBookOffer: Personality.DesiredGenre → DesiredGenre

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NPCStatsTicker))]
public class NPCBrain : MonoBehaviour
{
    // ── Public properties ─────────────────────────────────────────
    public NPCData        Data         { get; private set; }
    public NPCState       CurrentState { get; private set; }
    public BookGenre      DesiredGenre { get; private set; }  // жанр поточного слоту
    public BookTemplate   FoundBook    { get; private set; }
    public NPCPersonality Personality  { get; private set; }
    public NPCStatsTicker Ticker       { get; private set; }

    public float PatientNormalized =>
        Ticker?.Stats == null ? 1f
        : Mathf.InverseLerp(NPCStats.MIN, NPCStats.MAX, Ticker.Stats.Patience);

    // ── Events ────────────────────────────────────────────────────
    public event Action<NPCState>     OnStateChanged;
    public event Action<BookTemplate> OnBookFound;
    public event Action<BookTemplate> OnBookPickedUp;
    public event Action               OnPurchaseComplete;
    public event Action               OnNPCLeft;

    // ── Private refs ──────────────────────────────────────────────
    private NavMeshAgent  _agent;
    private NPCWorldUI    _worldUI;
    private ShelfScanner  _scanner;
    private CashRegister  _cashRegister;

    // ── State data ────────────────────────────────────────────────
    private float          _enteringTimer;
    private bool           _isInitialized;
    private bool           _isDestroyPending;

    private List<Shelf>    _visitedShelves    = new List<Shelf>();
    private HashSet<Shelf> _fullShelves       = new HashSet<Shelf>();
    private Shelf          _currentTargetShelf;
    private Shelf[]        _cachedShelves;

    private bool           _inspectStarted;
    private int            _playerOfferAttempts;
    private float          _inspectingTimer;
    private float          _inspectTargetTime;  // [FIX-INSPECT] кешований час огляду

    private float          _waitingTimer;
    [SerializeField] private float waitingForPlayerTimeout = 30f;

    private bool           _restingArrived;
    private float          _restingTimer;
    private const float    MAX_REST_DURATION = 30f;

    private float          _stayTimer;
    private bool           _stayTimerStarted;

    private float          _leavingTimer;  // [FIX-LEAVING] guard проти миттєвого destroy

    // ── Init ──────────────────────────────────────────────────────
    public void Initialize(NPCData data, CashRegister cashRegister)
    {
        Data          = data;
        _cashRegister = cashRegister;
        _agent        = GetComponent<NavMeshAgent>();
        _worldUI      = GetComponent<NPCWorldUI>();
        _scanner      = GetComponent<ShelfScanner>();
        Ticker        = GetComponent<NPCStatsTicker>();

        Personality  = NPCPersonality.Generate(data);
        // ✅ [FIX-COMPILE] NPCPersonality v4 — DesiredGenre видалено, є CurrentDesiredGenre
        DesiredGenre = Personality.CurrentDesiredGenre;

        Ticker.Initialize(Personality);

        var allShelves = ShelfRegistry.Instance != null
            ? new List<Shelf>(ShelfRegistry.Instance.GetAll()).ToArray()
            : FindObjectsByType<Shelf>(FindObjectsSortMode.None);
        _cachedShelves = Shuffle(allShelves);

        _isInitialized = true;
        ChangeState(NPCState.Entering);
        TrySetDestination(SampleNavMesh(transform.position));

        Debug.Log($"[NPCBrain] {data.npcName} spawned | " +
                  $"Mood:{Personality.CurrentMood} Books:{Personality.WantsToBuy} " +
                  $"OnNavMesh:{_agent?.isOnNavMesh} Shelves:{_cachedShelves.Length}");
    }

    // ── Unity ─────────────────────────────────────────────────────
    private void Update()
    {
        if (!_isInitialized || _isDestroyPending) return;
        UpdateStayTimer();
        UpdateCurrentState();
    }

    private void OnDestroy() { /* NPCStatsTicker.Deactivate знімає підписки */ }

    // ── State Timer ───────────────────────────────────────────────
    private void UpdateStayTimer()
    {
        if (!_stayTimerStarted) return;
        if (CurrentState == NPCState.Buying || CurrentState == NPCState.Leaving) return;

        _stayTimer += Time.deltaTime;
        float limit = Personality.StayDuration;
        if (limit <= 0f || _stayTimer < limit) return;

        _stayTimerStarted = false;
        Debug.Log($"[NPCBrain] {Data?.npcName}: stayDuration {limit:F0}s закінчився");
        NPCInspectorMount.Instance?.Controller?.ShowSpeechBubble(
                Data?.GetRandomStayEnd() ?? "Мені вже час!",
                Data?.stayEnd.duration ?? 4f);
        StartCoroutine(LeaveAfterSpeech(3f));
    }

    private IEnumerator LeaveAfterSpeech(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_isDestroyPending) GoToCashier();
    }

    // ── Public API ────────────────────────────────────────────────
    public void ChangeState(NPCState newState)
    {
        // Перехоплення Leaving: якщо є книги в кошику — каса
        if (newState == NPCState.Leaving
            && CurrentState != NPCState.Buying
            && Personality != null
            && Personality.BasketNotEmpty
            && !_isDestroyPending)
        {
            Debug.Log($"[NPCBrain] {Data?.npcName}: Leaving intercepted → GoToCashier " +
                      $"(basket={Personality.Basket.Count})");
            GoToCashier();
            return;
        }

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);

        // ✅ [FIX-LEAVING] скидаємо таймер виходу
        if (newState == NPCState.Leaving) _leavingTimer = 0f;

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
            case NPCState.CollectingBooks:
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
                    if (Personality.BasketNotEmpty) GoToCashier();
                    else ChangeState(NPCState.Leaving);
                }
                break;

            case NPCState.Buying:
                if (AgentArrived()) CompletePurchase();
                break;

            case NPCState.Leaving:
                // ✅ [FIX-LEAVING] мінімум 0.5s перед перевіркою arrival
                _leavingTimer += Time.deltaTime;
                if (_leavingTimer >= 0.5f && AgentArrived()) DestroyNPC();
                break;
        }
    }

    // ── Browsing ──────────────────────────────────────────────────
    private void UpdateBrowsing()
    {
        if (!AgentArrived() && _currentTargetShelf != null) return;
        if (Ticker.WantsRest && TryBeginResting()) return;

        int   limit = Data.shelvesToInspect + (Ticker.ExtraShelf ? 1 : 0);
        Shelf next  = FindNextShelf(limit);

        if (next != null)
        {
            _currentTargetShelf = next;
            ShelfAccessRegistry.Instance?.TryClaim(_currentTargetShelf,
                                                    Personality.UniqueID, out _);
            _visitedShelves.Add(next);
            TrySetDestination(SampleNavMesh(next.transform.position));
            ChangeState(NPCState.Inspecting);
        }
        else
        {
            // ✅ [FIX-MULTIBOOK] всі полиці для поточного слоту переглянуті — наступний слот
            bool hasMore = Personality.AdvanceToNextBook();
            if (hasMore)
                ResetForNextBook();  // нові полиці + новий жанр, лишаємось у Browsing/Collecting
            else
                GoToCashier();      // всі слоти вичерпано
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
            // ✅ [FIX-INSPECT] кешуємо час огляду ОДИН РАЗ — не random щокадру
            _inspectTargetTime = Personality.GetInspectTime();

            if (_currentTargetShelf != null && _currentTargetShelf.GetBookCount() == 0)
            {
                _fullShelves.Add(_currentTargetShelf);
                ReleaseCurrentShelf();
                ChangeState(CurrentState == NPCState.CollectingBooks
                    ? NPCState.CollectingBooks : NPCState.Browsing);
                return;
            }
        }

        _inspectingTimer += Time.deltaTime;
        if (_inspectingTimer < _inspectTargetTime) return;  // порівняння з кешованим значенням

        // Час огляду вичерпано
        var result = _scanner?.FindBook(_currentTargetShelf, Personality);
        ReleaseCurrentShelf();

        if (result.HasValue)
        {
            FoundBook = result.Value.template;
            OnBookFound?.Invoke(FoundBook);

            // BuyChance вже враховує настрій (знижений у Generate()).
            // НЕ застосовуємо GetAdjustedBuyChance() — уникаємо подвійного штрафу.
            // ≥0.99 = гарантована покупка (float-safe для Clamp(1.0 * mult, 0, 1)).
            bool buy = Personality.BuyChance >= 0.99f
                    || UnityEngine.Random.value <= Personality.BuyChance
                    || Ticker.CanImpulseBuy;

            if (buy)
            {
                _currentTargetShelf?.TakeBookAt(result.Value.index);
                Personality.AddToBasket(result.Value.template);
                OnBookPickedUp?.Invoke(result.Value.template);

                Debug.Log($"[NPCBrain] {Data?.npcName} взяв «{result.Value.template.title}» " +
                          $"(кошик {Personality.Basket.Count}/{Personality.WantsToBuy})");

                // ✅ [FIX-MULTIBOOK] переходимо до наступного слоту
                bool hasMore = Personality.AdvanceToNextBook();
                if (hasMore && !Ticker.IsBudgetLow)
                {
                    ResetForNextBook();
                    ChangeState(NPCState.CollectingBooks);
                }
                else
                {
                    GoToCashier();
                }
            }
            else
            {
                bool hasMore = Personality.AdvanceToNextBook();
                if (hasMore)
                {
                    ResetForNextBook();
                    ChangeState(CurrentState == NPCState.CollectingBooks
                        ? NPCState.CollectingBooks : NPCState.Browsing);
                }
                else
                {
                    GoToCashier();
                }
            }
        }
        else
        {
            if (_currentTargetShelf != null) _fullShelves.Add(_currentTargetShelf);
            ChangeState(CurrentState == NPCState.CollectingBooks
                ? NPCState.CollectingBooks : NPCState.Browsing);
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
                ? NPCState.CollectingBooks : NPCState.Browsing);
        }
    }

    // ── GoToCashier ───────────────────────────────────────────────
    private void GoToCashier()
    {
        if (!Personality.BasketNotEmpty)
        {
            Debug.Log($"[NPCBrain] {Data?.npcName}: кошик порожній → Leaving");
            ChangeState(NPCState.Leaving);

            // ✅ [FIX-EXIT] null guard — не виставляємо destination на поточну позицію
            var exitPos = NPCSpawner.Instance?.ExitPoint?.position;
            if (exitPos.HasValue)
                TrySetDestination(SampleNavMesh(exitPos.Value));
            else
            {
                Debug.LogWarning($"[NPCBrain] {Data?.npcName}: ExitPoint не призначений! " +
                                 "Зникне через 3с.");
                StartCoroutine(DelayedDestroy(3f));
            }
            return;
        }

        Debug.Log($"[NPCBrain] {Data?.npcName}: → каса ({Personality.Basket.Count} книг)");
        if (_cashRegister != null)
            TrySetDestination(SampleNavMesh(_cashRegister.transform.position));
        else
            Debug.LogWarning($"[NPCBrain] {Data?.npcName}: CashRegister не призначений!");

        ChangeState(NPCState.Buying);
    }

    private IEnumerator DelayedDestroy(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_isDestroyPending) DestroyNPC();
    }

    // ── CompletePurchase ──────────────────────────────────────────
    private void CompletePurchase()
    {
        float totalPrice = 0f;
        foreach (var book in Personality.Basket)
        {
            EconomyManager.Instance?.RecordBookSold(book.sellPrice);
            totalPrice += book.sellPrice;
        }
        Ticker.RegisterPurchase(totalPrice, Personality.MaxBudget);
        OnPurchaseComplete?.Invoke();

        Debug.Log($"[NPCBrain] {Data?.npcName}: оплатив {Personality.Basket.Count} книги " +
                  $"${totalPrice:F0}");

        Personality.ClearBasket();
        ChangeState(NPCState.Leaving);

        var exitPos = NPCSpawner.Instance?.ExitPoint?.position;
        if (exitPos.HasValue)
            TrySetDestination(SampleNavMesh(exitPos.Value));
        else
            StartCoroutine(DelayedDestroy(3f));
    }

    // ── ForceEndOfDay (викликається CashDeskBuffer / GameLoopManager при завершенні WorkDay) ──
    /// З книгами → нормально іде на касу та оплачує.
    /// Без книг → прощальне повідомлення + виходить.
    public BookInstance ForceLeaveAndTakeBook()
    {
        if (CurrentState == NPCState.Resting && _restingArrived)
            Ticker.StopResting();
        SeatRegistry.Instance?.Release(Personality.UniqueID);
        ReleaseCurrentShelf();
        _stayTimerStarted = false;

        if (Personality.BasketNotEmpty)
        {
            // Є книги → нормальна оплата на касі
            Debug.Log($"[NPCBrain] {Data?.npcName}: EndOfDay → GoToCashier " +
                      $"(basket={Personality.Basket.Count})");
            GoToCashier();
        }
        else
        {
            // Нічого не купив — прощається
            string msg = Data?.stayEnd.HasMessages == true
                ? Data.GetRandomStayEnd()
                : (Data?.GetRandomReject() ?? "Шкода що не вистачило часу!");
            float dur = Data?.stayEnd.duration ?? 4f;

            NPCInspectorMount.Instance?.Controller?.ShowSpeechBubble(msg, dur);
            // _worldUI не має ShowSpeechBubble — лише ShowRejectionFeedback (shake + flash)

            Debug.Log($"[NPCBrain] {Data?.npcName}: EndOfDay → порожній кошик → Leaving");
            StartCoroutine(LeaveAfterSpeech(3f));
        }
        return null;
    }

    // ── Player offer ──────────────────────────────────────────────
    public void ReceiveBookOffer(BookTemplate offeredBook)
    {
        if (CurrentState != NPCState.WaitingForPlayer) return;
        _playerOfferAttempts++;

        // ✅ [FIX-OFFER] порівнюємо з NPCBrain.DesiredGenre (вже оновлений per-slot)
        if (offeredBook != null
            && Personality.AcceptsRarity(offeredBook.rarity)
            && offeredBook.genre == DesiredGenre
            && offeredBook.sellPrice <= Personality.MaxBudget * (Ticker.Stats.Wallet / 100f))
        {
            FoundBook = offeredBook;
            OnBookFound?.Invoke(FoundBook);
            Personality.AddToBasket(offeredBook);
            OnBookPickedUp?.Invoke(offeredBook);

            bool hasMore = Personality.AdvanceToNextBook();
            if (hasMore && !Ticker.IsBudgetLow)
            {
                ResetForNextBook();
                ChangeState(NPCState.CollectingBooks);
            }
            else
            {
                GoToCashier();
            }
        }
        else
        {
            string reason = offeredBook == null          ? "Це не книга..."
                : offeredBook.genre != DesiredGenre      ? $"Не мій жанр ({DesiredGenre})."
                : !Personality.AcceptsRarity(offeredBook.rarity) ? "Не та якість."
                : "Задорого.";
            Debug.Log($"[NPCBrain] {Data?.npcName}: відмовився — {reason}");
            _worldUI?.ShowRejectionFeedback(reason);

            int maxAttempts = Data?.maxPlayerOfferAttempts > 0 ? Data.maxPlayerOfferAttempts : 3;
            if (_playerOfferAttempts >= maxAttempts)
            {
                if (Personality.BasketNotEmpty) GoToCashier();
                else ChangeState(NPCState.Leaving);
            }
        }
    }

    // ── ResetForNextBook ──────────────────────────────────────────
    /// Скидає стан пошуку для наступного слоту покупки.
    private void ResetForNextBook()
    {
        _visitedShelves.Clear();
        _fullShelves.Clear();
        if (_cachedShelves != null) _cachedShelves = Shuffle(_cachedShelves);
        _inspectStarted    = false;
        _inspectingTimer   = 0f;
        _inspectTargetTime = 0f;
        DesiredGenre       = Personality.CurrentDesiredGenre;

        Debug.Log($"[NPCBrain] {Data?.npcName}: → слот {Personality.CurrentBookIndex + 1}/" +
                  $"{Personality.WantsToBuy} ({DesiredGenre})");
    }

    // ── Resting helper ────────────────────────────────────────────
    private bool TryBeginResting()
    {
        if (SeatRegistry.Instance == null) return false;
        if (!SeatRegistry.Instance.TryClaim(Personality.UniqueID,
                out PropTemplate seatTemplate, out Vector3 seatPos)) return false;

        TrySetDestination(SampleNavMesh(seatPos));
        ChangeState(NPCState.Resting);
        _restingArrived = false;
        return true;
    }

    private void ReleaseCurrentShelf()
    {
        if (_currentTargetShelf != null)
            ShelfAccessRegistry.Instance?.Release(_currentTargetShelf, Personality.UniqueID);
        _currentTargetShelf = null;
        _inspectStarted     = false;
        _inspectingTimer    = 0f;
        _inspectTargetTime  = 0f;
    }

    // ── FindNextShelf ─────────────────────────────────────────────
    private Shelf FindNextShelf(int limit)
    {
        if (_visitedShelves.Count >= limit) return null;
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

    // ── NavMesh helpers ───────────────────────────────────────────
    private bool AgentArrived() =>
        _agent != null && !_agent.pathPending
        && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f;

    private void TrySetDestination(Vector3 pos)
    {
        if (_agent == null)
        {
            Debug.LogWarning($"[NPCBrain] {Data?.npcName}: NavMeshAgent null!");
            return;
        }
        if (!_agent.isOnNavMesh)
        {
            Debug.LogWarning($"[NPCBrain] {Data?.npcName}: isOnNavMesh=false → не може рухатись. " +
                             "Перевір NavMesh bake та NavMeshAgent на prefab.");
            return;
        }
        _agent.SetDestination(pos);
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