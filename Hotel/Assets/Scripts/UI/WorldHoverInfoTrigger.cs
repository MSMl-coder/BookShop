// Assets/Scripts/UI/WorldHoverInfoTrigger.cs
// Показує BookInfoCard при наведенні миші на BookWorldItem у 3D сцені.
// v4.2: ghost матеріалізується автоматично через ShelfInteractionHandler при hover.
// Цей скрипт ловить будь-який hover на colider-i в bookLayer і показує картку.
//
// UNITY SETUP:
//   Додай на той самий GO що InteractionRouter.
//   bookLayer — той самий layer що в Shelf.bookLayer (де спавняться ghost-и).

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class WorldHoverInfoTrigger : MonoBehaviour
{
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask bookLayer;     // layer ghost-книг (з Shelf.bookLayer)
    [SerializeField] private LayerMask cabinetLayer;  // layer шаф (Cabinet collider)
    [SerializeField] private float     maxDistance = 100f;

    private BookWorldItem _lastHovered;
    private Cabinet       _lastCabinet;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        // Не показуємо в EditMode або фаза не та
        var state = EditModeManager.GetEffectiveState();
        if (state == GameState.LootPhase || state == GameState.DayStats)
        {
            ClearAll();
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        // Не реагуємо якщо курсор над UI
        if (IsPointerOverUI()) { ClearAll(); return; }

        Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

        // ── 1. Перевіряємо ghost-книги ──────────────────────────
        bool foundBook = false;
        if (Physics.Raycast(ray, out RaycastHit bookHit, maxDistance, bookLayer,
                            QueryTriggerInteraction.Collide))
        {
            var wi = bookHit.collider.GetComponentInParent<BookWorldItem>();
            if (wi != null && wi != _lastHovered)
            {
                ClearAll();
                _lastHovered = wi;
                var tpl = wi.instance != null
                    ? BookDatabase.Instance?.GetBook(wi.instance.templateID)
                    : null;
                BookInfoCardController.Instance?.Show(tpl);
            }
            if (wi != null) foundBook = true;
        }

        if (foundBook) return;

        // ── 2. Hover на Shelf — матеріалізуємо ghost ─────────────
        // Shelf-и в layer "Shelves" — перевіряємо окремо
        LayerMask shelvesLayer = LayerMask.GetMask("Shelves");
        bool foundShelf = false;
        if (Physics.Raycast(ray, out RaycastHit shelfHit, maxDistance, shelvesLayer,
                            QueryTriggerInteraction.Collide))
        {
            var shelf = shelfHit.collider.GetComponentInParent<Shelf>();
            if (shelf != null)
            {
                int idx = shelf.GetBookIndexAtPoint(shelfHit.point);
                if (idx >= 0)
                {
                    var entry = shelf.GetBookData(idx);
                    var tpl   = BookDatabase.Instance?.GetBook(entry.templateID);
                    if (tpl != null && _lastHovered == null)
                    {
                        // Матеріалізуємо ghost (потрібен для context menu)
                        var ghost = shelf.GetOrMaterializeBookForInteraction(idx);
                        if (ghost != null && ghost != _lastHovered)
                        {
                            ClearAll();
                            _lastHovered = ghost;
                        }
                        BookInfoCardController.Instance?.Show(tpl);
                        foundShelf = true;
                    }
                }
            }
        }

        if (foundShelf) return;

        // ── 3. Hover на Cabinet ──────────────────────────────────
        bool foundCab = false;
        if (cabinetLayer.value != 0 &&
            Physics.Raycast(ray, out RaycastHit cabHit, maxDistance, cabinetLayer,
                            QueryTriggerInteraction.Collide))
        {
            var cab = cabHit.collider.GetComponentInParent<Cabinet>();
            if (cab != null && cab != _lastCabinet)
            {
                BookInfoCardController.Instance?.Hide();
                _lastCabinet = cab;
            }
            if (cab != null) foundCab = true;
        }

        if (foundCab) return;

        // ── Нічого — ховаємо ────────────────────────────────────
        ClearAll();
    }

    private void ClearAll()
    {
        bool hadAny = _lastHovered != null || _lastCabinet != null;
        _lastHovered = null;
        _lastCabinet = null;
        if (hadAny) BookInfoCardController.Instance?.Hide();
    }

    private static bool IsPointerOverUI()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;
        Vector2 pos = mouse.position.ReadValue();
        foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            if (doc?.rootVisualElement?.panel == null) continue;
            var panelPos = RuntimePanelUtils.ScreenToPanel(
                doc.rootVisualElement.panel,
                new Vector2(pos.x, Screen.height - pos.y));
            var picked = doc.rootVisualElement.panel.Pick(panelPos);
            if (picked != null && picked.pickingMode != UnityEngine.UIElements.PickingMode.Ignore)
                return true;
        }
        return false;
    }
}