// Assets/Scripts/World/NPC/NPCPersonality.cs
// ЗМІНИ v3:
//   - Додано Basket (List<BookTemplate>) — книги взяті з полиці але ще не оплачені
//   - Додано BasketFull, BasketNotEmpty, AddToBasket(), ClearBasket()
//   - BooksBought тепер = кількість книг в кошику (не оплачених)
//   - WantsMoreBooks залишається: BooksBought < WantsToBuy

using System.Collections.Generic;
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

    // ── [NEW v3] Кошик — книги взяті з полиць, ще не оплачені ──
    public List<BookTemplate> Basket      { get; } = new List<BookTemplate>();
    public bool               BasketFull  => Basket.Count >= WantsToBuy;
    public bool               BasketNotEmpty => Basket.Count > 0;

    public void AddToBasket(BookTemplate book)
    {
        Basket.Add(book);
        BooksBought = Basket.Count; // синхронізуємо для сумісності
    }

    public void ClearBasket() => Basket.Clear();

    // ── Поведінка ───────────────────────────────────────────────
    public float BuyChance      { get; private set; }
    public float StayDuration   { get; private set; }
    public float InspectTimeMin { get; private set; }
    public float InspectTimeMax { get; private set; }
    public int   ShelvesToVisit { get; private set; }

    // ── Настрій ─────────────────────────────────────────────────
    public enum Mood { Relaxed, Rushed, Picky, Impulsive }
    public Mood CurrentMood { get; private set; }

    // ── Початкові значення для NPCStats ─────────────────────────
    public float InitialMoodValue { get; private set; }
    public float InitialPatience  { get; private set; }
    public string UniqueID        { get; private set; }

    // ── Derived ──────────────────────────────────────────────────
    public bool WantsMoreBooks => BooksBought < WantsToBuy;

    public bool AcceptsRarity(BookRarity rarity) =>
        rarity >= MinRarity && rarity <= MaxRarity;

    public float GetInspectTime() =>
        Random.Range(InspectTimeMin, InspectTimeMax);

    public string DebugString() =>
        $"[{CurrentMood} mood={InitialMoodValue:F0} patience={InitialPatience:F0}] " +
        $"Genre:{DesiredGenre} Rarity:[{MinRarity}-{MaxRarity}] " +
        $"Budget:{MaxBudget:F0} Wants:{WantsToBuy} Basket:{Basket.Count} BuyChance:{BuyChance:P0}";

    // ── Generation ───────────────────────────────────────────────
    public static NPCPersonality Generate(NPCData data)
    {
        var p = new NPCPersonality();
        p.UniqueID = System.Guid.NewGuid().ToString();

        // Жанр
        if (data.preferredGenres != null && data.preferredGenres.Length > 0 && Random.value < 0.8f)
            p.DesiredGenre = data.preferredGenres[Random.Range(0, data.preferredGenres.Length)];
        else
            p.DesiredGenre = (BookGenre)Random.Range(0, System.Enum.GetValues(typeof(BookGenre)).Length);

        // Рарність
        p.MinRarity = data.minAcceptableRarity;
        p.MaxRarity = data.maxAcceptableRarity;

        // Бюджет
        float mult   = Random.Range(data.budgetMultiplierMin, data.budgetMultiplierMax);
        float budget = data.maxBudget * mult;
        p.MaxBudget  = Mathf.Max(Mathf.Round(budget / 5f) * 5f, 5f);

        // К-ть книг
        float buyRoll = Random.value;
        int want;
        if      (buyRoll < 0.55f) want = 1;
        else if (buyRoll < 0.78f) want = 2;
        else if (buyRoll < 0.92f) want = 3;
        else                      want = Random.Range(4, data.maxBooksToBuy + 1);
        p.WantsToBuy  = Mathf.Clamp(want, data.minBooksToBuy, data.maxBooksToBuy);
        p.BooksBought = 0;

        // Настрій
        p.CurrentMood = RollMood(data);

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
                p.BuyChance      = data.buyChance * Random.Range(0.5f, 0.75f);
                p.StayDuration   = data.stayDuration * Random.Range(1.1f, 1.5f);
                p.ShelvesToVisit = data.shelvesToInspect + Random.Range(1, 3);
                p.InspectTimeMin = data.inspectTimeMin * 1.2f;
                p.InspectTimeMax = data.inspectTimeMax * 1.5f;
                break;
            case Mood.Impulsive:
                p.BuyChance      = Mathf.Clamp(data.buyChance * Random.Range(1.3f, 1.6f), 0f, 1f);
                p.StayDuration   = data.stayDuration * Random.Range(0.6f, 0.9f);
                p.ShelvesToVisit = data.shelvesToInspect;
                p.InspectTimeMin = data.inspectTimeMin * 0.7f;
                p.InspectTimeMax = data.inspectTimeMax * 0.8f;
                break;
        }

        // InitialMoodValue (-100..+100)
        p.InitialMoodValue = p.CurrentMood switch
        {
            Mood.Relaxed   =>  Random.Range(20f,  60f),
            Mood.Rushed    =>  Random.Range(-20f, 20f),
            Mood.Picky     =>  Random.Range(-10f, 30f),
            Mood.Impulsive =>  Random.Range(40f,  90f),
            _              =>  0f
        };

        // InitialPatience залежить від BuyChance і Mood
        p.InitialPatience = p.CurrentMood switch
        {
            Mood.Relaxed   => Random.Range(40f,  80f),
            Mood.Rushed    => Random.Range(20f,  50f),
            Mood.Picky     => Random.Range(60f,  90f),
            Mood.Impulsive => Random.Range(30f,  70f),
            _              => 50f
        };

        return p;
    }

    private static Mood RollMood(NPCData data)
    {
        float r = Random.value;
        if      (r < 0.35f) return Mood.Relaxed;
        else if (r < 0.60f) return Mood.Rushed;
        else if (r < 0.80f) return Mood.Picky;
        else                return Mood.Impulsive;
    }
}