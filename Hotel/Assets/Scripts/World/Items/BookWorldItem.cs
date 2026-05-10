// Assets/Scripts/World/Shelf/BookWorldItem.cs
// ФАЗА 1 — додано систему резервації книг (BookReservation)
//
// ЗМІНИ відносно попередньої версії:
//   + bool IsReserved { get; private set; }
//   + void Reserve(CustomerBrain reserver)
//   + void Unreserve()
//   + GameObject reservationMarkerPrefab (іконка 🔒 над книгою)
//   + ReservationMarker показується/приховується автоматично
//
// UNITY SETUP:
//   1. Знайди prefab BookWorldItem (або клас що додається через AddComponent).
//   2. Призначи reservationMarkerPrefab (маленький GameObject з іконкою/спрайтом).
//   3. NPC викликає Reserve(this) коли знаходить книгу.
//   4. NPC викликає Unreserve() при виході або покупці.

using UnityEngine;

public class BookWorldItem : MonoBehaviour
{
    // ── Дані книги (існуючі) ───────────────────────────────────
    public BookInstance instance;
    public Shelf        parentShelf;
    public float        savedTilt;

    // ── Резервація (НОВЕ — Фаза 1) ────────────────────────────
    /// Чи зарезервована ця книга NPC-покупцем
    public bool IsReserved { get; private set; }

    /// Хто зарезервував (null якщо вільна)
    public CustomerBrain ReservedBy { get; private set; }

    [Header("Резервація — Фаза 1")]
    [Tooltip("Prefab маркера що показується над книгою (іконка замка або рамка)")]
    [SerializeField] private GameObject reservationMarkerPrefab;

    [Tooltip("Зміщення маркера відносно книги")]
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.12f, 0f);

    private GameObject _markerInstance;

    // ── Unity ──────────────────────────────────────────────────
    private void Awake()
    {
        // Маркер прихований за замовчуванням
        if (reservationMarkerPrefab != null)
        {
            _markerInstance = Instantiate(reservationMarkerPrefab, transform);
            _markerInstance.transform.localPosition = markerOffset;
            _markerInstance.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // Якщо книгу знищено — звільняємо резервацію
        if (IsReserved) Unreserve();
    }

    // ── Public API ─────────────────────────────────────────────

    /// Зарезервувати книгу для покупця.
    /// Повертає false якщо вже зарезервована іншим.
    public bool Reserve(CustomerBrain reserver)
    {
        if (IsReserved)
        {
            Debug.LogWarning($"[BookWorldItem] '{instance?.templateID}' вже зарезервована {ReservedBy?.name}!");
            return false;
        }

        IsReserved = true;
        ReservedBy = reserver;
        ShowMarker(true);

        Debug.Log($"[BookWorldItem] '{instance?.templateID}' зарезервована для {reserver?.name}.");
        return true;
    }

    /// Зняти резервацію (покупець пішов або купив).
    public void Unreserve()
    {
        if (!IsReserved) return;

        Debug.Log($"[BookWorldItem] '{instance?.templateID}' резервацію знято ({ReservedBy?.name}).");

        IsReserved = false;
        ReservedBy = null;
        ShowMarker(false);
    }

    // ── Private ────────────────────────────────────────────────

    private void ShowMarker(bool show)
    {
        if (_markerInstance != null)
            _markerInstance.SetActive(show);
    }
}