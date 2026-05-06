// Assets/Scripts/Data/Furniture/FurnitureTemplate.cs
// ОНОВЛЕНО: додано поля для бонусів, сетів та умов розблокування
using UnityEngine;

public enum FurnitureClass { WallShelf, CenterIsland, Decor }

[CreateAssetMenu(fileName = "FT_", menuName = "Bookstore/Furniture Template")]
public class FurnitureTemplate : ScriptableObject
{
    [Header("Identity")]
    public string furnitureID;
    public string furnitureName;
    public FurnitureClass furnitureClass;

    [Header("Visuals")]
    public GameObject prefab;   // Must contain the 'Cabinet' script
    public Sprite icon;

    [Header("Stats")]
    public int basePrice = 100;

    // ── НОВІ ПОЛЯ ──────────────────────────────────────

    [Header("Set & Bonus")]
    [Tooltip("ID набору меблів. Предмети з однаковим setID дають сет-бонус.")]
    public string setID = "";

    [Tooltip("Опис бонусу що дає предмет або набір.")]
    [TextArea(1, 3)]
    public string bonusDescription = "";

    [Header("Unlock")]
    [Tooltip("Текстовий опис умови розблокування (показується гравцю).")]
    [TextArea(1, 2)]
    public string unlockCondition = "";

    [Tooltip("Якщо true — предмет доступний з початку гри.")]
    public bool unlockedByDefault = false;

    // ── Runtime (не серіалізується) ────────────────────

    [System.NonSerialized]
    public bool IsPlaced = false;   // Чи розміщено в сцені зараз
}
