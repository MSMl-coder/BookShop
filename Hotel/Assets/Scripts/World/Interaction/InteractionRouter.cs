// Assets/Scripts/World/Interaction/InteractionRouter.cs
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// Єдина точка обробки кліків.
///
/// UNITY SETUP:
/// 1. Видали PlayerInteraction і CabinetClickHandler
/// 2. GameObject [InteractionRouter] → InteractionRouter
/// 3. Furniture Layer  = тільки шар "Furniture"  (root шафи)
/// 4. Box Layer        = тільки шар коробок
/// Два окремих raycast — чітко розділені, ніякого пересічення.
public class InteractionRouter : MonoBehaviour
{
    public static InteractionRouter Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera mainCamera;

    [Header("Layers — кожен окремо, не змішувати")]
    [Tooltip("Шар root GO шафи (Furniture). Тільки він.")]
    [SerializeField] private LayerMask furnitureLayer;

    [Tooltip("Шар коробок (Box або Default — той що на InteractableBox GO).")]
    [SerializeField] private LayerMask boxLayer;

    [Header("Settings")]
    [SerializeField] private float maxDistance = 30f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;
        if (IsPointerOverUI()) return;
        if (PlacementController.Instance != null && PlacementController.Instance.IsPlacing) return;
        if (PlacementController.Instance != null && PlacementController.Instance.JustConfirmedThisFrame) return;

        HandleClick(mouse.position.ReadValue());
    }

    private void HandleClick(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        // ── 1. Коробки ───────────────────────────────────────────
        // Перевіряємо першими — вони мають пріоритет
        if (Physics.Raycast(ray, out RaycastHit boxHit, maxDistance, boxLayer))
        {
            var box = boxHit.collider.GetComponentInParent<InteractableBox>();
            if (box != null && box.CanInteract)
            {
                Debug.Log($"[Router] Коробка: {boxHit.collider.name}");
                box.OnInteract();
                return;
            }
        }

        // ── 2. Меблі (тільки шар Furniture) ─────────────────────
        // Шари Shelf, Default, підлога — сюди не потраплять
        if (Physics.Raycast(ray, out RaycastHit furHit, maxDistance, furnitureLayer))
        {
            Debug.Log($"[Router] Furniture хіт: {furHit.collider.name}");

            // Шукаємо IInteractable тільки на хітнутому GO і його батьках
            // НЕ на root — бо root може бути сценою
            var interactables = furHit.collider.GetComponentsInParent<IInteractable>();

            foreach (var i in interactables)
            {
                if (!i.CanInteract) continue;
                Debug.Log($"[Router] → {i.GetType().Name}");
                i.OnInteract();
                return;
            }

            Debug.Log($"[Router] Furniture знайдено але CanInteract=false");
        }
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return true;
        return false;
    }
}