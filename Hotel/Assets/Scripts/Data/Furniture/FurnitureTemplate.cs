// Assets/Scripts/Data/Furniture/FurnitureTemplate.cs
using UnityEngine;

public enum FurnitureClass { WallShelf, CenterIsland, Decor }

[CreateAssetMenu(fileName = "FT_", menuName = "Bookstore/Furniture Template")]
public class FurnitureTemplate : ScriptableObject
{
    [Header("Identity")]
    public int           furnitureID;
    public string        furnitureName;
    public FurnitureClass furnitureClass;

    [Header("Visuals")]
    public GameObject prefab;
    public Sprite     icon;

    [Header("Stats")]
    public int basePrice = 100;

    [Header("Set & Bonus")]
    public string setID = "";
    [TextArea(1, 3)]
    public string bonusDescription = "";

    [Header("Unlock")]
    [TextArea(1, 2)]
    public string unlockCondition  = "";
    public bool   unlockedByDefault = false;

    [Header("Edit Mode — Placement")]
    public bool  canSnapToWall  = false;
    public bool  canSnapToShelf = false;
    public float pivotOffset    = 0f;
    public float wallOffset     = 0.05f;
    public float shelfOffset    = 0.01f;

    // IsPlaced ВИДАЛЕНО — стан живе у FurnitureInstance
}