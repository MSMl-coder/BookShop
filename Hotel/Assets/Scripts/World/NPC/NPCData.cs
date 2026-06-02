// Assets/Scripts/Data/NPCs/NPCData.cs
// v3 — NPCMessageGroup struct: повідомлення + таймер — завжди разом в Inspector.
//
// Кожна категорія — один блок з масивом рядків і float duration поруч.
// duration = 0 → хмаринка не закривається автоматично (WaitingForPlayer, Leaving).
// {0} в тексті → замінюється на назву книги (bookPickup).

using UnityEngine;

// ══════════════════════════════════════════════════════════════════
/// Група повідомлень + тривалість показу. Редагується в Inspector.
/// Один блок замість окремих масивів і float-полів.
// ══════════════════════════════════════════════════════════════════
[System.Serializable]
public struct NPCMessageGroup
{
    [Tooltip("Варіанти тексту — обирається рандомно")]
    public string[] messages;

    [Min(0f)]
    [Tooltip("Секунд показу хмаринки. 0 = не закривається автоматично")]
    public float duration;

    /// Рандомний рядок з масиву, або fallback якщо масив порожній.
    public string GetRandom(string fallback = "") =>
        messages is { Length: > 0 }
            ? messages[UnityEngine.Random.Range(0, messages.Length)]
            : fallback;

    /// Замінює {0} на arg0. Використовується для "Беру «{0}»!".
    public string GetFormatted(string fallback, string arg0)
    {
        string raw = GetRandom(fallback);
        return raw.Replace("{0}", arg0 ?? "...");
    }

    /// Чи є хоча б один рядок.
    public bool HasMessages => messages is { Length: > 0 };
}

// ══════════════════════════════════════════════════════════════════
[CreateAssetMenu(menuName = "Bookstore/NPC Data")]
public class NPCData : ScriptableObject
{
    // ─────────────────────────────────────────────
    [Header("Identity")]
    // ─────────────────────────────────────────────
    public string      npcName;
    public Sprite      portrait;
    public GameObject  prefab;

    // ─────────────────────────────────────────────
    [Header("Behavior")]
    // ─────────────────────────────────────────────
    [Tooltip("Базовий час перебування (сек). Множиться на коефіцієнт настрою.")]
    public float stayDuration = 60f;

    [Range(0f, 1f)]
    public float buyChance = 0.8f;

    public float maxBudget = 40f;

    [Range(1, 10)]
    public int shelvesToInspect = 3;

    public float inspectTimeMin = 1.5f;
    public float inspectTimeMax = 4f;

    [Tooltip("Жанри яким NPC надає перевагу (80% шанс вибрати з них)")]
    public BookGenre[] preferredGenres;

    [Range(1, 3)]  public int minBooksToBuy = 1;
    [Range(1, 8)]  public int maxBooksToBuy = 3;

    public BookRarity minAcceptableRarity = BookRarity.Common;
    public BookRarity maxAcceptableRarity = BookRarity.Legendary;

    [Range(0.3f, 1.0f)] public float budgetMultiplierMin = 0.6f;
    [Range(1.0f, 2.5f)] public float budgetMultiplierMax = 1.4f;

    public bool canBePicky  = true;
    public bool canBeRushed = true;

    [Range(0f, 0.5f)]
    public float impulsiveMoodChance = 0.15f;

    [Range(1, 5)]
    [Tooltip("Скільки разів гравець може запропонувати книгу до відмови")]
    public int maxPlayerOfferAttempts = 3;

    // ══════════════════════════════════════════════
    // ПОВІДОМЛЕННЯ — всі редагуються в Inspector.
    // Кожна група = масив рядків + тривалість показу.
    // ══════════════════════════════════════════════

    [Header("💬 Вітання (при вході в магазин)")]
    public NPCMessageGroup greeting = new NPCMessageGroup
    {
        duration = 2.5f,
        messages = new[]
        {
            "О, нові книги!",
            "Цікаво, що тут є...",
            "Давно хотів зайти!",
            "Може знайду щось цікаве?"
        }
    };

    [Header("📖 Знайшов книгу (до рішення купити)")]
    public NPCMessageGroup bookFound = new NPCMessageGroup
    {
        duration = 1.5f,
        messages = new[]
        {
            "Цікаво...",
            "Гм, подивимось...",
            "А це що?",
            "О, непогано...",
        }
    };

