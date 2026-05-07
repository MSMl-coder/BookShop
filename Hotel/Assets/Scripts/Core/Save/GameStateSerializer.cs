using UnityEngine;
using System.Collections.Generic;

// Збирає та відновлює стан гри з усіх менеджерів
public class GameStateSerializer : MonoBehaviour
{
    public static GameStateSerializer Instance { get; private set; }

    [Header("References")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private EconomyManager economyManager;
    [SerializeField] private GameLoopManager gameLoopManager;

    private float _sessionStartTime;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _sessionStartTime = Time.time;
    }

    // --- Збір даних для збереження ---
    public SaveData CollectSaveData(SaveData existing = null)
    {
        SaveData data = existing ?? new SaveData();

        // Economy
        data.money = economyManager.Money;
        data.currentDay = gameLoopManager.CurrentDay;
        data.totalPlayTime += Time.time - _sessionStartTime;

        // Inventory books
        data.inventoryBookIDs.Clear();
        data.inventoryInstanceIDs.Clear();
        var books = inventoryManager.GetSortedInventory(SortType.ByTitle);
        foreach (var b in books)
        {
            data.inventoryBookIDs.Add(b.templateID);
            data.inventoryInstanceIDs.Add(b.instanceID);
        }

        // Placed books on shelves
        data.placedBooks.Clear();
        var allShelves = FindObjectsByType<Shelf>(FindObjectsSortMode.None);
        foreach (var shelf in allShelves)
        {
            var entry = shelf.CollectSaveData(); // додати до Shelf.cs
            if (entry != null && entry.templateIDs.Count > 0)
                data.placedBooks.Add(entry);
        }

        // Unlocked furniture
        data.unlockedFurnitureIDs.Clear();
        var unlocked = inventoryManager.GetAllUnlockedFurniture();
        foreach (var f in unlocked)
           data.unlockedFurnitureIDs.Add(f.furnitureID.ToString());

        // Game state
        data.gameStateIndex = (int)gameLoopManager.CurrentState;

        Debug.Log($"[Serializer] Collected save data. Books: {books.Count}");
        return data;
    }

    // --- Відновлення стану ---
    public void ApplySaveData(SaveData data)
    {
        if (data == null) return;

        // Economy
        economyManager.SetMoney(data.money);

        // Inventory
        for (int i = 0; i < data.inventoryBookIDs.Count; i++)
        {
            var instance = new BookInstance(data.inventoryBookIDs[i]);
            if (i < data.inventoryInstanceIDs.Count)
                instance.instanceID = data.inventoryInstanceIDs[i];
            inventoryManager.AddExistingBook(instance);
        }

        // Placed books — відновлення через ShelfRestorer
        ShelfRestorer.RestoreAll(data.placedBooks);

        Debug.Log($"[Serializer] Applied save data. Day: {data.currentDay}");
    }

    // Автозбереження при виході
    private void OnApplicationPause(bool pause)
    {
        if (pause) QuickSave();
    }

    private void OnApplicationQuit() => QuickSave();

    public void QuickSave()
    {
        var data = CollectSaveData();
        SaveSystem.Save(data);
        Debug.Log("[Serializer] Auto-saved.");
    }
}