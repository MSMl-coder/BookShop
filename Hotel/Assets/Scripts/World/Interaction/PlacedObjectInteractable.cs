// Assets/Scripts/World/Interaction/PlacedObjectInteractable.cs
using UnityEngine;

/// Додається автоматично при ConfirmPlacement на розміщений prefab.
/// В EditMode — дозволяє підняти обʼєкт.
/// Поза EditMode — заблокований (Cabinet або інший компонент обробить клік).
public class PlacedObjectInteractable : MonoBehaviour, IInteractable
{
    // Посилання на PlacedObject (мітка з FurnitureInstance)
    private PlacedObject _placedObject;

    private void Awake()
    {
        _placedObject = GetComponent<PlacedObject>()
                     ?? GetComponentInParent<PlacedObject>();
    }

    public bool CanInteract
    {
        get
        {
            // Тільки в EditMode
            if (EditModeManager.Instance == null || !EditModeManager.Instance.IsEditMode)
                return false;

            // Не під час активного розміщення
            if (PlacementController.Instance != null && PlacementController.Instance.IsPlacing)
                return false;

            return true;
        }
    }

    public void OnInteract()
    {
        if (_placedObject == null)
        {
            Debug.LogWarning("[PlacedObjectInteractable] PlacedObject не знайдено!");
            return;
        }

        Debug.Log($"[PlacedObjectInteractable] Підбираємо: {gameObject.name}");
        PlacementController.Instance?.PickUpExisting(_placedObject);
    }
}