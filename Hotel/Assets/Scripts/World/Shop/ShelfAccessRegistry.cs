// Assets/Scripts/World/Shop/ShelfAccessRegistry.cs  v2
// ЗМІНА: TryClaim тепер повертає Vector3 slotPosition — зміщена позиція
// для кожного слоту щоб два NPC не стояли в одній точці і не блокували один одного.
// Slot 0: +right * offset, Slot 1: -right * offset

using System.Collections.Generic;
using UnityEngine;

public class ShelfAccessRegistry : MonoBehaviour
{
    public static ShelfAccessRegistry Instance { get; private set; }

    [SerializeField] [Range(1, 4)] private int maxPerShelf = 2;

    [Tooltip("Бічне зміщення між двома NPC що стоять біля однієї полиці (метри).")]
    [SerializeField] private float slotSideOffset = 0.55f;

    [Tooltip("Відстань від полиці (вперед).")]
    [SerializeField] private float standDistance = 1.2f;

    private class ShelfRecord
    {
        public readonly string[] Occupants; // NPC instanceID або null
        public ShelfRecord(int cap) { Occupants = new string[cap]; }

        public int FreeSlot()
        {
            for (int i = 0; i < Occupants.Length; i++)
                if (Occupants[i] == null) return i;
            return -1;
        }

        public int SlotOf(string id)
        {
            for (int i = 0; i < Occupants.Length; i++)
                if (Occupants[i] == id) return i;
            return -1;
        }

        public bool HasFree() => FreeSlot() >= 0;
    }

    private readonly Dictionary<Shelf, ShelfRecord> _records = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ── Public API ────────────────────────────────────────────────

    /// Резервує слот і повертає позицію де стояти NPC.
    /// Повертає false якщо полиця повна.
    public bool TryClaim(Shelf shelf, string npcID, out Vector3 standPos)
    {
        standPos = shelf != null ? shelf.transform.position : Vector3.zero;
        if (shelf == null) return false;

        if (!_records.TryGetValue(shelf, out var rec))
        {
            rec = new ShelfRecord(maxPerShelf);
            _records[shelf] = rec;
        }

        // Вже зарезервував — повертаємо його позицію
        int existing = rec.SlotOf(npcID);
        if (existing >= 0)
        {
            standPos = SlotPosition(shelf, existing);
            return true;
        }

        int slot = rec.FreeSlot();
        if (slot < 0)
        {
            Debug.Log($"[ShelfAccess] '{shelf.name}' full, {npcID} denied");
            return false;
        }

        rec.Occupants[slot] = npcID;
        standPos = SlotPosition(shelf, slot);
        Debug.Log($"[ShelfAccess] '{shelf.name}'[{slot}] → {npcID} @ {standPos}");
        return true;
    }

    public void Release(Shelf shelf, string npcID)
    {
        if (shelf == null || !_records.TryGetValue(shelf, out var rec)) return;
        for (int i = 0; i < rec.Occupants.Length; i++)
        {
            if (rec.Occupants[i] != npcID) continue;
            rec.Occupants[i] = null;
            Debug.Log($"[ShelfAccess] '{shelf.name}'[{i}] released by {npcID}");
            return;
        }
    }

    public void ReleaseAll(string npcID)
    {
        foreach (var rec in _records.Values)
            for (int i = 0; i < rec.Occupants.Length; i++)
                if (rec.Occupants[i] == npcID) rec.Occupants[i] = null;
    }

    public bool HasSlot(Shelf shelf)
    {
        if (shelf == null) return false;
        return !_records.TryGetValue(shelf, out var rec) || rec.HasFree();
    }

    // ── Helpers ───────────────────────────────────────────────────

    /// Slot 0 = вліво, Slot 1 = вправо відносно полиці.
    private Vector3 SlotPosition(Shelf shelf, int slot)
    {
        float side = slot == 0 ? slotSideOffset : -slotSideOffset;
        return shelf.transform.position
             + shelf.transform.forward * standDistance
             + shelf.transform.right   * side;
    }
}