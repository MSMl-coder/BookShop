// Assets/Scripts/Core/InteractionRouter.cs  [ВИПРАВЛЕНО v2 — Фаза 1]
// ВИПРАВЛЕННЯ:
//   - CustomerBrain → NPCBrain
//   - EditMode прибрано (не існує в GameState)
//   - Додано InputBlocker.IsBlocked перевірку
//
// UNITY SETUP:
//   1. Видали PlayerInteraction та CabinetClickHandler з усіх GO
//   2. Add Component → InteractionRouter на Manager GO
//   3. Assign mainCamera + interactionLayer

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-5)]
public class InteractionRouter : MonoBehaviour
{
    public static InteractionRouter Instance { get; private set; }

    [Header("Raycast")]
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask interactionLayer;
    [SerializeField] private float     maxDistance = 30f;

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
        // Блокування під час туторіалу
        if (InputBlocker.IsBlocked) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        HandleClick(mouse.position.ReadValue());
    }

    public void SimulateClick(Vector2 screenPos) => HandleClick(screenPos);

    private void HandleClick(Vector2 screenPos)
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactionLayer))
        {
            ContextMenuUI.Instance?.Hide();
            return;
        }

        GameObject target = hit.collider.gameObject;
        GameState  state  = GameLoopManager.Instance?.CurrentState ?? GameState.Preparation;

        Debug.Log($"[InteractionRouter] Hit: {target.name} | State: {state}");

        // 1. Книга на полиці
        var bookItem = target.GetComponentInParent<BookWorldItem>();
        if (bookItem != null)
        {
            ContextMenuUI.Instance?.ShowForBook(bookItem, hit.point, state);
            return;
        }

        // 2. Шафа
        var cabinet = target.GetComponentInParent<Cabinet>();
        if (cabinet != null)
        {
            ContextMenuUI.Instance?.ShowForCabinet(cabinet, hit.point, state);
            return;
        }

        // 3. NPC (тільки WorkDay)
        var npc = target.GetComponentInParent<NPCBrain>();
        if (npc != null && state == GameState.WorkDay)
        {
            ContextMenuUI.Instance?.ShowForNPC(npc, hit.point, state);
            return;
        }

        // Нічого → закрити меню
        ContextMenuUI.Instance?.Hide();
    }
}