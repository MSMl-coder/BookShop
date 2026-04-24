using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

// Перерахування має бути ТУТ або у окремому файлі, щоб його бачили всі
public enum SortType { ByTitle, ByAuthor, ByPrice, ByRarity, ByGenre }

 
/// Центральний менеджер запасів книг у крамниці.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Data Settings")]
    [SerializeField] private BookDatabase database;
    [Header("Furniture Inventory")]
    private List<FurnitureTemplate> _unlockedFurniture = new List<FurnitureTemplate>();
    private List<BookInstance> _ownedBooks = new List<BookInstance>();

    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (database != null) 
                database.Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UnlockFurniture(FurnitureTemplate newFurniture)
        {
            if (!_unlockedFurniture.Contains(newFurniture))
            {
                _unlockedFurniture.Add(newFurniture);
                Debug.Log($"[Inventory] Розблоковано нові меблі: {newFurniture.furnitureName}");
            }
        }

        // Метод для слотів, щоб вони знали, що можна показати в UI апгрейду
        public List<FurnitureTemplate> GetUnlockedFurnitureByClass(FurnitureClass targetClass)
        {
            return _unlockedFurniture.Where(f => f.furnitureClass == targetClass).ToList();
        }

    #region Керування книгами (API)

    public void AddBook(string templateID)
    {
        if (database == null) return;
        
        var template = database.GetBook(templateID);
        if (template == null)
        {
            Debug.LogError($"[Inventory] Книгу з ID {templateID} не знайдено!");
            return;
        }

        // ВИПРАВЛЕНО: Передаємо templateID у конструктор, як вимагає ваш клас BookInstance
        BookInstance newBook = new BookInstance(templateID);
        
        _ownedBooks.Add(newBook);
        OnInventoryChanged?.Invoke();
        Debug.Log($"[Inventory] Додано: {template.title}");
    }

    public BookDatabase GetDatabase() => database;

    public void RemoveBook(BookInstance instance)
    {
        if (_ownedBooks.Contains(instance))
        {
            _ownedBooks.Remove(instance);
            OnInventoryChanged?.Invoke();
        }
    }

    #endregion

    #region Логіка для полиць

    public void PushOneToShelf(Shelf targetShelf)
    {
        if (targetShelf == null || _ownedBooks.Count == 0) return;

        BookInstance bookToPlace = _ownedBooks[0];
        BookTemplate template = database.GetBook(bookToPlace.templateID);

        // ВИПРАВЛЕНО: Використовуємо containerPrefab (як у вашому BookTemplate)
        if (targetShelf.CanFitBook(template.containerPrefab))
        {
            targetShelf.PlaceBook(bookToPlace, template.containerPrefab);
            RemoveBook(bookToPlace);
        }
    }

    public void PushAllToShelf(Shelf targetShelf)
    {
        if (targetShelf == null || _ownedBooks.Count == 0) return;

        List<BookInstance> snapshot = new List<BookInstance>(_ownedBooks);

        foreach (var book in snapshot)
        {
            BookTemplate template = database.GetBook(book.templateID);
            
            // ВИПРАВЛЕНО: Використовуємо containerPrefab
            if (targetShelf.CanFitBook(template.containerPrefab))
            {
                targetShelf.PlaceBook(book, template.containerPrefab);
                RemoveBook(book);
            }
            else break; 
        }
    }

    #endregion

    #region Сортування

    public List<BookInstance> GetSortedInventory(SortType type)
    {
        if (_ownedBooks.Count == 0) return new List<BookInstance>();

        // Додано перевірку на null для шаблонів, щоб уникнути NullReference при сортуванні
        switch (type)
        {
            case SortType.ByTitle:
                return _ownedBooks.OrderBy(b => database.GetBook(b.templateID)?.title ?? "").ToList();
            case SortType.ByAuthor:
                return _ownedBooks.OrderBy(b => database.GetBook(b.templateID)?.author ?? "").ToList();
            case SortType.ByPrice:
                return _ownedBooks.OrderByDescending(b => database.GetBook(b.templateID)?.sellPrice ?? 0).ToList();
            default:
                return new List<BookInstance>(_ownedBooks);
        }
    }
    public void AddExistingBook(BookInstance instance)
    {
        _ownedBooks.Add(instance);
        OnInventoryChanged?.Invoke();
        Debug.Log($"[Inventory] Книгу повернуто на склад.");
    }
    #endregion
    
}