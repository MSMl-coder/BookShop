// Assets/Scripts/Core/Save/SaveData.cs
// ВИПРАВЛЕНО:
//   ShelfSaveEntry.instanceIDs  — List<string> замість "internal object"
//   ShelfSaveEntry.shelfID      — string (GameStateSerializer пише це поле)
// Всі поля відповідають GameStateSerializer.CollectSaveData() / ApplySaveData()
using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    // ── UI мета (для слотів збереження) ──────────────
    public int    slotIndex;
    public string saveName;       // "День 5 · 2 450 грн"
    public string saveDateTime;   // "2025-01-15 18:42"
    public string version = "1.0";

    // ── Прогрес ──────────────────────────────────────
    public int   currentDay     = 1;
    public int   money          = 500;   // EconomyManager.Money
    public float totalPlayTime  = 0f;
    public int   gameStateIndex = 0;     // (int)GameState

    // ── Інвентар книг ─────────────────────────────────
    // GameStateSerializer заповнює обидва паралельно: [i] = одна книга
    public List<string> inventoryBookIDs     = new();   // b.templateID
    public List<string> inventoryInstanceIDs = new();   // b.instanceID (string GUID)

    // ── Книги на полицях ──────────────────────────────
    // shelf.CollectSaveData() → ShelfSaveEntry
    public List<ShelfSaveEntry> placedBooks = new();

    // ── Розблоковані меблі ────────────────────────────
    // inventoryManager.GetAllUnlockedFurniture() → f.furnitureID
    public List<string> unlockedFurnitureIDs = new();
}

[Serializable]
public class ShelfSaveEntry
{
    public string cabinetName;
    public int    shelfIndex;

    // GameStateSerializer пише shelfID (рядок 197 Shelf.cs → GetInstanceID())
    // Але GetInstanceID() повертає int — конвертуємо в string при збереженні
    public string shelfID;

    // templateIDs — GameStateSerializer очікував це
    public List<string> templateIDs = new();

    // instanceIDs — string GUID кожного BookInstance
    // ВИПРАВЛЕНО: було "internal object instanceIDs" → тепер List<string>
    public List<string> instanceIDs = new();
}
