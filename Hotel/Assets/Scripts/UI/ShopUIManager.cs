// Assets/Scripts/UI/ShopUIManager.cs
// ОНОВЛЕНО: додано ShowBookInfo/HideBookInfo, виправлено hud-chip,
//           покращено RefreshInventory з hover-підтримкою
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

/// Головний UI-менеджер магазину.
/// Керує панелями Інвентаря та Шафи.
/// HUD оновлюється через HUDController (окремий скрипт).
///
/// UNITY SETUP:
/// - UIDocument → MainShopUI.uxml
/// - bookItemTemplate → BookItem.uxml
/// - BookInfoCardController — на тому самому або окремому GameObject
public class ShopUIManager : MonoBehaviour
{
    public static ShopUIManager Instance { get; private set; }

    [Header("UI Resources")]
    [SerializeField] private UIDocument      uiDocument;
    [SerializeField] private VisualTreeAsset bookItemTemplate;

    // ── Panels ──
    private VisualElement _inventoryPanel;
    private VisualElement _cabinetPanel;

    // ── Inventory ──
    private ScrollView _inventoryGrid;
    private Label      _invCountLabel;
    private SortType   _currentSort = SortType.ByTitle;

    // ── Cabinet / Shelf ──
    private ScrollView    _cabinetList;
    private ScrollView    _shelfList;
    private Label         _selectedShelfLabel;
    private Label         _cabZoneLabel;
    private Cabinet       _selectedCabinet;
    private Shelf         _selectedShelf;
    private List<Cabinet> _allCabinets = new List<Cabinet>();

    // ── HUD ──
    private Label         _moneyLabel;
    private Label         _dayLabel;
    private Label         _phaseLabel;
    private Label         _prestigeValue;
    private VisualElement _prestigeFill;

    private static readonly string[] PhaseNames = { "ПІДГОТОВКА", "ТОРГІВЛЯ", "НАГОРОДИ" };

