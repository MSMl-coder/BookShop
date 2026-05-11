// Assets/Scripts/World/NPC/ShelfScanner.cs
// ОНОВЛЕНО: замінено GetComponentsInChildren<BookWorldItem>() на shelf.GetAllBookData().
// Тепер NPC шукає книги безпосередньо в даних — швидше і не потребує GameObjects.
//
// ЗМІНИ відносно git:
//   - FindBookOnShelf(): GetComponentsInChildren замінено на GetAllBookData()
//   - ДОДАНО: FindBookIndexOnShelf() — повертає індекс для TakeBookAt()
//   - Решта без змін.
using UnityEngine;

public class ShelfScanner : MonoBehaviour
{
    // ── Існуючий метод — оновлено ─────────────────────────────────────────────

    /// Знаходить template книги що підходить NPC за жанром та бюджетом.
    /// СУМІСНІСТЬ: сигнатура та повернений тип без змін.
    public BookTemplate FindBookOnShelf(Shelf shelf, BookGenre genre, float maxBudget)
    {
        if (shelf == null) return null;
        if (shelf.ZoneType != ShopZoneType.Storefront) return null;

        // БУЛО: var bookItems = shelf.GetComponentsInChildren<BookWorldItem>();
        // СТАЛО: читаємо прямо з даних — без traversal по дереву трансформів
        var books = shelf.GetAllBookData();

        foreach (var entry in books)
        {
            if (entry.isReserved) continue; // пропускаємо зарезервовані

            BookTemplate template = BookDatabase.Instance?.GetBook(entry.templateID);
            if (template == null) continue;

            if (template.genre == genre && template.sellPrice <= maxBudget)
            {
                Debug.Log($"[Scanner] Found match: {template.title} ({genre}) ${template.sellPrice}");
                return template;
            }
        }

        return null;
    }

    // ── Новий метод — для TakeBookAt() ────────────────────────────────────────

    /// Знаходить індекс книги для подальшого TakeBookAt().
    /// NPC має викликати shelf.TakeBookAt(index) після підтвердження покупки.
    public int FindBookIndexOnShelf(Shelf shelf, BookGenre genre, float maxBudget)
    {
        if (shelf == null) return -1;
        if (shelf.ZoneType != ShopZoneType.Storefront) return -1;

        var books = shelf.GetAllBookData();

        for (int i = 0; i < books.Count; i++)
        {
            if (books[i].isReserved) continue;

            BookTemplate template = BookDatabase.Instance?.GetBook(books[i].templateID);
            if (template == null) continue;

            if (template.genre == genre && template.sellPrice <= maxBudget)
                return i;
        }

        return -1;
    }

    /// Резервує книгу для NPC (щоб інший NPC не взяв її першим).
    /// Повертає індекс або -1 якщо немає підходящої.
    public int ReserveBookForNPC(Shelf shelf, BookGenre genre, float maxBudget, string npcID)
    {
        int index = FindBookIndexOnShelf(shelf, genre, maxBudget);
        if (index < 0) return -1;

        bool reserved = shelf.ReserveBook(index, npcID);
        return reserved ? index : -1;
    }
}