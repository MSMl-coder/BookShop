// Assets/Scripts/UI/ShopUIManager.cs
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

// Головний координатор UI — делегує роботу панельним контролерам
public class ShopUIManager : MonoBehaviour
{
    public static ShopUIManager Instance { get; private set; }

    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Panel Controllers")]
    [SerializeField] private InventoryPanelUI inventoryPanel;
    [SerializeField] private CabinetPanelUI cabinetPanel;
    [SerializeField] private LootPanelUI lootPanel;
    [SerializeField] private SettingsUIController settingsPanel;

    // Загальні кореневі панелі
    private VisualElement _leftPanel;
    private VisualElement _rightPanel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _leftPanel  = root.Q<VisualElement>("LeftPanel");
        _rightPanel = root.Q<VisualElement>("RightPanel");

        root.Q<Button>("BtnOpenInventory")
            ?.RegisterCallback<ClickEvent>(_ => ToggleInventory());

        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += HandleStateChanged;

        CloseAll();
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            CloseAll();
    }

    // --- Public API ---

    public void OpenCabinetUI(Cabinet cabinet)
    {
        ShowPanels(true);
        inventoryPanel?.Show();
        cabinetPanel?.Open(cabinet);
    }

    public void ToggleInventory()
    {
        bool willShow = !inventoryPanel.IsVisible;
        ShowPanels(willShow);
        if (willShow) inventoryPanel?.Show();
        else inventoryPanel?.Hide();
    }

    public void CloseAll()
    {
        ShowPanels(false);
        inventoryPanel?.Hide();
        cabinetPanel?.Hide();
    }

    // --- Private ---

    private void ShowPanels(bool show)
    {
        var state = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (_leftPanel  != null) _leftPanel.style.display  = state;
        if (_rightPanel != null) _rightPanel.style.display = state;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.LootPhase)
        {
            CloseAll();
            lootPanel?.Show();
        }
        else
        {
            lootPanel?.Hide();
        }
    }
}