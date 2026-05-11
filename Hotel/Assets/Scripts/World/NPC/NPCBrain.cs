// Assets/Scripts/World/NPC/NPCBrain.cs
// ВИПРАВЛЕНО:
//   CS1061 BookWorldItem.Reserve()    — тепер є в BookWorldItem (делегує до Shelf)
//   CS1061 BookWorldItem.Unreserve()  — тепер є в BookWorldItem (делегує до Shelf)
//   CS1061 BookWorldItem.IsReserved   — тепер є в BookWorldItem (делегує до Shelf)
//   CS1061 Shelf.RemoveBook(item)     → Shelf.TakeBookAt(item.bookIndex)
//   CS0618 FindObjectsByType obsolete → FindObjectsByType<T>()
//
// ЛОГІКА НЕ ЗМІНЕНА — тільки виправлені API calls.
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCBrain : MonoBehaviour
{
    // --- Public State ---
    public NPCData      Data         { get; private set; }
    public NPCState     CurrentState { get; private set; }
    public BookGenre    DesiredGenre { get; private set; }
    public BookTemplate FoundBook    { get; private set; }

    // --- Events ---
    public event Action<NPCState>    OnStateChanged;
    public event Action<BookTemplate> OnBookFound;
    public event Action              OnPurchaseComplete;
    public event Action              OnNPCLeft;

    // --- Private ---
    private NavMeshAgent   _agent;
    private NPCInteractionUI _ui;
    private ShelfScanner   _scanner;
    private CashRegister   _cashRegister;

    private float         _stayTimer;
    private float         _browseTimer;
    private List<Shelf>   _visitedShelves    = new List<Shelf>();
    private Shelf         _currentTargetShelf;
    private bool          _isInitialized;

    // ВИПРАВЛЕНО: кешуємо список полиць один раз
    private Shelf[]       _cachedShelves;

    // Зберігаємо знайдену книгу + її індекс для TakeBookAt()
    private BookWorldItem _foundBookItem;
    private int           _foundBookIndex = -1;
    private Shelf         _foundBookShelf;
 // НОВЕ — Фаза 1: зарезервована книга в 3D-сцені
    private BookWorldItem _reservedBookWorldItem;

    // --- Init ---
    public void Initialize(NPCData data, CashRegister cashRegister)
    {
        Data          = data;
        _cashRegister = cashRegister;
        _agent        = GetComponent<NavMeshAgent>();
        _ui           = GetComponent<NPCInteractionUI>();
        _scanner      = GetComponent<ShelfScanner>();

        if (data.preferredGenres != null && data.preferredGenres.Length > 0)
            DesiredGenre = data.preferredGenres[
                UnityEngine.Random.Range(0, data.preferredGenres.Length)];

        _stayTimer     = data.stayDuration;
        _isInitialized = true;

        // ВИПРАВЛЕНО CS0618: прибрано FindObjectsSortMode
        _cachedShelves = FindObjectsByType<Shelf>(FindObjectsInactive.Exclude);
        Debug.Log($"[NPC] {data.npcName} initialized. Wants: {DesiredGenre}. " +
                  $"Shelves cached: {_cachedShelves.Length}");

        ChangeState(NPCState.Entering);
    }

    private void Update()
    {
        if (!_isInitialized) return;

        _stayTimer -= Time.deltaTime;

        if (_stayTimer <= 0f &&
            CurrentState != NPCState.Leaving &&
            CurrentState != NPCState.Buying)
        {
            Debug.Log($"[NPC] {Data.npcName} time expired. Leaving.");
            CleanupReservation();
            ChangeState(NPCState.Leaving);
            return;
        }

        UpdateCurrentState();
    }

    // --- State Machine ---
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
            Shelf nextShelf = FindUnvisitedShelf();
            if (nextShelf != null)
            {
                _currentTargetShelf = nextShelf;
                _visitedShelves.Add(nextShelf);
                _agent.SetDestination(
                    nextShelf.transform.position + nextShelf.transform.forward * 1.2f);
                ChangeState(NPCState.Inspecting);
            }
            else
            {
                Debug.Log($"[NPC] {Data.npcName} checked all shelves. Showing genre hint.");
                ChangeState(NPCState.ShowingHint);
            }
        }

        if (_browseTimer >= Data.browseTimeBeforeHint &&
            CurrentState == NPCState.Browsing)
            ChangeState(NPCState.ShowingHint);
    }

    private IEnumerator InspectShelf()
    {
        if (_currentTargetShelf == null) yield break;

        yield return StartCoroutine(LookAtTarget(_currentTargetShelf.transform.position));

        float inspectTime = UnityEngine.Random.Range(2f, 5f);
        yield return new WaitForSeconds(inspectTime);

        // ShelfScanner тепер повертає індекс, не GO
        int bookIdx = _scanner.ReserveBookForNPC(
            _currentTargetShelf, DesiredGenre, Data.maxBudget, name);

        if (bookIdx >= 0)
        {
            // Зберігаємо позицію для CompletePurchase
            _foundBookIndex = bookIdx;
            _foundBookShelf = _currentTargetShelf;
            FoundBook       = BookDatabase.Instance?.GetBook(
                _currentTargetShelf.GetBookData(bookIdx).templateID);

            // Матеріалізуємо книгу для показу гравцю (hover-ефект)
            if (FoundBook?.containerPrefab != null)
            {
                _currentTargetShelf.MaterializeBookForInteraction(
                    bookIdx, FoundBook.containerPrefab);
                _foundBookItem = _currentTargetShelf._materializedBookRef;
            }

            OnBookFound?.Invoke(FoundBook);
            Debug.Log($"[NPC] {Data.npcName} found: {FoundBook?.title}!");

            if (UnityEngine.Random.value <= Data.buyChance)
                ChangeState(NPCState.Buying);
            else
            {
                CleanupReservation();
                ChangeState(NPCState.Leaving);
            }
        }
        else
        {
            ChangeState(NPCState.Browsing);
        }
    }

    public void ReceiveBookOffer(BookTemplate offeredBook)
    {
        if (offeredBook == null ||
            (CurrentState != NPCState.ShowingHint &&
             CurrentState != NPCState.WaitingForPlayer))
            return;

        if (offeredBook.genre == DesiredGenre &&
            offeredBook.sellPrice <= Data.maxBudget)
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

        // ВИПРАВЛЕНО: shelf.RemoveBook(item) → shelf.TakeBookAt(index)
        if (_foundBookShelf != null && _foundBookIndex >= 0)
        {
            // Демате GO якщо є
            if (_foundBookItem != null)
                _foundBookShelf.DematerializeBook(_foundBookItem);

            // Забираємо книгу з полиці
            _foundBookShelf.TakeBookAt(_foundBookIndex);
            _foundBookItem  = null;
            _foundBookIndex = -1;
            _foundBookShelf = null;
        }

        EconomyManager.Instance?.RecordBookSold(FoundBook.sellPrice);
        OnPurchaseComplete?.Invoke();
        Debug.Log($"[NPC] {Data.npcName} bought {FoundBook.title} for ${FoundBook.sellPrice}");

        ChangeState(NPCState.Leaving);
    }

    // --- State Change ---
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

 /// Викликається CashDeskBuffer при EndDay.
    /// Знімає резервацію, прибирає книгу з полиці, відправляє NPC до виходу.
    /// Повертає BookInstance або null якщо NPC не тримав книгу.
    public BookInstance ForceLeaveAndTakeBook()
    {
        BookInstance result = null;

        if (_reservedBookWorldItem != null)
        {
            result = _reservedBookWorldItem.instance;

            _reservedBookWorldItem.Unreserve();

            if (_reservedBookWorldItem.parentShelf != null)
                _reservedBookWorldItem.parentShelf.DematerializeBook(_reservedBookWorldItem);

            _reservedBookWorldItem = null;
            FoundBook = null;

            Debug.Log($"[NPC] {Data?.npcName}: EndDay → '{result?.templateID}' → стіл каси.");
        }

        if (_isInitialized && CurrentState != NPCState.Leaving)
            ChangeState(NPCState.Leaving);

        return result;
    }
    // --- Helpers ---

    /// Знімає резервацію якщо NPC іде без покупки
    private void CleanupReservation()
    {
        if (_foundBookShelf != null && _foundBookIndex >= 0)
        {
            _foundBookShelf.UnreserveBook(_foundBookIndex);

            if (_foundBookItem != null)
                _foundBookShelf.DematerializeBook(_foundBookItem);

            _foundBookItem  = null;
            _foundBookIndex = -1;
            _foundBookShelf = null;
        }
    }

    private Shelf FindUnvisitedShelf()
    {
        if (_cachedShelves == null) return null;
        foreach (var s in _cachedShelves)
            if (s != null && !_visitedShelves.Contains(s) && s.GetBookCount() > 0)
                return s;
        return null;
    }

    private bool AgentArrived()
    {
        if (_agent == null || _agent.pathPending) return false;
        return _agent.remainingDistance <= _agent.stoppingDistance + 0.1f;
    }

    private IEnumerator LookAtTarget(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
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
        CleanupReservation();
        OnNPCLeft?.Invoke();
        Destroy(gameObject);
    }

    public float GetRemainingTimeNormalized() =>
        Data != null && Data.stayDuration > 0f
            ? Mathf.Clamp01(_stayTimer / Data.stayDuration)
            : 0f;
}