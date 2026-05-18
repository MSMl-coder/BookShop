// Assets/Scripts/World/Shelf/ShelfInteractionHandler.cs
//
// v4.0 — hover використовує той самий підхід що InteractionRouter для кліків:
//   Physics.RaycastAll → шукаємо BookWorldItem → підсвічуємо
//
// СУТЬ:
//   Щоб hover-ghost знаходився через RaycastAll, він має бути вже заспавнений.
//   Тому порядок такий:
//     1. RaycastAll → шукаємо ІСНУЮЧИЙ BookWorldItem (попередній hover ghost)
//        АБО Cabinet (якщо книги немає)
//     2. Паралельно: шукаємо Shelf серед hits → якщо знайшли → матеріалізуємо новий hover ghost
//     3. Наступного кадру цей ghost вже буде в hits і HoverHighlighter його підсвітить
//
// Спрощений підхід:
//   - Hover: RaycastAll → шукаємо Shelf collider → питаємо InteractionRouter-подібний пошук
//     але через GetBookIndexAtPoint (простий, без OBB math) → ShowHoverCube
//   - Tooltip + cursor при знаходженні книги

using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Bookstore/Shelf Interaction Handler")]
public class ShelfInteractionHandler : MonoBehaviour
{
    public static ShelfInteractionHandler Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask interactionLayer; // той самий що в InteractionRouter і HoverHighlighter

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 25f;

    [Header("Cursor")]
    [SerializeField] private Texture2D hoverCursor;
    [SerializeField] private Vector2   hoverCursorHotspot = new Vector2(8, 8);

    // ── State ────────────────────────────────────────────────────────────────
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
        if (InputBlocker.IsBlocked) { ResetHover(); return; }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        // Той самий RaycastAll що в InteractionRouter — бачить всі collider-и
        RaycastHit[] hits = Physics.RaycastAll(ray, interactDistance, interactionLayer,
                                               QueryTriggerInteraction.Collide);

        Shelf shelf    = null;
        int   bookIdx  = -1;

        // Шукаємо Shelf серед hits (той самий шар що Cabinet/Shelves)
        foreach (var hit in hits)
        {
            // Шукаємо Shelf в parent chain (може бути BookShelfColider → BookShelf004L → нема Shelf,
            // тому шукаємо в children теж)
            Shelf s = hit.collider.GetComponentInParent<Shelf>();
            if (s == null)
                s = hit.collider.GetComponentInChildren<Shelf>();
            if (s == null)
            {
                // Ще варіант: шукаємо в siblings через спільного parent
                var parent = hit.collider.transform.parent;
                if (parent != null)
                    s = parent.GetComponentInChildren<Shelf>();
            }

            if (s == null) continue;

            // Знайшли Shelf — визначаємо яка книга під hitPoint
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
            SetCursorHover(false);
            return;
        }

        // Показуємо hover ghost (він з BookWorldItem → HoverHighlighter підсвітить)
        shelf.ShowHoverCube(bookIdx);

        // Tooltip
        var data     = shelf.GetBookData(bookIdx);
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

    // ── Public: клік ─────────────────────────────────────────────────────────
    public void HandleShelfClick(Shelf shelf, Vector3 hitPoint)
    {
        if (shelf == null || mainCamera == null) return;

        // GetBookIndexAtPoint — простий X-проекційний метод (той самий що знаходить hover)
        int idx = shelf.GetBookIndexAtPoint(hitPoint);
        if (idx < 0) return;

        BookWorldItem worldItem = shelf.GetOrMaterializeBookForInteraction(idx);
        if (worldItem == null) return;

        GameState state = EditModeManager.GetEffectiveState();
        ContextMenuUI.Instance?.ShowForBook(worldItem, hitPoint, state);
    }
}