// Assets/Scripts/World/NPC/NPCStatsTicker.cs
// MonoBehaviour-обгортка над NPCStats.
// Живе на префабі NPC поряд з NPCBrain.
// Відповідальність:
//   - Ініціалізація NPCStats з NPCPersonality
//   - Tick кожен кадр (читає ShopAtmosphereService + SeatRegistry)
//   - Пробрасування подій до NPCBrain
//
// NPCBrain не знає про деталі tick-логіки — тільки читає Stats через Ticker.

using UnityEngine;

[RequireComponent(typeof(NPCBrain))]
public class NPCStatsTicker : MonoBehaviour
{
    // ─────────────────────────────────────────────
    [Header("Config")]
    // ─────────────────────────────────────────────

    [Tooltip("ScriptableObject з усіма константами швидкостей. " +
             "Один на проєкт — призначити в Inspector або підтягнеться з Resources.")]
    [SerializeField] private NPCStatConfig _config;

    // ─────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────

    public NPCStats Stats { get; private set; }

    /// Поточний комфорт-рейт (0 = не сидить, > 0 = сидить на конкретному пропі).
    public float CurrentComfortRate      { get; private set; }
    public float CurrentPatienceBonus    { get; private set; }
    public bool  IsResting               { get; private set; }

    // ─────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────

    private NPCBrain _brain;
    private bool     _isActive;

    // ─────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────

    private void Awake()
    {
        _brain = GetComponent<NPCBrain>();
        Stats  = new NPCStats();

        // Якщо config не призначено в Inspector — шукаємо в Resources
        if (_config == null)
            _config = Resources.Load<NPCStatConfig>("NPCStatConfig");

        if (_config == null)
            Debug.LogError("[NPCStatsTicker] NPCStatConfig не знайдено! " +
                           "Створи через Assets → Bookstore → NPC Stat Config");
    }

    private void Update()
    {
        if (!_isActive || _config == null) return;

        float moodRise = ShopAtmosphereService.Instance?.MoodRiseRate ?? 0f;

        Stats.Tick(
            Time.deltaTime,
            moodRise,
            CurrentComfortRate,
            CurrentPatienceBonus,
            _config);
    }

    // ─────────────────────────────────────────────
    // Init — викликається NPCBrain.Initialize()
    // ─────────────────────────────────────────────

    public void Initialize(NPCPersonality personality)
    {
        if (_config == null) return;

        Stats.Initialize(
            initialMood:     personality.InitialMoodValue,
            initialPatience: personality.InitialPatience,
            walletPercent:   100f);

        Stats.OnPatienceDepleted += OnPatienceDepleted;

        _isActive = true;

        Debug.Log($"[StatsTicker] Init: {Stats}");
    }

    public void Deactivate()
    {
        _isActive = false;
        if (Stats != null) Stats.OnPatienceDepleted -= OnPatienceDepleted;
    }

    // ─────────────────────────────────────────────
    // Seating control — викликається NPCBrain
    // ─────────────────────────────────────────────

    /// NPC сів на меблю з вказаними параметрами.
    public void BeginResting(float comfortForce, float patienceBonus)
    {
        CurrentComfortRate   = comfortForce;
        CurrentPatienceBonus = patienceBonus;
        IsResting            = true;
    }

    /// NPC встав з меблів.
    public void StopResting()
    {
        CurrentComfortRate   = 0f;
        CurrentPatienceBonus = 0f;
        IsResting            = false;
    }

    // ─────────────────────────────────────────────
    // Wallet
    // ─────────────────────────────────────────────

    /// Викликати після кожної покупки книги.
    /// fraction = bookPrice / MaxBudget (нормований 0..1).
    public void RegisterPurchase(float bookPrice, float maxBudget)
    {
        if (maxBudget <= 0f) return;
        Stats.SpendWallet(bookPrice / maxBudget);
    }

    // ─────────────────────────────────────────────
    // Helpers для NPCBrain
    // ─────────────────────────────────────────────

    public bool WantsRest       => _config != null && Stats.WantsRest(_config);
    public bool ExtraShelf      => _config != null && Stats.ExtraShelfUnlocked(_config);
    public bool CanImpulseBuy   => _config != null && Stats.CanImpulseBuy(_config);
    public bool IsBudgetLow     => Stats.IsBudgetLow;

    /// Скоригований BuyChance (базовий × множник від Mood).
    public float GetAdjustedBuyChance(float baseBuyChance) =>
        Mathf.Clamp01(baseBuyChance * Stats.BuyChanceMultiplier);

    // ─────────────────────────────────────────────
    // Internal
    // ─────────────────────────────────────────────

    private void OnPatienceDepleted()
    {
        Debug.Log($"[StatsTicker] {_brain?.Data?.npcName}: Patience = 0 → Leaving");
        _brain?.ChangeState(NPCState.Leaving);
    }
}