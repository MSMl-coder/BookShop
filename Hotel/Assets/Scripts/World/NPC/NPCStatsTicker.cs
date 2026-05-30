// Assets/Scripts/World/NPC/NPCStatsTicker.cs  v2
// ЗМІНИ:
//   [1] Null-config fallback — якщо NPCStatConfig не призначено,
//       використовуються вбудовані константи щоб Patience завжди спадала
//   [2] Hard session cap — абсолютний максимум часу в магазині (180с за замовчуванням)
//       гарантує що NPC завжди покине крамницю навіть якщо баг у логіці

using UnityEngine;

[RequireComponent(typeof(NPCBrain))]
public class NPCStatsTicker : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────
    [Header("Config (необов'язково — є fallback)")]
    [SerializeField] private NPCStatConfig _config;

    [Header("Hard session cap")]
    [Tooltip("Абсолютний максимум секунд в магазині. NPC піде незалежно від Patience.")]
    [SerializeField] private float hardSessionMaxTime = 180f;

    // ── [FIX 1] Fallback константи коли _config == null ─────────
    private const float FALLBACK_MOOD_DECAY     = 2f;
    private const float FALLBACK_COMFORT_DECAY  = 1.5f;
    private const float FALLBACK_PATIENCE_DECAY = 1.5f;  // трохи швидше ніж дефолт
    private const float FALLBACK_PATIENCE_RESTORE = 0.8f;
    private const float FALLBACK_MOOD_RISE_CAP  = 8f;

    // ── Public ───────────────────────────────────────────────────
    public NPCStats Stats { get; private set; }

    public float CurrentComfortRate   { get; private set; }
    public float CurrentPatienceBonus { get; private set; }
    public bool  IsResting            { get; private set; }

    // ── Private ──────────────────────────────────────────────────
    private NPCBrain _brain;
    private bool     _isActive;
    private float    _sessionTimer;  // [FIX 2]

    // ── Unity ─────────────────────────────────────────────────────
    private void Awake()
    {
        _brain = GetComponent<NPCBrain>();
        Stats  = new NPCStats();

        if (_config == null)
            _config = Resources.Load<NPCStatConfig>("NPCStatConfig");

        if (_config == null)
            Debug.LogWarning("[NPCStatsTicker] NPCStatConfig не знайдено — використовую fallback. " +
                             "Створи: Assets → Bookstore → NPC Stat Config і збережи у Resources/");
    }

    private void Update()
    {
        if (!_isActive) return;

        _sessionTimer += Time.deltaTime;

        // [FIX 2] Жорсткий ліміт сесії
        if (_sessionTimer >= hardSessionMaxTime)
        {
            Debug.LogWarning($"[StatsTicker] {_brain?.Data?.npcName}: hard session cap ({hardSessionMaxTime}s) → Leaving");
            OnPatienceDepleted();
            return;
        }

        float moodRise = ShopAtmosphereService.Instance?.MoodRiseRate ?? 0f;

        // [FIX 1] Tick з config або з fallback
        if (_config != null)
        {
            Stats.Tick(Time.deltaTime, moodRise, CurrentComfortRate, CurrentPatienceBonus, _config);
        }
        else
        {
            TickFallback(Time.deltaTime, moodRise);
        }
    }

    // ── [FIX 1] Fallback Tick без NPCStatConfig ───────────────────
    private void TickFallback(float dt, float moodRise)
    {
        // Будуємо тимчасовий inline config з константами
        // NPCStats.Tick вимагає NPCStatConfig — замість нього ручний tick
        // (щоб не дублювати формули — просто прямо змінюємо через TemporaryConfig)
        if (_tempFallbackConfig == null)
        {
            _tempFallbackConfig = ScriptableObject.CreateInstance<NPCStatConfig>();
            _tempFallbackConfig.moodDecayRate               = FALLBACK_MOOD_DECAY;
            _tempFallbackConfig.comfortDecayRate             = FALLBACK_COMFORT_DECAY;
            _tempFallbackConfig.patienceDecayRate            = FALLBACK_PATIENCE_DECAY;
            _tempFallbackConfig.patienceRestoreWhileResting  = FALLBACK_PATIENCE_RESTORE;
            _tempFallbackConfig.comfortPatienceSlowdown      = 2f;
            _tempFallbackConfig.moodPatienceSlowdown         = 1.5f;
            _tempFallbackConfig.moodRiseCap                  = FALLBACK_MOOD_RISE_CAP;
            _tempFallbackConfig.diversityBonusMultiplier     = 1.2f;
            _tempFallbackConfig.diversityMinCategories       = 2;
            _tempFallbackConfig.restingPatienceThreshold     = 30f;
            _tempFallbackConfig.extraShelfComfortThreshold   = 50f;
            _tempFallbackConfig.impulseBuyMoodThreshold      = 70f;
            _tempFallbackConfig.impulseBudgetMultiplier      = 1.3f;
        }
        Stats.Tick(dt, moodRise, CurrentComfortRate, CurrentPatienceBonus, _tempFallbackConfig);
    }
    private NPCStatConfig _tempFallbackConfig;

    // ── Init / Deactivate ─────────────────────────────────────────
    public void Initialize(NPCPersonality personality)
    {
        Stats.Initialize(
            personality.InitialMoodValue,
            personality.InitialPatience,
            100f);

        Stats.OnPatienceDepleted += OnPatienceDepleted;
        _sessionTimer = 0f;
        _isActive     = true;
    }

    public void Deactivate()
    {
        _isActive = false;
        if (Stats != null) Stats.OnPatienceDepleted -= OnPatienceDepleted;
        if (_tempFallbackConfig != null)
        {
            Destroy(_tempFallbackConfig);
            _tempFallbackConfig = null;
        }
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
        if (maxBudget > 0f)
            Stats.SpendWallet(bookPrice / maxBudget);
    }

    // ── Helpers для NPCBrain ──────────────────────────────────────
    public bool WantsRest     => (_config ?? _tempFallbackConfig) != null && Stats.WantsRest(_config ?? _tempFallbackConfig);
    public bool ExtraShelf    => (_config ?? _tempFallbackConfig) != null && Stats.ExtraShelfUnlocked(_config ?? _tempFallbackConfig);
    public bool CanImpulseBuy => (_config ?? _tempFallbackConfig) != null && Stats.CanImpulseBuy(_config ?? _tempFallbackConfig);
    public bool IsBudgetLow   => Stats.IsBudgetLow;

    public float GetAdjustedBuyChance(float baseBuyChance) =>
        Mathf.Clamp01(baseBuyChance * Stats.BuyChanceMultiplier);

    // ── Internal ──────────────────────────────────────────────────
    private void OnPatienceDepleted()
    {
        if (!_isActive) return;
        Debug.Log($"[StatsTicker] {_brain?.Data?.npcName}: Patience depleted → Leaving");
        _brain?.ChangeState(NPCState.Leaving);
    }
}