    // ─────────────────────────────────────────────
    #region Unity Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[ShopUI] UIDocument не призначено!");
            return;
        }

        var root = uiDocument.rootVisualElement;
        if (root == null) { Debug.LogWarning("[ShopUI] root = null"); return; }

        FindElements(root);
        BindButtons(root);
        BindSorts(root);
        BindNavTabs(root);
        SubscribeToManagers();

        _allCabinets = Object.FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude).ToList();

        CloseAllPanels();
        UpdateMoney(EconomyManager.Instance?.Money ?? 0);
        UpdatePhase(GameLoopManager.Instance?.CurrentState ?? GameState.Preparation);

        Debug.Log("[ShopUI] Initialized.");
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged -= UpdateMoney;
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            CloseAllPanels();
            BookInfoCardController.Instance?.Hide();
        }
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Setup
    // ─────────────────────────────────────────────

    private void FindElements(VisualElement root)
    {
        _inventoryPanel     = root.Q<VisualElement>("InventoryPanel");
        _cabinetPanel       = root.Q<VisualElement>("CabinetPanel");
        _inventoryGrid      = root.Q<ScrollView>("InventoryGrid");
        _invCountLabel      = root.Q<Label>("InvCount");
        _cabinetList        = root.Q<ScrollView>("CabinetList");
        _shelfList          = root.Q<ScrollView>("ShelfList");
        _selectedShelfLabel = root.Q<Label>("SelectedShelfLabel");
        _cabZoneLabel       = root.Q<Label>("CabZoneLabel");

        // HUD
        _moneyLabel    = root.Q<Label>("MoneyLabel");
        _dayLabel      = root.Q<Label>("DayLabel");
        _phaseLabel    = root.Q<Label>("PhaseLabel");
        _prestigeValue = root.Q<Label>("PrestigeValue");
        _prestigeFill  = root.Q<VisualElement>("PrestigeBarFill");
    }

    private void BindButtons(VisualElement root)
    {
        BindButton(root, "BtnPushOne", () => DoOnSelectedShelf(s => InventoryManager.Instance?.PushOneToShelf(s)));
        BindButton(root, "BtnPushAll", () => DoOnSelectedShelf(s => InventoryManager.Instance?.PushAllToShelf(s)));
        BindButton(root, "BtnPopOne",  () => DoOnSelectedShelf(s =>
        {
            var b = s.TakeLastBook();
            if (b != null) InventoryManager.Instance?.AddExistingBook(b);
        }));
        BindButton(root, "BtnPopAll",  () => DoOnSelectedShelf(s =>
        {
            BookInstance b;
            do { b = s.TakeLastBook(); if (b != null) InventoryManager.Instance?.AddExistingBook(b); }
            while (b != null);
        }));

        BindButton(root, "BtnOpenInventory", ToggleInventory);
        BindButton(root, "BtnSettings", () => SaveLoadPanelUI.Instance?.Open(SaveLoadMode.Save));
        BindButton(root, "BtnOpenDecoration", () => DecorationPanelUI.Instance?.Toggle());
    }

    private void BindButton(VisualElement root, string name, System.Action action)
    {
        var btn = root.Q<Button>(name);
        if (btn == null) { Debug.LogWarning($"[ShopUI] Button '{name}' not found."); return; }
        btn.clicked += action;
    }

    private void BindSorts(VisualElement root)
    {
        BindSort(root, "BtnSortTitle",  SortType.ByTitle);
        BindSort(root, "BtnSortAuthor", SortType.ByAuthor);
        BindSort(root, "BtnSortPrice",  SortType.ByPrice);
        BindSort(root, "BtnSortGenre",  SortType.ByGenre);
        BindSort(root, "BtnSortRarity", SortType.ByRarity);
    }

    private void BindSort(VisualElement root, string btnName, SortType sort)
    {
        var btn = root.Q<Button>(btnName);
        if (btn == null) return;
        btn.clicked += () =>
        {
            _currentSort = sort;
            foreach (var n in new[] {"BtnSortTitle","BtnSortAuthor","BtnSortPrice","BtnSortGenre","BtnSortRarity"})
                root.Q<Button>(n)?.RemoveFromClassList("active");
            btn.AddToClassList("active");
            RefreshInventory();
        };
    }

    private void BindNavTabs(VisualElement root)
    {
        string[] tabs = { "NavTabShop", "NavTabCatalog", "NavTabOrders", "NavTabCollections" };
        foreach (var name in tabs)
        {
            var btn = root.Q<Button>(name);
            if (btn == null) continue;
            string captured = name;
            btn.clicked += () =>
            {
                foreach (var t in tabs) root.Q<Button>(t)?.RemoveFromClassList("active");
                root.Q<Button>(captured)?.AddToClassList("active");
                OnNavTabChanged(captured);
            };
        }
    }

    private void OnNavTabChanged(string tabName)
    {
        // TODO: в майбутньому — перемикати вміст інвентаря між вкладками
        Debug.Log($"[ShopUI] Nav tab: {tabName}");
    }

    private void SubscribeToManagers()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += RefreshInventory;
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged += UpdateMoney;
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += HandleStateChanged;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Public API
    // ─────────────────────────────────────────────

    public void OpenCabinetUI(Cabinet cabinet)
    {
        _selectedCabinet = cabinet;
        _selectedShelf   = null;

        SetDisplay(_inventoryPanel, true);
        SetDisplay(_cabinetPanel,   true);

        if (_cabZoneLabel != null && cabinet != null)
            _cabZoneLabel.text = cabinet.cabinetName;

        RefreshInventory();
        RefreshCabinetList();
    }

    public void ToggleInventory()
    {
        bool isHidden = _inventoryPanel?.style.display == DisplayStyle.None;
        if (isHidden)
        {
            SetDisplay(_inventoryPanel, true);
            SetDisplay(_cabinetPanel,   true);
            RefreshInventory();
            RefreshCabinetList();
        }
        else
        {
            CloseAllPanels();
        }
    }

    public void CloseAllPanels()
    {
        SetDisplay(_inventoryPanel, false);
        SetDisplay(_cabinetPanel,   false);
        BookInfoCardController.Instance?.Hide();
    }

    /// Викликається з InventoryPanelUI та BookInfoCard hover
    public void ShowBookInfo(BookTemplate template)
    {
        BookInfoCardController.Instance?.Show(template);
    }

    /// Викликається при виході миші з картки книги
    public void HideBookInfo()
    {
        BookInfoCardController.Instance?.Hide();
    }

    /// Оновити престиж (зовнішній виклик)
    public void UpdatePrestige(int current, int max)
    {
        if (_prestigeValue != null)
            _prestigeValue.text = $"{current} / {max}";

        if (_prestigeFill != null)
        {
            float pct = max > 0 ? Mathf.Clamp01((float)current / max) * 100f : 0f;
            _prestigeFill.style.width = Length.Percent(pct);
        }
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Inventory
    // ─────────────────────────────────────────────

    private void RefreshInventory()
    {
        if (_inventoryGrid == null) return;
        _inventoryGrid.Clear();

        var books = InventoryManager.Instance?.GetSortedInventory(_currentSort);
        if (books == null) return;

        if (_invCountLabel != null)
            _invCountLabel.text = $"{books.Count} книг";

        if (bookItemTemplate == null)
        {
            Debug.LogWarning("[ShopUI] bookItemTemplate не призначено!");
            return;
        }

        foreach (var book in books)
        {
            var template = BookDatabase.Instance?.GetBook(book.templateID);
            if (template == null) continue;

            // Використовуємо InventoryItemUI для правильного заповнення
            var item = new InventoryItemUI(
                book,
                bookItemTemplate,
                onHoverEnter: t => ShowBookInfo(t),   // ← ВИПРАВЛЕНО: тепер працює
                onHoverExit:  () => HideBookInfo()
            );

            if (item.Root != null)
            {
                // Підписка на клік → виділення в UI
                item.OnClicked += (inst, tmpl) =>
                {
                    Debug.Log($"[ShopUI] Обрано: {tmpl.title}");
                    ShowBookInfo(tmpl);
                };
                _inventoryGrid.Add(item.Root);
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Cabinet / Shelf List
    // ─────────────────────────────────────────────

    private void RefreshCabinetList()
    {
        if (_cabinetList == null) return;
        _cabinetList.Clear();

        _allCabinets = Object.FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude).ToList();

        foreach (var cab in _allCabinets)
            _cabinetList.Add(MakeCabinetItem(cab));

        RefreshShelfList();
    }

    private VisualElement MakeCabinetItem(Cabinet cab)
    {
        var row = new VisualElement();
        row.AddToClassList("cabinet-item");
        if (cab == _selectedCabinet) row.AddToClassList("selected");

        var nameL = new Label(cab.cabinetName);
        nameL.AddToClassList("cabinet-item__name");

        int totalBooks = 0;
        foreach (var s in cab.shelves) if (s != null) totalBooks += s.GetBookCount();

        var countL = new Label($"{cab.shelves.Count} полиць · {totalBooks} книг");
        countL.AddToClassList("cabinet-item__count");

        row.Add(nameL);
        row.Add(countL);

        Cabinet captured = cab;
        row.RegisterCallback<ClickEvent>(_ =>
        {
            _selectedCabinet = captured;
            _selectedShelf   = null;
            if (_cabZoneLabel != null) _cabZoneLabel.text = captured.cabinetName;
            RefreshCabinetList();
        });

        return row;
    }

    private void RefreshShelfList()
    {
        if (_shelfList == null) return;
        _shelfList.Clear();

        if (_selectedCabinet == null)
        {
            foreach (var cab in _allCabinets)
                foreach (var shelf in cab.shelves)
                    if (shelf != null)
                        _shelfList.Add(MakeShelfItem(shelf, cab.cabinetName));

            if (_selectedShelfLabel != null)
                _selectedShelfLabel.text = "Оберіть шафу для роботи з полицями";
            return;
        }

        for (int i = 0; i < _selectedCabinet.shelves.Count; i++)
        {
            var shelf = _selectedCabinet.shelves[i];
            if (shelf == null) continue;
            _shelfList.Add(MakeShelfItem(shelf, $"Полиця {i + 1}"));
        }

        if (_selectedShelfLabel != null)
            _selectedShelfLabel.text = _selectedShelf == null
                ? $"{_selectedCabinet.cabinetName} — оберіть полицю"
                : $"{_selectedCabinet.cabinetName} · {_selectedShelf.GetBookCount()} книг";
    }

    private VisualElement MakeShelfItem(Shelf shelf, string displayName)
    {
        var row = new VisualElement();
        row.AddToClassList("shelf-item");
        if (shelf == _selectedShelf) row.AddToClassList("selected");

        var nameL = new Label(displayName);
        nameL.AddToClassList("shelf-item__name");

        // Fill bar
        var barBg   = new VisualElement(); barBg.AddToClassList("shelf-fill-bg");
        var barFill = new VisualElement(); barFill.AddToClassList("shelf-fill-bar");
        float ratio = shelf.GetFillRatio();
        barFill.style.width = Length.Percent(ratio * 100f);
        if (ratio >= 0.95f) barFill.AddToClassList("full");
        barBg.Add(barFill);

        var cntL = new Label($"{shelf.GetBookCount()} книг");
        cntL.AddToClassList("shelf-item__count");

        row.Add(nameL);
        row.Add(barBg);
        row.Add(cntL);

        Shelf captured = shelf;
        row.RegisterCallback<ClickEvent>(_ =>
        {
            _selectedShelf = captured;
            RefreshShelfList();
        });

        return row;
    }

    private void DoOnSelectedShelf(System.Action<Shelf> action)
    {
        if (_selectedShelf == null)
        {
            Debug.LogWarning("[ShopUI] Полиця не обрана!");
            return;
        }
        action(_selectedShelf);
        RefreshShelfList();
    }

    #endregion

    // ─────────────────────────────────────────────
    #region HUD Updates
    // ─────────────────────────────────────────────

    private void UpdateMoney(int amount)
    {
        if (_moneyLabel != null) _moneyLabel.text = amount.ToString("N0");
    }

    private void UpdatePhase(GameState state)
    {
        int day = GameLoopManager.Instance?.CurrentDay ?? 1;
        if (_dayLabel != null)   _dayLabel.text   = $"ДЕНЬ {day}";
        if (_phaseLabel != null) _phaseLabel.text =
            (int)state < PhaseNames.Length ? PhaseNames[(int)state] : state.ToString();
    }

    private void HandleStateChanged(GameState state) => UpdatePhase(state);

    #endregion

    // ─────────────────────────────────────────────
    private static void SetDisplay(VisualElement el, bool show)
    {
        if (el != null) el.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
