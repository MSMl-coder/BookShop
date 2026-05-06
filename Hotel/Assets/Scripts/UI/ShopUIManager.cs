// Assets/Scripts/UI/ShopUIManager.cs
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

/// ВИПРАВЛЕННЯ: прибрано .SetText() — такого методу немає в UI Toolkit.
/// Правильний спосіб: label.text = "value";
public class ShopUIManager : MonoBehaviour
{
    public static ShopUIManager Instance { get; private set; }

    [Header("UI Resources")]
    [SerializeField] private UIDocument      uiDocument;
    [SerializeField] private VisualTreeAsset bookItemTemplate;
    [SerializeField] private VisualTreeAsset lootCardTemplate;

    private bool    _isInventoryOpen = false;
    private Shelf   _selectedShelf;
    private Cabinet _selectedCabinet;
    private List<Cabinet> _allCabinets = new List<Cabinet>();
    private SortType _currentSort = SortType.ByTitle;

    // ── UI refs ──
    private VisualElement _inventoryPanel;
    private VisualElement _cabinetPanel;
    private VisualElement _leftPanel;
    private VisualElement _rightPanel;
    private VisualElement _endOfDayScreen;
    private VisualElement _lootContainer;
    private VisualElement _bookInfoCard;
    private VisualElement _tutorialBubble;
    private ScrollView    _inventoryGrid;
    private ScrollView    _cabinetList;
    private ScrollView    _shelfList;
    private Label         _selectedShelfLabel;

    // ══════════════════════════════════════════════
    #region Unity Lifecycle
    // ══════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void OnEnable()
    {
        if (uiDocument == null) { Debug.LogError("[ShopUI] UIDocument не призначено!"); return; }

        var root = uiDocument.rootVisualElement;

        _inventoryPanel     = root.Q<VisualElement>("InventoryPanel");
        _cabinetPanel       = root.Q<VisualElement>("CabinetPanel");
        _leftPanel          = root.Q<VisualElement>("LeftPanel");
        _rightPanel         = root.Q<VisualElement>("RightPanel");
        _endOfDayScreen     = root.Q<VisualElement>("EndDayPanel");
        _lootContainer      = root.Q<VisualElement>("LootContainer");
        _bookInfoCard       = root.Q<VisualElement>("BookInfoCard");
        _tutorialBubble     = root.Q<VisualElement>("TutorialBubble");
        _inventoryGrid      = root.Q<ScrollView>("InventoryGrid");
        _cabinetList        = root.Q<ScrollView>("CabinetList");
        _shelfList          = root.Q<ScrollView>("ShelfList");
        _selectedShelfLabel = root.Q<Label>("SelectedShelfLabel");

        // Головні кнопки
        root.Q<Button>("BtnOpenInventory")?.RegisterCallback<ClickEvent>(_ => ToggleInventory());
        root.Q<Button>("BtnSettings")?.RegisterCallback<ClickEvent>(_ => Debug.Log("[UI] Settings TODO"));
        root.Q<Button>("TutorialOkBtn")?.RegisterCallback<ClickEvent>(_ => OnTutorialOk());

        // Transfer (Footer або TransferRow)
        var footer = root.Q<VisualElement>("Footer") ?? root.Q<VisualElement>("TransferRow");
        footer?.Q<Button>("BtnPushOne")?.RegisterCallback<ClickEvent>(_ => PushOneToSelected());
        footer?.Q<Button>("BtnPushAll")?.RegisterCallback<ClickEvent>(_ => PushAllToSelected());
        footer?.Q<Button>("BtnPopOne") ?.RegisterCallback<ClickEvent>(_ => PopOneFromSelected());
        footer?.Q<Button>("BtnPopAll") ?.RegisterCallback<ClickEvent>(_ => PopAllFromSelected());

        // Sort tabs
        root.Q<Button>("BtnSortTitle") ?.RegisterCallback<ClickEvent>(_ => SetSort(SortType.ByTitle,  root));
        root.Q<Button>("BtnSortAuthor")?.RegisterCallback<ClickEvent>(_ => SetSort(SortType.ByAuthor, root));
        root.Q<Button>("BtnSortPrice") ?.RegisterCallback<ClickEvent>(_ => SetSort(SortType.ByPrice,  root));
        root.Q<Button>("BtnSortGenre") ?.RegisterCallback<ClickEvent>(_ => SetSort(SortType.ByGenre,  root));
        root.Q<Button>("BtnSortRarity")?.RegisterCallback<ClickEvent>(_ => SetSort(SortType.ByRarity, root));

        // Nav tabs
        foreach (var t in new[]{"NavTabShop","NavTabCatalog","NavTabOrders","NavTabCollections"})
            RegisterNavTab(root, t);

        // Events
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += RefreshInventory;
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += HandleGameStateChanged;
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnStepStarted += ShowTutorialBubble;

        _allCabinets = Object.FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude).ToList();
        RefreshCabinetList();
        CloseAllPanels();

