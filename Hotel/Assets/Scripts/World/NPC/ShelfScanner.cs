// Assets/Scripts/World/NPC/ShelfScanner.cs
using UnityEngine;

public class ShelfScanner : MonoBehaviour
{
    public struct ShelfScanResult
    {
        public BookTemplate template;
        public int          index;
    }

    /// Шукає підходящу книгу на полиці, повертає шаблон + індекс.
    public ShelfScanResult? FindBook(Shelf shelf, NPCPersonality personality)
    {
        if (shelf == null || personality == null) return null;

        // Пропускаємо тільки Storage — Storefront і невиставлений ZoneType дозволяємо
        if (shelf.ZoneType == ShopZoneType.Storage) return null;

        var allBooks = shelf.GetAllBookData();
        if (allBooks == null || allBooks.Count == 0) return null;

        for (int i = 0; i < allBooks.Count; i++)
        {
            var entry = allBooks[i];
            if (entry.isReserved) continue;

            BookTemplate tmpl = BookDatabase.Instance?.GetBook(entry.templateID);
            if (tmpl == null) continue;

            if (tmpl.genre    != personality.DesiredGenre)  continue;
            if (tmpl.sellPrice > personality.MaxBudget)      continue;
            if (!personality.AcceptsRarity(tmpl.rarity))    continue;

            Debug.Log($"[Scanner] Match: '{tmpl.title}' ({tmpl.rarity}) ${tmpl.sellPrice} idx={i}");
            return new ShelfScanResult { template = tmpl, index = i };
        }

        return null;
    }

    /// Повертає лише BookTemplate (без індексу). Legacy API.
    public BookTemplate FindBookOnShelf(Shelf shelf, NPCPersonality personality)
    {
        return FindBook(shelf, personality)?.template;
    }
}