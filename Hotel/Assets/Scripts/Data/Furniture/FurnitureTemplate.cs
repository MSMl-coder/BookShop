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
    public GameObject prefab; // Must contain the 'Cabinet' script
    public Sprite icon;

    [Header("Stats")]
    public int basePrice = 100;
}