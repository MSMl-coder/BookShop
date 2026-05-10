// Assets/Scripts/World/CashDesk/CashDeskBuffer.cs  [Фаза 1 — фінальна версія]
// Стіл каси як буфер для книг після закінчення WorkDay.
//
// Поведінка за ТЗ:
//   WorkDay → LootPhase:
//     → всі NPC отримують ForceLeaveAndTakeBook()
//     → книги кладуться на стіл (TryAddBook)
//     → переповнення → автоматично в інвентар гравця
//     → покупка НЕ зараховується
//   LootPhase → Preparation (новий день):
//     → стіл очищається, залишки → інвентар гравця
//
// UNITY SETUP:
//   1. GameObject "CashDesk" у сцені
//   2. Add Component → CashDeskBuffer
//   3. maxSlots = 8 (або потрібна кількість)

using System.Collections.Generic;
using UnityEngine;

public class CashDeskBuffer : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────
    public static CashDeskBuffer Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────
    [Header("Налаштування столу каси")]
    [Tooltip("Максимальна кількість книг на столі каси")]
    [SerializeField] private int maxSlots = 8;

    [Tooltip("Точки де книги відображаються фізично (опційно)")]
    [SerializeField] private Transform[] slotPoints;

    // ── State ──────────────────────────────────────────────────
    private readonly List<BookInstance> _bufferedBooks = new List<BookInstance>();

    // Статистика для екрану підсумків дня
    private int _booksCollectedThisDay;
    private int _booksOverflowedThisDay;

    // ── Properties ─────────────────────────────────────────────
    public IReadOnlyList<BookInstance> BufferedBooks        => _bufferedBooks;
    public int  Count                                       => _bufferedBooks.Count;
    public bool HasSpace                                    => _bufferedBooks.Count < maxSlots;
    public int  FreeSlots                                   => maxSlots - _bufferedBooks.Count;
    public int  BooksCollectedThisDay                       => _booksCollectedThisDay;
    public int  BooksOverflowedThisDay                      => _booksOverflowedThisDay;

    // ── Unity ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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
    /// Якщо місця немає — книга автоматично іде в інвентар гравця.
    /// Повертає true якщо книга потрапила на стіл.
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

        _booksOverflowedThisDay++;
        Debug.Log($"[CashDesk] Стіл повний! '{book.templateID}' → інвентар гравця.");
        InventoryManager.Instance?.AddExistingBook(book);
        return false;
    }

    /// Взяти всі книги зі столу (для серіалізації або нового дня).
    public List<BookInstance> TakeAllBooks()
    {
        var result = new List<BookInstance>(_bufferedBooks);
        _bufferedBooks.Clear();
        return result;
    }

    /// Повернути всі книги зі столу в інвентар гравця та очистити.
    public void ClearToInventory()
    {
        if (_bufferedBooks.Count == 0) return;

        int count = _bufferedBooks.Count;
        foreach (var book in _bufferedBooks)
            InventoryManager.Instance?.AddExistingBook(book);

        _bufferedBooks.Clear();
        Debug.Log($"[CashDesk] {count} книг повернуто в інвентар гравця (новий день).");
    }

    // ── Private ────────────────────────────────────────────────

    private void OnStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.LootPhase:
                // WorkDay закінчився — збираємо книги від усіх NPC
                ResetDailyStats();
                CollectBooksFromAllNPCs();
                break;

            case GameState.Preparation:
                // Новий день — залишки столу повертаємо в інвентар
                ClearToInventory();
                break;
        }
    }

    /// Знаходить усіх активних NPC.
    /// ForceLeaveAndTakeBook() — відправляє NPC до виходу та повертає BookInstance
    /// яку NPC тримав/зарезервував (без нарахування грошей гравцю).
    private void CollectBooksFromAllNPCs()
    {
        var allNPCs = Object.FindObjectsByType<NPCBrain>();

        int collected = 0;
        foreach (var npc in allNPCs)
        {
            BookInstance heldBook = npc.ForceLeaveAndTakeBook();
            if (heldBook != null)
            {
                TryAddBook(heldBook);
                collected++;
            }
        }

        Debug.Log($"[CashDesk] EndDay: {collected} книг від {allNPCs.Length} NPC. " +
                  $"На столі: {_bufferedBooks.Count}/{maxSlots}. " +
                  $"В інвентар (переповнення): {_booksOverflowedThisDay}.");
    }

    private void ResetDailyStats()
    {
        _booksCollectedThisDay  = 0;
        _booksOverflowedThisDay = 0;
    }
}