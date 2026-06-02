// Assets/Scripts/Data/Props/PropInstance.cs
// RENAME: FurnitureInstance → PropInstance
// ЗМІНИ: templateID тепер propID (з backward-compat shim)
//
// Стан розміщення живе тут, не в ScriptableObject.
// PlacementRegistry зберігає PropInstance замість FurnitureInstance.

using UnityEngine;

[System.Serializable]
public class PropInstance
{
    // ── Identity ─────────────────────────────────────────────────
    public int    propID;       // PropTemplate.propID
    public string instanceID;   // унікальний GUID

    // ── Placement state ──────────────────────────────────────────
    public bool       isPlaced;
    public Vector3    placedPosition;
    public Quaternion placedRotation;

    // ─────────────────────────────────────────────────────────────
    // Constructors
    // ─────────────────────────────────────────────────────────────

    /// Новий екземпляр — генерує GUID
    public PropInstance(int propID)
    {
        this.propID   = propID;
        instanceID    = System.Guid.NewGuid().ToString();
        isPlaced      = false;
        placedPosition = Vector3.zero;
        placedRotation = Quaternion.identity;
    }

    /// Відновлення із збереження — зберігає існуючий GUID
    public PropInstance(int propID, string existingInstanceID)
    {
        this.propID    = propID;
        instanceID     = existingInstanceID;
        isPlaced       = false;
        placedPosition = Vector3.zero;
        placedRotation = Quaternion.identity;
    }

    // ── Backward-compat shim (видалити після рефакторингу) ───────
    public int templateID => propID;
}