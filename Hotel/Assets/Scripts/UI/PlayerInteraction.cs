using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask interactionLayer; // Виберіть тут Furniture
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
            // Перевірка на клік по UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Debug.Log("[Interaction] Клік ігнорується: миша над інтерфейсом.");
                return;
            }

            HandleRaycast(mouse.position.ReadValue());
        }
    }

    private void HandleRaycast(Vector2 mousePosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, interactionLayer))
        {
            GameState currentState = GameLoopManager.Instance.CurrentState;
            Debug.Log($"[Interaction] Клік по: {hit.collider.name}. Стан гри: {currentState}");

            // 1. Спроба знайти Слот (для апгрейдів)
            FurnitureSlot slot = hit.collider.GetComponentInParent<FurnitureSlot>();
            if (slot != null && currentState == GameState.Preparation)
            {
                Debug.Log($"[Interaction] Відкриваємо апгрейд для: {slot.name}");
                // _uiManager.OpenUpgradeMenu(slot); 
                return; // Виходимо, щоб не спрацював клік по шафі одночасно
            }

            // 2. Спроба знайти Шафу (для книг)
            Cabinet cabinet = hit.collider.GetComponentInParent<Cabinet>();
            if (cabinet != null)
            {
                // Дозволяємо відкривати шафу у робочий день АБО під час вибору луту
                if (currentState == GameState.Preparation ||  currentState == GameState.WorkDay || currentState == GameState.LootPhase)
                {
                    _uiManager.OpenCabinetUI(cabinet);
                }
                else
                {
                    Debug.Log($"[Interaction] Шафа знайдена, але стан {currentState} не дозволяє її відкрити.");
                }
            }
        }
    }
}