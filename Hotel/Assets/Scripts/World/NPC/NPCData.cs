// NPCData.cs
// ЗМІНИ v2: додано блок "Personality Ranges" — діапазони для NPCPersonality.Generate().
// NPCData = архетип (Студент/Турист/Професор), задає МЕЖІ рандомізації.
// NPCPersonality = конкретна особа, генерується один раз при спавні.
//
// Поля Behavior (stayDuration, buyChance, maxBudget, shelvesToInspect)
// тепер є BASE значеннями — NPCPersonality множить їх на рандомний коефіцієнт.

using UnityEngine;

[CreateAssetMenu(fileName = "NPC_", menuName = "Bookstore/NPC Data")]
public class NPCData : ScriptableObject
{
    // ─────────────────────────────────────────────
    [Header("Identity")]
    // ─────────────────────────────────────────────

    public string     npcName = "Visitor";
    public Sprite     portrait;
    public GameObject prefab;

    // ─────────────────────────────────────────────
    [Header("Behavior — базові значення для рандомізації")]
    // ─────────────────────────────────────────────

    [Tooltip("Базовий час перебування в крамниці (секунди). " +
             "NPCPersonality множить на коефіцієнт залежно від Mood.")]
    public float stayDuration = 60f;

    [Tooltip("Базовий шанс купити книгу якщо жанр знайдено (0–1). " +
             "NPCPersonality коригує залежно від Mood.")]
    [Range(0f, 1f)]
    public float buyChance = 0.75f;

    [Tooltip("Базова максимальна ціна. " +
             "NPCPersonality варіює ±40% від цього значення.")]
    public float maxBudget = 50f;

    [Tooltip("Базова к-ть полиць для огляду. " +
             "NPCPersonality коригує залежно від Mood.")]
    [Range(1, 8)]
    public int shelvesToInspect = 3;

    [Tooltip("Час огляду однієї полиці — рандом між min і max")]
    public float inspectTimeMin = 2f;
    public float inspectTimeMax = 5f;

    // ─────────────────────────────────────────────
    [Header("Preferences — жанри архетипу")]
    // ─────────────────────────────────────────────

    [Tooltip("Жанри які цей архетип шукає з підвищеною ймовірністю (80%). " +
             "З 20% ймовірністю обирається будь-який жанр з БД.")]
    public BookGenre[] preferredGenres;

    // ─────────────────────────────────────────────
    [Header("Personality Ranges — межі рандомізації особистості")]
    // ─────────────────────────────────────────────

    [Tooltip("Мінімальна кількість книг яку NPC планує купити за візит.")]
    [Range(1, 3)]
    public int minBooksToBuy = 1;

    [Tooltip("Максимальна кількість книг яку NPC планує купити за візит.")]
    [Range(1, 8)]
    public int maxBooksToBuy = 3;

    [Tooltip("Мінімальна рарність книги яка цікавить цей архетип. " +
             "Студент = Common, Колекціонер = Rare.")]
    public BookRarity minAcceptableRarity = BookRarity.Common;

    [Tooltip("Максимальна рарність яку NPC може собі дозволити / хоче. " +
             "Обмежує пошук зверху.")]
    public BookRarity maxAcceptableRarity = BookRarity.Legendary;

    [Tooltip("Бюджетний коефіцієнт MIN — наскільки бюджет може бути нижчим від базового. " +
             "0.6 = може прийти з 60% від maxBudget.")]
    [Range(0.3f, 1.0f)]
    public float budgetMultiplierMin = 0.6f;

    [Tooltip("Бюджетний коефіцієнт MAX — наскільки бюджет може бути вищим від базового. " +
             "1.4 = може прийти з 140% від maxBudget.")]
    [Range(1.0f, 2.5f)]
    public float budgetMultiplierMax = 1.4f;

    [Tooltip("Чи може цей архетип прийти в поганому настрої (Mood.Picky/Rushed)?")]
    public bool canBePicky  = true;
    public bool canBeRushed = true;

    [Tooltip("Ймовірність що NPC прийде в імпульсивному настрої (Mood.Impulsive). " +
             "0 =ніколи, 1 = завжди.")]
    [Range(0f, 0.5f)]
    public float impulsiveMoodChance = 0.15f;

    // ─────────────────────────────────────────────
    [Header("UI Behavior")]
    // ─────────────────────────────────────────────

    [Tooltip("Скільки секунд показується вітальна хмаринка при вході")]
    public float greetingDuration = 2.5f;

    [Tooltip("Скільки секунд показується хмаринка подяки після прийняття книги")]
    public float acceptMessageDuration = 2f;

    [Tooltip("Скільки разів гравець може запропонувати книгу до відмови")]
    [Range(1, 5)]
    public int maxPlayerOfferAttempts = 3;

    // ─────────────────────────────────────────────
    [Header("Greeting Messages")]
    // ─────────────────────────────────────────────

    [Tooltip("Варіанти вітального тексту — обирається рандомно")]
    public string[] greetingMessages =
    {
        "О, нові книги!",
        "Цікаво, що тут є...",
        "Давно хотів зайти!",
        "Може знайду щось цікаве?"
    };

    // ─────────────────────────────────────────────
    [Header("Accept Messages")]
    // ─────────────────────────────────────────────

    [Tooltip("Варіанти тексту при прийнятті книги — обирається рандомно")]
    public string[] acceptMessages =
    {
        "Дуже цікаво, дякую!",
        "Саме те що шукав!",
        "Чудова рекомендація!",
        "Обов'язково прочитаю!"
    };

    // ─────────────────────────────────────────────
    [Header("Reject Messages")]
    // ─────────────────────────────────────────────

    [Tooltip("Варіанти тексту при відмові від книги — обирається рандомно")]
    public string[] rejectMessages =
    {
        "Ні, це не те...",
        "Не зовсім мій жанр.",
        "Трохи дорогувато.",
        "Може щось інше?"
    };

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────

    public string GetRandomGreeting() =>
        greetingMessages is { Length: > 0 }
            ? greetingMessages[Random.Range(0, greetingMessages.Length)]
            : "";

    public string GetRandomAccept() =>
        acceptMessages is { Length: > 0 }
            ? acceptMessages[Random.Range(0, acceptMessages.Length)]
            : "";

    public string GetRandomReject() =>
        rejectMessages is { Length: > 0 }
            ? rejectMessages[Random.Range(0, rejectMessages.Length)]
            : "";
}