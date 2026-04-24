using UnityEngine;

public enum LootCardType { FurnitureUpgrade, MoneyBonus, BookPack }

[CreateAssetMenu(fileName = "LCT_", menuName = "Bookstore/Loot Card Template")]
public class LootCardTemplate : ScriptableObject
{
    public string cardName;
    [TextArea] public string description;
    public Sprite icon;
    public LootCardType type;
    public bool isGold; // For legendary 5% drop rate

    [Header("Payload")]
    public FurnitureTemplate furniturePayload;
    public int moneyPayload;

    [Header("Economics")]
    public int cost;
}