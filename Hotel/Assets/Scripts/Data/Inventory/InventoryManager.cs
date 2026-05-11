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

    // ── Books ──
    private readonly List<BookInstance> _ownedBooks = new();

    // ── Furniture ──
    private readonly List<FurnitureInstance>             _furnitureInventory = new();
    private readonly Dictionary<int, FurnitureTemplate> _furnitureDB        = new();

    public event Action OnInventoryChanged;
    public event Action OnFurnitureChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ─────────────────────────────────────────────
    #region Furniture DB Init
    // ─────────────────────────────────────────────

    /// Викликається з DecorationPanelUI.OnEnable — реєструє шаблони
    /// і авто-видає екземпляри для unlockedByDefault
    public void InitFurnitureDatabase(IEnumerable<FurnitureTemplate> allTemplates)
    {
        _furnitureDB.Clear();
        foreach (var t in allTemplates)
        {
            if (t == null) continue;
            _furnitureDB[t.furnitureID] = t;

            if (t.unlockedByDefault && !HasFurnitureTemplate(t.furnitureID))
            {
                var fi = new FurnitureInstance(t.furnitureID);
                _furnitureInventory.Add(fi);
                Debug.Log($"[Inventory] Auto-unlocked: {t.furnitureName}");
            }
        }
        OnFurnitureChanged?.Invoke();
    }

    public FurnitureTemplate GetTemplate(int templateID) =>
        _furnitureDB.TryGetValue(templateID, out var t) ? t : null;

    #endregion

    // ─────────────────────────────────────────────
    #region Furniture Inventory
    // ─────────────────────────────────────────────

    public void AddFurnitureInstance(FurnitureInstance instance)
    {
        if (instance == null) return;
        _furnitureInventory.Add(instance);
        OnFurnitureChanged?.Invoke();
        Debug.Log($"[Inventory] Furniture added: templateID={instance.templateID}");
    }

    /// Розблокувати = додати новий екземпляр (викликається з LootManager)
    public void UnlockFurniture(FurnitureTemplate template)
    {
        if (template == null) return;
        var fi = new FurnitureInstance(template.furnitureID);
        _furnitureInventory.Add(fi);

        // Реєструємо шаблон якщо ще не зареєстровано
        if (!_furnitureDB.ContainsKey(template.furnitureID))
            _furnitureDB[template.furnitureID] = template;

        OnFurnitureChanged?.Invoke();
        Debug.Log($"[Inventory] Unlocked furniture: {template.furnitureName}");
    }

    public void RemoveFurnitureInstance(FurnitureInstance instance)
    {
        if (_furnitureInventory.Remove(instance))
            OnFurnitureChanged?.Invoke();
    }

    public bool HasFurnitureTemplate(int templateID) =>
        _furnitureInventory.Any(f => f.templateID == templateID);

    public bool IsTemplateUnlocked(int templateID) =>
        HasFurnitureTemplate(templateID);

    /// Перший нерозміщений екземпляр шаблону (для BeginPlacement)
    public FurnitureInstance GetFirstUnplaced(int templateID) =>
        _furnitureInventory.FirstOrDefault(f => f.templateID == templateID && !f.isPlaced);

    public List<FurnitureInstance> GetUnplacedInstances() =>
        _furnitureInventory.Where(f => !f.isPlaced).ToList();

    public List<FurnitureInstance> GetPlacedInstances() =>
        _furnitureInventory.Where(f => f.isPlaced).ToList();

    public List<FurnitureInstance> GetInstancesByTemplate(int templateID) =>
        _furnitureInventory.Where(f => f.templateID == templateID).ToList();

    public List<FurnitureInstance> GetAllFurnitureInstances() =>
        new List<FurnitureInstance>(_furnitureInventory);

    /// Всі унікальні шаблони що є у гравця (для DecorationPanelUI)
    public List<FurnitureTemplate> GetAllUnlockedTemplates()
    {
        var ids = _furnitureInventory.Select(f => f.templateID).Distinct();
        return ids.Select(id => GetTemplate(id)).Where(t => t != null).ToList();
    }

    // Сумісність зі старим кодом
    public List<FurnitureTemplate> GetUnlockedFurnitureByClass(FurnitureClass targetClass) =>
        GetAllUnlockedTemplates().Where(t => t.furnitureClass == targetClass).ToList();

    public List<FurnitureTemplate> GetAllUnlockedFurniture() =>
        GetAllUnlockedTemplates();

    #endregion

    // ─────────────────────────────────────────────
    #region Books
    // ─────────────────────────────────────────────

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

        if (_ownedBooks.Count == 1)
            TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnFirstBook);

        Debug.Log($"[Inventory] Book added: {template.title}. Total: {_ownedBooks.Count}");
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

    #endregion

    // ─────────────────────────────────────────────
    #region Shelf Operations
    // ─────────────────────────────────────────────

    public void PushOneToShelf(Shelf targetShelf)
    {
        if (targetShelf == null || _ownedBooks.Count == 0) return;
        if (!ValidateDatabase()) return;

        var bookToPlace = _ownedBooks[0];
        var template    = database.GetBook(bookToPlace.templateID);

        if (template?.containerPrefab == null)
        {
            Debug.LogWarning($"[Inventory] No prefab for: {bookToPlace.templateID}");
            return;
        }

        if (targetShelf.CanFitBook(template))
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

        var snapshot = new List<BookInstance>(_ownedBooks);
        foreach (var book in snapshot)
        {
            var template = database.GetBook(book.templateID);
            if (template?.containerPrefab == null) continue;
            if (!targetShelf.CanFitBook(template)) break;
            targetShelf.PlaceBook(book, template.containerPrefab);
            RemoveBook(book);
        }
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Sorting
    // ─────────────────────────────────────────────

    public List<BookInstance> GetSortedInventory(SortType type)
    {
        if (_ownedBooks.Count == 0 || !ValidateDatabase())
            return new List<BookInstance>();

        return type switch
        {
            SortType.ByTitle  => _ownedBooks.OrderBy(b => database.GetBook(b.templateID)?.title  ?? "").ToList(),
            SortType.ByAuthor => _ownedBooks.OrderBy(b => database.GetBook(b.templateID)?.author ?? "").ToList(),
            SortType.ByPrice  => _ownedBooks.OrderByDescending(b => database.GetBook(b.templateID)?.sellPrice ?? 0).ToList(),
            SortType.ByRarity => _ownedBooks.OrderByDescending(b => (int)(database.GetBook(b.templateID)?.rarity ?? 0)).ToList(),
            SortType.ByGenre  => _ownedBooks.OrderBy(b => (int)(database.GetBook(b.templateID)?.genre ?? 0)).ToList(),
            _ => new List<BookInstance>(_ownedBooks)
        };
    }

    public BookDatabase GetDatabase() => database;

    #endregion

    // ─────────────────────────────────────────────
    #region Private Helpers
    // ─────────────────────────────────────────────

    private bool ValidateDatabase()
    {
        if (database != null) return true;
        Debug.LogError("[Inventory] Database is not assigned!");
        return false;
    }

    #endregion
}