        Debug.Log("[ShopUI] Ready.");
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleGameStateChanged;
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnStepStarted -= ShowTutorialBubble;
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            CloseAllPanels();
    }

    #endregion

    // ══════════════════════════════════════════════
    #region Public API
    // ══════════════════════════════════════════════

    public void OpenCabinetUI(Cabinet cabinet)
    {
        _selectedCabinet = cabinet;
        _selectedShelf   = null;
        _isInventoryOpen = true;

        SetVisible(_leftPanel,      true);
        SetVisible(_rightPanel,     true);
        SetVisible(_inventoryPanel, true);
        SetVisible(_cabinetPanel,   true);

        RefreshCabinetList();
        BuildShelfList(cabinet);
        RefreshInventory();
        Debug.Log($"[ShopUI] Cabinet: {cabinet.cabinetName}");
    }

    public void ToggleInventory()
    {
        _isInventoryOpen = !_isInventoryOpen;
        SetVisible(_leftPanel,      _isInventoryOpen);
        SetVisible(_rightPanel,     _isInventoryOpen);
        SetVisible(_inventoryPanel, _isInventoryOpen);
        SetVisible(_cabinetPanel,   _isInventoryOpen);
        if (_isInventoryOpen) RefreshInventory();
    }

    public void CloseAllPanels()
    {
        _isInventoryOpen = false;
        SetVisible(_leftPanel,      false);
        SetVisible(_rightPanel,     false);
        SetVisible(_inventoryPanel, false);
        SetVisible(_cabinetPanel,   false);
        SetVisible(_endOfDayScreen, false);
        if (_endOfDayScreen != null) _endOfDayScreen.pickingMode = PickingMode.Ignore;
    }

    public void ShowBookInfo(BookTemplate t)
    {
        if (_bookInfoCard == null || t == null) return;
        var root = uiDocument.rootVisualElement;

        // Правильно: label.text = "..." — НЕ .SetText()
        SetLabelText(root, "BiTitle",  t.title);
        SetLabelText(root, "BiAuthor", t.author);
        SetLabelText(root, "BiYear",   t.writingYear.ToString());
        SetLabelText(root, "BiGenre",  t.genre.ToString());
        SetLabelText(root, "BiPrice",  $"{t.sellPrice:F0} грн");

        var rl = root.Q<Label>("BiRarity");
        if (rl != null)
        {
            rl.text = t.rarity.ToString();
            foreach (var c in new[]{"common","uncommon","rare","epic","legendary"})
                rl.RemoveFromClassList(c);
            rl.AddToClassList(t.rarity.ToString().ToLower());
        }
        SetVisible(_bookInfoCard, true);
    }

    public void HideBookInfo() => SetVisible(_bookInfoCard, false);

    #endregion

    // ══════════════════════════════════════════════
    #region Inventory
    // ══════════════════════════════════════════════

    private void RefreshInventory()
    {
        if (_inventoryGrid == null) return;
        _inventoryGrid.Clear();

        var books = InventoryManager.Instance?.GetSortedInventory(_currentSort);
        if (books == null) return;

        // Лічильник — label.text, не SetText
        SetLabelText(uiDocument.rootVisualElement, "InvCount", $"{books.Count} книг");

        if (bookItemTemplate == null) { Debug.LogWarning("[ShopUI] bookItemTemplate не призначено!"); return; }

        foreach (var book in books)
        {
            var item = new InventoryItemUI(
                book, bookItemTemplate,
                onHoverEnter: tmpl => ShowBookInfo(tmpl),
                onHoverExit:  ()   => HideBookInfo()
            );
            if (item.Root != null) _inventoryGrid.Add(item.Root);
        }
    }

    private void SetSort(SortType sort, VisualElement root)
    {
        _currentSort = sort;
        RefreshInventory();

        // Highlight active tab
        var tabMap = new Dictionary<SortType, string>
        {
            {SortType.ByTitle,  "BtnSortTitle"},
            {SortType.ByAuthor, "BtnSortAuthor"},
            {SortType.ByPrice,  "BtnSortPrice"},
            {SortType.ByGenre,  "BtnSortGenre"},
            {SortType.ByRarity, "BtnSortRarity"},
        };
        foreach (var kv in tabMap)
        {
            var btn = root.Q<Button>(kv.Value);
            if (btn == null) continue;
            if (kv.Key == sort) btn.AddToClassList("active");
            else btn.RemoveFromClassList("active");
        }
    }

    #endregion

    // ══════════════════════════════════════════════
    #region Cabinet / Shelf
    // ══════════════════════════════════════════════

    private void RefreshCabinetList()
    {
        if (_cabinetList == null) return;
        _cabinetList.Clear();
        _allCabinets = Object.FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude).ToList();

        foreach (var cab in _allCabinets)
        {
            var row = new VisualElement();
            row.AddToClassList("cabinet-item");
            if (cab == _selectedCabinet) row.AddToClassList("selected");

            var nm = new Label(cab.cabinetName); nm.AddToClassList("cabinet-item__name");
            var ct = new Label($"{cab.shelves.Count} полиці"); ct.AddToClassList("cabinet-item__count");
            row.Add(nm); row.Add(ct);

            Cabinet cap = cab;
            row.RegisterCallback<ClickEvent>(_ => { _selectedCabinet = cap; _selectedShelf = null; RefreshCabinetList(); BuildShelfList(cap); });
            _cabinetList.Add(row);
        }
    }

    private void BuildShelfList(Cabinet cabinet)
    {
        if (_shelfList == null || cabinet == null) return;
        _shelfList.Clear();

        for (int i = 0; i < cabinet.shelves.Count; i++)
        {
            int   idx   = i;
            Shelf shelf = cabinet.shelves[i];

            var row = new VisualElement();
            row.AddToClassList("shelf-item");
            if (shelf == _selectedShelf) row.AddToClassList("selected");

            var nm = new Label($"Полиця {idx + 1}"); nm.AddToClassList("shelf-item__name");
            var bg = new VisualElement(); bg.AddToClassList("shelf-fill-bg");
            var fl = new VisualElement(); fl.AddToClassList("shelf-fill-bar");
            fl.style.width = Length.Percent(shelf.GetFillRatio() * 100f);
            bg.Add(fl);
            var ct = new Label($"{shelf.GetBookCount()} книг"); ct.AddToClassList("shelf-item__count");

            row.Add(nm); row.Add(bg); row.Add(ct);

            Shelf capShelf = shelf; int capIdx = idx;
            row.RegisterCallback<ClickEvent>(_ => SelectShelf(capShelf, capIdx + 1));
            _shelfList.Add(row);
        }
    }

    private void SelectShelf(Shelf shelf, int number)
    {
        _selectedShelf = shelf;
        if (_selectedShelfLabel != null)
            _selectedShelfLabel.text = $"{_selectedCabinet?.cabinetName} · Полиця {number}  [{shelf.GetBookCount()} книг]";
        BuildShelfList(_selectedCabinet);
    }

    private void PushOneToSelected()
    {
        if (_selectedShelf != null) InventoryManager.Instance?.PushOneToShelf(_selectedShelf);
        RefreshAfterTransfer();
    }

    private void PushAllToSelected()
    {
        if (_selectedShelf != null) InventoryManager.Instance?.PushAllToShelf(_selectedShelf);
        RefreshAfterTransfer();
    }

    private void PopOneFromSelected()
    {
        if (_selectedShelf == null) return;
        var b = _selectedShelf.TakeLastBook();
        if (b != null) InventoryManager.Instance?.AddExistingBook(b);
        RefreshAfterTransfer();
    }

    private void PopAllFromSelected()
    {
        if (_selectedShelf == null) return;
        BookInstance b;
        do { b = _selectedShelf.TakeLastBook(); if (b != null) InventoryManager.Instance?.AddExistingBook(b); } while (b != null);
        RefreshAfterTransfer();
    }

    private void RefreshAfterTransfer()
    {
        if (_selectedShelf != null && _selectedCabinet != null)
        {
            int n = _selectedCabinet.shelves.IndexOf(_selectedShelf) + 1;
            if (_selectedShelfLabel != null)
                _selectedShelfLabel.text = $"{_selectedCabinet.cabinetName} · Полиця {n}  [{_selectedShelf.GetBookCount()} книг]";
            BuildShelfList(_selectedCabinet);
        }
        RefreshInventory();
    }

    #endregion

    // ══════════════════════════════════════════════
    #region End of Day / Loot
    // ══════════════════════════════════════════════

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.LootPhase) { CloseAllPanels(); ShowEndOfDayScreen(true); }
        else ShowEndOfDayScreen(false);
    }

    private void ShowEndOfDayScreen(bool show)
    {
        SetVisible(_endOfDayScreen, show);
        if (_endOfDayScreen != null)
            _endOfDayScreen.pickingMode = show ? PickingMode.Position : PickingMode.Ignore;
        if (show) { RefreshLootStats(); RefreshLootCards(); }
    }

    private void RefreshLootStats()
    {
        if (_endOfDayScreen == null || EconomyManager.Instance == null) return;
        // label.text — правильно
        var sb = _endOfDayScreen.Q<Label>("StatBooks");
        var sm = _endOfDayScreen.Q<Label>("StatMoney");
        if (sb != null) sb.text = EconomyManager.Instance.BooksSoldToday.ToString();
        if (sm != null) sm.text = $"{EconomyManager.Instance.MoneyEarnedToday:F0} грн";
    }

    private void RefreshLootCards()
    {
        if (_lootContainer == null) { Debug.LogError("[ShopUI] LootContainer не знайдено!"); return; }
        if (lootCardTemplate == null) { Debug.LogError("[ShopUI] lootCardTemplate не призначено!"); return; }

        _lootContainer.Clear();
        if (LootManager.Instance == null) return;

        SetLabelText(uiDocument.rootVisualElement, "PicksLabel",
            $"Виборів залишилось: {LootManager.Instance.PicksRemaining}");

        foreach (var card in LootManager.Instance.GetCurrentPool() ?? new System.Collections.Generic.List<LootCardTemplate>())
        {
            var cardUI = lootCardTemplate.Instantiate();

            var nl = cardUI.Q<Label>("CardName"); if (nl != null) nl.text = card.cardName;
            var dl = cardUI.Q<Label>("CardDesc"); if (dl != null) dl.text = card.description ?? "";
            var cl = cardUI.Q<Label>("CardCost");
            if (cl != null) { cl.text = card.cost <= 0 ? "БЕЗКОШТОВНО" : $"{card.cost} грн"; if (card.cost <= 0) cl.AddToClassList("free"); }

            var btn = cardUI.Q<Button>("PickButton");
            if (btn != null)
            {
                bool ok = (EconomyManager.Instance?.Money >= card.cost) && (LootManager.Instance?.CanPick ?? false);
                btn.SetEnabled(ok);
                LootCardTemplate cap = card;
                btn.clicked += () => { if (cap.cost <= 0 || EconomyManager.Instance.SpendMoney(cap.cost)) { LootManager.Instance.SelectCard(cap); RefreshLootCards(); } };
            }
            _lootContainer.Add(cardUI);
        }
    }

    #endregion

    // ══════════════════════════════════════════════
    #region Tutorial
    // ══════════════════════════════════════════════

    private void ShowTutorialBubble(TutorialStep step)
    {
        if (_tutorialBubble == null) return;
        var tl = _tutorialBubble.Q<Label>("TutorialText");
        if (tl != null) tl.text = step.message; // .text — правильно
        SetVisible(_tutorialBubble, true);
    }

    private void OnTutorialOk()
    {
        TutorialManager.Instance?.CompleteCurrentStep();
        SetVisible(_tutorialBubble, false);
    }

    #endregion

    // ══════════════════════════════════════════════
    #region Helpers
    // ══════════════════════════════════════════════

    private void RegisterNavTab(VisualElement root, string name)
    {
        root.Q<Button>(name)?.RegisterCallback<ClickEvent>(_ =>
        {
            foreach (var t in new[]{"NavTabShop","NavTabCatalog","NavTabOrders","NavTabCollections"})
                root.Q<Button>(t)?.RemoveFromClassList("active");
            root.Q<Button>(name)?.AddToClassList("active");
        });
    }

    private static void SetVisible(VisualElement el, bool v)
    {
        if (el != null) el.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// Безпечна установка тексту Label.
    /// ВИКОРИСТОВУЙ ЦЕ ЗАМІСТЬ будь-якого .SetText() — такого методу немає в UI Toolkit!
    private static void SetLabelText(VisualElement root, string elName, string text)
    {
        var label = root?.Q<Label>(elName);
        if (label != null) label.text = text;
    }

    #endregion
}