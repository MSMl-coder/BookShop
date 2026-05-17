// Assets/Scripts/World/Shelf/ShelfInteractionHandler.cs
//
// v3.1 ЗМІНИ:
//   • При hover: shelf.ShowHoverCube(idx) — простий прозорий cube +5% без collider
//   • При hover off: shelf.HideHoverCube() — куб знищується
//   • Click: HitTestRay → MaterializeBookForInteraction (тимчасовий BookWorldItem)
//          → ContextMenuUI.ShowForBook
//          → ContextMenu при закритті викликає DematerializeBook
//   • Cube НЕ блокує raycast (без collider), тому клік проходить крізь нього
//     і потрапляє в Shelf BoxCollider як зазвичай
//   • Зміна курсора при hover

using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Bookstore/Shelf Interaction Handler")]
public class ShelfInteractionHandler : MonoBehaviour
{
    public static ShelfInteractionHandler Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask shelfLayer;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 25f;

    [Header("Cursor")]
    [Tooltip("Текстура курсора при hover на книгу. Null → системний.")]
    [SerializeField] private Texture2D hoverCursor;
    [SerializeField] private Vector2   hoverCursorHotspot = new Vector2(8, 8);

    // ── Hover state ──────────────────────────────────────────────────────────
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

    // ── Hover ────────────────────────────────────────────────────────────────
    private void UpdateHover()
    {
        if (mainCamera == null || Mouse.current == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        Shelf shelf = null;
        int   idx   = -1;

        if (Physics.Raycast(ray, out RaycastHit rh, interactDistance, shelfLayer))
        {
            shelf = rh.collider.GetComponentInParent<Shelf>();
            if (shelf != null)
                idx = shelf.HitTestRay(ray);
        }

        // Стан не змінився
        if (shelf == _hoveredShelf && idx == _hoveredBookIndex) return;

        // ── Знімаємо hover-cube зі старої полиці ──
        if (_hoveredShelf != null && _hoveredShelf != shelf)
            _hoveredShelf.HideHoverCube();

        _hoveredShelf     = shelf;
        _hoveredBookIndex = idx;

        // ── Виставляємо новий hover ──
        if (shelf == null || idx < 0)
        {
            if (_hoveredShelf != null) _hoveredShelf.HideHoverCube();
            BookInfoCardController.Instance?.Hide();
            SetCursorHover(false);
            return;
        }

        shelf.ShowHoverCube(idx);

        // Tooltip
        var data     = shelf.GetBookData(idx);
        var template = BookDatabase.Instance?.GetBook(data.templateID);
        if (template != null)
            BookInfoCardController.Instance?.Show(template);

        SetCursorHover(true);
    }

    // ── Cursor ───────────────────────────────────────────────────────────────
    private void SetCursorHover(bool on)
    {
        if (on == _cursorChanged) return;
        _cursorChanged = on;

        if (on) Cursor.SetCursor(hoverCursor, hoverCursorHotspot, CursorMode.Auto);
        else    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void ResetHover()
    {
        if (_hoveredShelf != null) _hoveredShelf.HideHoverCube();
        _hoveredShelf     = null;
        _hoveredBookIndex = -1;
        BookInfoCardController.Instance?.Hide();
        SetCursorHover(false);
    }

    // ── Public: клік (з InteractionRouter) ───────────────────────────────────
    public void HandleShelfClick(Shelf shelf, Vector3 hitPoint)
    {
        if (shelf == null || mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        int idx = shelf.HitTestRay(ray);
        if (idx < 0) idx = shelf.GetBookIndexAtPoint(hitPoint);
        if (idx < 0) return;

        // Створюємо тимчасовий BookWorldItem-ghost для ContextMenu
        BookWorldItem worldItem = shelf.GetOrMaterializeBookForInteraction(idx);
        if (worldItem == null) return;

        GameState state = EditModeManager.GetEffectiveState();
        ContextMenuUI.Instance?.ShowForBook(worldItem, hitPoint, state);
    }
}