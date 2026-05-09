// Assets/Scripts/EditMode/PlaceableItem.cs
using UnityEngine;

[CreateAssetMenu(fileName = "New PlaceableItem", menuName = "Bookshop/PlaceableItem")]
public class PlaceableItem : ScriptableObject
{
    [Header("Identity")]
    public string itemName;
    public Sprite icon;
    public GameObject prefab;

    [Header("Placement Constraints")]
    public bool canSnapToWall   = false;
    public bool canSnapToShelf  = false;
    public bool canSnapToFloor  = true;

    [Header("Offsets")]
    public float pivotOffset  = 0f; // від підлоги до pivot
    public float wallOffset   = 0.02f;
    public float shelfOffset  = 0.01f;

    [Header("Unlock")]
    public bool unlockedByDefault = true;
    public int  unlockDay = 1;
}