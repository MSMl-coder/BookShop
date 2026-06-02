// Assets/Scripts/UI/Components/InventoryPanel/InventoryPanelMount.cs
//
// MonoBehaviour-обгортка для InventoryPanelController.
// Підключається до кнопки BtnInventory в GameHUDController.
//
// SETUP:
//   1. Empty GO "InventoryPanel_UI"
//   2. UIDocument (PanelSettings shared, sortingOrder=15)
//   3. InventoryPanelMount
//      - Inspector Asset = InventoryPanel.uxml
//
// GameHUDController автоматично знаходить Instance і прив'язує BtnInventory.

using UnityEngine;
using UnityEngine.UIElements;

public class InventoryPanelMount : MonoBehaviour
{
    public static InventoryPanelMount Instance { get; private set; }

    [Header("UXML asset")]
    [SerializeField] private VisualTreeAsset panelAsset;

    [Header("Position")]
    [SerializeField] private float posLeft   = 70f;
    [SerializeField] private float posTop    = 40f;

    private InventoryPanelController _controller;

    public InventoryPanelController Controller => _controller;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || panelAsset == null)
        {
            Debug.LogWarning("[InventoryPanelMount] UIDocument або panelAsset не призначені");
            return;
        }

        var el = panelAsset.Instantiate();
        el.style.position = Position.Absolute;
        el.style.left  = 0; el.style.top    = 0;
        el.style.right = 0; el.style.bottom = 0;
        el.pickingMode = PickingMode.Ignore;

        doc.rootVisualElement.pickingMode = PickingMode.Ignore;
        doc.rootVisualElement.Add(el);

        _controller = new InventoryPanelController(el);

        // Реєструємо в UIScreenManager як transparent
        UIPointerChecker.RegisterTransparentDocuments(new[] { doc });

        ApplyPosition(el);
        Debug.Log("[InventoryPanelMount] Mounted");
    }

    // ── Public API ────────────────────────────────────────────────

    public void Show()   => _controller?.Show();
    public void Hide()   => _controller?.Hide();
    public void Toggle() => _controller?.Toggle();

    // ── Position ──────────────────────────────────────────────────

    [ContextMenu("Apply Position")]
    public void ApplyPosition() => ApplyPosition(null);

    private void ApplyPosition(VisualElement container)
    {
        var doc = GetComponent<UIDocument>();
        var el  = container ?? doc?.rootVisualElement?.ElementAt(0);
        if (el == null) return;

        var panel = el.Q<VisualElement>("InventoryPanel");
        if (panel == null) return;

        panel.style.left = posLeft;
        panel.style.top  = posTop;
    }

    /// Викликається V2HUDInjector замість OnEnable монтування.
    /// Передає вже створений контролер (UXML змонтований зовні).
    public void InjectController(InventoryPanelController controller)
    {
        _controller = controller;
        Debug.Log("[InventoryPanelMount] Controller injected by V2HUDInjector");
    }

    private void OnDestroy() => _controller?.Dispose();
}