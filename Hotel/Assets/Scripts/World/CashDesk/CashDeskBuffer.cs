// Assets/Scripts/World/CashDesk/CashDeskBuffer.cs
// ФАЗА 1 — стіл каси як буфер для книг після закінчення WorkDay
//
// Поведінка за ТЗ:
//   - Коли WorkDay закінчується (таймер → 0 або гравець натискає "Завершити день"):
//     ВСІ NPC миттєво переходять до каси.
//   - Книги що NPC тримали / зарезервували — кладуться на стіл каси.
//   - Якщо на столі каси немає місця — книги повертаються в інвентар гравця.
//   - Покупка НЕ зараховується якщо NPC не встиг оплатити.
//
// UNITY SETUP:
//   1. Створи GameObject "CashDesk" у сцені.
//   2. Додай CashDeskBuffer.
//   3. Встанови maxSlots (скільки книг вміщує стіл каси).
//   4. Підпишись на GameLoopManager.Instance.OnStateChanged.

using System.Collections.Generic;
using UnityEngine;

public class CashDeskBuffer : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────
    public static CashDeskBuffer Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────
    [Header("Налаштування столу каси")]
    [SerializeField] private int maxSlots = 8;

    [Tooltip("Точки де книги з'являються на столі (опційно)")]
    [SerializeField] private Transform[] slotPoints;

    // ── State ──────────────────────────────────────────────────
    private readonly List<BookInstance> _bufferedBooks = new();

    public IReadOnlyList<BookInstance> BufferedBooks => _bufferedBooks;
    public bool HasSpace => _bufferedBooks.Count < maxSlots;
    public int  FreeSlots => maxSlots - _bufferedBooks.Count;

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

    /// Додати книгу на стіл каси.
    /// Повертає true якщо успішно, false якщо немає місця (книга → інвентар).
    public bool TryAddBook(BookInstance book)
    {
        if (book == null) return false;

        if (!HasSpace)
        {
            Debug.Log($"[CashDesk] Немає місця! Книга '{book.templateID}' → інвентар гравця.");
            InventoryManager.Instance?.AddExistingBook(book);
            return false;
        }

        _bufferedBooks.Add(book);
        Debug.Log($"[CashDesk] Книга '{book.templateID}' покладена на стіл каси ({_bufferedBooks.Count}/{maxSlots}).");
        return true;
    }

    /// Взяти всі книги зі столу каси (наприклад при переході до нового дня)
    public List<BookInstance> TakeAllBooks()
    {
        var result = new List<BookInstance>(_bufferedBooks);
        _bufferedBooks.Clear();
        return result;
    }

    /// Очистити стіл каси (книги повертаються в інвентар)
    public void ClearToInventory()
    {
        foreach (var book in _bufferedBooks)
            InventoryManager.Instance?.AddExistingBook(book);
        _bufferedBooks.Clear();
        Debug.Log("[CashDesk] Стіл каси очищено, книги повернуто в інвентар.");
    }

    // ── Private ────────────────────────────────────────────────

    private void OnStateChanged(GameState newState)
    {
        if (newState == GameState.DayStats)
        {
            // Перехід WorkDay → DayStats: збираємо книги від усіх NPC
            CollectBooksFromAllNPCs();
        }
        else if (newState == GameState.Preparation)
        {
            // На початку нового дня повертаємо буфер у інвентар
            ClearToInventory();
        }
    }

    /// Збираємо всі зарезервовані/тримані книги від активних NPC
    private void CollectBooksFromAllNPCs()
    {
        // Знаходимо всіх активних покупців
        var allCustomers = Object.FindObjectsByType<CustomerBrain>(FindObjectsSortMode.None);

        foreach (var customer in allCustomers)
        {
            // Забираємо книгу яку NPC ніс/знайшов
            BookInstance heldBook = customer.TakeHeldBook();
            if (heldBook != null)
            {
                TryAddBook(heldBook);
            }

            // Знімаємо всі резервації що цей NPC зробив
            customer.ClearReservations();
        }

        Debug.Log($"[CashDesk] EndDay: зібрано {_bufferedBooks.Count} книг на стіл каси.");
    }
}