// Assets/Scripts/World/Interaction/InteractionRouter.cs
// v3.2 — ФІКС: кешування UIDocument[]
//
// ФІКС: IsPointerOverUIToolkit() раніше викликала FindObjectsByType<UIDocument>
//   при кожному кліку — O(n) сканування сцени для кожного mouseDown.
//   Тепер UIDocuments кешуються в _cachedUIDocs і оновлюються через
//   RefreshUIDocumentCache() (виклик з GameHUDController або при зміні сцени).
//
// Всі інші зміни vs v3.1 — відсутні.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-5)]
public class InteractionRouter : MonoBehaviour
{
    public static InteractionRouter Instance { get; private set; }

    [Header("Raycast")]
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask interactionLayer;
    [SerializeField] private float     maxDistance = 100f;

    // ✅ ФІКС: кеш UIDocuments — оновлюється при ініціалізації та через API
    private UIDocument[] _cachedUIDocs = System.Array.Empty<UIDocument>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        RefreshUIDocumentCache();
    }

    private void Update()
    {
        if (InputBlocker.IsBlocked) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        if (IsPointerOverUIToolkit()) return;

        HandleClick(mouse.position.ReadValue());
    }

    /// Оновити кеш UIDocuments. Викликати при появі/зникненні UI панелей.
    /// GameHUDController, LootPanelUI тощо можуть викликати це після ініціалізації.
    public void RefreshUIDocumentCache()
    {
        _cachedUIDocs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude);
        Debug.Log($"[InteractionRouter] UIDocument кеш оновлено: {_cachedUIDocs.Length} документів.");
    }

    public void SimulateClick(Vector2 screenPos) => HandleClick(screenPos);

    private void HandleClick(Vector2 screenPos)
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        Debug.DrawRay(ray.origin, ray.direction * 200f, Color.red, 2f);

        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, interactionLayer,
                                               QueryTriggerInteraction.Collide);

        if (hits.Length == 0)
        {
            RaycastHit[] allHits = Physics.RaycastAll(ray, maxDistance, ~0,
                                                      QueryTriggerInteraction.Collide);
            if (allHits.Length > 0)
            {
                Debug.Log($"[InteractionRouter] 0 hits в interactionLayer. БЕЗ маски: {allHits.Length}:");
                foreach (var h in allHits)
                    Debug.Log($"  → {h.collider.gameObject.name} layer:{LayerMask.LayerToName(h.collider.gameObject.layer)}");
            }
            else
            {
                Debug.Log("[InteractionRouter] Взагалі нічого під курсором.");
            }

            ContextMenuUI.Instance?.Hide();
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        GameState state = EditModeManager.GetEffectiveState();

        // ── Пріоритет 0: LootBox ──────────────────────────────────────────
        foreach (var hit in hits)
        {
            var lootBox = hit.collider.GetComponentInParent<LootBox>();
            if (lootBox != null && !lootBox.IsOpened)
            {
                lootBox.OpenBox();
                return;
            }
        }

        // ── Пріоритет 1: NPC — тільки WorkDay ────────────────────────────
        foreach (var hit in hits)
        {
            var npc = hit.collider.GetComponentInParent<NPCBrain>();
            if (npc == null) continue;

            if (state != GameState.WorkDay)
            {
                Debug.Log($"[InteractionRouter] NPC click ignored: state={state} (потрібен WorkDay)");
                break;
            }

            Debug.Log($"[InteractionRouter] NPC clicked: {npc.Data?.npcName} → OnNPCClicked()");
            ContextMenuUI.Instance?.Hide();
            npc.OnNPCClicked();
            return;
        }

        // ── Пріоритет 2А: BookWorldItem через collider (ghost вже є) ──────
        foreach (var hit in hits)
        {
            var bookItem = hit.collider.GetComponentInParent<BookWorldItem>();
            if (bookItem != null)
            {
                Debug.Log($"[InteractionRouter] Book (collider): {hit.collider.name}");
                ContextMenuUI.Instance?.ShowForBook(bookItem, hit.point, state);
                return;
            }
        }

        // ── Пріоритет 2Б: Книга через Shelf math (ghost ще не матеріалізований) ──
        foreach (var hit in hits)
        {
            var shelf = hit.collider.GetComponentInParent<Shelf>();
            if (shelf == null) continue;

            int bookIdx = shelf.GetBookIndexAtPoint(hit.point);
            if (bookIdx < 0) continue;

            Debug.Log($"[InteractionRouter] Book (shelf math): {shelf.name}[{bookIdx}]");

            var ghost = shelf.GetOrMaterializeBookForInteraction(bookIdx);
            if (ghost != null)
            {
                var bookItem = ghost.GetComponent<BookWorldItem>();
                if (bookItem != null)
                {
                    ContextMenuUI.Instance?.ShowForBook(bookItem, hit.point, state);
                    return;
                }
            }

            var cabinet = shelf.GetComponentInParent<Cabinet>();
            if (cabinet != null)
                ContextMenuUI.Instance?.ShowForCabinet(cabinet, hit.point, state);
            return;
        }

        // ── Пріоритет 3: Cabinet ──────────────────────────────────────────
        foreach (var hit in hits)
        {
            var cabinet = hit.collider.GetComponentInParent<Cabinet>();
            if (cabinet != null)
            {
                Debug.Log($"[InteractionRouter] Cabinet: {hit.collider.name}");
                ContextMenuUI.Instance?.ShowForCabinet(cabinet, hit.point, state);
                return;
            }
        }

        ContextMenuUI.Instance?.Hide();
    }

    // ── UIToolkit перевірка ────────────────────────────────────────────────

    private bool IsPointerOverUIToolkit()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;

        Vector2 screenPos = mouse.position.ReadValue();

        // ✅ ФІКС: використовуємо кешований масив замість FindObjectsByType
        foreach (var doc in _cachedUIDocs)
        {
            if (doc == null || doc.rootVisualElement == null) continue;
            var panel = doc.rootVisualElement.panel;
            if (panel == null) continue;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
                panel,
                new Vector2(screenPos.x, Screen.height - screenPos.y)
            );

            var picked = doc.rootVisualElement.panel.Pick(panelPos);
            if (picked != null && picked.pickingMode != PickingMode.Ignore)
                return true;
        }

        return false;
    }
}