// Assets/Scripts/World/NPC/NPCPersonality.cs
// Генерується один раз при спавні — симулює "реальну людину".
//
// NPCData  = архетип (Студент/Турист/Професор) — задає МЕЖІ рандомізації.
// NPCPersonality = конкретна особа цього дня — унікальні параметри цього візиту.
//
// Використання:
//   var p = NPCPersonality.Generate(data);
//   brain.Initialize(data, p, cashRegister);

using UnityEngine;

public class NPCPersonality
{
    // ── Що хоче купити ──────────────────────────────────────────
    public BookGenre  DesiredGenre  { get; private set; }
    public BookRarity MinRarity     { get; private set; }
    public BookRarity MaxRarity     { get; private set; }
    public float      MaxBudget     { get; private set; }
    public int        WantsToBuy    { get; private set; } // к-ть книг за цей візит
    public int        BooksBought   { get; set;         } // лічильник куплених

    // ── Поведінка ───────────────────────────────────────────────
    public float BuyChance      { get; private set; }
    public float StayDuration   { get; private set; }
    public float InspectTimeMin { get; private set; }
    public float InspectTimeMax { get; private set; }
    public int   ShelvesToVisit { get; private set; }

    // ── Настрій ─────────────────────────────────────────────────
    public enum Mood { Relaxed, Rushed, Picky, Impulsive }
    public Mood CurrentMood { get; private set; }

    // ────────────────────────────────────────────────────────────
    // Генерація
    // ────────────────────────────────────────────────────────────

    public static NPCPersonality Generate(NPCData data)
    {
        var p = new NPCPersonality();

        // ── Жанр ────────────────────────────────────────────────
        // 80% — з preferredGenres архетипу, 20% — будь-який
        if (data.preferredGenres != null && data.preferredGenres.Length > 0
            && Random.value < 0.8f)
        {
            p.DesiredGenre = data.preferredGenres[Random.Range(0, data.preferredGenres.Length)];
        }
        else
        {
            var allGenres = (BookGenre[])System.Enum.GetValues(typeof(BookGenre));
            p.DesiredGenre = allGenres[Random.Range(0, allGenres.Length)];
        }

        // ── Рарність — в межах архетипу ────────────────────────
        // Розбиваємо допустимий діапазон на підзони з вагами
        int minR = (int)data.minAcceptableRarity;
        int maxR = (int)data.maxAcceptableRarity;

        float roll = Random.value;
        int chosenMin, chosenMax;

        if (roll < 0.50f)
        {
            // Нижня половина діапазону (типові покупки)
            chosenMin = minR;
            chosenMax = Mathf.Min(minR + 1, maxR);
        }
        else if (roll < 0.80f)
        {
            // Середній діапазон
            chosenMin = minR;
            chosenMax = Mathf.Min(minR + 2, maxR);
        }
        else if (roll < 0.95f)
        {
            // Верхній середній
            chosenMin = Mathf.Max(minR, maxR - 2);
            chosenMax = maxR;
        }
        else
        {
            // Рідко — весь дозволений діапазон
            chosenMin = minR;
            chosenMax = maxR;
        }

        p.MinRarity = (BookRarity)chosenMin;
        p.MaxRarity = (BookRarity)chosenMax;

        // ── Бюджет ──────────────────────────────────────────────
        float mult   = Random.Range(data.budgetMultiplierMin, data.budgetMultiplierMax);
        float budget = data.maxBudget * mult;
        p.MaxBudget  = Mathf.Max(Mathf.Round(budget / 5f) * 5f, 5f); // округлення до 5

        // ── К-ть книг ───────────────────────────────────────────
        // Нелінійний розподіл: більшість хоче 1 книгу
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
                // Поспішає — швидко вирішує, мало часу, мало полиць
                p.BuyChance      = Mathf.Clamp(data.buyChance * Random.Range(1.1f, 1.3f), 0f, 1f);
                p.StayDuration   = data.stayDuration * Random.Range(0.35f, 0.55f);
                p.ShelvesToVisit = Mathf.Max(1, data.shelvesToInspect - Random.Range(1, 3));
                p.InspectTimeMin = data.inspectTimeMin * 0.5f;
                p.InspectTimeMax = data.inspectTimeMax * 0.6f;
                break;

            case Mood.Relaxed:
                // Розслаблений — більше часу, більше полиць, середній buy chance
                p.BuyChance      = data.buyChance * Random.Range(0.85f, 1.05f);
                p.StayDuration   = data.stayDuration * Random.Range(1.1f, 1.6f);
                p.ShelvesToVisit = data.shelvesToInspect + Random.Range(0, 3);
                p.InspectTimeMin = data.inspectTimeMin * 1.2f;
                p.InspectTimeMax = data.inspectTimeMax * 1.4f;
                break;

            case Mood.Picky:
                // Перебірливий — довго дивиться, рідко купує, обходить багато полиць
                p.BuyChance      = data.buyChance * Random.Range(0.3f, 0.55f);
                p.StayDuration   = data.stayDuration * Random.Range(0.9f, 1.2f);
                p.ShelvesToVisit = data.shelvesToInspect + Random.Range(1, 4);
                p.InspectTimeMin = data.inspectTimeMin * 1.5f;
                p.InspectTimeMax = data.inspectTimeMax * 2.0f;
                break;

            case Mood.Impulsive:
                // Імпульсивний — майже завжди купує, швидко вирішує
                p.BuyChance      = Mathf.Clamp(data.buyChance * Random.Range(1.2f, 1.5f), 0f, 1f);
                p.StayDuration   = data.stayDuration * Random.Range(0.5f, 0.75f);
                p.ShelvesToVisit = Mathf.Max(1, data.shelvesToInspect - 1);
                p.InspectTimeMin = data.inspectTimeMin * 0.4f;
                p.InspectTimeMax = data.inspectTimeMax * 0.5f;
                break;
        }

