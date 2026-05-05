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
    public BookEnums.BookGenre DesiredGenre { get; private set; }
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

    // --- Init ---
    public void Initialize(NPCData data, CashRegister cashRegister)
    {
        Data = data;
        _cashRegister = cashRegister;
        _agent = GetComponent<NavMeshAgent>();
        _ui = GetComponent<NPCInteractionUI>();
        _scanner = GetComponent<ShelfScanner>();

        // Pick a random desired genre from preferences
        if (data.preferredGenres != null && data.preferredGenres.Length > 0)
            DesiredGenre = data.preferredGenres[UnityEngine.Random.Range(0, data.preferredGenres.Length)];

        _stayTimer = data.stayDuration;
        _isInitialized = true;

        Debug.Log($"[NPC] {data.npcName} initialized. Wants: {DesiredGenre}");
        ChangeState(NPCState.Entering);
    }

    private void Update()
    {
        if (!_isInitialized) return;

        _stayTimer -= Time.deltaTime;

        // Force leave if time runs out
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
                // Wait until agent reaches entry point
                if (AgentArrived()) ChangeState(NPCState.Browsing);
                break;

            case NPCState.Browsing:
                UpdateBrowsing();
                break;

            case NPCState.Inspecting:
                // Wait until agent reaches shelf
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

        // Pick next shelf to inspect
        if (AgentArrived() || _currentTargetShelf == null)
        {
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
                // Checked all shelves — show hint
                Debug.Log($"[NPC] {Data.npcName} checked all shelves. Showing genre hint.");
                ChangeState(NPCState.ShowingHint);
            }
        }

        // Show hint icon after browseTimeBeforeHint even while browsing
        if (_browseTimer >= Data.browseTimeBeforeHint && CurrentState == NPCState.Browsing)
        {
            ChangeState(NPCState.ShowingHint);
        }
    }

    private IEnumerator InspectShelf()
    {
        if (_currentTargetShelf == null) yield break;

        // Look at shelf (face it)
        yield return StartCoroutine(LookAtTarget(_currentTargetShelf.transform.position));

        // Simulate browsing time
        float inspectTime = UnityEngine.Random.Range(2f, 5f);
        yield return new WaitForSeconds(inspectTime);

        // Scan shelf for desired genre
        BookTemplate found = _scanner.FindBookOnShelf(_currentTargetShelf, DesiredGenre, Data.maxBudget);

        if (found != null)
        {
            FoundBook = found;
            OnBookFound?.Invoke(found);
            Debug.Log($"[NPC] {Data.npcName} found: {found.title}!");

            // Check buy chance
            if (UnityEngine.Random.value <= Data.buyChance)
                ChangeState(NPCState.Buying);
            else
                ChangeState(NPCState.Leaving);
        }
        else
        {
            // Continue browsing
            ChangeState(NPCState.Browsing);
        }
    }

    // Called by PlayerInteraction when player offers a book
    public void ReceiveBookOffer(BookTemplate offeredBook)
    {
        if (offeredBook == null || CurrentState != NPCState.ShowingHint &&
            CurrentState != NPCState.WaitingForPlayer) return;

        if (offeredBook.genre == DesiredGenre && offeredBook.sellPrice <= Data.maxBudget)
        {
            FoundBook = offeredBook;
            Debug.Log($"[NPC] {Data.npcName} accepted offer: {offeredBook.title}");
            ChangeState(NPCState.Buying);
        }
        else
        {
            Debug.Log($"[NPC] {Data.npcName} rejected offer.");
            _ui.ShowRejection();
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
                ChangeState(NPCState.WaitingForPlayer);
                break;
        }
    }

    // --- Helpers ---
    private Shelf FindUnvisitedShelf()
    {
        var allShelves = FindObjectsByType<Shelf>(FindObjectsSortMode.None);
        List<Shelf> unvisited = new List<Shelf>();

        foreach (var s in allShelves)
            if (!_visitedShelves.Contains(s)) unvisited.Add(s);

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