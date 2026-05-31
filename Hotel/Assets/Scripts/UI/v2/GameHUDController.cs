// Assets/Scripts/UI/v2/GameHUD/GameHUDController.cs
//
// Головний HUD контролер — годинник, speed, nav, NPC inspector.
//
// РЕЖИМ 1 — Standalone (власний UIDocument):
//   GO "UI_v2_Screen1" → UIDocument + GameHUDController
//   uiDocument призначено в Inspector
//
// РЕЖИМ 2 — Injected (через V2HUDInjector в BookshopUI document):
//   V2HUDInjector викликає InjectRoot(element) після монтування UXML
//   uiDocument = null (не потрібен)

using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-15)]
public class GameHUDController : MonoBehaviour
{
    public static GameHUDController Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────
    [Header("UIDocument (standalone режим, залиш null якщо V2HUDInjector)")]
    [SerializeField] private UIDocument uiDocument;

    [Header("NPC Inspector UXML (standalone режим)")]
    [SerializeField] private VisualTreeAsset npcInspectorAsset;

    [Header("NPC Inspector Position")]
    [SerializeField] private float npcInspectorLeft   = 70f;
    [SerializeField] private float npcInspectorBottom = 160f;

    [Header("Testing")]
    [SerializeField] private bool showNPCPreviewOnStart = false;

    // ── Runtime ───────────────────────────────────────────────────
    private Label   _clockHH;
    private Label   _clockMM;
    private Button  _speedBtn1, _speedBtn2, _speedBtn3;
    private Button  _btnAwards, _btnBookHunters, _btnDecorations;
    private Button  _btnCatalog, _btnInventory;
    private Button  _activeNavBtn;

    private NPCInspectorController _npcInspector;
    private VisualElement          _npcInspectorElement;
    private VisualElement          _injectedRoot;

    private const string CLS_SPEED_ACTIVE = "speed-btn--active";
    private const string CLS_NAV_ACTIVE   = "nav-btn--active";

