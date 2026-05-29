// Assets/Scripts/World/Shop/SeatRegistry.cs
// Singleton — управління резервуванням місць для NPC.
// Патерн аналогічний до BookReservation на полицях.
//
// Дизайн:
//   - Кожен розміщений Seating-проп має capacity місць
//   - NPC резервує одне місце через TryClaim()
//   - При виході або вставанні — Release()
//   - Всі операції O(1) або O(місця) — прийнятно

using System.Collections.Generic;
using UnityEngine;

public class SeatRegistry : MonoBehaviour
{
    public static SeatRegistry Instance { get; private set; }

    // ─────────────────────────────────────────────
    // Internal seat record
    // ─────────────────────────────────────────────

    private class SeatRecord
    {
        public GameObject    SeatGO;
        public PropTemplate  Prop;

        // instanceID NPC → індекс слоту (-1 = вільно)
        public string[] Occupants;

        public SeatRecord(GameObject go, PropTemplate prop)
        {
            SeatGO    = go;
            Prop      = prop;
            Occupants = new string[prop.seatCapacity];
            for (int i = 0; i < Occupants.Length; i++)
                Occupants[i] = null;
        }

        public int FreeSlot()
        {
            for (int i = 0; i < Occupants.Length; i++)
                if (Occupants[i] == null) return i;
            return -1;
        }

        public bool HasFreeSlot() => FreeSlot() >= 0;

        public Vector3 SlotPosition(int slot)
        {
            // Кілька місць розташовуємо вздовж правого вектора пропа
            float offset = (slot - (Occupants.Length - 1) * 0.5f) * 0.6f;
            return SeatGO.transform.position + SeatGO.transform.right * offset;
        }
    }

    // ─────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────

    // npcInstanceID → (SeatRecord, slotIndex)
    private readonly Dictionary<string, (SeatRecord record, int slot)> _npcToSeat = new();

    // всі зареєстровані місця
    private readonly List<SeatRecord> _seats = new();

    // ─────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        if (PlacementRegistry.Instance != null)
            PlacementRegistry.Instance.OnRegistryChanged += RebuildSeats;
    }

    private void OnDisable()
    {
        if (PlacementRegistry.Instance != null)
            PlacementRegistry.Instance.OnRegistryChanged -= RebuildSeats;
    }

    private void Start() => RebuildSeats();

    // ─────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────

    /// <summary>
    /// NPC намагається зайняти місце. Повертає позицію для NavMesh або null якщо немає місць.
    /// </summary>
    /// <param name="npcInstanceID">Унікальний ID NPC (NPCBrain.InstanceID)</param>
    /// <param name="claimedProp">Проп на якому зарезервовано місце</param>
    /// <param name="seatPosition">Позиція куди рухатись NPC</param>
    public bool TryClaim(
        string npcInstanceID,
        out PropTemplate claimedProp,
        out Vector3 seatPosition)
    {
        claimedProp  = null;
        seatPosition = Vector3.zero;

        // Вже сидить?
        if (_npcToSeat.ContainsKey(npcInstanceID))
        {
            var existing = _npcToSeat[npcInstanceID];
            claimedProp  = existing.record.Prop;
            seatPosition = existing.record.SlotPosition(existing.slot);
            return true;
        }

        // Шукаємо найближче вільне місце (спрощено: перше знайдене)
        foreach (var seat in _seats)
        {
            if (!seat.HasFreeSlot()) continue;

            int slot = seat.FreeSlot();
            seat.Occupants[slot] = npcInstanceID;
            _npcToSeat[npcInstanceID] = (seat, slot);

            claimedProp  = seat.Prop;
            seatPosition = seat.SlotPosition(slot);

            Debug.Log($"[SeatRegistry] {npcInstanceID} → {seat.Prop.propName}[{slot}] @ {seatPosition}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// NPC звільняє своє місце (встав або вийшов з магазину).
    /// </summary>
    public void Release(string npcInstanceID)
    {
        if (!_npcToSeat.TryGetValue(npcInstanceID, out var entry)) return;

        entry.record.Occupants[entry.slot] = null;
        _npcToSeat.Remove(npcInstanceID);

        Debug.Log($"[SeatRegistry] Released: {npcInstanceID}");
    }

    /// Кількість вільних місць зараз (для UI).
    public int FreeSeatsTotal()
    {
        int count = 0;
        foreach (var s in _seats)
            for (int i = 0; i < s.Occupants.Length; i++)
                if (s.Occupants[i] == null) count++;
        return count;
    }

    // ─────────────────────────────────────────────
    // Rebuild
    // ─────────────────────────────────────────────

    private void RebuildSeats()
    {
        // Звільняємо всі записи (NPC що зараз сидять — залишаться висячими,
        // тому NPCBrain має перевіряти валідність при Resting Update)
        _seats.Clear();

        if (PlacementRegistry.Instance == null) return;

        foreach (var (go, inst) in PlacementRegistry.Instance.GetAll())
        {
            if (go == null) continue;

            var prop = InventoryManager.Instance?.GetPropTemplate(inst.propID);
            if (prop == null || !prop.IsSeating) continue;

            _seats.Add(new SeatRecord(go, prop));
        }

        Debug.Log($"[SeatRegistry] Rebuilt: {_seats.Count} seating prop(s)");
    }
}