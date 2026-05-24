// Assets/Scripts/World/Shelf/ShelfInteractionHandler.cs
// ═══════════════════════════════════════════════════════════════════
// v4.1 — Розв'язка від ContextMenuUI
//
// ЗМІНИ vs v4.0:
//   • HandleShelfClick() більше НЕ викликає ContextMenuUI.ShowForBook напряму.
//     Тепер тільки матеріалізує ghost. ContextMenuUI викликається з InteractionRouter.
//   • Це дозволяє InteractionRouter зібрати book+shelf в одному ShowFromHits().
//
// HOVER (без змін):
//   • RaycastAll → шукаємо Shelf → GetBookIndexAtPoint → ShowHoverCube
//   • Tooltip + cursor при знаходженні книги
// ═══════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Bookstore/Shelf Interaction Handler")]
public class ShelfInteractionHandler : MonoBehaviour
{
    public static ShelfInteractionHandler Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask interactionLayer;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 25f;

    [Header("Cursor")]
    [SerializeField] private Texture2D hoverCursor;
    [SerializeField] private Vector2   hoverCursorHotspot = new Vector2(8, 8);

    // ── State ─────────────────────────────────────────────────────
    private Shelf _hoveredShelf;
    private int   _hoveredBookIndex = -1;
    private bool  _cursorChanged    = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void OnDisable() => ResetHover();

    private void Update() => UpdateHover();

    // ── Hover ──────────────────────────────────────────────────────
    private void UpdateHover()
    {
        if (mainCamera == null || Mouse.current == null) return;
        if (InputBlocker.IsBlocked) { ResetHover(); return; }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        RaycastHit[] hits = Physics.RaycastAll(ray, interactDistance, interactionLayer,
                                               QueryTriggerInteraction.Collide);

        Shelf shelf    = null;
        int   bookIdx  = -1;

        foreach (var hit in hits)
        {
            Shelf s = hit.collider.GetComponentInParent<Shelf>();
            if (s == null) s = hit.collider.GetComponentInChildren<Shelf>();
            if (s == null)
            {
                var parent = hit.collider.transform.parent;
                if (parent != null) s = parent.GetComponentInChildren<Shelf>();
            }
            if (s == null) continue;

            int idx = s.GetBookIndexAtPoint(hit.point);
            if (idx >= 0)
            {
                shelf   = s;
                bookIdx = idx;
                break;
            }
        }

        // Стан не змінився
        if (shelf == _hoveredShelf && bookIdx == _hoveredBookIndex) return;

        // Знімаємо старий hover
        if (_hoveredShelf != null && _hoveredShelf != shelf)
            _hoveredShelf.HideHoverCube();

        _hoveredShelf     = shelf;
        _hoveredBookIndex = bookIdx;

        if (shelf == null || bookIdx < 0)
        {
            BookInfoCardController.Instance?.Hide();
            ResetCursor();
            return;
        }

        // Показуємо hover cube
        shelf.ShowHoverCube(bookIdx);

        // Показуємо tooltip
        var entry = shelf.GetBookData(bookIdx);
        var tpl   = BookDatabase.Instance?.GetBook(entry.templateID);
        BookInfoCardController.Instance?.Show(tpl);

        // Змінюємо курсор
        if (hoverCursor != null && !_cursorChanged)
        {
            Cursor.SetCursor(hoverCursor, hoverCursorHotspot, CursorMode.Auto);
            _cursorChanged = true;
        }
    }

    private void ResetHover()
    {
        if (_hoveredShelf != null)
        {
            _hoveredShelf.HideHoverCube();
            _hoveredShelf     = null;
            _hoveredBookIndex = -1;
        }
        BookInfoCardController.Instance?.Hide();
        ResetCursor();
    }

    private void ResetCursor()
    {
        if (_cursorChanged)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _cursorChanged = false;
        }
    }

    // ── Click (виклик з InteractionRouter) ───────────────────────
    /// <summary>
    /// Матеріалізує ghost BookWorldItem для вказаної книги на полиці.
    /// ContextMenuUI відкривається окремо через InteractionRouter.ShowFromHits().
    /// </summary>
    public void HandleShelfClick(Shelf shelf, Vector3 hitPoint)
    {
        if (shelf == null) return;

        int bookIdx = shelf.GetBookIndexAtPoint(hitPoint);
        if (bookIdx < 0)
        {
            Debug.Log($"[ShelfInteractionHandler] HandleShelfClick: книга не знайдена у {shelf.name}");
            return;
        }

        Debug.Log($"[ShelfInteractionHandler] Матеріалізую ghost для {shelf.name}[{bookIdx}]");

        // Матеріалізуємо ghost (або отримуємо існуючий)
        // InteractionRouter отримає BookWorldItem з ghost і покаже меню
        shelf.GetOrMaterializeBookForInteraction(bookIdx);
    }
}