    // ── Lifecycle ─────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        // Standalone режим: ініціалізуємо з власного UIDocument
        if (_injectedRoot == null && uiDocument != null)
            InitializeFromRoot(uiDocument.rootVisualElement);
    }

    private void Update()
    {
        _npcInspector?.Tick();
    }

    // ── Inject API (V2HUDInjector) ────────────────────────────────

    /// Викликається V2HUDInjector замість UIDocument.
    /// element = TemplateContainer з GameHUD.uxml.Instantiate()
    public void InjectRoot(VisualElement element)
    {
        _injectedRoot = element;
        InitializeFromRoot(element);
    }

    // ── Init ──────────────────────────────────────────────────────

    private void InitializeFromRoot(VisualElement root)
    {
        if (root == null) return;

        // Корінь і HUDRoot — Ignore щоб не блокували 3D
        root.pickingMode = PickingMode.Ignore;
        if (root.childCount > 0)
            root.ElementAt(0).pickingMode = PickingMode.Ignore;

        BindClock(root);
        BindSpeed(root);
        BindNav(root);

        // NPC Inspector монтується тут тільки в standalone режимі.
        // В injected режимі NPCInspector монтує V2HUDInjector окремо.
        if (npcInspectorAsset != null)
            MountNPCInspector(root);

        UpdateClock(7, 0);
    }

    // ── Clock ─────────────────────────────────────────────────────

    private void BindClock(VisualElement root)
    {
        _clockHH = root.Q<Label>("ClockHH");
        _clockMM = root.Q<Label>("ClockMM");
    }

    public void UpdateClock(int hours, int minutes)
    {
        if (_clockHH != null) _clockHH.text = hours.ToString("D2");
        if (_clockMM != null) _clockMM.text = minutes.ToString("D2");
    }

    // ── Speed ─────────────────────────────────────────────────────

    private void BindSpeed(VisualElement root)
    {
        _speedBtn1 = root.Q<Button>("SpeedBtn1");
        _speedBtn2 = root.Q<Button>("SpeedBtn2");
        _speedBtn3 = root.Q<Button>("SpeedBtn3");

        _speedBtn1?.RegisterCallback<ClickEvent>(_ => SetSpeed(1));
        _speedBtn2?.RegisterCallback<ClickEvent>(_ => SetSpeed(2));
        _speedBtn3?.RegisterCallback<ClickEvent>(_ => SetSpeed(3));

        UIHoverSound.RegisterButton(_speedBtn1);
        UIHoverSound.RegisterButton(_speedBtn2);
        UIHoverSound.RegisterButton(_speedBtn3);
    }

    private void SetSpeed(int speed)
    {
        _speedBtn1?.EnableInClassList(CLS_SPEED_ACTIVE, speed == 1);
        _speedBtn2?.EnableInClassList(CLS_SPEED_ACTIVE, speed == 2);
        _speedBtn3?.EnableInClassList(CLS_SPEED_ACTIVE, speed == 3);
    }

    // ── Nav ───────────────────────────────────────────────────────

    private void BindNav(VisualElement root)
    {
        _btnAwards      = root.Q<Button>("BtnAwards");
        _btnBookHunters = root.Q<Button>("BtnBookHunters");
        _btnDecorations = root.Q<Button>("BtnDecorations");
        _btnCatalog     = root.Q<Button>("BtnCatalog");
        _btnInventory   = root.Q<Button>("BtnInventory");

        // Програмно фіксуємо picking-mode
        var leftNav = root.Q<VisualElement>("LeftNav");
        if (leftNav != null) leftNav.pickingMode = PickingMode.Position;

        foreach (var btn in new[] { _btnAwards, _btnBookHunters, _btnDecorations,
                                     _btnCatalog, _btnInventory })
            if (btn != null) btn.pickingMode = PickingMode.Position;

        BindNavBtn(_btnAwards,      () => { });
        BindNavBtn(_btnBookHunters, () => { });
        BindNavBtn(_btnDecorations, () => UIScreenManager.Instance?.ToggleEditMode());
        BindNavBtn(_btnCatalog,     () => { });
        BindNavBtn(_btnInventory,   () =>
        {
            if (InventoryPanelMount.Instance != null)
                InventoryPanelMount.Instance.Toggle();
            else
                BookshopUIController.Instance?.OpenInventoryFromShelf();
        });

        UIHoverSound.RegisterButton(_btnAwards);
        UIHoverSound.RegisterButton(_btnBookHunters);
        UIHoverSound.RegisterButton(_btnDecorations);
        UIHoverSound.RegisterButton(_btnCatalog);
        UIHoverSound.RegisterButton(_btnInventory);
    }

    private void BindNavBtn(Button btn, System.Action action)
    {
        if (btn == null) return;
        btn.RegisterCallback<ClickEvent>(_ => { SetActiveNav(btn); action?.Invoke(); });
    }

    private void SetActiveNav(Button btn)
    {
        if (_activeNavBtn != null && _activeNavBtn != btn)
            _activeNavBtn.RemoveFromClassList(CLS_NAV_ACTIVE);

        if (_activeNavBtn == btn)
        {
            btn.RemoveFromClassList(CLS_NAV_ACTIVE);
            _activeNavBtn = null;
        }
        else
        {
            btn.AddToClassList(CLS_NAV_ACTIVE);
            _activeNavBtn = btn;
        }
    }

    // ── NPC Inspector (standalone mount) ─────────────────────────

    private void MountNPCInspector(VisualElement root)
    {
        _npcInspectorElement = npcInspectorAsset.Instantiate();

        var hudRoot = root.Q<VisualElement>("HUDRoot") ?? root;
        hudRoot.Add(_npcInspectorElement);

        // TemplateContainer — fullscreen absolute wrapper
        _npcInspectorElement.style.position = Position.Absolute;
        _npcInspectorElement.style.left     = 0;
        _npcInspectorElement.style.top      = 0;
        _npcInspectorElement.style.right    = 0;
        _npcInspectorElement.style.bottom   = 0;

        ApplyNPCInspectorPosition();

        _npcInspector = new NPCInspectorController(_npcInspectorElement);

        if (showNPCPreviewOnStart)
            _npcInspector.ShowForPreview();
    }

    /// Використовується NPCInspectorMount.InjectController() в Injected режимі
    public void SetNPCInspectorController(NPCInspectorController controller)
    {
        _npcInspector = controller;
    }

    // ── NPC Inspector Position ────────────────────────────────────

    [ContextMenu("Apply NPC Inspector Position")]
    public void ApplyNPCInspectorPosition()
    {
        if (_npcInspectorElement == null) return;

        var el = _npcInspectorElement.Q<VisualElement>("NPCInspector")
                 ?? _npcInspectorElement;

        el.style.position = Position.Absolute;
        el.style.left     = npcInspectorLeft;
        el.style.bottom   = npcInspectorBottom;
        el.style.top      = StyleKeyword.Auto;
        el.style.right    = StyleKeyword.Auto;
    }

    // ── Context menus ─────────────────────────────────────────────

    [ContextMenu("Show NPC Preview")]
    public void ShowNPCPreviewFromMenu()
    {
        if (_npcInspector == null) { Debug.LogWarning("[GameHUD] NPC Inspector не змонтовано."); return; }
        _npcInspector.ShowForPreview();
    }

    [ContextMenu("Hide NPC Inspector")]
    public void HideNPCFromMenu() => _npcInspector?.Hide();

    // ── Public NPC API ────────────────────────────────────────────

    public void ShowNPCInspector(NPCBrain npc)              => _npcInspector?.Show(npc);
    public void HideNPCInspector()                          => _npcInspector?.Hide();
    public void HideNPCInspectorFor(NPCBrain npc)           => _npcInspector?.HideIfShowing(npc);
    public void MarkNPCBookFulfilled(int index)             => _npcInspector?.MarkCardFulfilled(index);
    public void UpdateNPCBookCard(int i, string title, float price, string cls)
                                                            => _npcInspector?.UpdateCardBook(i, title, price, cls);
}