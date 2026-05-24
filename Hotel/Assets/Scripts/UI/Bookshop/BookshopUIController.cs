// ═══════════════════════════════════════════════════════════════════
// BookshopUIController.cs — Master UI orchestrator v3
// Path: Assets/Scripts/UI/Bookshop/BookshopUIController.cs
//
// FIXED in v3:
//  • Modal names match new UXML (nb-modal--inv etc.)
//  • OpenInventoryFromShelf() properly opens InvModal
//  • No blur on overlay — inventory uses inv-open (lighter dim)
//  • ClubCard uses fade flip, not scaleX
//  • FlipClock replaces old ClockController
// ═══════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class BookshopUIController : MonoBehaviour
{
    public static BookshopUIController Instance { get; private set; }

    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("UXML Templates")]
    [SerializeField] private VisualTreeAsset buffCardTemplate;
    [SerializeField] private VisualTreeAsset inventoryRowTemplate;
    [SerializeField] private VisualTreeAsset catalogBookCardTemplate;
    [SerializeField] private VisualTreeAsset catalogAuthorSectionTemplate;
    [SerializeField] private VisualTreeAsset decorItemCardTemplate;

    [Header("Sub-controllers (auto-found on same GameObject)")]
    [SerializeField] private ClubCardController       clubCardCtrl;
    [SerializeField] private BuffsController          buffsCtrl;
    [SerializeField] private FlipClockController      flipClockCtrl;
    [SerializeField] private PhaseBarController       phaseBarCtrl;
    [SerializeField] private CalendarController       calendarCtrl;
    [SerializeField] private BookInfoController       bookInfoCtrl;
    [SerializeField] private InventoryModalController inventoryCtrl;
    [SerializeField] private CatalogModalController   catalogCtrl;
    [SerializeField] private DecorModalController     decorCtrl;
    [SerializeField] private NPCModalController       npcCtrl;
    [SerializeField] private AwardsModalController    awardsCtrl;
    [SerializeField] private ToastController          toastCtrl;

    // Root & overlay
    private VisualElement _root;
    private VisualElement _overlay;

    // Modal map: logical id → VisualElement
    private System.Collections.Generic.Dictionary<string, VisualElement> _modals;
    private string        _activeModal;
    private VisualElement _activeButton;

    // Accessors for sub-controllers
    public VisualTreeAsset BuffCardTemplate             => buffCardTemplate;
    public VisualTreeAsset InventoryRowTemplate         => inventoryRowTemplate;
    public VisualTreeAsset CatalogBookCardTemplate      => catalogBookCardTemplate;
    public VisualTreeAsset CatalogAuthorSectionTemplate => catalogAuthorSectionTemplate;
    public VisualTreeAsset DecorItemCardTemplate        => decorItemCardTemplate;
    public BookInfoController BookInfo                  => bookInfoCtrl;
    public ToastController    Toast                     => toastCtrl;

    // ─────────────────────────────────────────────
    #region Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Auto-find sub-controllers
        clubCardCtrl  = clubCardCtrl  ?? GetComponent<ClubCardController>();
        buffsCtrl     = buffsCtrl     ?? GetComponent<BuffsController>();
        flipClockCtrl = flipClockCtrl ?? GetComponent<FlipClockController>();
        phaseBarCtrl  = phaseBarCtrl  ?? GetComponent<PhaseBarController>();
        calendarCtrl  = calendarCtrl  ?? GetComponent<CalendarController>();
        bookInfoCtrl  = bookInfoCtrl  ?? GetComponent<BookInfoController>();
        inventoryCtrl = inventoryCtrl ?? GetComponent<InventoryModalController>();
        catalogCtrl   = catalogCtrl   ?? GetComponent<CatalogModalController>();
        decorCtrl     = decorCtrl     ?? GetComponent<DecorModalController>();
        npcCtrl       = npcCtrl       ?? GetComponent<NPCModalController>();
        awardsCtrl    = awardsCtrl    ?? GetComponent<AwardsModalController>();
        toastCtrl     = toastCtrl     ?? GetComponent<ToastController>();
    }

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[BookshopUI] UIDocument not assigned!");
            return;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null) { Debug.LogError("[BookshopUI] Root is null"); return; }

        _overlay = _root.Q<VisualElement>("Overlay");

        // Modal elements — names match UXML
        _modals = new System.Collections.Generic.Dictionary<string, VisualElement>
        {
            { "inv",    _root.Q<VisualElement>("InvModal")    },
            { "cat",    _root.Q<VisualElement>("CatModal")    },
            { "decor",  _root.Q<VisualElement>("DecorModal")  },
            { "npc",    _root.Q<VisualElement>("NPCModal")    },
            { "awards", _root.Q<VisualElement>("AwardsModal") },
        };

        // Log missing modals
        foreach (var kv in _modals)
            if (kv.Value == null) Debug.LogWarning($"[BookshopUI] Modal '{kv.Key}' not found in UXML");

        BindFuncBar();
        BindCloseButtons();
        BindOverlay();

        // Init sub-controllers
        clubCardCtrl?.Initialize(_root, this);
        buffsCtrl?.Initialize(_root, this);
        flipClockCtrl?.Initialize(_root, this);
        phaseBarCtrl?.Initialize(_root, flipClockCtrl);
        calendarCtrl?.Initialize(_root);
        bookInfoCtrl?.Initialize(_root);
        inventoryCtrl?.Initialize(_root, this);
        catalogCtrl?.Initialize(_root, this);
        decorCtrl?.Initialize(_root, this);
        npcCtrl?.Initialize(_root, this);
        awardsCtrl?.Initialize(_root, this);
        toastCtrl?.Initialize(_root);

        CloseAllModals();
        Debug.Log("[BookshopUI] Initialized v3");
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            if (_activeModal != null) CloseModal(_activeModal);
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Modal management
    // ─────────────────────────────────────────────

    private void BindFuncBar()
    {
        BindModalButton("BtnInventory", "inv");
        BindModalButton("BtnCatalog",   "cat");
        BindModalButton("BtnDecor",     "decor");
        BindModalButton("BtnNPC",       "npc");
        BindModalButton("BtnAwards",    "awards");
    }

    private void BindModalButton(string btnName, string modalKey)
    {
        var btn = _root.Q<Button>(btnName);
        if (btn == null) { Debug.LogWarning($"[BookshopUI] Button '{btnName}' not found"); return; }

        btn.clicked += () =>
        {
            if (_modals.TryGetValue(modalKey, out var m) && m != null && !m.ClassListContains("hidden"))
                CloseModal(modalKey);
            else
                OpenModal(modalKey, btn);
        };
    }

    private void BindCloseButtons()
    {
        BindClose("InvCloseBtn",      "inv");
        BindClose("CatCloseBtn",      "cat");
        BindClose("DecorCloseBtn",    "decor");
        BindClose("NPCCloseBtn",      "npc");
        BindClose("AwardsCloseBtn",   "awards");
        BindClose("InvCloseStamp",    "inv");
        BindClose("CatCloseStamp",    "cat");
        BindClose("DecorCloseStamp",  "decor");
        BindClose("NPCCloseStamp",    "npc");
        BindClose("AwardsCloseStamp", "awards");
    }

    private void BindClose(string btnName, string modalKey)
    {
        var btn = _root.Q<Button>(btnName);
        if (btn != null) btn.clicked += () => CloseModal(modalKey);
    }

    private void BindOverlay()
    {
        // Clicking overlay only closes non-inventory modals
        _overlay?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_activeModal == "inv") return; // inventory overlay is light, don't auto-close
            CloseAllModals();
        });
    }

    public void OpenModal(string modalKey, VisualElement triggerBtn = null)
    {
        if (!_modals.TryGetValue(modalKey, out var modal) || modal == null)
        {
            Debug.LogWarning($"[BookshopUI] OpenModal: key '{modalKey}' not found");
            return;
        }

        // Close previous modal (unless same)
        if (_activeModal != null && _activeModal != modalKey)
            CloseModal(_activeModal);

        modal.RemoveFromClassList("hidden");

        // Overlay: lighter for inventory
        if (_overlay != null)
        {
            _overlay.RemoveFromClassList("hidden");
            _overlay.RemoveFromClassList("inv-open");
            if (modalKey == "inv")
                _overlay.AddToClassList("inv-open");
        }

        _activeModal = modalKey;

        _activeButton?.RemoveFromClassList("active");
        if (triggerBtn != null)
        {
            triggerBtn.AddToClassList("active");
            _activeButton = triggerBtn;
        }

        // Show book info card when inventory opens
        if (modalKey == "inv") bookInfoCtrl?.Show();

        Debug.Log($"[BookshopUI] Opened: {modalKey}");
    }

    public void CloseModal(string modalKey)
    {
        if (!_modals.TryGetValue(modalKey, out var modal) || modal == null) return;

        modal.AddToClassList("hidden");

        if (_activeModal == modalKey)
        {
            _activeModal = null;
            _overlay?.AddToClassList("hidden");
            _overlay?.RemoveFromClassList("inv-open");
        }

        _activeButton?.RemoveFromClassList("active");
        _activeButton = null;

        if (modalKey == "inv")    bookInfoCtrl?.Hide();
        if (modalKey == "decor")  decorCtrl?.CloseEditor();
    }

    public void CloseAllModals()
    {
        foreach (var kv in _modals) kv.Value?.AddToClassList("hidden");
        _overlay?.AddToClassList("hidden");
        _overlay?.RemoveFromClassList("inv-open");
        _activeModal = null;
        _activeButton?.RemoveFromClassList("active");
        _activeButton = null;
        bookInfoCtrl?.Hide();
    }

    #endregion

    // ─────────────────────────────────────────────
    #region External API (called from 3D world)
    // ─────────────────────────────────────────────

    /// Called by CabinetClickHandler / InteractionRouter when player opens a shelf.
    /// Opens inventory modal and optionally filters by shelf ID.
    ///
    /// Example usage:
    ///   BookshopUIController.Instance.OpenInventoryFromShelf("ShelfA");
    public void OpenInventoryFromShelf(string shelfId = null)
    {
        var invBtn = _root?.Q<Button>("BtnInventory");
        OpenModal("inv", invBtn);

        if (!string.IsNullOrEmpty(shelfId))
            inventoryCtrl?.FilterByShelf(shelfId);
        else
            inventoryCtrl?.ClearFilter();

        toastCtrl?.Show("📚", $"Shelf opened{(string.IsNullOrEmpty(shelfId) ? "" : $": {shelfId}")}", ToastType.Info);
    }

    /// Open inventory showing a specific book
    public void OpenInventoryFor(BookTemplate template)
    {
        var invBtn = _root?.Q<Button>("BtnInventory");
        OpenModal("inv", invBtn);
        bookInfoCtrl?.Show(template);
    }

    /// Open NPC dialogue panel
    public void OpenNPCDialog(BookHunterData npc)
    {
        OpenModal("npc");
        npcCtrl?.LoadNPC(npc);
    }

    /// Update shop name on club card front face
    public void SetShopName(string name)
    {
        clubCardCtrl?.SetShopName(name);
    }

    #endregion
}
