using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCBrain : MonoBehaviour
{
    // --- Public State ---
    public NPCData Data { get; private set; }
    public NPCState CurrentState { get; private set; }
    public BookGenre DesiredGenre { get; private set; }
    public BookTemplate FoundBook { get; private set; }

    // --- Events ---
    public event Action<NPCState> OnStateChanged;
    public event Action<BookTemplate> OnBookFound;
    public event Action OnPurchaseComplete;
    public event Action OnNPCLeft;

    // --- Private ---
    private NavMeshAgent _agent;
    private NPCInteractionUI _ui;
    private ShelfScanner _scanner;
    private CashRegister _cashRegister;

    private float _stayTimer;
    private float _browseTimer;
    private List<Shelf> _visitedShelves = new List<Shelf>();
    private Shelf _currentTargetShelf;
    private bool _isInitialized;

    // ВИПРАВЛЕНО: кешуємо список полиць один раз щоб не викликати
    // FindObjectsByType щоразу в Update (дорога операція)
    private Shelf[] _cachedShelves;

    // --- Init ---
    public void Initialize(NPCData data, CashRegister cashRegister)
    {
        Data = data;
        _cashRegister = cashRegister;
        _agent = GetComponent<NavMeshAgent>();
        _ui = GetComponent<NPCInteractionUI>();
        _scanner = GetComponent<ShelfScanner>();

        if (data.preferredGenres != null && data.preferredGenres.Length > 0)
            DesiredGenre = data.preferredGenres[UnityEngine.Random.Range(0, data.preferredGenres.Length)];

        _stayTimer = data.stayDuration;
        _isInitialized = true;

        // Кешуємо полиці при ініціалізації NPC
        
        _cachedShelves = FindObjectsByType<Shelf>(FindObjectsSortMode.None);
        Debug.Log($"[NPC] {data.npcName} initialized. Wants: {DesiredGenre}. Shelves cached: {_cachedShelves.Length}");
        ChangeState(NPCState.Entering);
    }

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

    // --- State Machine Core ---
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
            // ВИПРАВЛЕНО: використовуємо закешовані полиці замість FindObjectsByType в Update
            Shelf nextShelf = FindUnvisitedShelf();

            if (nextShelf != null)
            {
                _currentTargetShelf = nextShelf;
                _visitedShelves.Add(nextShelf);
                _agent.SetDestination(nextShelf.transform.position + nextShelf.transform.forward * 1.2f);
                ChangeState(NPCState.Inspecting);
            }
            else
            {
                Debug.Log($"[NPC] {Data.npcName} checked all shelves. Showing genre hint.");
                ChangeState(NPCState.ShowingHint);
            }
        }

        if (_browseTimer >= Data.browseTimeBeforeHint && CurrentState == NPCState.Browsing)
        {
            ChangeState(NPCState.ShowingHint);
        }
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

    // ВИПРАВЛЕНО: додано дужки для явного пріоритету операторів &&/||
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

        EconomyManager.Instance?.RecordBookSold(FoundBook.sellPrice);
        OnPurchaseComplete?.Invoke();
        Debug.Log($"[NPC] {Data.npcName} bought {FoundBook.title} for ${FoundBook.sellPrice}");

        ChangeState(NPCState.Leaving);
    }

    // --- State Change ---
    // ВИПРАВЛЕНО: ShowingHint більше не викликає ChangeState рекурсивно в тому ж кадрі.
    // WaitingForPlayer тепер обробляється окремим case-ом.
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
                // ВИПРАВЛЕНО: замість рекурсивного ChangeState просто встановлюємо стан
                // WaitingForPlayer напряму. Раніше ShowingHint → ChangeState(WaitingForPlayer)
                // що могло спричинити подвійний виклик OnStateChanged.
                CurrentState = NPCState.WaitingForPlayer;
                OnStateChanged?.Invoke(NPCState.WaitingForPlayer);
                break;
        }
    }

    // --- Helpers ---

    // ВИПРАВЛЕНО: використовуємо _cachedShelves замість FindObjectsByType в методі
    // що викликається з Update — критичне покращення продуктивності
    private Shelf FindUnvisitedShelf()
    {
        if (_cachedShelves == null || _cachedShelves.Length == 0) return null;

        List<Shelf> unvisited = new List<Shelf>();
        foreach (var s in _cachedShelves)
            if (s != null && !_visitedShelves.Contains(s)) unvisited.Add(s);

        if (unvisited.Count == 0) return null;
        return unvisited[UnityEngine.Random.Range(0, unvisited.Count)];
    }

    private bool AgentArrived()
    {
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            return !_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f;
        return false;
    }

    private IEnumerator LookAtTarget(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;
        Quaternion targetRot = Quaternion.LookRotation(dir);
        float t = 0;
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
        Destroy(gameObject, 0.5f);
    }

    public float GetRemainingTimeNormalized() => Mathf.Clamp01(_stayTimer / Data.stayDuration);
    public float GetRemainingTime() => Mathf.Max(0f, _stayTimer);
}
