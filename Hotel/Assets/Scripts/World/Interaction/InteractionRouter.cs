// Assets/Scripts/Core/InteractionRouter.cs
// ФАЗА 1 — замінює PlayerInteraction + CabinetClickHandler
// Єдина точка обробки ЛКМ на 3D-об'єктах.
// Визначає тип об'єкта + поточний GameState → показує ContextMenuUI.
//
// UNITY SETUP:
// 1. Видали PlayerInteraction та CabinetClickHandler з усіх GameObject-ів.
// 2. Додай InteractionRouter на той самий GO що й GameLoopManager (або окремий Manager GO).
// 3. Призначи mainCamera (або залиш порожнім — знайде Camera.main).
// 4. Виставте interactionLayer — шар/шари що мають реагувати на кліки.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-5)]
public class InteractionRouter : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────
    public static InteractionRouter Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────
    [Header("Raycast")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask interactionLayer;
    [SerializeField] private float maxDistance = 30f;

    // ── Private ────────────────────────────────────────────────
    private Mouse _mouse;

    // ── Unity ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _mouse = Mouse.current;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null)
            Debug.LogError("[InteractionRouter] Camera not found! Assign mainCamera in Inspector.");
    }

    private void Update()
    {
        _mouse = Mouse.current;
        if (_mouse == null) return;
        if (!_mouse.leftButton.wasPressedThisFrame) return;

        // Ігноруємо кліки по UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("[InteractionRouter] Клік по UI — ігноруємо.");
            return;
        }

        HandleClick(_mouse.position.ReadValue());
    }

    // ── Public API ─────────────────────────────────────────────

    /// Програмне «симулювання» кліку (для тестів / туторіалу)
    public void SimulateClick(Vector2 screenPos) => HandleClick(screenPos);

    // ── Private ────────────────────────────────────────────────

    private void HandleClick(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactionLayer))
        {
            // Клік у порожнечу — закриваємо відкрите меню
            ContextMenuUI.Instance?.Hide();
            return;
        }

        GameObject target  = hit.collider.gameObject;
        GameState   state  = GameLoopManager.Instance?.CurrentState ?? GameState.Preparation;

        Debug.Log($"[InteractionRouter] Hit: {target.name} | State: {state}");

        // ── Визначаємо тип об'єкта ──────────────────────────────

        // 1. BookWorldItem (книга на полиці)
        var bookItem = target.GetComponentInParent<BookWorldItem>();
        if (bookItem != null)
        {
            ContextMenuUI.Instance?.ShowForBook(bookItem, hit.point, state);
            return;
        }

        // 2. Cabinet (шафа)
        var cabinet = target.GetComponentInParent<Cabinet>();
        if (cabinet != null)
        {
            ContextMenuUI.Instance?.ShowForCabinet(cabinet, hit.point, state);
            return;
        }

        // 3. NPC (покупець) — тільки у WorkDay
        var npc = target.GetComponentInParent<CustomerBrain>();
        if (npc != null && state == GameState.WorkDay)
        {
            ContextMenuUI.Instance?.ShowForNPC(npc, hit.point, state);
            return;
        }

        // 4. Нічого не знайдено — закриваємо меню
        ContextMenuUI.Instance?.Hide();
    }
}