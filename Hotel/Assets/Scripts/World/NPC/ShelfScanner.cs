// Assets/Scripts/World/NPC/ShelfScanner.cs
// FIXES:
//   - Додано struct ShelfScanResult { template, index }
//   - Додано метод FindBook() що повертає ShelfScanResult?
//     (саме його очікує NPCBrain після рефакторингу)
//   - FindBookOnShelf() збережено для зворотної сумісності

using UnityEngine;

public class ShelfScanner : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // Result type
    // ─────────────────────────────────────────────────────────────

    /// Результат сканування: шаблон книги + її індекс у Shelf._books.
    public struct ShelfScanResult
    {
        public BookTemplate template;
        public int          index;
    }

    // ─────────────────────────────────────────────────────────────
    // New API — використовується NPCBrain
    // ─────────────────────────────────────────────────────────────

    /// Шукає підходящу книгу на полиці, повертає шаблон + індекс.
    /// Повертає null якщо нічого не знайдено.
    public ShelfScanResult? FindBook(Shelf shelf, NPCPersonality personality)
    {
        if (shelf == null || personality == null) return null;
        if (shelf.ZoneType != ShopZoneType.Storefront) return null;

        var allBooks = shelf.GetAllBookData();
        if (allBooks == null) return null;

        for (int i = 0; i < allBooks.Count; i++)
        {
            var entry = allBooks[i];
            if (entry.isReserved) continue;

            BookTemplate tmpl = BookDatabase.Instance?.GetBook(entry.templateID);
            if (tmpl == null) continue;

            if (tmpl.genre      != personality.DesiredGenre)  continue;
            if (tmpl.sellPrice   > personality.MaxBudget)      continue;
            if (!personality.AcceptsRarity(tmpl.rarity))      continue;

            Debug.Log($"[Scanner] Match: {tmpl.title} ({tmpl.rarity}) ${tmpl.sellPrice} idx={i}");
            return new ShelfScanResult { template = tmpl, index = i };
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────
    // Legacy API — зворотна сумісність
    // ─────────────────────────────────────────────────────────────

    /// Повертає лише BookTemplate (без індексу).
    /// Залишено для старого коду що не потребує індексу.
    public BookTemplate FindBookOnShelf(Shelf shelf, NPCPersonality personality)
    {
        return FindBook(shelf, personality)?.template;
    }
}