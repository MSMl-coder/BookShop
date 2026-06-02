// Assets/Scripts/World/Interaction/CabinetInteractable.cs
// ФІКС: OnInteract() тіло було закоментоване —
//   натискання на шафу в грі не відкривало жодного UI.
//   Тепер підключено до ShelfManagerPanelController.Open(cabinet).

using UnityEngine;

/// Додається на той самий GameObject що Cabinet.
/// Обробляє відкриття менеджера полиць при натисканні на шафу.
/// В EditMode — заблокований (CanInteract = false).
[RequireComponent(typeof(Cabinet))]
public class CabinetInteractable : MonoBehaviour, IInteractable
{
    private Cabinet _cabinet;

    private void Awake()
    {
        _cabinet = GetComponent<Cabinet>();
    }

    public bool CanInteract
    {
        get
        {
            if (EditModeManager.Instance != null && EditModeManager.Instance.IsEditMode)
                return false;

            var state = GameLoopManager.Instance?.CurrentState;
            return state == GameState.Preparation
                || state == GameState.WorkDay
                || state == GameState.LootPhase;
        }
    }

    public void OnInteract()
    {
        if (_cabinet == null)
        {
            Debug.LogWarning("[CabinetInteractable] Cabinet компонент не знайдено.");
            return;
        }

        Debug.Log($"[CabinetInteractable] Відкриваємо менеджер полиць: {_cabinet.cabinetName}");

        // ✅ ФІКС: підключено до ShelfManagerPanelController
        if (ShelfManagerPanelController.Instance != null)
        {
            ShelfManagerPanelController.Instance.Open(_cabinet);
        }
        else
        {
            // Fallback: відкриваємо загальний інвентар якщо менеджер недоступний
            Debug.LogWarning("[CabinetInteractable] ShelfManagerPanelController відсутній — " +
                             "відкриваємо загальний інвентар.");
            BookshopUIController.Instance?.OpenInventoryFromShelf(_cabinet.cabinetName);
        }
    }
}