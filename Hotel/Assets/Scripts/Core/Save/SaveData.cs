// Assets/Scripts/Core/Save/SaveData.cs
// Поля відповідають GameStateSerializer.CollectSaveData() / ApplySaveData()
using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    // ── UI мета (для відображення в слотах) ──────────
    public int    slotIndex;
    public string saveName;       // "День 5 · 2 450 грн"
    public string saveDateTime;   // "2025-01-15 18:42"
    public string version = "1.0";

    // ── Прогрес ──────────────────────────────────────
    public int   currentDay     = 1;
    public int   money          = 500;   // EconomyManager.Money
    public float totalPlayTime  = 0f;    // накопичений час
    public int   gameStateIndex = 0;     // (int)GameState

    // ── Інвентар книг ─────────────────────────────────
    // GameStateSerializer додає обидва списки паралельно:
    //   inventoryBookIDs[i]     → templateID  (тип книги)
    //   inventoryInstanceIDs[i] → instanceID  (унікальний екземпляр)
    public List<string> inventoryBookIDs      = new();
    public List<string> inventoryInstanceIDs  = new();

    // ── Книги на полицях ──────────────────────────────
    // GameStateSerializer → shelf.CollectSaveData() → ShelfSaveEntry
    public List<ShelfSaveEntry> placedBooks = new();

    // ── Меблі ─────────────────────────────────────────
    // inventoryManager.GetAllUnlockedFurniture() → f.furnitureID
    public List<string> unlockedFurnitureIDs = new();
}

[Serializable]
public class ShelfSaveEntry
{
    public string cabinetName;
    public int    shelfIndex;
    // shelf.CollectSaveData() заповнює templateIDs
    public List<string> templateIDs = new();
    internal object instanceIDs;
}
