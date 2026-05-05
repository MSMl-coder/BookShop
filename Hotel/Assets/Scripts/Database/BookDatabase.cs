// Assets/Scripts/Database/BookDatabase.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BookDatabase", menuName = "Bookstore/Database")]
public class BookDatabase : ScriptableObject
{
    // ВИПРАВЛЕНО: Instance тепер керується BookDatabaseLoader,
    // а не самим SO
    public static BookDatabase Instance { get; private set; }

    public List<BookTemplate> allBooks = new List<BookTemplate>();

    private Dictionary<string, BookTemplate> _cache;
    private bool _initialized = false;

    public void Initialize()
    {
        if (_initialized) return;

        Instance = this;
        _cache = new Dictionary<string, BookTemplate>();

        foreach (var book in allBooks)
        {
            if (book == null || string.IsNullOrEmpty(book.bookID)) continue;

            if (!_cache.ContainsKey(book.bookID))
                _cache.Add(book.bookID, book);
            else
                Debug.LogWarning($"[Database] Duplicate bookID: {book.bookID}");
        }

        _initialized = true;
        Debug.Log($"[Database] Initialized. Books: {_cache.Count}");
    }

    public BookTemplate GetBook(string id)
    {
        if (!_initialized) Initialize();
        if (string.IsNullOrEmpty(id)) return null;

        _cache.TryGetValue(id, out BookTemplate book);
        return book;
    }

    public bool HasBook(string id)
    {
        if (!_initialized) Initialize();
        return !string.IsNullOrEmpty(id) && _cache.ContainsKey(id);
    }

    private void OnDisable()
    {
        // Скидаємо стан бази при виході з Play Mode, 
        // щоб наступний запуск ініціалізував її наново
        _initialized = false;
        
        if (_cache != null)
        {
            _cache.Clear();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
    
}