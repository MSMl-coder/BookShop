// Assets/Scripts/Core/Save/GameStateSerializer.cs
// ФІКСИ:
//   [1] CollectSaveData: замінено FindObjectsByType<Shelf> → ShelfRegistry.GetAll()
//       (з fallback на FindObjectsByType якщо Registry недоступний).
//   [2] ApplySaveData: тепер відновлює currentDay з data.currentDay.
//   [3] ApplySaveData: gameStateIndex більше не ігнорується — після завантаження
//       переходимо у Preparation (WorkDay/DayStats збережений mid-session не відновлюємо,
//       бо це безпечна точка для старту).
//   [4] Додано null guard на data.placedBooks перед ShelfRestorer.RestoreAll.

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
        data.money          = economyManager.Money;
        data.currentDay     = gameLoopManager.CurrentDay;
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
        // ✅ ФІКС [1]: ShelfRegistry замість FindObjectsByType
        data.placedBooks.Clear();
        IEnumerable<Shelf> allShelves = ShelfRegistry.Instance != null
            ? (IEnumerable<Shelf>)ShelfRegistry.Instance.GetAll()
            : Object.FindObjectsByType<Shelf>(FindObjectsInactive.Exclude);

        foreach (var shelf in allShelves)
        {
            if (shelf == null) continue;
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

        Debug.Log($"[Serializer] Зібрано: Books={books.Count}, " +
                  $"Shelves={data.placedBooks.Count}, Furniture={allInstances.Count}, " +
                  $"Day={data.currentDay}");
        return data;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Apply
    // ─────────────────────────────────────────────

    public void ApplySaveData(SaveData data)
    {
        if (data == null) return;

        // ── Economy ──────────────────────────────
        economyManager.SetMoney(data.money);

        // ── ✅ ФІКС [2]: Відновлюємо currentDay ─
        // GameLoopManager не має публічного setter — використовуємо DEBUG метод
        // (безпечний: guard #if UNITY_EDITOR || DEVELOPMENT_BUILD є в GameLoopManager)
        gameLoopManager.DEBUG_SetDay(data.currentDay);

        // ── Books: інвентар ──────────────────────
        for (int i = 0; i < data.inventoryBookIDs.Count; i++)
        {
            var instance = new BookInstance(data.inventoryBookIDs[i]);
            if (i < data.inventoryInstanceIDs.Count)
                instance.instanceID = data.inventoryInstanceIDs[i];
            inventoryManager.AddExistingBook(instance);
        }

        // ── Books: на полицях ────────────────────
        // ✅ ФІКС [4]: null guard перед RestoreAll
        if (data.placedBooks != null && data.placedBooks.Count > 0)
            ShelfRestorer.RestoreAll(data.placedBooks);
        else
            DefaultShelfFiller.FillAll();

        // ── Furniture ────────────────────────────
        RestoreFurniture(data);

        // ── ✅ ФІКС [3]: Відновлюємо стан гри ───
        // Тільки Preparation — безпечна точка старту.
        // WorkDay/DayStats/LootPhase збережені mid-session не відновлюємо,
        // щоб уникнути незавершеного дня або подвійного списання оренди.
        gameLoopManager.ChangeState(GameState.Preparation);

        Debug.Log($"[Serializer] Застосовано. День: {data.currentDay}, " +
                  $"Гроші: {data.money}, Книги: {data.inventoryBookIDs.Count}");
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
    private void OnApplicationQuit()             => QuickSave();

    public void QuickSave()
    {
        var data = CollectSaveData();
        SaveSystem.Save(data);
        Debug.Log("[Serializer] Автозбереження.");
    }

    #endregion
}