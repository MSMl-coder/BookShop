// Assets/Scripts/World/Shelf/DefaultShelfFiller.cs
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Заповнює полиці книгами при першому запуску (нова гра).
/// Викликається з GameStateSerializer.ApplySaveData() якщо placedBooks порожній.
/// </summary>
public static class DefaultShelfFiller
{
    public static void FillAll()
    {
        var db = BookDatabase.Instance;
        if (db == null || db.allBooks == null || db.allBooks.Count == 0)
        {
            Debug.LogWarning("[DefaultShelfFiller] BookDatabase порожній або не ініціалізований!");
            return;
        }

        var allShelves = Object.FindObjectsByType<Shelf>(FindObjectsInactive.Exclude);
        if (allShelves.Length == 0)
        {
            Debug.LogWarning("[DefaultShelfFiller] Полиці не знайдені на сцені!");
            return;
        }

        int placed = 0;
        int templateIndex = 0;
        var templates = db.allBooks;

        foreach (var shelf in allShelves)
        {
            if (shelf == null) continue;

            while (templateIndex < templates.Count)
            {
                var template = templates[templateIndex];
                templateIndex++;

                if (template == null) continue;

                if (!shelf.CanFitBook(template))
                {
                    templateIndex--; // ця книга не влізла — спробуємо на наступній полиці
                    break;
                }

                var instance = new BookInstance(template.bookID);
                shelf.PlaceBook(instance); // v4.2 API — тільки instance, без prefab
                placed++;
            }
        }

        Debug.Log($"[DefaultShelfFiller] Розміщено {placed} книг на {allShelves.Length} полицях.");
    }
}