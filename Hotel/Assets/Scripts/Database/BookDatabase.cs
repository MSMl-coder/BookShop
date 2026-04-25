using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BookDatabase", menuName = "Bookstore/Database")]
public class BookDatabase : ScriptableObject
{
    public static BookDatabase Instance;
    public List<BookTemplate> allBooks;
    private Dictionary<string, BookTemplate> _cache = new Dictionary<string, BookTemplate>();

    public void Initialize()
    {
        Instance = this;
        if (_cache != null && _cache.Count > 0) return; // вже ініціалізовано
        _cache = new Dictionary<string, BookTemplate>();
        _cache.Clear();
        foreach (var book in allBooks)
        {
            if (book != null && !string.IsNullOrEmpty(book.bookID))
            {
                if (!_cache.ContainsKey(book.bookID))
                {
                    _cache.Add(book.bookID, book);
                }
            }
        }
        Debug.Log($"[Database] Ініціалізовано. Книг у базі: {_cache.Count}");
    }

    public BookTemplate GetBook(string id)
    {
        if (_cache.Count == 0) Initialize();
        _cache.TryGetValue(id, out var book);
        return book;
    }
}