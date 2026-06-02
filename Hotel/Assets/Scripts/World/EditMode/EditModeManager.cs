// Assets/Scripts/World/EditMode/EditModeManager.cs
// EditMode — підстан Preparation. GameLoopManager.CurrentState НЕ змінюється.
//
// API:
//   EditModeManager.Instance.IsEditMode  — instance property (як було в оригіналі)
//   EditModeManager.IsEditMode           — static shortcut (те саме)
//   EditModeManager.GetEffectiveState()  — повертає GameState.EditMode якщо активний

using UnityEngine;

public class EditModeManager : MonoBehaviour
{
    public static EditModeManager Instance { get; private set; }

    // ── Instance property (як в оригінальному коді проекту) ────
    // EditModeInputHandler та CabinetInteractable читають через Instance.IsEditMode
    public bool IsEditMode { get; private set; }

    // ── Static shortcut (для нового коду без Instance) ─────────
    // ContextMenuUI, InteractionRouter, HoverHighlighter читають так
    public static bool IsActive => Instance != null && Instance.IsEditMode;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ── Public API ─────────────────────────────────────────────

    public void ToggleEditMode()
    {
        if (IsEditMode) ExitEditMode();
        else            EnterEditMode();
    }

    public void EnterEditMode()
    {
        if (GameLoopManager.Instance?.CurrentState != GameState.Preparation)
        {
            Debug.LogWarning("[EditMode] Тільки в Preparation!");
            return;
        }

        IsEditMode = true;
        Debug.Log("[EditMode] Увійшли в режим редагування");
        //DecorationPanelUI.Instance?.Open();
    }

    public void ExitEditMode()
    {
        IsEditMode = false;
        Debug.Log("[EditMode] Вийшли з режиму редагування");
        //DecorationPanelUI.Instance?.Close();
    }

    // ── Static helper для ContextMenuUI / InteractionRouter ────

    /// Повертає GameState.EditMode якщо активний, інакше поточний GameState.
    public static GameState GetEffectiveState()
    {
        var gs = GameLoopManager.Instance?.CurrentState ?? GameState.Preparation;
        if (IsActive && gs == GameState.Preparation)
            return GameState.EditMode;
        return gs;
    }
}