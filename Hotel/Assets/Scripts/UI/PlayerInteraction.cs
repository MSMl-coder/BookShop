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

        // ── Коробки — завжди в Preparation ─────────────────────────
        InteractableBox box = hit.collider.GetComponentInParent<InteractableBox>();
        if (box != null && currentState == GameState.Preparation)
        {
            box.TryOpen();
            return;
        }

        // ── EditMode — тільки переміщення обʼєктів ─────────────────
        if (EditModeManager.Instance != null && EditModeManager.Instance.IsEditMode)
        {
           PlacedObject placed = hit.collider.GetComponentInParent<PlacedObject>();
             if (placed == null)
            placed = hit.collider.transform.root.GetComponentInChildren<PlacedObject>();
            return; // все інше блокуємо
        }

        // ── Звичайний режим ─────────────────────────────────────────
        FurnitureSlot slot = hit.collider.GetComponentInParent<FurnitureSlot>();
        if (slot != null && currentState == GameState.Preparation)
        {
            UpgradeSlotUI.Instance?.Open(slot);
            return;
        }

        Cabinet cabinet = hit.collider.GetComponentInParent<Cabinet>();
        if (cabinet != null &&
            (currentState == GameState.Preparation ||
            currentState == GameState.WorkDay     ||
            currentState == GameState.LootPhase))
        {
            _uiManager?.OpenCabinetUI(cabinet);
        }
    }


}
