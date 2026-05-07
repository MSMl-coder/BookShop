// Assets/Scripts/Data/Furniture/FurnitureTemplate.cs
using UnityEngine;

public enum FurnitureClass { WallShelf, CenterIsland, Decor }

[CreateAssetMenu(fileName = "FT_", menuName = "Bookstore/Furniture Template")]
public class FurnitureTemplate : ScriptableObject
{
    [Header("Identity")]
    // ВИПРАВЛЕНО: було public string furnitureID, але в .asset файлах (FT_.asset, FT_ 1.asset)
    // це поле серіалізовано як int (furnitureID: 1, furnitureID: 2).
    // Зміна на string призводила до того що Unity десеріалізував значення як порожній рядок.
    public int furnitureID;
    public string furnitureName;
    public FurnitureClass furnitureClass;

    [Header("Visuals")]
    public GameObject prefab;
    public Sprite icon;

    [Header("Stats")]
    public int basePrice = 100;

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

    [System.NonSerialized]
    public bool IsPlaced = false;
}
