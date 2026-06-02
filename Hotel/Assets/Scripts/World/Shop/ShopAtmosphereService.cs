// Assets/Scripts/World/Shop/ShopAtmosphereService.cs
// Singleton — агрегує moodForce всіх розміщених Decor-пропів.
// Перераховується при зміні PlacementRegistry (не щокадру).
//
// Залежності:
//   - PlacementRegistry (повинен мати подію OnRegistryChanged)
//   - PropTemplate (читає isDecor, moodForce, decorCategory)
//   - NPCStatConfig (мoodRiseCap, diversityBonus)
//
// Результат: MoodRiseRate — читається NPCStatsTicker кожен кадр.

using System.Collections.Generic;
using UnityEngine;

public class ShopAtmosphereService : MonoBehaviour
{
    public static ShopAtmosphereService Instance { get; private set; }

    // ─────────────────────────────────────────────
    [Header("Config")]
    // ─────────────────────────────────────────────

    [Tooltip("Той самий NPCStatConfig що і в NPCStatsTicker.")]
    [SerializeField] private NPCStatConfig _config;

    // ─────────────────────────────────────────────
    // Public state
    // ─────────────────────────────────────────────

    /// Фінальний moodRiseRate магазину — читається кожен кадр NPCStatsTicker-ами.
    public float MoodRiseRate { get; private set; }

    /// Скільки унікальних DecorCategory зараз розміщено (для UI бонусів).
    public int ActiveCategoryCount { get; private set; }

    /// Чи активний diversity bonus зараз.
    public bool DiversityBonusActive =>
        _config != null && ActiveCategoryCount >= _config.diversityMinCategories;

    // ─────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────

    private readonly HashSet<DecorCategory> _activeCategories = new();

    // ─────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (_config == null)
            _config = Resources.Load<NPCStatConfig>("NPCStatConfig");
    }

    private void OnEnable()
    {
        if (PlacementRegistry.Instance != null)
            PlacementRegistry.Instance.OnRegistryChanged += Recalculate;
    }

    private void OnDisable()
    {
        if (PlacementRegistry.Instance != null)
            PlacementRegistry.Instance.OnRegistryChanged -= Recalculate;
    }

    private void Start() => Recalculate();

    // ─────────────────────────────────────────────
    // Core calculation
    // ─────────────────────────────────────────────

    /// Перераховується при кожній зміні PlacementRegistry.
    /// O(n) де n = к-ть розміщених пропів — прийнятно для магазину.
    public void Recalculate()
    {
        if (_config == null) return;

        float rawTotal = 0f;
        _activeCategories.Clear();

        if (PlacementRegistry.Instance != null)
        {
            foreach (var (go, inst) in PlacementRegistry.Instance.GetAll())
            {
                if (go == null) continue;

                // Отримуємо PropTemplate через InventoryManager
                var prop = InventoryManager.Instance?.GetPropTemplate(inst.propID);
                if (prop == null || !prop.IsDecor) continue;

                rawTotal += prop.moodForce;

                if (prop.decorCategory != DecorCategory.None)
                    _activeCategories.Add(prop.decorCategory);
            }
        }

        ActiveCategoryCount = _activeCategories.Count;

        // Diversity bonus
        float total = rawTotal;
        if (DiversityBonusActive)
            total *= _config.diversityBonusMultiplier;

        // Invisible cap
        MoodRiseRate = Mathf.Min(total, _config.moodRiseCap);

        Debug.Log($"[Atmosphere] MoodRiseRate={MoodRiseRate:F2} " +
                  $"(raw={rawTotal:F2} categories={ActiveCategoryCount} " +
                  $"diversity={DiversityBonusActive} cap={_config.moodRiseCap})");
    }

    // ─────────────────────────────────────────────
    // Debug
    // ─────────────────────────────────────────────

    public string GetDebugString() =>
        $"MoodRiseRate={MoodRiseRate:F1}/s | Categories={ActiveCategoryCount} " +
        $"| DiversityBonus={DiversityBonusActive}";
}