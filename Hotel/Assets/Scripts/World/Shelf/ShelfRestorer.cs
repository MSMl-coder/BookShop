// Assets/Scripts/World/Shelf/ShelfRestorer.cs
// ФІКС:
//   BuildShelfMap() використовував GetInstanceID().ToString() як ключ,
//   тоді як ShelfSaveEntry.shelfID зберігається як "{name}_{siblingIndex}".
//   Результат: RestoreAllAsync() НІКОЛИ не знаходила жодної полиці — мовчазна
//   корупція збережених книг при async-відновленні.
//
//   Обидва методи тепер використовують однаковий ключ і ShelfRegistry як
//   основне джерело (FindObjectsByType — запасний варіант).

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class ShelfRestorer
{
    // ── Sync (основний шлях) ──────────────────────────────────────────

    public static void RestoreAll(List<ShelfSaveEntry> entries)
    {
        if (entries == null || entries.Count == 0) return;

        var shelfMap = BuildShelfMap();

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.shelfID))
            {
                Debug.LogWarning("[ShelfRestorer] Запис з порожнім shelfID — пропускаємо.");
                continue;
            }

            if (shelfMap.TryGetValue(entry.shelfID, out Shelf shelf))
                shelf.LoadFromSaveEntry(entry);
            else
                Debug.LogWarning($"[ShelfRestorer] Полиця не знайдена: '{entry.shelfID}'");
        }

        Debug.Log($"[ShelfRestorer] Відновлено {entries.Count} записів.");
    }

    // ── Async (для корутин) ───────────────────────────────────────────

    public static IEnumerator RestoreAllAsync(List<ShelfSaveEntry> entries)
    {
        if (entries == null) yield break;

        // ✅ ФІКС: використовуємо той самий BuildShelfMap() що і RestoreAll
        var shelfMap = BuildShelfMap();

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.shelfID))
            {
                Debug.LogWarning("[ShelfRestorer] Запис з порожнім shelfID — пропускаємо.");
                continue;
            }

            if (shelfMap.TryGetValue(entry.shelfID, out Shelf shelf))
                shelf.LoadFromSaveEntry(entry);
            else
                Debug.LogWarning($"[ShelfRestorer] (async) Полиця не знайдена: '{entry.shelfID}'");

            yield return null;
        }

        Debug.Log("[ShelfRestorer] Async відновлення завершено.");
    }

    // ── Shared helper ─────────────────────────────────────────────────

    /// Будує словник: "{name}_{siblingIndex}" → Shelf
    /// Збігається з ключем який генерує Shelf.CollectSaveData().
    /// Використовує ShelfRegistry як основне джерело (O(1) доступ),
    /// FindObjectsByType як запасний варіант.
    private static Dictionary<string, Shelf> BuildShelfMap()
    {
        var map = new Dictionary<string, Shelf>();

        IEnumerable<Shelf> allShelves = ShelfRegistry.Instance != null
            ? (IEnumerable<Shelf>)ShelfRegistry.Instance.GetAll()
            : Object.FindObjectsByType<Shelf>(FindObjectsSortMode.None);

        foreach (var s in allShelves)
        {
            if (s == null) continue;
            // ✅ ФІКС: "{name}_{siblingIndex}" — той самий формат що в Shelf.CollectSaveData()
            string key = $"{s.name}_{s.transform.GetSiblingIndex()}";
            map[key] = s;
        }

        return map;
    }
}