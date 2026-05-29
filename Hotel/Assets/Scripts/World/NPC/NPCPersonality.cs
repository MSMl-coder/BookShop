// Assets/Scripts/World/NPC/NPCPersonality.cs
// ЗМІНИ v2:
//   - Додано InitialMoodValue — початковий Mood для NPCStats (конвертується з enum Mood)
//   - Додано InitialPatience — початкове терпіння для NPCStats
//   - Додано UniqueID для SeatRegistry
//   - StayDuration видалено з логіки — замінено на Patience (NPCStatsTicker)
//
// Решта логіки незмінна.

using UnityEngine;

public class NPCPersonality
{
    // ── Що хоче купити ──────────────────────────────────────────
    public BookGenre  DesiredGenre  { get; private set; }
    public BookRarity MinRarity     { get; private set; }
    public BookRarity MaxRarity     { get; private set; }
    public float      MaxBudget     { get; private set; }
    public int        WantsToBuy    { get; private set; }
    public int        BooksBought   { get; set;         }

    // ── Поведінка ───────────────────────────────────────────────
    public float BuyChance      { get; private set; }
    public float StayDuration   { get; private set; } // залишено для сумісності
    public float InspectTimeMin { get; private set; }
    public float InspectTimeMax { get; private set; }
    public int   ShelvesToVisit { get; private set; }

    // ── Настрій ─────────────────────────────────────────────────
    public enum Mood { Relaxed, Rushed, Picky, Impulsive }
    public Mood CurrentMood { get; private set; }

    // ── [NEW] Початкові значення для NPCStats ───────────────────
    /// Стартовий Mood у числовому вигляді (-100..+100) для NPCStats.Initialize().
    public float InitialMoodValue    { get; private set; }

    /// Стартове Patience (-100..+100) — визначається архетипом і Mood.
    public float InitialPatience     { get; private set; }

    // ── [NEW] Унікальний ID для SeatRegistry ────────────────────
    public string UniqueID { get; private set; }

    // ── Derived ──────────────────────────────────────────────────
    public bool WantsMoreBooks => BooksBought < WantsToBuy;

    public bool AcceptsRarity(BookRarity rarity) =>
        rarity >= MinRarity && rarity <= MaxRarity;

    public float GetInspectTime() =>
        Random.Range(InspectTimeMin, InspectTimeMax);

    public string DebugString() =>
        $"[{CurrentMood} mood={InitialMoodValue:F0} patience={InitialPatience:F0}] " +
        $"Genre:{DesiredGenre} Rarity:[{MinRarity}–{MaxRarity}] " +
        $"Budget:{MaxBudget:F0} Wants:{WantsToBuy} BuyChance:{BuyChance:P0} " +
        $"Shelves:{ShelvesToVisit}";

    // ────────────────────────────────────────────────────────────
    // Generation
    // ────────────────────────────────────────────────────────────

