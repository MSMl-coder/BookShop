using UnityEngine;
using System.Collections.Generic;

// Відновлює книги на полицях після завантаження
public static class ShelfRestorer
{
    public static void RestoreAll(List<ShelfSaveEntry> entries)
    {
        if (entries == null) return;

        // Build shelfID → Shelf map
        var shelfMap = new Dictionary<string, Shelf>();
        var allShelves = Object.FindObjectsByType<Shelf>(FindObjectsSortMode.None);
        foreach (var shelf in allShelves)
            shelfMap[shelf.GetInstanceID().ToString()] = shelf;

        foreach (var entry in entries)
        {
            if (!shelfMap.TryGetValue(entry.shelfID, out Shelf shelf)) continue;

            for (int i = 0; i < entry.templateIDs.Count; i++)
            {
                BookTemplate template = BookDatabase.Instance?.GetBook(entry.templateIDs[i]);
                if (template?.containerPrefab == null) continue;

                var instance = new BookInstance(entry.templateIDs[i]);
                if (i < entry.instanceIDs.Count)
                    instance.instanceID = entry.instanceIDs[i];

                shelf.PlaceBook(instance, template.containerPrefab);
            }
        }

        Debug.Log("[ShelfRestorer] All shelves restored.");
    }
}