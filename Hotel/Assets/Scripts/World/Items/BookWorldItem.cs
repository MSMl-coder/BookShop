// Assets/Scripts/World/Shelf/BookWorldItem.cs  [ВИПРАВЛЕНО v2 — Фаза 1]
// ВИПРАВЛЕННЯ:
//   - CustomerBrain → NPCBrain
//
// ЗМІНИ відносно оригіналу:
//   + bool IsReserved { get; private set; }
//   + NPCBrain ReservedBy { get; private set; }
//   + bool Reserve(NPCBrain reserver)
//   + void Unreserve()
//   + reservationMarkerPrefab — іконка над книгою

using UnityEngine;

public class BookWorldItem : MonoBehaviour
{
    // ── Існуючі поля (без змін) ────────────────────────────────
    public BookInstance instance;
    public Shelf        parentShelf;
    public float        savedTilt;

    // ── Резервація — НОВЕ (Фаза 1) ────────────────────────────
    public bool     IsReserved { get; private set; }
    public NPCBrain ReservedBy { get; private set; }

    [Header("Reservation Marker")]
    [SerializeField] private GameObject reservationMarkerPrefab;
    [SerializeField] private Vector3    markerOffset = new Vector3(0f, 0.12f, 0f);

    private GameObject _markerInstance;

    // ── Unity ──────────────────────────────────────────────────
    private void Awake()
    {
        if (reservationMarkerPrefab != null)
        {
            _markerInstance = Instantiate(reservationMarkerPrefab, transform);
            _markerInstance.transform.localPosition = markerOffset;
            _markerInstance.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (IsReserved) Unreserve();
    }

    // ── Public API ─────────────────────────────────────────────

    /// Зарезервувати книгу. Повертає false якщо вже зайнята.
    public bool Reserve(NPCBrain reserver)
    {
        if (IsReserved)
        {
            Debug.LogWarning($"[BookWorldItem] '{instance?.templateID}' вже зарезервована {ReservedBy?.name}!");
            return false;
        }
        IsReserved = true;
        ReservedBy = reserver;
        if (_markerInstance != null) _markerInstance.SetActive(true);
        Debug.Log($"[BookWorldItem] '{instance?.templateID}' → зарезервована для {reserver?.name}.");
        return true;
    }

    /// Зняти резервацію.
    public void Unreserve()
    {
        if (!IsReserved) return;
        Debug.Log($"[BookWorldItem] '{instance?.templateID}' → резервацію знято.");
        IsReserved = false;
        ReservedBy = null;
        if (_markerInstance != null) _markerInstance.SetActive(false);
    }
}