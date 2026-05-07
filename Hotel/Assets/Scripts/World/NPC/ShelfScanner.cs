using UnityEngine;

// Scans a shelf for a book matching genre and budget
public class ShelfScanner : MonoBehaviour
{
    // ВИПРАВЛЕНО: перевірка shelf == null тепер виконується ПЕРШОЮ
    // Раніше shelf.ZoneType зверталось до shelf до null-перевірки — NullReferenceException
    public BookTemplate FindBookOnShelf(Shelf shelf,  BookGenre genre, float maxBudget)
    {
        if (shelf == null) return null;

        // NPC не може купити книгу з Book Club зони
        if (shelf.ZoneType != ShopZoneType.Storefront) return null;

        var bookItems = shelf.GetComponentsInChildren<BookWorldItem>();

        foreach (var item in bookItems)
        {
            if (item?.instance == null) continue;

            BookTemplate template = BookDatabase.Instance?.GetBook(item.instance.templateID);
            if (template == null) continue;

            if (template.genre == genre && template.sellPrice <= maxBudget)
            {
                Debug.Log($"[Scanner] Found match: {template.title} ({genre}) ${template.sellPrice}");
                return template;
            }
        }

        return null;
    }
}
