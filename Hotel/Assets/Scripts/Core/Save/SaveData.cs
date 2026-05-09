// Assets/Scripts/Core/Save/SaveData.cs
using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    // ── UI мета ──────────────────────────────────────
    public int    slotIndex;
    public string saveName;
    public string saveDateTime;
    public string version = "1.0";

    // ── Прогрес ──────────────────────────────────────
    public int   currentDay     = 1;
    public int   money          = 500;
    public float totalPlayTime  = 0f;
    public int   gameStateIndex = 0;

    // ── Книги: інвентар ───────────────────────────────
    public List<string> inventoryBookIDs     = new();
    public List<string> inventoryInstanceIDs = new();

    // ── Книги: на полицях ─────────────────────────────
    public List<ShelfSaveEntry> placedBooks = new();

    // ── Меблі: інвентар (всі екземпляри) ─────────────
    public List<FurnitureSaveEntry> furnitureInventory = new();

    // ── Меблі: позиції розміщених ─────────────────────
    public List<FurniturePlacedEntry> placedFurniture = new();

    // ── Застаріле — лишаємо для сумісності зі старими сейвами ──
    // Не використовується в новому коді, але Unity не зламає старі JSON
    public List<string> unlockedFurnitureIDs = new();
}

// ─────────────────────────────────────────────────────
// Книги на полицях
// ─────────────────────────────────────────────────────

[Serializable]
public class ShelfSaveEntry
{
    public string cabinetName;
    public int    shelfIndex;
    public string shelfID;
    public List<string> templateIDs = new();
    public List<string> instanceIDs = new();
}

// ─────────────────────────────────────────────────────
// Меблі
// ─────────────────────────────────────────────────────

[Serializable]
public class FurnitureSaveEntry
{
    public int    templateID;
    public string instanceID;
    public bool   isPlaced;
}

[Serializable]
public class FurniturePlacedEntry
{
    public string instanceID;
    // Position
    public float px, py, pz;
    // Rotation (quaternion)
    public float rx, ry, rz, rw;
}