// ═══════════════════════════════════════════════════════════
// BookshopUIController.cs — Main UI controller
// Path: Assets/Scripts/UI/Bookshop/BookshopUIController.cs
//
// PURPOSE:
//   Master controller that wires up the entire bookshop UI.
//   Replaces the legacy ShopUIManager when using Bookshop UI prefab.
//   Holds references to all sub-controllers and templates.
//
// UNITY SETUP:
//   1. Create empty GameObject "BookshopUI" in scene
//   2. Add UIDocument component, assign BookshopMainUI.uxml
//   3. Add this script
//   4. Assign all VisualTreeAsset templates from Templates/ folder
//   5. Optional: PanelSettings — Scale Mode=ConstantPixelSize, Match=1.0
//
// HOTKEY: ESC closes any open modal
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class BookshopUIController : MonoBehaviour
{
    public static BookshopUIController Instance { get; private set; }

    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Templates (assign in Inspector)")]
    [SerializeField] private VisualTreeAsset buffCardTemplate;
    [SerializeField] private VisualTreeAsset inventoryRowTemplate;
    [SerializeField] private VisualTreeAsset catalogBookCardTemplate;
    [SerializeField] private VisualTreeAsset catalogAuthorSectionTemplate;
    [SerializeField] private VisualTreeAsset decorItemCardTemplate;

    [Header("Sub-controllers (auto-found on same GameObject)")]
    [SerializeField] private ClubCardController       clubCardCtrl;
    [SerializeField] private BuffsController          buffsCtrl;
    [SerializeField] private ClockController          clockCtrl;
    [SerializeField] private CalendarController       calendarCtrl;
    [SerializeField] private BookInfoController       bookInfoCtrl;
    [SerializeField] private InventoryModalController inventoryCtrl;
    [SerializeField] private CatalogModalController   catalogCtrl;
    [SerializeField] private DecorModalController     decorCtrl;
    [SerializeField] private NPCModalController       npcCtrl;
    [SerializeField] private AwardsModalController    awardsCtrl;
    [SerializeField] private ToastController          toastCtrl;

    // ── Root references ──
    private VisualElement _root;
    private VisualElement _overlay;

    // Modal name → element map for toggling
    private System.Collections.Generic.Dictionary<string, VisualElement> _modals;

    private string _activeModal;
    private VisualElement _activeButton;

    // Public accessors for sub-controllers
    public VisualTreeAsset BuffCardTemplate              => buffCardTemplate;
    public VisualTreeAsset InventoryRowTemplate          => inventoryRowTemplate;
    public VisualTreeAsset CatalogBookCardTemplate       => catalogBookCardTemplate;
    public VisualTreeAsset CatalogAuthorSectionTemplate  => catalogAuthorSectionTemplate;
    public VisualTreeAsset DecorItemCardTemplate         => decorItemCardTemplate;
    public BookInfoController BookInfo => bookInfoCtrl;
    public ToastController Toast => toastCtrl;

    // ─────────────────────────────────────────────
    #region Unity Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Auto-find sub-controllers if not assigned
        if (clubCardCtrl    == null) clubCardCtrl    = GetComponent<ClubCardController>();
        if (buffsCtrl       == null) buffsCtrl       = GetComponent<BuffsController>();
        if (clockCtrl       == null) clockCtrl       = GetComponent<ClockController>();
        if (calendarCtrl    == null) calendarCtrl    = GetComponent<CalendarController>();
        if (bookInfoCtrl    == null) bookInfoCtrl    = GetComponent<BookInfoController>();
        if (inventoryCtrl   == null) inventoryCtrl   = GetComponent<InventoryModalController>();
        if (catalogCtrl     == null) catalogCtrl     = GetComponent<CatalogModalController>();
        if (decorCtrl       == null) decorCtrl       = GetComponent<DecorModalController>();
        if (npcCtrl         == null) npcCtrl         = GetComponent<NPCModalController>();
        if (awardsCtrl      == null) awardsCtrl      = GetComponent<AwardsModalController>();
        if (toastCtrl       == null) toastCtrl       = GetComponent<ToastController>();
    }

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[BookshopUI] UIDocument not assigned!");
            return;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null) { Debug.LogWarning("[BookshopUI] Root is null"); return; }

        _overlay = _root.Q<VisualElement>("Overlay");

        // Map modals by their nameId
        _modals = new System.Collections.Generic.Dictionary<string, VisualElement>
        {
            { "inv-modal",     _root.Q<VisualElement>("InvModal")     },
            { "cat-modal",     _root.Q<VisualElement>("CatModal")     },
            { "decor-modal",   _root.Q<VisualElement>("DecorModal")   },
            { "npc-modal",     _root.Q<VisualElement>("NPCModal")     },
            { "awards-modal",  _root.Q<VisualElement>("AwardsModal")  },
        };

        BindFuncBar();
        BindCloseButtons();
        BindOverlay();

        // Initialize all sub-controllers
        clubCardCtrl?.Initialize(_root);
        buffsCtrl?.Initialize(_root, this);
        clockCtrl?.Initialize(_root, this);
        calendarCtrl?.Initialize(_root);
        bookInfoCtrl?.Initialize(_root);
        inventoryCtrl?.Initialize(_root, this);
        catalogCtrl?.Initialize(_root, this);
        decorCtrl?.Initialize(_root, this);
        npcCtrl?.Initialize(_root, this);
        awardsCtrl?.Initialize(_root, this);
        toastCtrl?.Initialize(_root);

        CloseAllModals();
        Debug.Log("[BookshopUI] Initialized");
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            if (_activeModal != null) CloseModal(_activeModal);
        }
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Modal management
    // ─────────────────────────────────────────────

    private void BindFuncBar()
    {
        BindModalButton("BtnInventory", "inv-modal");
        BindModalButton("BtnCatalog",   "cat-modal");
        BindModalButton("BtnDecor",     "decor-modal");
        BindModalButton("BtnNPC",       "npc-modal");
        BindModalButton("BtnAwards",    "awards-modal");
    }

    private void BindModalButton(string buttonName, string modalId)
    {
        var btn = _root.Q<Button>(buttonName);
        if (btn == null) { Debug.LogWarning($"[BookshopUI] Button '{buttonName}' not found"); return; }

        btn.clicked += () =>
        {
            var modal = _modals[modalId];
            if (!modal.ClassListContains("hidden"))
            {
                CloseModal(modalId);
            }
            else
            {
                OpenModal(modalId, btn);
            }
        };
    }

    private void BindCloseButtons()
    {
        // ✕ buttons
        BindClose("InvCloseBtn",     "inv-modal");
        BindClose("CatCloseBtn",     "cat-modal");
        BindClose("DecorCloseBtn",   "decor-modal");
        BindClose("NPCCloseBtn",     "npc-modal");
        BindClose("AwardsCloseBtn",  "awards-modal");
        // Stamp buttons
        BindClose("InvCloseStamp",    "inv-modal");
        BindClose("CatCloseStamp",    "cat-modal");
        BindClose("DecorCloseStamp",  "decor-modal");
        BindClose("NPCCloseStamp",    "npc-modal");
        BindClose("AwardsCloseStamp", "awards-modal");
    }

    private void BindClose(string buttonName, string modalId)
    {
        var btn = _root.Q<Button>(buttonName);
        if (btn != null) btn.clicked += () => CloseModal(modalId);
    }

    private void BindOverlay()
    {
        if (_overlay == null) return;
        _overlay.RegisterCallback<ClickEvent>(_ => CloseAllModals());
    }

    public void OpenModal(string modalId, VisualElement triggerButton = null)
    {
        if (!_modals.TryGetValue(modalId, out var modal) || modal == null) return;

        // Close currently open modal first
        if (_activeModal != null && _activeModal != modalId)
            CloseModal(_activeModal);

        modal.RemoveFromClassList("hidden");
        _overlay?.RemoveFromClassList("hidden");
        _activeModal = modalId;

        // Highlight button
        _activeButton?.RemoveFromClassList("active");
        if (triggerButton != null)
        {
            triggerButton.AddToClassList("active");
            _activeButton = triggerButton;
        }

        // Inventory shows book info card alongside
        if (modalId == "inv-modal")
            bookInfoCtrl?.Show();

        Debug.Log($"[BookshopUI] Opened modal: {modalId}");
    }

    public void CloseModal(string modalId)
    {
        if (!_modals.TryGetValue(modalId, out var modal) || modal == null) return;

        modal.AddToClassList("hidden");

        if (_activeModal == modalId)
        {
            _activeModal = null;
            _overlay?.AddToClassList("hidden");
        }

        _activeButton?.RemoveFromClassList("active");
        _activeButton = null;

        if (modalId == "inv-modal")
            bookInfoCtrl?.Hide();
        if (modalId == "decor-modal")
            decorCtrl?.CloseEditor();
    }

    public void CloseAllModals()
    {
        foreach (var key in _modals.Keys)
            _modals[key]?.AddToClassList("hidden");
        _overlay?.AddToClassList("hidden");
        _activeModal = null;
        _activeButton?.RemoveFromClassList("active");
        _activeButton = null;
        bookInfoCtrl?.Hide();
    }

    #endregion

    #region Public API for external systems

    /// Open inventory with book info card pre-populated
    public void OpenInventoryFor(BookTemplate template)
    {
        OpenModal("inv-modal");
        bookInfoCtrl?.Show(template);
    }

    /// Trigger NPC modal with given NPC data
    public void OpenNPCDialog(BookHunterData npc) // expects existing BookHunterData type
    {
        OpenModal("npc-modal");
        npcCtrl?.LoadNPC(npc);
    }

    #endregion
}
