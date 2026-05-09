// Assets/Scripts/World/Interaction/CabinetInteractable.cs
using UnityEngine;

/// Додається на той самий GameObject що Cabinet.
/// Обробляє відкриття інвентарю шафи.
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
            // В EditMode — завжди заблокований
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
        Debug.Log($"[CabinetInteractable] Відкриваємо: {_cabinet.cabinetName}");
        ShopUIManager.Instance?.OpenCabinetUI(_cabinet);
    }
}