    [Header("🛒 Бере книгу ({0} = назва)")]
    public NPCMessageGroup bookPickup = new NPCMessageGroup
    {
        duration = 2.5f,
        messages = new[]
        {
            "Беру «{0}»!",
            "О, саме те! «{0}»",
            "«{0}» — давно шукав!",
            "Чудово, «{0}» мій!",
        }
    };

    [Header("🔍 Шукає ще (CollectingBooks)")]
    public NPCMessageGroup collecting = new NPCMessageGroup
    {
        duration = 2f,
        messages = new[]
        {
            "Пошукаю ще...",
            "Може є щось інше?",
            "Продовжую шукати...",
            "Ще не все знайшов!",
        }
    };

    [Header("💰 Іде на касу (Buying)")]
    public NPCMessageGroup buying = new NPCMessageGroup
    {
        duration = 3f,
        messages = new[]
        {
            "Йду на касу!",
            "Все знайшов, платити!",
            "Беру це все!",
            "До каси!",
        }
    };

    [Header("👋 Виходить (Leaving) [0 = не закривати]")]
    public NPCMessageGroup leaving = new NPCMessageGroup
    {
        duration = 0f,   // 0 = залишається до закриття панелі
        messages = new[]
        {
            "До побачення!",
            "Повернуся ще!",
            "Дякую, до зустрічі!",
            "Чудовий магазин!",
        }
    };

    [Header("🤔 Чекає на гравця (WaitingForPlayer) [0 = не закривати]")]
    public NPCMessageGroup waiting = new NPCMessageGroup
    {
        duration = 0f,   // 0 = залишається поки гравець не відреагує
        messages = new[]
        {
            "Чи є у вас щось для мене?",
            "Може порекомендуєте?",
            "Допоможіть знайти!",
            "Нічого не підходить...",
        }
    };

    [Header("⏰ Час вийшов (stayDuration / кінець дня)")]
    public NPCMessageGroup stayEnd = new NPCMessageGroup
    {
        duration = 4f,
        messages = new[]
        {
            "Мені вже час, до побачення!",
            "На жаль, треба йти...",
            "Повернуся наступного разу!",
            "Шкода що не вистачило часу!",
        }
    };

    [Header("✅ Приймає книгу від гравця")]
    public NPCMessageGroup accept = new NPCMessageGroup
    {
        duration = 2f,
        messages = new[]
        {
            "Дуже цікаво, дякую!",
            "Саме те що шукав!",
            "Чудова рекомендація!",
            "Обов'язково прочитаю!"
        }
    };

    [Header("❌ Відмовляється від книги / кінець дня без покупки")]
    public NPCMessageGroup reject = new NPCMessageGroup
    {
        duration = 3f,
        messages = new[]
        {
            "Ні, це не те...",
            "Не зовсім мій жанр.",
            "Трохи дорогувато.",
            "Може щось інше?"
        }
    };

    // ══════════════════════════════════════════════
    // Helpers — єдина точка для отримання повідомлень.
    // Використовуйте їх у коді, а не звертайтесь до масивів напряму.
    // ══════════════════════════════════════════════

    public string GetRandomGreeting()   => greeting.GetRandom();
    public string GetRandomBookFound()  => bookFound.GetRandom("...");
    public string GetRandomCollecting() => collecting.GetRandom("Шукаю...");
    public string GetRandomBuying()     => buying.GetRandom("Йду на касу!");
    public string GetRandomLeaving()    => leaving.GetRandom("До побачення!");
    public string GetRandomWaiting()    => waiting.GetRandom("Чи є щось?");
    public string GetRandomStayEnd()    => stayEnd.GetRandom("Мені вже час!");
    public string GetRandomAccept()     => accept.GetRandom("Дякую!");
    public string GetRandomReject()     => reject.GetRandom("Не те...");

    /// Повідомлення при підніятті книги. {0} → назва книги.
    public string GetPickupMessage(string bookTitle) =>
        bookPickup.GetFormatted("Беру «{0}»!", bookTitle);

    // ── Backward compat: окремі float що були раніше ──────────────
    // (для коду що ще використовує старі назви)
    public float GreetingDuration        => greeting.duration;
    public float AcceptMessageDuration   => accept.duration;

    // Legacy — для коду що не оновлений
    public string[] greetingMessages   => greeting.messages;
    public string[] acceptMessages     => accept.messages;
    public string[] rejectMessages     => reject.messages;
}