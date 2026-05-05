using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Spawns NPCs during WorkDay phase
public class NPCSpawner : MonoBehaviour
{
    public static NPCSpawner Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private CashRegister cashRegister;

    [Header("NPC Pool")]
    [SerializeField] private List<NPCData> availableNPCTypes;

    [Header("Spawn Settings")]
    [SerializeField] private float minSpawnInterval = 15f;
    [SerializeField] private float maxSpawnInterval = 40f;
    [SerializeField] private int maxSimultaneousNPCs = 4;

    public Transform ExitPoint => exitPoint;

    private int _currentNPCCount = 0;
    private bool _isSpawning = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.WorkDay)
        {
            Debug.Log("[Spawner] WorkDay started. Beginning NPC spawning.");
            StartSpawning();
        }
        else
        {
            Debug.Log("[Spawner] WorkDay ended. Stopping NPC spawning.");
            StopSpawning();
        }
    }

    public void StartSpawning()
    {
        if (_isSpawning) return;
        _isSpawning = true;
        StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        _isSpawning = false;
        StopAllCoroutines();
    }

    private IEnumerator SpawnLoop()
    {
        while (_isSpawning)
        {
            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);

            if (_currentNPCCount < maxSimultaneousNPCs)
                SpawnNPC();
        }
    }

    private void SpawnNPC()
    {
        if (availableNPCTypes == null || availableNPCTypes.Count == 0)
        {
            Debug.LogWarning("[Spawner] No NPC types assigned!");
            return;
        }

        if (BookDatabase.Instance == null)
        {
            Debug.LogWarning("[Spawner] BookDatabase not ready. Skipping spawn.");
            return;
        }

        // Pick random NPC type
        NPCData data = availableNPCTypes[Random.Range(0, availableNPCTypes.Count)];
        if (data.prefab == null) return;

        GameObject npcObj = Instantiate(data.prefab, spawnPoint.position, spawnPoint.rotation);
        NPCBrain brain = npcObj.GetComponent<NPCBrain>();

        if (brain == null)
        {
            Debug.LogError("[Spawner] NPC prefab missing NPCBrain component!");
            Destroy(npcObj);
            return;
        }

        brain.Initialize(data, cashRegister);
        brain.OnNPCLeft += () => _currentNPCCount--;
        brain.OnStateChanged += state =>
        {
            if (state == NPCState.Buying)
                cashRegister.JoinQueue(brain);
        };

        // Tutorial
        if (_currentNPCCount == 1) // Перший NPC
            TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnFirstSale);

        _currentNPCCount++;
        Debug.Log($"[Spawner] Spawned {data.npcName}. Total NPCs: {_currentNPCCount}");
    }
}