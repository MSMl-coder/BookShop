// Assets/Scripts/World/NPC/ShelfScanner.cs
// v2 — ЗМІНА: personality.DesiredGenre → personality.CurrentDesiredGenre
//   NPCPersonality v4 видалила DesiredGenre на користь ShoppingList + CurrentDesiredGenre.
//   ShelfScanner тепер автоматично шукає правильний жанр для ПОТОЧНОГО слоту.

using UnityEngine;

public class ShelfScanner : MonoBehaviour
{
    public struct ShelfScanResult
    {
        public BookTemplate template;
        public int          index;
    }

    /// Шукає підходящу книгу на полиці для поточного слоту покупки.
    public ShelfScanResult? FindBook(Shelf shelf, NPCPersonality personality)
    {
        if (shelf == null || personality == null) return null;

        // Пропускаємо Storage зони
        if (shelf.ZoneType == ShopZoneType.Storage) return null;

        var allBooks = shelf.GetAllBookData();
        if (allBooks == null || allBooks.Count == 0) return null;

        // ✅ ЗМІНА: використовуємо CurrentDesiredGenre (жанр поточного слоту)
        // Кожна книга в кошику може шукатись за своїм окремим жанром.
        BookGenre targetGenre = personality.CurrentDesiredGenre;

        for (int i = 0; i < allBooks.Count; i++)
        {
            var entry = allBooks[i];
            if (entry.isReserved) continue;

            BookTemplate tmpl = BookDatabase.Instance?.GetBook(entry.templateID);
            if (tmpl == null) continue;

            if (tmpl.genre    != targetGenre)              continue;
            if (tmpl.sellPrice > personality.MaxBudget)    continue;
            if (!personality.AcceptsRarity(tmpl.rarity))   continue;

            Debug.Log($"[Scanner] Match slot {personality.CurrentBookIndex}: " +
                      $"'{tmpl.title}' ({tmpl.rarity}) ${tmpl.sellPrice} idx={i}");
            return new ShelfScanResult { template = tmpl, index = i };
        }

        return null;
    }

    /// Legacy API — повертає лише BookTemplate (без індексу).
    public BookTemplate FindBookOnShelf(Shelf shelf, NPCPersonality personality)
    {
        return FindBook(shelf, personality)?.template;
    }
}