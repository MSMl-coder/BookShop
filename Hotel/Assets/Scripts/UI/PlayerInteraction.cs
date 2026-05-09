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
            // ← ця перевірка блокує клік коли миша над UI Toolkit
            // але після ConfirmPlacement ghost зник і наступний клік
            // проходить крізь UI прямо в 3D
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

        if (PlacementController.Instance != null && PlacementController.Instance.JustConfirmedThisFrame)
        return;

        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayer))
            return;

        GameState currentState = GameLoopManager.Instance.CurrentState;

        // ── Коробки — завжди в Preparation незалежно від EditMode ──
        InteractableBox box = hit.collider.GetComponentInParent<InteractableBox>();
        if (box != null && currentState == GameState.Preparation)
        {
            box.TryOpen();
            return;
        }

        // ── EditMode — блокуємо ВСЕ крім PlacedObject ──────────────
        if (EditModeManager.Instance != null && EditModeManager.Instance.IsEditMode)
        {
            // Якщо зараз іде розміщення — клік обробляє PlacementController
            // через свій Update, сюди не лізем взагалі
            if (PlacementController.Instance != null && PlacementController.Instance.IsPlacing)
                return;

            // Клік по розміщеному обʼєкту — підняти
            PlacedObject placed = hit.collider.GetComponentInParent<PlacedObject>();
            if (placed == null)
                placed = hit.collider.transform.root.GetComponentInChildren<PlacedObject>();

            if (placed != null)
                PlacementController.Instance?.PickUpExisting(placed);

            return; // в EditMode більше нічого не робимо
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
