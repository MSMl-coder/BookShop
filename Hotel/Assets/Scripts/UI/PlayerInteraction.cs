// Assets/Scripts/UI/PlayerInteraction.cs
// ОНОВЛЕНО: розкоментовано OpenUpgradeMenu → тепер викликає UpgradeSlotUI.Instance.Open()
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask interactionLayer;
    [SerializeField] private float interactionDistance = 20f;

    private ShopUIManager _uiManager;

    void Start()
    {
        _uiManager = Object.FindAnyObjectByType<ShopUIManager>();
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Debug.Log("[Interaction] Клік ігнорується: миша над UI.");
                return;
            }

            HandleRaycast(mouse.position.ReadValue());
        }
    }

    private void HandleRaycast(Vector2 mousePosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayer))
            return;

        GameState currentState = GameLoopManager.Instance.CurrentState;
        Debug.Log($"[Interaction] Клік по: {hit.collider.name}. Стан: {currentState}");

        // ── 1. Слот меблів → апгрейд (тільки у Preparation) ──
        FurnitureSlot slot = hit.collider.GetComponentInParent<FurnitureSlot>();
        if (slot != null && currentState == GameState.Preparation)
        {
            Debug.Log($"[Interaction] Відкриваємо апгрейд для: {slot.name}");
            UpgradeSlotUI.Instance?.Open(slot);  // ← РОЗКОМЕНТОВАНО
            return;
        }

        // ── 2. Шафа → інвентар ──
        Cabinet cabinet = hit.collider.GetComponentInParent<Cabinet>();
        if (cabinet != null)
        {
            if (currentState == GameState.Preparation ||
                currentState == GameState.WorkDay     ||
                currentState == GameState.LootPhase)
            {
                _uiManager?.OpenCabinetUI(cabinet);
            }
            else
            {
                Debug.Log($"[Interaction] Стан {currentState} не дозволяє відкрити шафу.");
            }
        }
    }
}
