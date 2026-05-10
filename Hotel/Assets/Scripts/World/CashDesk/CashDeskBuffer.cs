// Assets/Scripts/World/CashDesk/CashDeskBuffer.cs
// ФАЗА 1 — стіл каси як буфер для книг після закінчення WorkDay
//
// Поведінка за ТЗ:
//   - При переході WorkDay → LootPhase:
//       → всі NPC миттєво йдуть до каси (ForceLeave)
//       → книги які NPC тримали кладуться на стіл каси (TryAddBook)
//       → якщо немає місця — книги йдуть в інвентар гравця
//       → покупка НЕ зараховується (NPC не встиг оплатити)
//   - При переході LootPhase → Preparation:
//       → буфер очищається, книги повертаються в інвентар
//
// UNITY SETUP:
//   1. Знайди або створи GameObject "CashDesk" у сцені
//   2. Add Component → CashDeskBuffer
//   3. Встанови maxSlots (рекомендовано 8)
//   4. Опційно: призначи slotPoints — точки де книги відображаються на столі

using System.Collections.Generic;
using UnityEngine;

public class CashDeskBuffer : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────
    public static CashDeskBuffer Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────
    [Header("Налаштування")]
    [Tooltip("Максимальна кількість книг на столі каси")]
    [SerializeField] private int maxSlots = 8;

    [Tooltip("Точки де книги відображаються фізично (опційно)")]
    [SerializeField] private Transform[] slotPoints;

    // ── State ──────────────────────────────────────────────────
    private readonly List<BookInstance> _bufferedBooks = new List<BookInstance>();

    // Статистика для DayStats екрану
    private int _booksCollectedThisDay;
    private int _booksOverflowedThisDay; // скільки пішло в інвентар через переповнення

    // ── Properties ─────────────────────────────────────────────
    public IReadOnlyList<BookInstance> BufferedBooks   => _bufferedBooks;
    public int  Count      => _bufferedBooks.Count;
    public bool HasSpace   => _bufferedBooks.Count < maxSlots;
    public int  FreeSlots  => maxSlots - _bufferedBooks.Count;
    public int  BooksCollectedThisDay  => _booksCollectedThisDay;
    public int  BooksOverflowedThisDay => _booksOverflowedThisDay;

    // ── Unity ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= OnStateChanged;
    }

    // ── Public API ─────────────────────────────────────────────

    /// Покласти книгу на стіл каси.
    /// Якщо місця немає — книга автоматично йде в інвентар гравця.
    /// Повертає true якщо книга потрапила на стіл, false — в інвентар.
    public bool TryAddBook(BookInstance book)
    {
        if (book == null) return false;

        if (HasSpace)
        {
            _bufferedBooks.Add(book);
            _booksCollectedThisDay++;
            Debug.Log($"[CashDesk] '{book.templateID}' → стіл каси ({_bufferedBooks.Count}/{maxSlots}).");
            return true;
        }
        else
        {
            _booksOverflowedThisDay++;
            Debug.Log($"[CashDesk] Стіл повний! '{book.templateID}' → інвентар гравця.");
            InventoryManager.Instance?.AddExistingBook(book);
            return false;
        }
    }

    /// Забрати всі книги зі столу (наприклад для серіалізації або нового дня).
    public List<BookInstance> TakeAllBooks()
    {
        var result = new List<BookInstance>(_bufferedBooks);
        _bufferedBooks.Clear();
        return result;
    }

    /// Повернути всі книги зі столу назад в інвентар гравця та очистити буфер.
    public void ClearToInventory()
    {
        if (_bufferedBooks.Count == 0) return;

        foreach (var book in _bufferedBooks)
            InventoryManager.Instance?.AddExistingBook(book);

        int count = _bufferedBooks.Count;
        _bufferedBooks.Clear();
        Debug.Log($"[CashDesk] {count} книг повернуто в інвентар гравця.");
    }

    // ── Private ────────────────────────────────────────────────

    private void OnStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.LootPhase:
                // WorkDay закінчився → збираємо книги від усіх NPC
                ResetDailyStats();
                CollectBooksFromAllNPCs();
                break;

            case GameState.Preparation:
                // Новий день починається → повертаємо залишки в інвентар
                ClearToInventory();
                break;
        }
    }

    /// Знаходить усіх активних NPC, примусово відправляє їх до виходу
    /// та забирає книги які вони тримали або зарезервували.
    private void CollectBooksFromAllNPCs()
    {
        var allNPCs = Object.FindObjectsByType<NPCBrain>(FindObjectsSortMode.None);

        int collected = 0;
        foreach (var npc in allNPCs)
        {
            // Знімаємо резервацію та отримуємо книгу яку NPC тримав
            BookInstance heldBook = npc.ForceLeaveAndTakeBook();
            if (heldBook != null)
            {
                TryAddBook(heldBook);
                collected++;
            }
        }

        Debug.Log($"[CashDesk] EndDay: зібрано {collected} книг від {allNPCs.Length} NPC. " +
                  $"На столі: {_bufferedBooks.Count}/{maxSlots}. " +
                  $"В інвентар через переповнення: {_booksOverflowedThisDay}.");
    }

    private void ResetDailyStats()
    {
        _booksCollectedThisDay  = 0;
        _booksOverflowedThisDay = 0;
    }
}