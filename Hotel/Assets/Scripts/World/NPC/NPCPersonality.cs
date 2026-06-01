// Assets/Scripts/World/NPC/NPCPersonality.cs
// v4 — ЗМІНИ:
//   [1] DesiredGenre (single) → ShoppingList (BookGenre[]) — по одному жанру на кожну книгу
//   [2] CurrentBookIndex — який слот шукаємо зараз
//   [3] CurrentDesiredGenre — жанр поточного слоту (читають ShelfScanner і NPCBrain)
//   [4] AdvanceToNextBook() — переходимо до наступного слоту; повертає true якщо є ще
//   [5] Generate(): для кожного з WantsToBuy слотів рандомно вибираємо жанр
//       (80% — з preferredGenres архетипу, 20% — будь-який)
//   [6] DebugString() оновлено — показує весь ShoppingList

using System.Collections.Generic;
using UnityEngine;

public class NPCPersonality
{
    // ── Що хоче купити ──────────────────────────────────────────

    /// Список жанрів: ShoppingList[i] = бажаний жанр для i-ї книги.
    /// Генерується один раз при спавні — кожна книга може бути різного жанру.
    public BookGenre[] ShoppingList   { get; private set; }

    /// Індекс поточної книги яку шукаємо (0-based).
    public int         CurrentBookIndex { get; private set; }

    /// Жанр поточної книги яку шукаємо.
    /// Читається ShelfScanner та NPCBrain.DesiredGenre.
    public BookGenre   CurrentDesiredGenre =>
        (ShoppingList != null && CurrentBookIndex < ShoppingList.Length)
        ? ShoppingList[CurrentBookIndex]
        : BookGenre.Classic; // fallback

    public BookRarity  MinRarity    { get; private set; }
    public BookRarity  MaxRarity    { get; private set; }
    public float       MaxBudget    { get; private set; }
    public int         WantsToBuy   { get; private set; } // = ShoppingList.Length
    public int         BooksBought  { get; set;         }

    // ── Кошик ───────────────────────────────────────────────────

    public List<BookTemplate> Basket        { get; } = new List<BookTemplate>();
    public bool               BasketFull    => Basket.Count >= WantsToBuy;
    public bool               BasketNotEmpty => Basket.Count > 0;

    public void AddToBasket(BookTemplate book)
    {
        Basket.Add(book);
        BooksBought = Basket.Count;
    }

    public void ClearBasket() => Basket.Clear();

    // ── Пошук ───────────────────────────────────────────────────

    /// Чи є ще книги для пошуку (кошик ще не повний і слоти не вичерпані).
    public bool WantsMoreBooks => BooksBought < WantsToBuy;

    /// Чи всі слоти пройдені (незалежно від того чи знайдені).
    public bool AllSlotsProcessed => CurrentBookIndex >= WantsToBuy;

    /// Переходимо до наступного слоту покупки.
    /// Повертає true якщо є ще невідпрацьовані слоти.
    public bool AdvanceToNextBook()
    {
        CurrentBookIndex++;
        return CurrentBookIndex < WantsToBuy;
    }

    // ── Поведінка ───────────────────────────────────────────────

    public float BuyChance      { get; private set; }
    public float StayDuration   { get; private set; }
    public float InspectTimeMin { get; private set; }
    public float InspectTimeMax { get; private set; }
    public int   ShelvesToVisit { get; private set; }

    public float GetInspectTime() =>
        Random.Range(InspectTimeMin, InspectTimeMax);

    // ── Настрій ─────────────────────────────────────────────────

    public enum Mood { Relaxed, Rushed, Picky, Impulsive }
    public Mood CurrentMood { get; private set; }

    public float InitialMoodValue { get; private set; }
    public float InitialPatience  { get; private set; }
    public string UniqueID        { get; private set; }

    // ── Derived ─────────────────────────────────────────────────

    public bool AcceptsRarity(BookRarity rarity) =>
        rarity >= MinRarity && rarity <= MaxRarity;

    public string DebugString()
    {
        var genres = ShoppingList != null
            ? string.Join(", ", ShoppingList)
            : "none";
        return $"[{CurrentMood} mood={InitialMoodValue:F0} patience={InitialPatience:F0}] " +
               $"Books:[{genres}] Rarity:[{MinRarity}-{MaxRarity}] " +
               $"Budget:{MaxBudget:F0} Wants:{WantsToBuy} " +
               $"CurrentSlot:{CurrentBookIndex} Basket:{Basket.Count} BuyChance:{BuyChance:P0}";
    }

