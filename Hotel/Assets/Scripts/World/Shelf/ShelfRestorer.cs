// Assets/Scripts/World/Shelf/ShelfRestorer.cs
// ОНОВЛЕНО: Shelf.LoadFromSaveEntry() замість PlaceBook з Instantiate.
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class ShelfRestorer
{
   public static void RestoreAll(List<ShelfSaveEntry> entries)
{
    if (entries == null || entries.Count == 0) return;

    var shelfMap = new Dictionary<string, Shelf>();
    var allShelves = Object.FindObjectsByType<Shelf>(FindObjectsInactive.Exclude);
    foreach (var shelf in allShelves)
        shelfMap[$"{shelf.name}_{shelf.transform.GetSiblingIndex()}"] = shelf;

    foreach (var entry in entries)
    {
        if (shelfMap.TryGetValue(entry.shelfID, out Shelf shelf))
            shelf.LoadFromSaveEntry(entry); // делегуємо Shelf — вона знає свій формат
        else
            Debug.LogWarning($"[ShelfRestorer] Shelf не знайдена: '{entry.shelfID}'");
    }

    Debug.Log("[ShelfRestorer] All shelves restored.");
}

    public static IEnumerator RestoreAllAsync(List<ShelfSaveEntry> entries)
    {
        if (entries == null) yield break;
        var shelfMap = BuildShelfMap();
        foreach (var entry in entries)
        {
            if (shelfMap.TryGetValue(entry.shelfID, out Shelf shelf))
                shelf.LoadFromSaveEntry(entry);
            yield return null;
        }
    }

    private static Dictionary<string, Shelf> BuildShelfMap()
    {
        var map = new Dictionary<string, Shelf>();
        foreach (var s in Object.FindObjectsByType<Shelf>(FindObjectsSortMode.None))
            map[s.GetInstanceID().ToString()] = s;
        return map;
    }
}