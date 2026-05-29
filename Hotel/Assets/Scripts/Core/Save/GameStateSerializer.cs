// Assets/Scripts/Core/Save/GameStateSerializer.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameStateSerializer : MonoBehaviour
{
    public static GameStateSerializer Instance { get; private set; }

    [Header("References")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private EconomyManager   economyManager;
    [SerializeField] private GameLoopManager  gameLoopManager;

    private float _sessionStartTime;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        _sessionStartTime = Time.time;
    }

    // ─────────────────────────────────────────────
    #region Collect
    // ─────────────────────────────────────────────

    public SaveData CollectSaveData(SaveData existing = null)
    {
        SaveData data = existing ?? new SaveData();

        // ── Economy ──────────────────────────────
        data.money         = economyManager.Money;
        data.currentDay    = gameLoopManager.CurrentDay;
        data.totalPlayTime += Time.time - _sessionStartTime;
        data.gameStateIndex = (int)gameLoopManager.CurrentState;

        // ── Books: інвентар ──────────────────────
        data.inventoryBookIDs.Clear();
        data.inventoryInstanceIDs.Clear();
        var books = inventoryManager.GetSortedInventory(SortType.ByTitle);
        foreach (var b in books)
        {
            data.inventoryBookIDs.Add(b.templateID);
            data.inventoryInstanceIDs.Add(b.instanceID);
        }

        // ── Books: на полицях ────────────────────
        data.placedBooks.Clear();
        var allShelves = FindObjectsByType<Shelf>(FindObjectsInactive.Exclude);
        foreach (var shelf in allShelves)
        {
            var entry = shelf.CollectSaveData();
            if (entry != null && entry.templateIDs.Count > 0)
                data.placedBooks.Add(entry);
        }

        // ── Furniture ────────────────────────────
        data.furnitureInventory.Clear();
        data.placedFurniture.Clear();

        PlacementRegistry.Instance?.SyncPositions();

        var allInstances = inventoryManager.GetAllFurnitureInstances();
        foreach (var fi in allInstances)
        {
            data.furnitureInventory.Add(new FurnitureSaveEntry
            {
                templateID = fi.templateID,
                instanceID = fi.instanceID,
                isPlaced   = fi.isPlaced
            });

            if (fi.isPlaced)
            {
                data.placedFurniture.Add(new FurniturePlacedEntry
                {
                    instanceID = fi.instanceID,
                    px = fi.placedPosition.x,
                    py = fi.placedPosition.y,
                    pz = fi.placedPosition.z,
                    rx = fi.placedRotation.x,
                    ry = fi.placedRotation.y,
                    rz = fi.placedRotation.z,
                    rw = fi.placedRotation.w
                });
            }
        }

        Debug.Log($"[Serializer] Collected. Books: {books.Count}, Furniture: {allInstances.Count}");
        return data;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Apply
    // ─────────────────────────────────────────────

    public void ApplySaveData(SaveData data)
    {
        if (data == null) return;

        economyManager.SetMoney(data.money);

        // Books: інвентар
        for (int i = 0; i < data.inventoryBookIDs.Count; i++)
        {
            var instance = new BookInstance(data.inventoryBookIDs[i]);
            if (i < data.inventoryInstanceIDs.Count)
                instance.instanceID = data.inventoryInstanceIDs[i];
            inventoryManager.AddExistingBook(instance);
        }

        // Books: полиці
        if (data.placedBooks != null && data.placedBooks.Count > 0)
        ShelfRestorer.RestoreAll(data.placedBooks);
        else
        DefaultShelfFiller.FillAll(); // нова гра

        // Furniture
        RestoreFurniture(data);

        Debug.Log($"[Serializer] Applied. Day: {data.currentDay}");
    }

    private void RestoreFurniture(SaveData data)
    {
        if (data.furnitureInventory == null || data.furnitureInventory.Count == 0) return;

        var placedDict = (data.placedFurniture ?? new List<FurniturePlacedEntry>())
            .ToDictionary(e => e.instanceID);

        foreach (var entry in data.furnitureInventory)
        {
            var fi = new PropInstance(entry.templateID, entry.instanceID);
            inventoryManager.AddFurnitureInstance(fi);

            if (!entry.isPlaced) continue;
            if (!placedDict.TryGetValue(entry.instanceID, out var placed)) continue;

            var template = inventoryManager.GetTemplate(entry.templateID);
            if (template?.prefab == null)
            {
                Debug.LogWarning($"[Serializer] Prefab не знайдено: templateID={entry.templateID}");
                continue;
            }

            var pos = new Vector3(placed.px, placed.py, placed.pz);
            var rot = new Quaternion(placed.rx, placed.ry, placed.rz, placed.rw);
            var go  = Object.Instantiate(template.prefab, pos, rot);

            var placedObj = go.AddComponent<PlacedObject>();
            placedObj.Init(fi);

            PlacementRegistry.Instance?.Register(go, fi);
        }
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Auto Save
    // ─────────────────────────────────────────────

    private void OnApplicationPause(bool pause) { if (pause) QuickSave(); }
    private void OnApplicationQuit() => QuickSave();

    public void QuickSave()
    {
        var data = CollectSaveData();
        SaveSystem.Save(data);
        Debug.Log("[Serializer] Auto-saved.");
    }

    #endregion
}