    // ── Generation ───────────────────────────────────────────────

    public static NPCPersonality Generate(NPCData data)
    {
        var p = new NPCPersonality();
        p.UniqueID = System.Guid.NewGuid().ToString();

        // Рарність — однакова для всіх книг цього візиту
        p.MinRarity = data.minAcceptableRarity;
        p.MaxRarity = data.maxAcceptableRarity;

        // Бюджет
        float budgetMult = Random.Range(data.budgetMultiplierMin, data.budgetMultiplierMax);
        p.MaxBudget = data.maxBudget * budgetMult;

        // К-ть книг
        p.WantsToBuy = Random.Range(data.minBooksToBuy, data.maxBooksToBuy + 1);

        // ✅ [NEW v4] ShoppingList — РІЗНИЙ жанр для кожної книги
        // 80% → з уподобань архетипу; 20% → будь-який жанр з БД
        p.ShoppingList = new BookGenre[p.WantsToBuy];
        for (int i = 0; i < p.WantsToBuy; i++)
            p.ShoppingList[i] = RollGenre(data);

        p.CurrentBookIndex = 0; // починаємо з першого слоту

        // Настрій
        p.CurrentMood = data.canBePicky || data.canBeRushed
            ? RollMood(data)
            : Mood.Relaxed;

        switch (p.CurrentMood)
        {
            case Mood.Relaxed:
                p.BuyChance      = Mathf.Clamp(data.buyChance * Random.Range(0.9f, 1.1f), 0f, 1f);
                p.StayDuration   = data.stayDuration * Random.Range(1.0f, 1.4f);
                p.ShelvesToVisit = data.shelvesToInspect;
                p.InspectTimeMin = data.inspectTimeMin;
                p.InspectTimeMax = data.inspectTimeMax;
                break;

            case Mood.Rushed:
                p.BuyChance      = Mathf.Clamp(data.buyChance * Random.Range(0.7f, 1.0f), 0f, 1f);
                p.StayDuration   = data.stayDuration * Random.Range(0.4f, 0.7f);
                p.ShelvesToVisit = Mathf.Max(1, data.shelvesToInspect - 1);
                p.InspectTimeMin = data.inspectTimeMin * 0.6f;
                p.InspectTimeMax = data.inspectTimeMax * 0.7f;
                break;

            case Mood.Picky:
                p.BuyChance      = Mathf.Clamp(data.buyChance * Random.Range(0.5f, 0.8f), 0f, 1f);
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

        // NPCStats початкові значення
        p.InitialMoodValue = p.CurrentMood switch
        {
            Mood.Relaxed   => Random.Range(20f,  60f),
            Mood.Rushed    => Random.Range(-20f, 20f),
            Mood.Picky     => Random.Range(-10f, 30f),
            Mood.Impulsive => Random.Range(40f,  90f),
            _              => 0f
        };

        p.InitialPatience = p.CurrentMood switch
        {
            Mood.Relaxed   => Random.Range(40f, 80f),
            Mood.Rushed    => Random.Range(20f, 50f),
            Mood.Picky     => Random.Range(60f, 90f),
            Mood.Impulsive => Random.Range(30f, 70f),
            _              => 50f
        };

        return p;
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// Вибрати жанр для одного слоту покупки.
    private static BookGenre RollGenre(NPCData data)
    {
        if (data.preferredGenres != null
            && data.preferredGenres.Length > 0
            && Random.value < 0.8f)
        {
            return data.preferredGenres[Random.Range(0, data.preferredGenres.Length)];
        }
        // 20% — будь-який жанр
        var allGenres = System.Enum.GetValues(typeof(BookGenre));
        return (BookGenre)allGenres.GetValue(Random.Range(0, allGenres.Length));
    }

    private static Mood RollMood(NPCData data)
    {
        float canRushed  = data.canBeRushed  ? 1f : 0f;
        float canPicky   = data.canBePicky   ? 1f : 0f;
        float canImpuls  = data.impulsiveMoodChance;

        float total = 0.35f + canRushed * 0.25f + canPicky * 0.20f + canImpuls * 0.20f;
        float r     = Random.value * total;

        float cursor = 0.35f;
        if (r < cursor) return Mood.Relaxed;
        cursor += canRushed * 0.25f;
        if (r < cursor) return Mood.Rushed;
        cursor += canPicky * 0.20f;
        if (r < cursor) return Mood.Picky;
        return Mood.Impulsive;
    }
}