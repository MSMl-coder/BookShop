// Assets/Scripts/Data/Inventory/InventoryManager.cs
// FIXES v2:
//   [1] _furnitureDB: Dictionary<int, PropTemplate>  (було PropInstance — type confusion)
//   [2] InitFurnitureDatabase: IEnumerable<PropTemplate>  (було PropInstance)
//   [3] GetTemplate: повертає PropTemplate  (було PropInstance)
//   [4] UnlockProp: видалено дублювання PropInstance (створювався двічі)
//   [5] InventoryManagerExtensions: винесено з класу (nested extension = compile error)
//   [6] GetUnlockedFurnitureByClass: повертає List<PropTemplate>  (було PropInstance)
//   [7] GetAllUnlockedPropTemplates / GetAllUnlockedTemplates: консолідовано в одну

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private BookDatabase database;

    // ── Books ──────────────────────────────────────────────────────
    private readonly List<BookInstance> _ownedBooks = new();

    // ── Props ──────────────────────────────────────────────────────
    // _furnitureInventory — runtime екземпляри (PropInstance)
    // _furnitureDB        — шаблони ScriptableObject (PropTemplate), індекс по propID
    private readonly List<PropInstance>            _furnitureInventory = new();
    private readonly Dictionary<int, PropTemplate> _furnitureDB        = new(); // [FIX 1]

    public event Action OnInventoryChanged;
    public event Action OnFurnitureChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ─────────────────────────────────────────────
    #region Prop DB Init
    // ─────────────────────────────────────────────

    /// Викликається з DecorationPanelUI.OnEnable — реєструє шаблони
    /// і авто-видає екземпляри для unlockedByDefault.
    public void InitFurnitureDatabase(IEnumerable<PropTemplate> allTemplates) // [FIX 2]
    {
        _furnitureDB.Clear();
        foreach (var t in allTemplates)
        {
            if (t == null) continue;
            _furnitureDB[t.propID] = t;

            if (t.unlockedByDefault && !HasFurnitureTemplate(t.propID))
            {
                _furnitureInventory.Add(new PropInstance(t.propID));
                Debug.Log($"[Inventory] Auto-unlocked: {t.propName}");
            }
        }
        OnFurnitureChanged?.Invoke();
    }

    /// Повертає PropTemplate за propID.
    public PropTemplate GetTemplate(int propID) =>           // [FIX 3]
        _furnitureDB.TryGetValue(propID, out var t) ? t : null;

    /// Alias — зворотна сумісність з кодом що звав GetPropTemplate().
    public PropTemplate GetPropTemplate(int propID) => GetTemplate(propID);

    #endregion

    // ─────────────────────────────────────────────
    #region Prop Inventory
    // ─────────────────────────────────────────────

    public void AddFurnitureInstance(PropInstance instance)
    {
        if (instance == null) return;
        _furnitureInventory.Add(instance);
        OnFurnitureChanged?.Invoke();
        Debug.Log($"[Inventory] Prop instance added: propID={instance.propID}");
    }

    /// Розблокувати через LootManager — додає PropInstance.
    public void UnlockFurniture(PropTemplate template)
    {
        if (template == null) return;

        _furnitureInventory.Add(new PropInstance(template.propID));

        if (!_furnitureDB.ContainsKey(template.propID))
            _furnitureDB[template.propID] = template;

        OnFurnitureChanged?.Invoke();
        Debug.Log($"[Inventory] Unlocked prop: {template.propName}");
    }

    /// Alias — зворотна сумісність.
    public void UnlockProp(PropTemplate prop) => UnlockFurniture(prop); // [FIX 4]

    public void RemoveFurnitureInstance(PropInstance instance)
    {
        if (_furnitureInventory.Remove(instance))
            OnFurnitureChanged?.Invoke();
    }

    public bool HasFurnitureTemplate(int propID) =>
        _furnitureInventory.Any(f => f.propID == propID);

    public bool IsTemplateUnlocked(int propID) =>
        HasFurnitureTemplate(propID);

    /// Перший нерозміщений екземпляр шаблону (для BeginPlacement).
    public PropInstance GetFirstUnplaced(int propID) =>
        _furnitureInventory.FirstOrDefault(f => f.propID == propID && !f.isPlaced);

    public List<PropInstance> GetUnplacedInstances() =>
        _furnitureInventory.Where(f => !f.isPlaced).ToList();

    public List<PropInstance> GetPlacedInstances() =>
        _furnitureInventory.Where(f => f.isPlaced).ToList();

    public List<PropInstance> GetInstancesByTemplate(int propID) =>
        _furnitureInventory.Where(f => f.propID == propID).ToList();

    public List<PropInstance> GetAllFurnitureInstances() =>
        new List<PropInstance>(_furnitureInventory);

    /// Всі унікальні PropTemplate що є у гравця.          [FIX 7 — консолідація]
    public List<PropTemplate> GetAllUnlockedTemplates()
    {
        return _furnitureInventory
            .Select(f => f.propID)
            .Distinct()
            .Select(id => GetTemplate(id))
            .Where(t => t != null)
            .ToList();
    }

    /// Alias для нового API.
    public List<PropTemplate> GetAllUnlockedPropTemplates() => GetAllUnlockedTemplates();

    /// Всі PropTemplate заданого класу що є у гравця.    [FIX 6]
    public List<PropTemplate> GetUnlockedFurnitureByClass(PropClass targetClass) =>
        GetAllUnlockedTemplates().Where(t => t.propClass == targetClass).ToList();

    /// Alias — повертає всі розблоковані шаблони.
    public List<PropTemplate> GetAllUnlockedFurniture() => GetAllUnlockedTemplates();

    #endregion

    // ─────────────────────────────────────────────
    #region Books
    // ─────────────────────────────────────────────

    public void AddBook(string templateID)
    {
        if (string.IsNullOrEmpty(templateID)) return;

        var template = BookDatabase.Instance?.GetBook(templateID);
        if (template == null) return;

        if (string.IsNullOrEmpty(template.title))
        {
            Debug.LogWarning($"[Inventory] Skipped book with empty title: {templateID}");
            return;
        }

        if (!ValidateDatabase()) return;

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