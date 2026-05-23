// Assets/Scripts/UI/ShopUIManager.cs
// ВИПРАВЛЕННЯ:
//   • FindElements — імена відповідають BookshopMainUI_patched.uxml (InvModal, InvList, etc.)
//   • ToggleInventory → показує InvModal через .hidden клас (не display)
//   • BtnInventory → "BtnInventory" в UXML (func-bar)
//   • InvModal picking-mode="Position" — не блокує world (вже в UXML)
//   • Hover BookInfoCard: підписка MouseEnter/MouseLeave на кожен inv-row

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

public class ShopUIManager : MonoBehaviour
{
    public static ShopUIManager Instance { get; private set; }

    [Header("UI Resources")]
    [SerializeField] private UIDocument uiDocument;

    // ── Modals ───────────────────────────────────────────────────
    private VisualElement _invModal;         // "InvModal"

    // ── Inventory Page ───────────────────────────────────────────
    private ScrollView    _invList;          // "InvList"
    private Label         _invPageTitle;     // "InvPageTitle"
    private Label         _invPageSubtitle;  // "InvPageSubtitle"
    private Label         _invTotal;         // "InvTotal"
    private Label         _invValue;         // "InvValue"
    private Label         _invPageNum;       // "InvPageNum"
    private SortType      _currentSort = SortType.ByTitle;

    // ── HUD ──────────────────────────────────────────────────────
    private Label         _moneyLabel;       // "MoneyLabel"
    private Label         _dayLabel;         // "DayLabel"
    private Label         _phaseLabel;       // "PhaseLabel"

    // ── State ────────────────────────────────────────────────────
    private bool _invOpen = false;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle
    // ─────────────────────────────────────────────────────────────

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

        CacheElements(root);
        BindFuncBar(root);
        BindSorts(root);
        SubscribeToManagers();

        // Початковий стан — всі панелі закриті
        HideAllModals();

        UpdateMoney(EconomyManager.Instance?.Money ?? 0);
        UpdatePhase(GameLoopManager.Instance?.CurrentState ?? GameState.Preparation);

