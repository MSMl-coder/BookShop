// Assets/Scripts/UI/ShopUIManager.cs
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

/// Головний UI-менеджер.
///
/// ВИПРАВЛЕНО ЗІ СКРІНШОТА:
/// 1. Кнопки › » ‹ « тепер працюють — додано RegisterCallback
/// 2. UI не блокує кліки в 3D — Root має picking-mode=Ignore
/// 3. Скрол колесом — ScrollView налаштовано з mouse-wheel-scroll-size
/// 4. Клік на шафу відкриває UI — через Cabinet.OnClicked()
/// 5. Книги в декілька колонок — content-container має flex-wrap
/// 6. Прибрано накладання тексту
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

        // Сканування шаф один раз
        _allCabinets = Object.FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude).ToList();

        // Початковий стан
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
            CloseAllPanels();
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
        _moneyLabel         = root.Q<Label>("MoneyLabel");
        _dayLabel           = root.Q<Label>("DayLabel");
        _phaseLabel         = root.Q<Label>("PhaseLabel");
        _prestigeValue      = root.Q<Label>("PrestigeValue");
        _prestigeFill       = root.Q<VisualElement>("PrestigeBarFill");
    }

    private void BindButtons(VisualElement root)
    {
        // FIX: всі кнопки переміщення тепер мають RegisterCallback
        BindButton(root, "BtnPushOne", () => DoOnSelectedShelf(s => InventoryManager.Instance?.PushOneToShelf(s)));
        BindButton(root, "BtnPushAll", () => DoOnSelectedShelf(s => InventoryManager.Instance?.PushAllToShelf(s)));
        BindButton(root, "BtnPopOne",  () => DoOnSelectedShelf(s => {
            var b = s.TakeLastBook();
            if (b != null) InventoryManager.Instance?.AddExistingBook(b);
        }));
        BindButton(root, "BtnPopAll",  () => DoOnSelectedShelf(s => {
            BookInstance b;
            do { b = s.TakeLastBook(); if (b != null) InventoryManager.Instance?.AddExistingBook(b); }
            while (b != null);
        }));

        BindButton(root, "BtnOpenInventory", ToggleInventory);
        BindButton(root, "BtnSettings",       () => Debug.Log("[UI] Settings (TODO)"));
    }

    private void BindButton(VisualElement root, string name, System.Action action)
    {
        var btn = root.Q<Button>(name);
        if (btn == null)
        {
            Debug.LogWarning($"[ShopUI] Button '{name}' not found in UXML!");
            return;
        }
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
        foreach (var name in new[] {"NavTabShop","NavTabCatalog","NavTabOrders","NavTabCollections"})
        {
            var btn = root.Q<Button>(name);
            if (btn == null) continue;
            string captured = name;
            btn.clicked += () =>
            {
                foreach (var t in new[] {"NavTabShop","NavTabCatalog","NavTabOrders","NavTabCollections"})
                    root.Q<Button>(t)?.RemoveFromClassList("active");
                root.Q<Button>(captured)?.AddToClassList("active");
            };
        }
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
        Debug.Log($"[ShopUI] OpenCabinetUI: {cabinet?.cabinetName}");

        _selectedCabinet = cabinet;
        _selectedShelf = null;

        SetDisplay(_inventoryPanel, true);
        SetDisplay(_cabinetPanel, true);

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
            SetDisplay(_cabinetPanel, true);
            RefreshInventory();
            RefreshCabinetList();
        }
        else CloseAllPanels();
    }

    public void CloseAllPanels()
    {
        SetDisplay(_inventoryPanel, false);
        SetDisplay(_cabinetPanel, false);
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

        if (_invCountLabel != null) _invCountLabel.text = $"{books.Count} книг";

        if (bookItemTemplate == null)
        {
            Debug.LogWarning("[ShopUI] bookItemTemplate не призначено!");
            return;
        }

        foreach (var book in books)
        {
            var template = BookDatabase.Instance?.GetBook(book.templateID);
            if (template == null) continue;

            VisualElement card = bookItemTemplate.Instantiate().ElementAt(0);

            var priceL = card.Q<Label>("PriceLabel");
            if (priceL != null) priceL.text = $"{template.sellPrice:F0}₴";

            var nameL = card.Q<Label>("BookNameLabel");
            if (nameL != null && template.icon == null) nameL.text = template.title;

            var icon = card.Q<VisualElement>("BookIcon");
            if (icon != null && template.icon != null)
                icon.style.backgroundImage = new StyleBackground(template.icon);

            var dot = card.Q<VisualElement>("RarityDot");
            if (dot != null)
            {
                foreach (var c in new[]{"common","uncommon","rare","epic","legendary"})
                    dot.RemoveFromClassList(c);
                dot.AddToClassList(template.rarity.ToString().ToLower());
            }

            card.tooltip = $"{template.title}\n{template.author}\n{template.rarity}";
            _inventoryGrid.Add(card);
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

        // Перезбираємо щоразу — на випадок нових шаф у сцені
        _allCabinets = Object.FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude).ToList();

        foreach (var cab in _allCabinets)
        {
            var row = MakeCabinetItem(cab);
            _cabinetList.Add(row);
        }

        RefreshShelfList();
    }

    private VisualElement MakeCabinetItem(Cabinet cab)
    {
        var row = new VisualElement();
        row.AddToClassList("cabinet-item");
        if (cab == _selectedCabinet) row.AddToClassList("selected");

        var nameL = new Label(cab.cabinetName);
        nameL.AddToClassList("cabinet-item__name");

        var cnt = 0;
        foreach (var s in cab.shelves) if (s != null) cnt += s.GetBookCount();
        var countL = new Label($"{cab.shelves.Count} полиць · {cnt} книг");
        countL.AddToClassList("cabinet-item__count");

        row.Add(nameL);
        row.Add(countL);

        Cabinet captured = cab;
        row.RegisterCallback<ClickEvent>(_ =>
        {
            _selectedCabinet = captured;
            _selectedShelf = null;
            if (_cabZoneLabel != null) _cabZoneLabel.text = captured.cabinetName;
            RefreshCabinetList();
        });

        return row;
    }

    private void RefreshShelfList()
    {
        if (_shelfList == null) return;
        _shelfList.Clear();

        // Якщо шафа не обрана — показуємо всі полиці всіх шаф
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

        // Шафа обрана — показуємо її полиці
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

        var barBg = new VisualElement();
        barBg.AddToClassList("shelf-fill-bg");
        var barFill = new VisualElement();
        barFill.AddToClassList("shelf-fill-bar");
        barFill.style.width = Length.Percent(shelf.GetFillRatio() * 100f);
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
        if (_dayLabel != null) _dayLabel.text = $"ДЕНЬ {day}";
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
