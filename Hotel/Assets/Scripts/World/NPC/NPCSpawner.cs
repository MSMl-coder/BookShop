// Assets/Scripts/World/NPC/NPCSpawner.cs
// ЗМІНИ: SpawnNPC тепер зважено обирає NPC тип залежно від рівня крамниці.
// Низький рівень → більше низькорівневих NPC.
// Високий рівень → більше високорівневих + VIP.
//
// NPCData отримав новий optional int minShopLevel (0 = доступний завжди).

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NPCSpawner : MonoBehaviour
{
    public static NPCSpawner Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform    spawnPoint;
    [SerializeField] private Transform    exitPoint;
    [SerializeField] private CashRegister cashRegister;

    [Header("NPC Pool")]
    [Tooltip("Всі можливі типи NPC. Вагу кожного визначає ShopLevel.")]
    [SerializeField] private List<NPCData> availableNPCTypes;

    [Header("Spawn Settings")]
    [SerializeField] private float minSpawnInterval    = 15f;
    [SerializeField] private float maxSpawnInterval    = 40f;
    [SerializeField] private int   maxSimultaneousNPCs = 4;

    [Header("Level Scaling")]
    [Tooltip("Крива: X = нормалізований рівень крамниці [0..1], Y = мінімальний рівень NPC що може спавнитись.\n" +
             "Наприклад: рівень 1 → NPC рівень 1-3, рівень 5 → NPC рівень 5-10.")]
    [SerializeField] private AnimationCurve minNPCLevelCurve = AnimationCurve.Linear(0f, 1f, 1f, 6f);

    [Tooltip("Чим вищий рівень крамниці — тим більше шанс отримати NPC вищого рівня.\n" +
             "Крива: X = нормалізований рівень магазину, Y = бонус до ваги для NPC level 7+.")]
    [SerializeField] private AnimationCurve highLevelBonusCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 3f);

    public Transform ExitPoint => exitPoint;

    private int  _currentNPCCount = 0;
    private bool _isSpawning      = false;
    private bool _firstNPCSpawned = false;

    // ── Unity ────────────────────────────────────────────────────

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

    // ── State ────────────────────────────────────────────────────

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.WorkDay) StartSpawning();
        else                            StopSpawning();
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

    // ── Spawn Loop ───────────────────────────────────────────────

    private IEnumerator SpawnLoop()
    {
        while (_isSpawning)
        {
            yield return new WaitForSeconds(Random.Range(minSpawnInterval, maxSpawnInterval));
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
            Debug.LogWarning("[Spawner] BookDatabase not ready.");
            return;
        }

        NPCData data = PickNPCByShopLevel();
        if (data?.prefab == null) return;

        var npcObj = Instantiate(data.prefab, spawnPoint.position, spawnPoint.rotation);
        var brain  = npcObj.GetComponent<NPCBrain>();

        if (brain == null)
        {
            Debug.LogError("[Spawner] NPC prefab missing NPCBrain!");
            Destroy(npcObj);
            return;
        }

        if (!_firstNPCSpawned)
        {
            _firstNPCSpawned = true;
            TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnFirstSale);
        }

        brain.Initialize(data, cashRegister);
        brain.OnNPCLeft     += () => _currentNPCCount--;
        brain.OnStateChanged += state =>
        {
            if (state == NPCState.Buying) cashRegister.JoinQueue(brain);
        };

        _currentNPCCount++;
        Debug.Log($"[Spawner] Spawned {data.npcName}. Total: {_currentNPCCount}");
    }

    // ── Level-weighted NPC selection ─────────────────────────────

    /// Зважений вибір NPC: вищий рівень крамниці = більше шансів отримати дорогого NPC.
    private NPCData PickNPCByShopLevel()
    {
        float shopLevelNorm = ShopLevelProvider.Instance?.LevelNormalized ?? 0f;

        // Мінімальний NPC рівень що може прийти при цьому рівні крамниці
        float minNPCLevel   = minNPCLevelCurve.Evaluate(shopLevelNorm);

        // Бонус ваги для елітних NPC при високому рівні крамниці
        float highBonus     = highLevelBonusCurve.Evaluate(shopLevelNorm);

        // Збираємо зважений пул
        var   pool          = new List<(NPCData data, float weight)>();
        float totalWeight   = 0f;

        foreach (var npcData in availableNPCTypes)
        {
            if (npcData == null || npcData.prefab == null) continue;

            // Базова вага — рівномірна
            float weight = 1f;

            // Генеруємо "очікуваний рівень" NPC на основі його бюджету як proxy
            // (справжній рівень буде відомий тільки після Generate, тут апроксимуємо)
            float expectedLevel = Mathf.Lerp(1f, 10f,
                Mathf.Clamp01(npcData.maxBudget / 200f));

            // NPC занадто низького рівня для цієї крамниці — зменшуємо вагу
            if (expectedLevel < minNPCLevel)
                weight *= Mathf.Lerp(0.1f, 1f, expectedLevel / Mathf.Max(minNPCLevel, 1f));

            // Бонус для елітних NPC (рівень 7+)
            if (expectedLevel >= 7f)
                weight += highBonus;

            pool.Add((npcData, weight));
            totalWeight += weight;
        }

        if (pool.Count == 0) return availableNPCTypes[0];

        // Зважений рандом
        float roll   = Random.Range(0f, totalWeight);
        float cursor = 0f;
        foreach (var (data, weight) in pool)
        {
            cursor += weight;
            if (roll <= cursor) return data;
        }

        return pool[pool.Count - 1].data;
    }
}