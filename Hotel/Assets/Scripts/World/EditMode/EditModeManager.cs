// Assets/Scripts/World/EditMode/EditModeManager.cs
using UnityEngine;
using System;

public class EditModeManager : MonoBehaviour
{
    public static EditModeManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GridOverlay gridOverlay;
    [SerializeField] private PlacementController placementController;
    [SerializeField] private DecorationPanelUI decorationPanel; // ← замість furniturePickerPanel
    [SerializeField] private GameObject editButtonRoot;

    public bool IsEditMode { get; private set; }
    public event Action<bool> OnEditModeChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void OnEnable()
    {
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged += HandleStateChanged;
            HandleStateChanged(GameLoopManager.Instance.CurrentState);
        }
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState state)
    {
        bool allowed = state == GameState.Preparation;
        if (editButtonRoot != null) editButtonRoot.SetActive(allowed);
        if (!allowed && IsEditMode) ExitEditMode();
    }

    public void ToggleEditMode()
    {
        if (GameLoopManager.Instance.CurrentState != GameState.Preparation) return;
        if (IsEditMode) ExitEditMode();
        else EnterEditMode();
    }

    public void EnterEditMode()
    {
        IsEditMode = true;
        gridOverlay?.Show();
        // ← decorationPanel?.OpenInEditMode() — прибрати звідси
        placementController?.EnablePlacement();
        PlacementFeedback.Instance?.PlaySound(PlacementFeedback.SoundType.ModeEnter);
        OnEditModeChanged?.Invoke(true);
        Debug.Log("[EditMode] Увійшли в режим редагування");
    }

    public void ExitEditMode()
    {
        IsEditMode = false;
        placementController?.CancelPlacement();
        gridOverlay?.Hide();
        decorationPanel?.Close();          // ← закрити при виході
        placementController?.DisablePlacement();
        PlacementFeedback.Instance?.PlaySound(PlacementFeedback.SoundType.ModeExit);
        OnEditModeChanged?.Invoke(false);
        Debug.Log("[EditMode] Вийшли з режиму редагування");
    }
}