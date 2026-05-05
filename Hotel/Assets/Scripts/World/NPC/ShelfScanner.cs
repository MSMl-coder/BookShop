using UnityEngine;

// Scans a shelf for a book matching genre and budget
public class ShelfScanner : MonoBehaviour
{
    // Returns first matching BookTemplate found on shelf, or null
   public BookTemplate FindBookOnShelf(Shelf shelf, BookEnums.BookGenre genre, float maxBudget)
    {
    // NPC не може купити книгу з Book Club зони
    if (shelf.ZoneType != ShopZoneType.Storefront) return null;

        if (shelf == null) return null;

        // Get all BookWorldItems on this shelf
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