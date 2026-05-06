// Assets/Scripts/UI/ShopUIManager.cs
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class ShopUIManager : MonoBehaviour
{
    public static ShopUIManager Instance { get; private set; }

    [Header("UIDocument")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Sub-controllers")]
    [SerializeField] private InventoryPanelUI inventoryPanelUI;
    [SerializeField] private CabinetPanelUI   cabinetPanelUI;
    [SerializeField] private LootPanelUI      lootPanelUI;
    [SerializeField] private HUDController    hudController;

    private VisualElement _inventoryPanel;
    private VisualElement _cabinetPanel;
    private VisualElement _endDayPanel;
    private VisualElement _tutorialBubble;
    private VisualElement _bookInfoCard;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void OnEnable()
    {
        if (uiDocument == null) { Debug.LogError("[ShopUI] UIDocument не призначено!"); return; }

        var root = uiDocument.rootVisualElement;

        _inventoryPanel = root.Q<VisualElement>("InventoryPanel");
        _cabinetPanel   = root.Q<VisualElement>("CabinetPanel");
        _endDayPanel    = root.Q<VisualElement>("EndDayPanel");
        _tutorialBubble = root.Q<VisualElement>("TutorialBubble");
        _bookInfoCard   = root.Q<VisualElement>("BookInfoCard");

        root.Q<Button>("BtnOpenInventory")?.RegisterCallback<ClickEvent>(_ => ToggleInventory());
        root.Q<Button>("BtnSettings")?.RegisterCallback<ClickEvent>(_ => Debug.Log("[UI] Settings"));
        root.Q<Button>("TutorialOkBtn")?.RegisterCallback<ClickEvent>(_ => OnTutorialOk());

        RegisterNavTab(root, "NavTabShop");
        RegisterNavTab(root, "NavTabCatalog");
        RegisterNavTab(root, "NavTabOrders");
        RegisterNavTab(root, "NavTabCollections");

        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += HandleGameStateChanged;
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnStepStarted += ShowTutorialBubble;

        CloseAll();
        Debug.Log("[ShopUI] Ready.");
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleGameStateChanged;
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnStepStarted -= ShowTutorialBubble;
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            CloseAll();
    }

    // ── Public ──

    public void OpenCabinetUI(Cabinet cabinet)
    {
        Show(_inventoryPanel);
        Show(_cabinetPanel);
        inventoryPanelUI?.Refresh();
        cabinetPanelUI?.Open(cabinet);
    }

    public void ToggleInventory()
    {
        if (IsHidden(_inventoryPanel)) { Show(_inventoryPanel); Show(_cabinetPanel); inventoryPanelUI?.Refresh(); }
        else CloseAll();
    }

    public void CloseAll()
    {
        Hide(_inventoryPanel);
        Hide(_cabinetPanel);
        HideEndDay();
    }

    public void ShowBookInfo(BookTemplate t)
    {
        if (_bookInfoCard == null || t == null) return;
        var root = uiDocument.rootVisualElement;
        SetLabel(root, "BiTitle",  t.title);
        SetLabel(root, "BiAuthor", t.author);
        SetLabel(root, "BiYear",   t.writingYear.ToString());
        SetLabel(root, "BiGenre",  t.genre.ToString());
        SetLabel(root, "BiPrice",  $"{t.sellPrice:F0} грн");

        var rl = root.Q<Label>("BiRarity");
        if (rl != null)
        {
            foreach (var c in new[]{"common","uncommon","rare","epic","legendary"})
                rl.RemoveFromClassList(c);
            rl.text = t.rarity.ToString();
            rl.AddToClassList(t.rarity.ToString().ToLower());
        }
        Show(_bookInfoCard);
    }

    public void HideBookInfo() => Hide(_bookInfoCard);

    // ── Private ──

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.LootPhase) { CloseAll(); ShowEndDay(); }
        else HideEndDay();
    }

    private void ShowEndDay()
    {
        Show(_endDayPanel);
        if (_endDayPanel != null) _endDayPanel.pickingMode = PickingMode.Position;
        lootPanelUI?.Show();
    }

    private void HideEndDay()
    {
        Hide(_endDayPanel);
        if (_endDayPanel != null) _endDayPanel.pickingMode = PickingMode.Ignore;
    }

    private void RegisterNavTab(VisualElement root, string name)
    {
        root.Q<Button>(name)?.RegisterCallback<ClickEvent>(_ =>
        {
            foreach (var t in new[]{"NavTabShop","NavTabCatalog","NavTabOrders","NavTabCollections"})
                root.Q<Button>(t)?.RemoveFromClassList("active");
            root.Q<Button>(name)?.AddToClassList("active");
        });
    }

    private void ShowTutorialBubble(TutorialStep step)
    {
        if (_tutorialBubble == null) return;
        var root = uiDocument.rootVisualElement;
        SetLabel(root, "TutorialText", step.message);
        Show(_tutorialBubble);
    }

    private void OnTutorialOk()
    {
        TutorialManager.Instance?.CompleteCurrentStep();
        Hide(_tutorialBubble);
    }

    private static void Show(VisualElement el) { if (el != null) el.style.display = DisplayStyle.Flex; }
    private static void Hide(VisualElement el) { if (el != null) el.style.display = DisplayStyle.None; }
    private static bool IsHidden(VisualElement el) => el == null || el.style.display == DisplayStyle.None;
    private static void SetLabel(VisualElement root, string name, string text)
        => root.Q<Label>(name)?.SetText(text);
}
