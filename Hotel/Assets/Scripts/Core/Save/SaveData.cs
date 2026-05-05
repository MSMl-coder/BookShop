using System;
using System.Collections.Generic;

// Весь стан гри в одному серіалізованому класі
[Serializable]
public class SaveData
{
    // Meta
    public string saveVersion = "1.0";
    public string saveDate;
    public float totalPlayTime;

    // Economy
    public int money;
    public int currentDay;
    public float totalDebt; // Кредит за приміщення

    // Inventory
    public List<string> inventoryBookIDs = new List<string>(); // templateID
    public List<string> inventoryInstanceIDs = new List<string>();

    // Shelves: Key = shelfInstanceID, Value = список instanceID книг
    public List<ShelfSaveEntry> placedBooks = new List<ShelfSaveEntry>();

    // Unlocks
    public List<string> unlockedFurnitureIDs = new List<string>();
    public List<string> unlockedZoneNames = new List<string>();

    // Game Loop
    public int gameStateIndex; // (int)GameState

    // Settings (зберігаємо окремо, але можна тут)
    public float masterVolume = 1f;
    public float musicVolume = 0.8f;
    public int qualityLevel = 2;
}

[Serializable]
public class ShelfSaveEntry
{
    public string shelfID;        // Унікальний ID полиці в сцені
    public List<string> templateIDs = new List<string>();
    public List<string> instanceIDs = new List<string>();
}