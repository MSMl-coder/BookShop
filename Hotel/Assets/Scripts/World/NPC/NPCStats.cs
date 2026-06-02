// Assets/Scripts/World/NPC/NPCStats.cs
// Pure C# клас — живі показники одного NPC-візиту.
// Діапазон: -100 .. +100 для Mood, Comfort, Patience.
// Wallet: 0..100 (% від MaxBudget).
//
// Tick() викликається кожен кадр із NPCStatsTicker.
// Не має залежностей від UnityEngine крім Mathf.

using System;
using UnityEngine;

public class NPCStats
{
    // ─────────────────────────────────────────────
    // Constants
    // ─────────────────────────────────────────────

    public const float MIN    = -100f;
    public const float MAX    =  100f;
    public const float WALLET_MIN = 0f;
    public const float WALLET_MAX = 100f;

    // ─────────────────────────────────────────────
    // Live Values (read-only зовні)
    // ─────────────────────────────────────────────

    public float Mood     { get; private set; }
    public float Comfort  { get; private set; }
    public float Patience { get; private set; }

    /// Залишок бюджету у % (100 = повний MaxBudget, 0 = витрачено все).
    public float Wallet   { get; private set; }

    // ─────────────────────────────────────────────
    // Events
    // ─────────────────────────────────────────────

    /// Будь-який показник змінився — передає себе для UI.
    public event Action<NPCStats> OnChanged;

    /// Patience досягла мінімуму — NPC повинен піти.
    public event Action OnPatienceDepleted;

    // ─────────────────────────────────────────────
    // Init
    // ─────────────────────────────────────────────

    /// <param name="initialMood">Стартовий настрій (з NPCPersonality.InitialMoodValue)</param>
    /// <param name="initialPatience">Стартове терпіння (з NPCPersonality)</param>
    /// <param name="walletPercent">Початковий бюджет (зазвичай 100)</param>
    public void Initialize(float initialMood, float initialPatience, float walletPercent = 100f)
    {
        Mood     = Mathf.Clamp(initialMood,     MIN, MAX);
        Comfort  = 0f;
        Patience = Mathf.Clamp(initialPatience, MIN, MAX);
        Wallet   = Mathf.Clamp(walletPercent,   WALLET_MIN, WALLET_MAX);
    }

    // ─────────────────────────────────────────────
    // Tick — викликається кожен кадр
    // ─────────────────────────────────────────────

    /// <param name="deltaTime">Time.deltaTime</param>
    /// <param name="moodRiseRate">З ShopAtmosphereService (сума декорацій)</param>
    /// <param name="comfortRate">0 = не сидить; > 0 = comfortForce пропа</param>
    /// <param name="patienceRestoreBonus">patienceRestoreBonus пропа (0 якщо не сидить)</param>
    /// <param name="config">NPCStatConfig зі сцени</param>
    public void Tick(
        float deltaTime,
        float moodRiseRate,
        float comfortRate,
        float patienceRestoreBonus,
        NPCStatConfig config)
    {
        bool changed = false;

        // ── Mood ─────────────────────────────────────────────────
        // Декорації піднімають, час спускає. Нетто = різниця.
        float moodNet = (moodRiseRate - config.moodDecayRate) * deltaTime;
        float newMood = Mathf.Clamp(Mood + moodNet, MIN, MAX);
        if (!Mathf.Approximately(Mood, newMood))
        {
            Mood    = newMood;
            changed = true;
        }

        // ── Comfort ──────────────────────────────────────────────
        float comfortDelta = comfortRate > 0f
            ?  comfortRate             * deltaTime   // сидить — росте
            : -config.comfortDecayRate * deltaTime;  // не сидить — спадає
        float newComfort = Mathf.Clamp(Comfort + comfortDelta, MIN, MAX);
        if (!Mathf.Approximately(Comfort, newComfort))
        {
            Comfort = newComfort;
            changed = true;
        }

        // ── Patience ─────────────────────────────────────────────
        // Comfort і Mood сповільнюють спад; сидіння відновлює.
        float patienceNewVal;
        if (comfortRate > 0f)
        {
            // Відпочинок — відновлення
            float restore = (config.patienceRestoreWhileResting + patienceRestoreBonus) * deltaTime;
            patienceNewVal = Mathf.Clamp(Patience + restore, MIN, MAX);
        }
        else
        {
            // Не сидить — спад зі сповільненням від Comfort+Mood
            float comfortFactor = Mathf.Lerp(
                1f, config.comfortPatienceSlowdown,
                Mathf.InverseLerp(0f, MAX, Comfort));

            float moodFactor = Mathf.Lerp(
                1f, config.moodPatienceSlowdown,
                Mathf.InverseLerp(0f, MAX, Mood));

            float effectiveDecay = config.patienceDecayRate / (comfortFactor * moodFactor);
            patienceNewVal = Mathf.Clamp(Patience - effectiveDecay * deltaTime, MIN, MAX);
        }

        if (!Mathf.Approximately(Patience, patienceNewVal))
        {
            Patience = patienceNewVal;
            changed  = true;
        }

        if (Patience <= MIN)
            OnPatienceDepleted?.Invoke();

        if (changed)
            OnChanged?.Invoke(this);
    }

    // ─────────────────────────────────────────────
    // Mutations
    // ─────────────────────────────────────────────

    /// Витратити частину бюджету. fraction = 0..1 (частка від MaxBudget).
    public void SpendWallet(float fraction)
    {
        Wallet  = Mathf.Clamp(Wallet - fraction * 100f, WALLET_MIN, WALLET_MAX);
        OnChanged?.Invoke(this);
    }

    // ─────────────────────────────────────────────
    // Derived helpers — читаються з NPCBrain
    // ─────────────────────────────────────────────

    /// Множник BuyChance від поточного Mood: Mood=-100 → 0.3x, Mood=+100 → 1.5x.
    public float BuyChanceMultiplier =>
        Mathf.Lerp(0.3f, 1.5f, Mathf.InverseLerp(MIN, MAX, Mood));

    /// WantsToBuy може збільшитись на 1 якщо комфорт перевищив поріг.
    public bool ExtraShelfUnlocked(NPCStatConfig config) =>
        Comfort >= config.extraShelfComfortThreshold;

    /// Чи хоче NPC відпочити (шукає Seating).
    public bool WantsRest(NPCStatConfig config) =>
        Patience < config.restingPatienceThreshold;

    /// Чи може NPC зробити імпульсну покупку.
    public bool CanImpulseBuy(NPCStatConfig config) =>
        Mood >= config.impulseBuyMoodThreshold;

    /// Бюджет критично низький — NPC ігнорує дорогі книги.
    public bool IsBudgetLow => Wallet < 20f;

    public override string ToString() =>
        $"Mood={Mood:F0} Comfort={Comfort:F0} Patience={Patience:F0} Wallet={Wallet:F0}%";
}