        p.BuyChance = Mathf.Clamp01(p.BuyChance);
        return p;
    }

    // ────────────────────────────────────────────────────────────
    // Public API
    // ────────────────────────────────────────────────────────────

    /// Чи хоче NPC ще книг цього візиту.
    public bool WantsMoreBooks => BooksBought < WantsToBuy;

    /// Чи підходить ця рарність книги для цього NPC.
    public bool AcceptsRarity(BookRarity rarity) =>
        rarity >= MinRarity && rarity <= MaxRarity;

    /// Рандомний час огляду полиці.
    public float GetInspectTime() =>
        Random.Range(InspectTimeMin, InspectTimeMax);

    public string DebugString() =>
        $"[{CurrentMood}] Genre:{DesiredGenre} " +
        $"Rarity:[{MinRarity}–{MaxRarity}] " +
        $"Budget:{MaxBudget:F0} " +
        $"Wants:{WantsToBuy} book(s) " +
        $"BuyChance:{BuyChance:P0} " +
        $"Stay:{StayDuration:F0}s " +
        $"Shelves:{ShelvesToVisit}";

    // ────────────────────────────────────────────────────────────
    // Private helpers
    // ────────────────────────────────────────────────────────────

    private static Mood RollMood(NPCData data)
    {
        // Імпульсивний — окремий шанс з даних архетипу
        if (Random.value < data.impulsiveMoodChance)
            return Mood.Impulsive;

        // Будуємо зважений пул доступних настроїв
        // Базові ваги: Relaxed=40, Rushed=30, Picky=30
        int totalWeight = 40; // Relaxed завжди є
        if (data.canBeRushed) totalWeight += 30;
        if (data.canBePicky)  totalWeight += 30;

        int roll = Random.Range(0, totalWeight);

        if (data.canBeRushed && roll < 30)   return Mood.Rushed;
        roll -= data.canBeRushed ? 30 : 0;

        if (data.canBePicky  && roll < 30)   return Mood.Picky;

        return Mood.Relaxed;
    }
}