// Assets/Scripts/World/NPC/NPCStatsTicker.cs
// ФІКСИ:
//   [1] Явний захист від Stats = null перед Tick() — запобігає NullRef якщо
//       Initialize() не викликали
//   [2] Явний лог коли _isActive = true/false для діагностики
//   [3] _sessionTimer не рахується поки _isActive = false
//   [4] Захист від подвійного виклику OnPatienceDepleted()

using UnityEngine;

[RequireComponent(typeof(NPCBrain))]
public class NPCStatsTicker : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────
    [Header("Config (необов'язково — є fallback)")]
    [SerializeField] private NPCStatConfig _config;

    [Header("Hard session cap")]
    [Tooltip("Абсолютний максимум секунд в магазині.")]
    [SerializeField] private float hardSessionMaxTime = 180f;

    // ── Fallback константи ────────────────────────────────────────
    private const float FALLBACK_MOOD_DECAY      = 2f;
    private const float FALLBACK_COMFORT_DECAY   = 1.5f;
    private const float FALLBACK_PATIENCE_DECAY  = 1.5f;
    private const float FALLBACK_PATIENCE_RESTORE = 0.8f;
    private const float FALLBACK_MOOD_RISE_CAP   = 8f;

    // ── Public ────────────────────────────────────────────────────
    public NPCStats Stats { get; private set; }

    public float CurrentComfortRate   { get; private set; }
    public float CurrentPatienceBonus { get; private set; }
    public bool  IsResting            { get; private set; }

    // ── Private ───────────────────────────────────────────────────
    private NPCBrain      _brain;
    private bool          _isActive;
    private bool          _patienceDepletedFired;  // ✅ [4] guard проти подвійного виклику
    private float         _sessionTimer;
    private NPCStatConfig _tempFallbackConfig;

    // ── Helpers ───────────────────────────────────────────────────
    private NPCStatConfig ActiveConfig => _config ?? _tempFallbackConfig;

    public bool WantsRest     => ActiveConfig != null && Stats != null && Stats.WantsRest(ActiveConfig);
    public bool ExtraShelf    => ActiveConfig != null && Stats != null && Stats.ExtraShelfUnlocked(ActiveConfig);
    public bool CanImpulseBuy => ActiveConfig != null && Stats != null && Stats.CanImpulseBuy(ActiveConfig);
    public bool IsBudgetLow   => Stats?.IsBudgetLow ?? false;

    public float GetAdjustedBuyChance(float baseBuyChance) =>
        Stats != null ? Mathf.Clamp01(baseBuyChance * Stats.BuyChanceMultiplier) : baseBuyChance;

    // ── Unity ─────────────────────────────────────────────────────
    private void Awake()
    {
        _brain = GetComponent<NPCBrain>();
        Stats  = new NPCStats();

        if (_config == null)
            _config = Resources.Load<NPCStatConfig>("NPCStatConfig");

        if (_config == null)
            Debug.LogWarning("[NPCStatsTicker] NPCStatConfig не знайдено в Resources/ — " +
                             "використовую fallback. Створи: Assets → Bookstore → NPC Stat Config");
    }

    private void Update()
    {
        // ✅ [1] Захист: якщо не ініціалізований або деактивований — нічого не робимо
        if (!_isActive || Stats == null) return;

        _sessionTimer += Time.deltaTime;

        // Hard session cap
        if (_sessionTimer >= hardSessionMaxTime)
        {
            Debug.LogWarning($"[StatsTicker] {_brain?.Data?.npcName}: " +
                             $"hard session cap ({hardSessionMaxTime}s) → Leaving");
            OnPatienceDepleted();
            return;
        }

        float moodRise = ShopAtmosphereService.Instance?.MoodRiseRate ?? 0f;
        var   config   = ActiveConfig;

        if (config != null)
            Stats.Tick(Time.deltaTime, moodRise, CurrentComfortRate, CurrentPatienceBonus, config);
        else
            TickFallback(Time.deltaTime, moodRise);
    }

    // ── Init / Deactivate ─────────────────────────────────────────

    public void Initialize(NPCPersonality personality)
    {
        if (personality == null)
        {
            Debug.LogError("[NPCStatsTicker] Initialize called with null personality!");
            return;
        }

        Stats.Initialize(
            personality.InitialMoodValue,
            personality.InitialPatience,
            100f);

        Stats.OnPatienceDepleted += OnPatienceDepleted;

        _sessionTimer          = 0f;
        _patienceDepletedFired = false;
        _isActive              = true;   // ✅ явно встановлюємо

        Debug.Log($"[StatsTicker] Initialized: Mood={personality.InitialMoodValue:F0} " +
                  $"Patience={personality.InitialPatience:F0} _isActive=true");
    }

    public void Deactivate()
    {
        if (!_isActive) return;  // ✅ [4] не деактивуємо двічі

        _isActive = false;

        if (Stats != null)
            Stats.OnPatienceDepleted -= OnPatienceDepleted;

        if (_tempFallbackConfig != null)
        {
            Destroy(_tempFallbackConfig);
            _tempFallbackConfig = null;
        }

        Debug.Log($"[StatsTicker] Deactivated: {_brain?.Data?.npcName}");
    }

    // ── Seating ───────────────────────────────────────────────────

    public void BeginResting(float comfortForce, float patienceBonus)
    {
        CurrentComfortRate   = comfortForce;
        CurrentPatienceBonus = patienceBonus;
        IsResting            = true;
    }

    public void StopResting()
    {
        CurrentComfortRate   = 0f;
        CurrentPatienceBonus = 0f;
        IsResting            = false;
    }

    // ── Purchase ──────────────────────────────────────────────────

    public void RegisterPurchase(float bookPrice, float maxBudget)
    {
        if (maxBudget > 0f && Stats != null)
            Stats.SpendWallet(bookPrice / maxBudget);
    }

    // ── Internal ──────────────────────────────────────────────────

    private void OnPatienceDepleted()
    {
        if (!_isActive) return;

        // ✅ [4] Захист від подвійного виклику (OnPatienceDepleted може прийти
        // і з Stats.OnPatienceDepleted і з hard session cap)
        if (_patienceDepletedFired) return;
        _patienceDepletedFired = true;

        Debug.Log($"[StatsTicker] {_brain?.Data?.npcName}: Patience depleted → Leaving");
        _brain?.ChangeState(NPCState.Leaving);
    }

    // ── Fallback tick ─────────────────────────────────────────────

    private void TickFallback(float dt, float moodRise)
    {
        if (_tempFallbackConfig == null)
        {
            _tempFallbackConfig = ScriptableObject.CreateInstance<NPCStatConfig>();
            _tempFallbackConfig.moodDecayRate               = FALLBACK_MOOD_DECAY;
            _tempFallbackConfig.comfortDecayRate            = FALLBACK_COMFORT_DECAY;
            _tempFallbackConfig.patienceDecayRate           = FALLBACK_PATIENCE_DECAY;
            _tempFallbackConfig.patienceRestoreWhileResting = FALLBACK_PATIENCE_RESTORE;
            _tempFallbackConfig.comfortPatienceSlowdown     = 2f;
            _tempFallbackConfig.moodPatienceSlowdown        = 1.5f;
            _tempFallbackConfig.moodRiseCap                 = FALLBACK_MOOD_RISE_CAP;
            _tempFallbackConfig.diversityBonusMultiplier    = 1.2f;
            _tempFallbackConfig.diversityMinCategories      = 2;
            _tempFallbackConfig.restingPatienceThreshold    = 30f;
            _tempFallbackConfig.extraShelfComfortThreshold  = 50f;
            _tempFallbackConfig.impulseBuyMoodThreshold     = 70f;
            _tempFallbackConfig.impulseBudgetMultiplier     = 1.3f;
        }
        Stats.Tick(dt, moodRise, CurrentComfortRate, CurrentPatienceBonus, _tempFallbackConfig);
    }
}