// Assets/Scripts/World/NPC/ShelfScanner.cs
using UnityEngine;

public class ShelfScanner : MonoBehaviour
{
public BookTemplate FindBookOnShelf(Shelf shelf, NPCPersonality personality)
{
    if (shelf == null || personality == null) return null;
    if (shelf.ZoneType != ShopZoneType.Storefront) return null;

    var allBooks = shelf.GetAllBookData();
    if (allBooks == null) return null;

    for (int i = 0; i < allBooks.Count; i++)
    {
        var entry = allBooks[i];
        if (entry.isReserved) continue;

        BookTemplate template = BookDatabase.Instance?.GetBook(entry.templateID);
        if (template == null) continue;

        if (template.genre       != personality.DesiredGenre)      continue;
        if (template.sellPrice    > personality.MaxBudget)          continue;
        if (!personality.AcceptsRarity(template.rarity))           continue;

        Debug.Log($"[Scanner] Match: {template.title} ({template.rarity}) ${template.sellPrice}");
        return template;
    }
    return null;
}
}