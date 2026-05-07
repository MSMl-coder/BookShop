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
    public int   money          = 500;
    public float totalPlayTime  = 0f;
    public int   gameStateIndex = 0;

    // ── Інвентар книг ─────────────────────────────────
    public List<string> inventoryBookIDs      = new();
    public List<string> inventoryInstanceIDs  = new();

    // ── Книги на полицях ──────────────────────────────
    public List<ShelfSaveEntry> placedBooks = new();

    // ── Меблі ─────────────────────────────────────────
    public List<string> unlockedFurnitureIDs = new();
}

[Serializable]
public class ShelfSaveEntry
{
    public string cabinetName;
    public int    shelfIndex;
    public string shelfID;  // використовується ShelfRestorer

    // ВИПРАВЛЕНО: було "internal object instanceIDs" — не серіалізується JsonUtility
    // Тепер обидва списки є public List<string> і серіалізуються коректно
    public List<string> templateIDs  = new();
    public List<string> instanceIDs  = new();
}