    public static NPCPersonality Generate(NPCData data)
    {
        var p = new NPCPersonality();

        p.UniqueID = System.Guid.NewGuid().ToString();

        // ── Жанр ────────────────────────────────────────────────
        if (data.preferredGenres != null && data.preferredGenres.Length > 0 && Random.value < 0.8f)
            p.DesiredGenre = data.preferredGenres[Random.Range(0, data.preferredGenres.Length)];
        else
            p.DesiredGenre = (BookGenre)Random.Range(0, System.Enum.GetValues(typeof(BookGenre)).Length);

        // ── Рарність ────────────────────────────────────────────
        p.MinRarity = data.minAcceptableRarity;
        p.MaxRarity = data.maxAcceptableRarity;

        // ── Бюджет ──────────────────────────────────────────────
        float mult   = Random.Range(data.budgetMultiplierMin, data.budgetMultiplierMax);
        float budget = data.maxBudget * mult;
        p.MaxBudget  = Mathf.Max(Mathf.Round(budget / 5f) * 5f, 5f);

        // ── К-ть книг ───────────────────────────────────────────
        float buyRoll = Random.value;
        int want;
        if      (buyRoll < 0.55f) want = 1;
        else if (buyRoll < 0.78f) want = 2;
        else if (buyRoll < 0.92f) want = 3;
        else                      want = Random.Range(4, data.maxBooksToBuy + 1);
        p.WantsToBuy  = Mathf.Clamp(want, data.minBooksToBuy, data.maxBooksToBuy);
        p.BooksBought = 0;

        // ── Настрій ─────────────────────────────────────────────
        p.CurrentMood = RollMood(data);

        // ── Поведінка залежно від настрою ───────────────────────
        switch (p.CurrentMood)
        {
            case Mood.Rushed:
                p.BuyChance      = Mathf.Clamp(data.buyChance * Random.Range(1.1f, 1.3f), 0f, 1f);
                p.StayDuration   = data.stayDuration * Random.Range(0.35f, 0.55f);
                p.ShelvesToVisit = Mathf.Max(1, data.shelvesToInspect - Random.Range(1, 3));
                p.InspectTimeMin = data.inspectTimeMin * 0.5f;
                p.InspectTimeMax = data.inspectTimeMax * 0.6f;
                break;

            case Mood.Relaxed:
                p.BuyChance      = data.buyChance * Random.Range(0.85f, 1.05f);
                p.StayDuration   = data.stayDuration * Random.Range(0.9f, 1.2f);
                p.ShelvesToVisit = data.shelvesToInspect;
                p.InspectTimeMin = data.inspectTimeMin;
                p.InspectTimeMax = data.inspectTimeMax;
                break;

            case Mood.Picky:
                p.BuyChance      = Mathf.Clamp(data.buyChance * Random.Range(0.5f, 0.7f), 0f, 1f);
                p.StayDuration   = data.stayDuration * Random.Range(0.7f, 0.9f);
                p.ShelvesToVisit = data.shelvesToInspect + Random.Range(0, 2);
                p.InspectTimeMin = data.inspectTimeMin * 1.2f;
                p.InspectTimeMax = data.inspectTimeMax * 1.5f;
                break;

            case Mood.Impulsive:
                p.BuyChance      = Mathf.Clamp(data.buyChance * Random.Range(1.3f, 1.6f), 0f, 1f);
                p.StayDuration   = data.stayDuration * Random.Range(0.5f, 0.8f);
                p.ShelvesToVisit = Mathf.Max(1, data.shelvesToInspect - 1);
                p.InspectTimeMin = data.inspectTimeMin * 0.4f;
                p.InspectTimeMax = data.inspectTimeMax * 0.5f;
                break;
        }

        // ── [NEW] Конвертація Mood enum → числові значення для NPCStats ──
        p.InitialMoodValue = MoodToInitialValue(p.CurrentMood);
        p.InitialPatience  = RollInitialPatience(p.CurrentMood, data);

        return p;
    }

    // ─────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────

    private static Mood RollMood(NPCData data)
    {
        if (Random.value < data.impulsiveMoodChance) return Mood.Impulsive;

        int totalWeight = 40; // Relaxed завжди
        if (data.canBeRushed) totalWeight += 30;
        if (data.canBePicky)  totalWeight += 30;

        int roll = Random.Range(0, totalWeight);

        if (data.canBeRushed && roll < 30)   return Mood.Rushed;
        roll -= data.canBeRushed ? 30 : 0;
        if (data.canBePicky  && roll < 30)   return Mood.Picky;

        return Mood.Relaxed;
    }

    /// Конвертує enum Mood у стартовий числовий Mood для NPCStats.
    private static float MoodToInitialValue(Mood mood) => mood switch
    {
        Mood.Relaxed   =>  Random.Range(10f,  35f),   // приємний настрій
        Mood.Rushed    =>  Random.Range(-20f,  5f),   // злегка негативний
        Mood.Picky     =>  Random.Range(-30f, -5f),   // незадоволений
        Mood.Impulsive =>  Random.Range(35f,  60f),   // збуджений/ейфорія
        _              =>  0f
    };

    /// Початкове Patience залежно від Mood та архетипу.
    /// Rushed = мало часу → низьке Patience; Relaxed = багато → високе.
    private static float RollInitialPatience(Mood mood, NPCData data)
    {
        // Базова patience з stayDuration (нормалізовано відносно 60с)
        float baseFactor = Mathf.Clamp(data.stayDuration / 60f, 0.3f, 2f);

        return mood switch
        {
            Mood.Relaxed   => Random.Range(40f, 70f) * baseFactor,
            Mood.Rushed    => Random.Range(10f, 30f) * baseFactor,
            Mood.Picky     => Random.Range(25f, 50f) * baseFactor,
            Mood.Impulsive => Random.Range(20f, 45f) * baseFactor,
            _              => 50f
        };
    }
}