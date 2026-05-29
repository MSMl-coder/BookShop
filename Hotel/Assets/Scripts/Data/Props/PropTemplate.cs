// Assets/Scripts/Data/Props/PropTemplate.cs
// RENAME: FurnitureTemplate → PropTemplate
// ЗМІНИ:
//   - FurnitureClass → PropClass
//   - furnitureID → propID, furnitureName → propName
//   - Додано секцію [NPC Mood — Decor] з moodForce та decorCategory
//   - Додано секцію [NPC Comfort — Seating] з comfortForce, capacity, patienceRestoreRate
//
// СУМІСНІСТЬ: всі старі поля збережені, нові — додаткові.
// При рефакторингу замінити:
//   FurnitureTemplate → PropTemplate
//   furnitureID       → propID
//   furnitureName     → propName
//   furnitureClass    → propClass
//   FurnitureClass    → PropClass

using UnityEngine;

[CreateAssetMenu(fileName = "Prop_", menuName = "Bookstore/Prop Template")]
public class PropTemplate : ScriptableObject
{
    // ─────────────────────────────────────────────
    [Header("Identity")]
    // ─────────────────────────────────────────────

    public int       propID;
    public string    propName;
    public PropClass propClass;

    // ─────────────────────────────────────────────
    [Header("Visuals")]
    // ─────────────────────────────────────────────

    public GameObject prefab;
    public Sprite     icon;

    // ─────────────────────────────────────────────
    [Header("Economy")]
    // ─────────────────────────────────────────────

    public int basePrice = 100;

    // ─────────────────────────────────────────────
    [Header("Set & Bonus")]
    // ─────────────────────────────────────────────

    public string setID = "";

    [TextArea(1, 3)]
    public string bonusDescription = "";

    // ─────────────────────────────────────────────
    [Header("Unlock")]
    // ─────────────────────────────────────────────

    [TextArea(1, 2)]
    public string unlockCondition   = "";
    public bool   unlockedByDefault = false;

    // ─────────────────────────────────────────────
    [Header("Edit Mode — Placement")]
    // ─────────────────────────────────────────────

    public bool  canSnapToWall  = false;
    public bool  canSnapToShelf = false;
    public float pivotOffset    = 0f;
    public float wallOffset     = 0.05f;
    public float shelfOffset    = 0.01f;

    // ─────────────────────────────────────────────
    [Header("NPC Mood — Decor")]
    // ─────────────────────────────────────────────

    [Tooltip("Лише для propClass == Decor. " +
             "Скільки одиниць/сек додає до moodRiseRate магазину. " +
             "Сумується з усіма декорами, кепується ShopAtmosphereService.")]
    [Range(0f, 5f)]
    public float moodForce = 0f;

    [Tooltip("Підкатегорія декору для diversity bonus. " +
             "2+ різних категорій = множник ×1.2 на весь moodRiseRate.")]
    public DecorCategory decorCategory = DecorCategory.None;

    // ─────────────────────────────────────────────
    [Header("NPC Comfort — Seating")]
    // ─────────────────────────────────────────────

    [Tooltip("Лише для propClass == Seating. " +
             "Швидкість росту Comfort NPC поки сидить (одиниць/сек).")]
    [Range(0f, 10f)]
    public float comfortForce = 0f;

    [Tooltip("Скільки NPC можуть одночасно сидіти на цьому меблі.")]
    [Range(1, 6)]
    public int seatCapacity = 1;

    [Tooltip("Скільки Patience відновлюється на секунду під час сидіння. " +
             "Додатково до базового restoreWhileResting з NPCStatConfig.")]
    [Range(0f, 2f)]
    public float patienceRestoreBonus = 0f;

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────

    public bool IsDecor   => propClass == PropClass.Decor;
    public bool IsSeating => propClass == PropClass.Seating;

    // ── Backward-compat shims (видалити після повного рефакторингу) ──
    public int            furnitureID    => propID;
    public string         furnitureName  => propName;
    public PropClass      furnitureClass => propClass;
}