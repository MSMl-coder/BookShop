// Assets/Scripts/World/Shelf/ShelfRestorer.cs
// ОНОВЛЕНО: Shelf.LoadFromSaveEntry() замість PlaceBook з Instantiate.
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class ShelfRestorer
{
    public static void RestoreAll(List<ShelfSaveEntry> entries)
    {
        if (entries == null) return;
        var shelfMap = BuildShelfMap();
        int restored = 0;
        foreach (var entry in entries)
        {
            if (!shelfMap.TryGetValue(entry.shelfID, out Shelf shelf)) continue;
            shelf.LoadFromSaveEntry(entry);
            restored++;
        }
        Debug.Log(string.Format("[ShelfRestorer] Restored {0} shelves.", restored));
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