        Debug.Log("[ShopUI] Initialized.");
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance   != null) InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        if (EconomyManager.Instance     != null) EconomyManager.Instance.OnMoneyChanged       -= UpdateMoney;
        if (GameLoopManager.Instance    != null) GameLoopManager.Instance.OnStateChanged      -= HandleStateChanged;
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            HideAllModals();
            BookInfoCardController.Instance?.Hide();
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Setup
    // ─────────────────────────────────────────────────────────────

    private void CacheElements(VisualElement root)
    {
        // Модалі (в ModalsLayer)
        _invModal        = root.Q("InvModal");

        // Інвентар — сторінка
        _invList         = root.Q<ScrollView>("InvList");
        _invPageTitle    = root.Q<Label>("InvPageTitle");
        _invPageSubtitle = root.Q<Label>("InvPageSubtitle");
        _invTotal        = root.Q<Label>("InvTotal");
        _invValue        = root.Q<Label>("InvValue");
        _invPageNum      = root.Q<Label>("InvPageNum");

        // HUD
        _moneyLabel = root.Q<Label>("MoneyLabel");
        _dayLabel   = root.Q<Label>("DayLabel");
        _phaseLabel = root.Q<Label>("PhaseLabel");

        // Діагностика
        if (_invModal  == null) Debug.LogError("[ShopUI] 'InvModal' не знайдено у UXML!");
        if (_invList   == null) Debug.LogError("[ShopUI] 'InvList' не знайдено у UXML!");
        if (_moneyLabel== null) Debug.LogWarning("[ShopUI] 'MoneyLabel' не знайдено.");
    }

    private void BindFuncBar(VisualElement root)
    {
        // FuncBar кнопки (імена з BookshopMainUI_patched.uxml)
        root.Q<Button>("BtnInventory")?.RegisterCallback<ClickEvent>(_ => ToggleInventory());
        root.Q<Button>("BtnCatalog")  ?.RegisterCallback<ClickEvent>(_ => Debug.Log("[ShopUI] Catalog TODO"));
        root.Q<Button>("BtnDecor")    ?.RegisterCallback<ClickEvent>(_ => DecorationPanelUI.Instance?.Toggle());
        root.Q<Button>("BtnNPC")      ?.RegisterCallback<ClickEvent>(_ => Debug.Log("[ShopUI] NPC list TODO"));
        root.Q<Button>("BtnAwards")   ?.RegisterCallback<ClickEvent>(_ => Debug.Log("[ShopUI] Awards TODO"));

        // Close кнопки в InvModal
        root.Q<Button>("InvCloseBtn")  ?.RegisterCallback<ClickEvent>(_ => CloseInventory());
        root.Q<Button>("InvCloseStamp")?.RegisterCallback<ClickEvent>(_ => CloseInventory());

        // Пагінація (поки просто заглушка)
        root.Q<Button>("BtnInvPrev")?.RegisterCallback<ClickEvent>(_ => { });
        root.Q<Button>("BtnInvNext")?.RegisterCallback<ClickEvent>(_ => { });

        // Settings
        root.Q<Button>("BtnSettings")?.RegisterCallback<ClickEvent>(_ =>
            SaveLoadPanelUI.Instance?.Open(SaveLoadMode.Save));

        // EditMode / Decoration
        root.Q<Button>("BtnOpenDecoration")?.RegisterCallback<ClickEvent>(_ =>
        {
            if (EditModeManager.Instance != null && EditModeManager.Instance.IsEditMode)
                DecorationPanelUI.Instance?.OpenInEditMode();
            else
                DecorationPanelUI.Instance?.Toggle();
        });
    }

    private void BindSorts(VisualElement root)
    {
        var sortBtns = new[] {"BtnSortTitle","BtnSortAuthor","BtnSortPrice","BtnSortRarity"};
        var sortTypes = new[] { SortType.ByTitle, SortType.ByAuthor, SortType.ByPrice, SortType.ByRarity };

        for (int i = 0; i < sortBtns.Length; i++)
        {
            int idx = i;
            var btn = root.Q<Button>(sortBtns[i]);
            if (btn == null) continue;
            btn.RegisterCallback<ClickEvent>(_ =>
            {
                _currentSort = sortTypes[idx];
                foreach (var n in sortBtns) root.Q<Button>(n)?.RemoveFromClassList("active");
                btn.AddToClassList("active");
                RefreshInventory();
            });
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

    // ─────────────────────────────────────────────────────────────
    #region Public API
    // ─────────────────────────────────────────────────────────────

    public void ToggleInventory()
    {
        if (_invOpen) CloseInventory();
        else          OpenInventory();
    }

    public void OpenInventory()
    {
        if (_invModal == null) { Debug.LogError("[ShopUI] InvModal == null!"); return; }

        _invOpen = true;
        _invModal.RemoveFromClassList("hidden");

        // Overlay — напівпрозорий (не блокує world)
        uiDocument.rootVisualElement.Q("Overlay")?.RemoveFromClassList("hidden");
        uiDocument.rootVisualElement.Q("Overlay")?.AddToClassList("inv-open");

        RefreshInventory();
    }

    public void CloseInventory()
    {
        _invOpen = false;
        _invModal?.AddToClassList("hidden");

        var overlay = uiDocument?.rootVisualElement.Q("Overlay");
        overlay?.AddToClassList("hidden");
        overlay?.RemoveFromClassList("inv-open");

        BookInfoCardController.Instance?.Hide();
    }

    public void HideAllModals()
    {
        CloseInventory();
        ShelfManagerPanelController.Instance?.Close();
        BookInfoCardController.Instance?.Hide();
    }

    /// Відкрити ShelfManagerPanel для конкретної шафи (виклик з ContextMenuUI)
    public void OpenShelfManager(Cabinet cabinet)
    {
        HideAllModals();
        ShelfManagerPanelController.Instance?.Open(cabinet);
    }

    /// Залишаємо для сумісності зі старим кодом
    public void OpenCabinetUI(Cabinet cabinet) => OpenShelfManager(cabinet);

    public void ShowBookInfo(BookTemplate template)  => BookInfoCardController.Instance?.Show(template);
    public void HideBookInfo()                       => BookInfoCardController.Instance?.Hide();

    public void UpdatePrestige(int current, int max)
    {
        // Prestige labels якщо є в UXML
        var root = uiDocument?.rootVisualElement;
        if (root == null) return;
        root.Q<Label>("PrestigeValue")?.text.ToString(); // no-op read
        if (root.Q<Label>("PrestigeValue") is Label pl) pl.text = $"{current} / {max}";
        float pct = max > 0 ? Mathf.Clamp01((float)current / max) * 100f : 0f;
        var fill = root.Q<VisualElement>("PrestigeBarFill");
        if (fill != null) fill.style.width = Length.Percent(pct);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Inventory Refresh
    // ─────────────────────────────────────────────────────────────

    private void RefreshInventory()
    {
        if (_invList == null) return;
        _invList.Clear();

        var books = InventoryManager.Instance?.GetSortedInventory(_currentSort);

        int   count = books?.Count ?? 0;
        float value = 0f;

        if (count == 0)
        {
            if (_invPageSubtitle != null) _invPageSubtitle.text = "Інвентар порожній";
            if (_invTotal        != null) _invTotal.text        = "0";
            if (_invValue        != null) _invValue.text        = "$0";
            return;
        }

        foreach (var book in books)
        {
            var template = BookDatabase.Instance?.GetBook(book.templateID);
            if (template == null) continue;

            value += template.sellPrice;
            var row = BuildInvRow(book, template);
            _invList.Add(row);
        }

        if (_invPageSubtitle != null) _invPageSubtitle.text = $"{count} книг у наявності";
        if (_invTotal        != null) _invTotal.text        = count.ToString();
        if (_invValue        != null) _invValue.text        = $"${value:F0}";
    }

    private VisualElement BuildInvRow(BookInstance book, BookTemplate template)
    {
        // Контейнер — inv-row з рарністю
        var row = new VisualElement();
        row.AddToClassList("inv-row");
        row.AddToClassList(RarityCss(template.rarity));

        // Іконка (Sprite або emoji)
        var icon = new VisualElement();
        icon.AddToClassList("inv-row__icon");
        ApplyBookIcon(icon, template);
        row.Add(icon);

        // Інфо
        var info = new VisualElement();
        info.AddToClassList("inv-row__info");

        var title = new Label(template.title);
        title.AddToClassList("inv-row__title");
        info.Add(title);

        var author = new Label(template.author ?? "");
        author.AddToClassList("inv-row__author");
        info.Add(author);

        var pills = new VisualElement();
        pills.AddToClassList("inv-row__pills");

        var genrePill = new Label(template.genre.ToString());
        genrePill.AddToClassList("inv-pill");
        genrePill.AddToClassList("inv-pill--genre");
        pills.Add(genrePill);

        var rarityPill = new Label(template.rarity.ToString());
        rarityPill.AddToClassList("inv-pill");
        rarityPill.AddToClassList(RarityCss(template.rarity));
        pills.Add(rarityPill);

        info.Add(pills);
        row.Add(info);

        // Ціна
        var priceBlock = new VisualElement();
        priceBlock.AddToClassList("inv-row__price-block");
        var price = new Label($"${template.sellPrice:F0}");
        price.AddToClassList("inv-row__price");
        priceBlock.Add(price);
        row.Add(priceBlock);

        // ── Hover → BookInfoCard ─────────────────────────────────
        var tpl = template;
        row.RegisterCallback<MouseEnterEvent>(_ => BookInfoCardController.Instance?.Show(tpl));
        row.RegisterCallback<MouseLeaveEvent>(_ => BookInfoCardController.Instance?.Hide());

        // ── Click ────────────────────────────────────────────────
        row.RegisterCallback<ClickEvent>(_ => BookInfoCardController.Instance?.Show(tpl));

        return row;
    }

    private static string RarityCss(BookRarity r) => r switch
    {
        BookRarity.Common    => "rc",
        BookRarity.Uncommon  => "ru",
        BookRarity.Rare      => "rr",
        BookRarity.Epic      => "re",
        BookRarity.Legendary => "rl",
        _                    => "rc"
    };

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region HUD
    // ─────────────────────────────────────────────────────────────

    private void UpdateMoney(int amount)
    {
        if (_moneyLabel != null) _moneyLabel.text = $"$ {amount:N0}";
    }

    private void UpdatePhase(GameState state)
    {
        string[] names = { "ПІДГОТОВКА", "ТОРГІВЛЯ", "НАГОРОДИ" };
        int day = GameLoopManager.Instance?.CurrentDay ?? 1;
        if (_dayLabel   != null) _dayLabel.text   = day.ToString("D2");
        if (_phaseLabel != null) _phaseLabel.text = (int)state < names.Length ? names[(int)state] : state.ToString();
    }

    private void HandleStateChanged(GameState state) => UpdatePhase(state);

    // ── Icon helper ──────────────────────────────────────────────
    private static void ApplyBookIcon(VisualElement container, BookTemplate tpl)
    {
        if (container == null || tpl == null) return;
        container.Clear();
        if (tpl.icon != null)
            container.style.backgroundImage = new StyleBackground(tpl.icon);
        else
        {
            string emoji = tpl.genre switch
            {
                BookGenre.Fantasy   => "🧙",
                BookGenre.Horror    => "💀",
                BookGenre.Mystery   => "🔍",
                BookGenre.Classic   => "📜",
                BookGenre.SciFi     => "🚀",
                BookGenre.Biography => "👤",
                BookGenre.Academic  => "🎓",
                _                   => "📖"
            };
            container.Add(new Label(emoji));
        }
    }
     #endregion
}