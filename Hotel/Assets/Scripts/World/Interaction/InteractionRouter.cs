// Assets/Scripts/Core/InteractionRouter.cs  [Фаза 1 — v3, RaycastAll]
// ВИПРАВЛЕННЯ:
//   - Замість Physics.Raycast (перший hit) → Physics.RaycastAll + сортування по відстані
//   - Пріоритет: BookWorldItem > Cabinet > NPCBrain
//     (книга завжди важливіша за шафу навіть якщо шафа ближче до камери)
//   - Шафа більше не "з'їдає" клік по книзі що знаходиться всередині неї

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using System.Collections.Generic;

[DefaultExecutionOrder(-5)]
public class InteractionRouter : MonoBehaviour
{
    public static InteractionRouter Instance { get; private set; }

    [Header("Raycast")]
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask interactionLayer;
    [SerializeField] private float     maxDistance = 100f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        if (InputBlocker.IsBlocked) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        // Діагностика
        Debug.Log($"[InteractionRouter] ЛКМ | UIToolkit block: {IsPointerOverUIToolkit()}");

        if (IsPointerOverUIToolkit()) return;

        HandleClick(mouse.position.ReadValue());
    }

    public void SimulateClick(Vector2 screenPos) => HandleClick(screenPos);

    private void HandleClick(Vector2 screenPos)
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        // Debug: малюємо промінь в Scene View (видно через Gizmos)
        Debug.DrawRay(ray.origin, ray.direction * 200f, Color.red, 2f);
        Debug.Log($"[InteractionRouter] Ray origin:{ray.origin:F1} dir:{ray.direction:F2} cam:{mainCamera.name} ortho:{mainCamera.orthographic}");
        // QueryTriggerInteraction.Collide — бачить і звичайні і trigger collider-и
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, interactionLayer,
                                               QueryTriggerInteraction.Collide);

        // Діагностика
        if (hits.Length == 0)
        {
            Debug.Log($"[InteractionRouter] Hits: 0 — нічого в interactionLayer ({interactionLayer.value})");

            // Діагностика: що взагалі є під курсором (без маски)?
            RaycastHit[] allHits = Physics.RaycastAll(ray, maxDistance, ~0,
                                                      QueryTriggerInteraction.Collide);
            if (allHits.Length > 0)
            {
                Debug.Log($"[InteractionRouter] Але БЕЗ маски знайдено {allHits.Length} об'єктів:");
                foreach (var h in allHits)
                    Debug.Log($"  → {h.collider.gameObject.name} layer:{h.collider.gameObject.layer} ({LayerMask.LayerToName(h.collider.gameObject.layer)})");
            }
            else
            {
                Debug.Log("[InteractionRouter] Взагалі нічого під курсором — перевір позицію камери або collider.");
            }

            ContextMenuUI.Instance?.Hide();
            return;
        }

        Debug.Log($"[InteractionRouter] Hits: {hits.Length} об'єктів:");
        foreach (var h in hits)
            Debug.Log($"  → {h.collider.gameObject.name} (layer: {LayerMask.LayerToName(h.collider.gameObject.layer)})");

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // Враховуємо EditMode (підстан Preparation)
        GameState state = EditModeManager.GetEffectiveState();

        // Пріоритет 0 — LootBox (стартові коробки, відкриваються одразу без меню)
        foreach (var hit in hits)
        {
            var lootBox = hit.collider.GetComponentInParent<LootBox>();
            if (lootBox != null && !lootBox.IsOpened)
            {
                Debug.Log($"[InteractionRouter] LootBox hit: {hit.collider.name}");
                lootBox.OpenBox();
                return;
            }
        }

        // Пріоритет 1А — BookWorldItem через collider (якщо книга в interactionLayer)
        foreach (var hit in hits)
        {
            var bookItem = hit.collider.GetComponentInParent<BookWorldItem>();
            if (bookItem != null)
            {
                Debug.Log($"[InteractionRouter] Book hit (collider): {hit.collider.name} | State: {state}");
                ContextMenuUI.Instance?.ShowForBook(bookItem, hit.point, state);
                return;
            }
        }

        // Пріоритет 1Б — книга через математику полиці (Shelf.GetBookIndexAtPoint)
        // Використовується коли Book000 collider не в interactionLayer.
        // Shelf вже знайдений Raycast — використовуємо hit.point для точного визначення книги.
        foreach (var hit in hits)
        {
            var shelf = hit.collider.GetComponentInParent<Shelf>();
            if (shelf == null) continue;

            int bookIdx = shelf.GetBookIndexAtPoint(hit.point);
            if (bookIdx < 0) continue;

            // Знайшли книгу — матеріалізуємо для взаємодії (Ghost-on-Demand)
            ShelfBookEntry bookData = shelf.GetBookData(bookIdx);
            var template = BookDatabase.Instance?.GetBook(bookData.templateID);
            if (template?.containerPrefab == null)
            {
                Debug.Log($"[InteractionRouter] Book math hit idx={bookIdx} але prefab null");
                continue;
            }

            // Отримуємо або матеріалізуємо BookWorldItem
            BookWorldItem worldItem = shelf.GetOrMaterializeBookForInteraction(bookIdx, template.containerPrefab);
            if (worldItem == null) continue;

            Debug.Log($"[InteractionRouter] Book hit (math): shelf={shelf.name} idx={bookIdx} | State: {state}");
            ContextMenuUI.Instance?.ShowForBook(worldItem, hit.point, state);
            return;
        }

        // Пріоритет 2 — NPC (тільки WorkDay)
        foreach (var hit in hits)
        {
            var npc = hit.collider.GetComponentInParent<NPCBrain>();
            if (npc != null && state == GameState.WorkDay)
            {
                Debug.Log($"[InteractionRouter] NPC hit: {hit.collider.name}");
                ContextMenuUI.Instance?.ShowForNPC(npc, hit.point, state);
                return;
            }
        }

        // Пріоритет 3 — Cabinet
        foreach (var hit in hits)
        {
            var cabinet = hit.collider.GetComponentInParent<Cabinet>();
            if (cabinet != null)
            {
                Debug.Log($"[InteractionRouter] Cabinet hit: {hit.collider.name} | State: {state}");
                ContextMenuUI.Instance?.ShowForCabinet(cabinet, hit.point, state);
                return;
            }
        }

        // Нічого не знайдено
        ContextMenuUI.Instance?.Hide();
    }

    // ── UIToolkit перевірка ─────────────────────────────────────

    /// Перевіряє чи курсор знаходиться над UIToolkit елементом що блокує кліки.
    /// На відміну від EventSystem.IsPointerOverGameObject() — працює з UIToolkit.
    private static bool IsPointerOverUIToolkit()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;

        Vector2 screenPos = mouse.position.ReadValue();

        // Перебираємо всі активні UIDocument в сцені
        foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            if (doc == null || doc.rootVisualElement == null) continue;

            // Конвертуємо screen coordinates в UIToolkit panel coordinates
            var panel = doc.rootVisualElement.panel;
            if (panel == null) continue;

            // UIToolkit Y-вісь інвертована відносно Screen
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
                panel,
                new Vector2(screenPos.x, Screen.height - screenPos.y)
            );

            // Перевіряємо чи є під курсором елемент з picking-mode != Ignore
            var picked = doc.rootVisualElement.panel.Pick(panelPos);
            if (picked != null && picked.pickingMode != PickingMode.Ignore)
                return true;
        }

        return false;
    }
}