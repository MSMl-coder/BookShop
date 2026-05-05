// Assets/Scripts/Data/Inventory/InventoryManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private BookDatabase database;

    private List<BookInstance> _ownedBooks = new List<BookInstance>();
    private List<FurnitureTemplate> _unlockedFurniture = new List<FurnitureTemplate>();

    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // ВИПРАВЛЕНО: не ініціалізуємо базу тут — це робить BookDatabaseLoader
        }
        else Destroy(gameObject);
    }

    // --- Books ---

    public void AddBook(string templateID)
    {
        if (!ValidateDatabase()) return;

        BookTemplate template = database.GetBook(templateID);
        if (template == null)
        {
            Debug.LogError($"[Inventory] Book not found: {templateID}");
            return;
        }

        var instance = new BookInstance(templateID);
        _ownedBooks.Add(instance);
        OnInventoryChanged?.Invoke();

        // Туторіал — перша книга
        if (_ownedBooks.Count == 1)
            TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnFirstBook);

        Debug.Log($"[Inventory] Added: {template.title}. Total: {_ownedBooks.Count}");
    }

    public void AddExistingBook(BookInstance instance)
    {
        if (instance == null) return;
        _ownedBooks.Add(instance);
        OnInventoryChanged?.Invoke();
    }

    public void RemoveBook(BookInstance instance)
    {
        if (_ownedBooks.Remove(instance))
            OnInventoryChanged?.Invoke();
    }

    public int GetBookCount() => _ownedBooks.Count;

    // --- Shelf Operations ---

    public void PushOneToShelf(Shelf targetShelf)
    {
        if (targetShelf == null || _ownedBooks.Count == 0) return;
        if (!ValidateDatabase()) return;

        BookInstance bookToPlace = _ownedBooks[0];
        BookTemplate template = database.GetBook(bookToPlace.templateID);

        if (template?.containerPrefab == null)
        {
            Debug.LogWarning($"[Inventory] No prefab for: {bookToPlace.templateID}");
            return;
        }

        if (targetShelf.CanFitBook(template.containerPrefab))
        {
            targetShelf.PlaceBook(bookToPlace, template.containerPrefab);
            RemoveBook(bookToPlace);
        }
        else
        {
            Debug.Log("[Inventory] Shelf is full.");
        }
    }

    public void PushAllToShelf(Shelf targetShelf)
    {
        if (targetShelf == null) return;
        if (!ValidateDatabase()) return;

        // Snapshot щоб уникнути модифікації колекції під час ітерації
        var snapshot = new List<BookInstance>(_ownedBooks);

        foreach (var book in snapshot)
        {
            BookTemplate template = database.GetBook(book.templateID);
            if (template?.containerPrefab == null) continue;

            if (!targetShelf.CanFitBook(template.containerPrefab)) break;

            targetShelf.PlaceBook(book, template.containerPrefab);
            RemoveBook(book);
        }
    }

    // --- Furniture ---

    public void UnlockFurniture(FurnitureTemplate furniture)
    {
        if (furniture == null || _unlockedFurniture.Contains(furniture)) return;
        _unlockedFurniture.Add(furniture);
        Debug.Log($"[Inventory] Unlocked furniture: {furniture.furnitureName}");
    }

    public List<FurnitureTemplate> GetUnlockedFurnitureByClass(FurnitureClass targetClass)
        => _unlockedFurniture.Where(f => f.furnitureClass == targetClass).ToList();

    public List<FurnitureTemplate> GetAllUnlockedFurniture()
        => new List<FurnitureTemplate>(_unlockedFurniture);

    // --- Sorting ---

    // ВИПРАВЛЕНО: повертає порожній список замість null
    public List<BookInstance> GetSortedInventory(SortType type)
    {
        if (_ownedBooks.Count == 0 || !ValidateDatabase())
            return new List<BookInstance>();

        return type switch
        {
            SortType.ByTitle  => _ownedBooks
                .OrderBy(b => database.GetBook(b.templateID)?.title ?? "")
                .ToList(),
            SortType.ByAuthor => _ownedBooks
                .OrderBy(b => database.GetBook(b.templateID)?.author ?? "")
                .ToList(),
            SortType.ByPrice  => _ownedBooks
                .OrderByDescending(b => database.GetBook(b.templateID)?.sellPrice ?? 0)
                .ToList(),
            SortType.ByRarity => _ownedBooks
                .OrderByDescending(b => (int)(database.GetBook(b.templateID)?.rarity ?? 0))
                .ToList(),
            SortType.ByGenre  => _ownedBooks
                .OrderBy(b => (int)(database.GetBook(b.templateID)?.genre ?? 0))
                .ToList(),
            _ => new List<BookInstance>(_ownedBooks)
        };
    }

    public BookDatabase GetDatabase() => database;

    // --- Private Helpers ---

    private bool ValidateDatabase()
    {
        if (database != null) return true;
        Debug.LogError("[Inventory] Database is not assigned!");
        return false;
    }
}