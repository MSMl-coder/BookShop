// Assets/Scripts/Data/Furniture/FurnitureTemplate.cs
using UnityEngine;

public enum FurnitureClass { WallShelf, CenterIsland, Decor }

[CreateAssetMenu(fileName = "FT_", menuName = "Bookstore/Furniture Template")]
public class FurnitureTemplate : ScriptableObject
{
    [Header("Identity")]
    public int furnitureID;
    public string furnitureName;
    public FurnitureClass furnitureClass;

    [Header("Visuals")]
    public GameObject prefab;
    public Sprite icon;

    [Header("Stats")]
    public int basePrice = 100;

    [Header("Set & Bonus")]
    public string setID = "";
    [TextArea(1, 3)]
    public string bonusDescription = "";

    [Header("Unlock")]
    [TextArea(1, 2)]
    public string unlockCondition = "";
    public bool unlockedByDefault = false;

    // ── НОВІ ПОЛЯ ДЛЯ EDIT MODE ─────────────────────
    [Header("Edit Mode — Placement")]
    [Tooltip("Може кріпитись до стіни (наприклад картина, полиця)")]
    public bool canSnapToWall = false;

    [Tooltip("Може ставитись на горизонтальну поверхню (полиця, стіл)")]
    public bool canSnapToShelf = false;

    [Tooltip("Зміщення pivot від підлоги вгору")]
    public float pivotOffset = 0f;

    [Tooltip("Відступ від стіни")]
    public float wallOffset = 0.05f;

    [Tooltip("Відступ від поверхні полиці")]
    public float shelfOffset = 0.01f;
    // ─────────────────────────────────────────────────

    [System.NonSerialized]
    public bool IsPlaced = false;
}