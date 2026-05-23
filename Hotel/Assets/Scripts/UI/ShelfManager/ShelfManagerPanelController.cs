// Assets/Scripts/UI/ShelfManager/ShelfManagerPanelController.cs
// FIX: CacheElements перенесено з OnEnable в Start —
//      UIDocument.rootVisualElement може бути null в OnEnable
//      якщо UIDocument ще не ініціалізований.
//
// Повна версія з усіма виправленнями.

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class ShelfManagerPanelController : MonoBehaviour
{
    public static ShelfManagerPanelController Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    // ── Visual Elements ──────────────────────────────────────────
    private VisualElement _panel;
    private VisualElement _invList;
    private VisualElement _genreFilterBar;
    private VisualElement _shelfTabs;
    private VisualElement _shelfViewContent;
    private VisualElement _selectedInfo;
    private VisualElement _selIcon;
    private Label         _selTitle;
    private Label         _selMeta;
    private Label         _cabinetName;
    private Label         _shelfName;
    private Label         _shelfInfo;
    private Label         _invCount;
    private Label         _pageNum;
    private Button        _btnToShelf;
    private Button        _btnAllToShelf;
    private Button        _btnToInv;
    private Button        _btnAllToInv;

    private bool _uiReady = false;

    // ── State ────────────────────────────────────────────────────
    private Cabinet      _cabinet;
    private int          _currentShelfIdx   = 0;
    private BookInstance _selectedInvBook   = null;
    private int          _selectedShelfIdx  = -1;
    private BookGenre?   _activeGenreFilter = null;
    private bool         _isOpen            = false;

    // ── Unity ────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // FIX: Start замість OnEnable — UIDocument гарантовано готовий
    private void Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[ShelfMgr] UIDocument не призначено!");
            return;
        }

        var root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[ShelfMgr] rootVisualElement == null!");
            return;
        }

        CacheElements(root);
        BindButtons(root);
        _uiReady = true;

        // Панель прихована на старті
        _panel?.AddToClassList("hidden");
        Debug.Log($"[ShelfMgr] Initialized. Panel found: {_panel != null}");
    }

    // ── Cache ────────────────────────────────────────────────────

    private void CacheElements(VisualElement root)
    {
        _panel          = root.Q("ShelfManagerPanel");
        _invList        = root.Q("ShelfMgrInvList");
        _genreFilterBar = root.Q("ShelfMgrGenreFilter");
        _shelfTabs      = root.Q("ShelfMgrShelfTabs");
        _selectedInfo   = root.Q("ShelfMgrSelectedInfo");
        _selIcon        = root.Q("ShelfMgrSelIcon");
        _selTitle       = root.Q<Label>("ShelfMgrSelTitle");
        _selMeta        = root.Q<Label>("ShelfMgrSelMeta");
        _cabinetName    = root.Q<Label>("ShelfMgrCabinetName");
        _shelfName      = root.Q<Label>("ShelfMgrShelfName");
        _shelfInfo      = root.Q<Label>("ShelfMgrShelfInfo");
        _invCount       = root.Q<Label>("ShelfMgrInvCount");
        _pageNum        = root.Q<Label>("ShelfMgrPageNum");
        _btnToShelf     = root.Q<Button>("BtnTransferOneToShelf");
        _btnAllToShelf  = root.Q<Button>("BtnTransferAllToShelf");
        _btnToInv       = root.Q<Button>("BtnTransferOneToInv");
        _btnAllToInv    = root.Q<Button>("BtnTransferAllToInv");

        // ShelfView content container
        var sv = root.Q<ScrollView>("ShelfMgrShelfView");
        if (sv != null)
            _shelfViewContent = sv.Q<VisualElement>(className: "unity-scroll-view__content-container") ?? sv;

        if (_panel == null)
            Debug.LogError("[ShelfMgr] 'ShelfManagerPanel' не знайдено! Перевір BookshopMainUI.uxml.");
        else
            Debug.Log("[ShelfMgr] Panel cached OK.");
    }

    private void BindButtons(VisualElement root)
    {
        root.Q<Button>("ShelfMgrCloseBtn")  ?.RegisterCallback<ClickEvent>(_ => Close());
        root.Q<Button>("ShelfMgrCloseStamp")?.RegisterCallback<ClickEvent>(_ => Close());
        _btnToShelf?   .RegisterCallback<ClickEvent>(_ => TransferOneToShelf());
        _btnAllToShelf?.RegisterCallback<ClickEvent>(_ => TransferAllToShelf());
        _btnToInv?     .RegisterCallback<ClickEvent>(_ => TransferOneToInv());
        _btnAllToInv?  .RegisterCallback<ClickEvent>(_ => TransferAllToInv());
    }

    // ── Public API ───────────────────────────────────────────────

    public void Open(Cabinet cabinet)
    {
        if (!_uiReady)
        {
            Debug.LogError("[ShelfMgr] Open() викликано до Start(). UI не готовий!");
            return;
        }
        if (_panel == null)
        {
            Debug.LogError("[ShelfMgr] _panel == null. Перевір що ShelfManagerPanel є в UXML.");
            return;
        }
        if (cabinet == null) return;

        _cabinet           = cabinet;
        _currentShelfIdx   = 0;
        _selectedInvBook   = null;
        _selectedShelfIdx  = -1;
        _activeGenreFilter = null;
        _isOpen            = true;

        if (_cabinetName != null)
            _cabinetName.text = (cabinet.cabinetName ?? "CABINET").ToUpper();

        _panel.RemoveFromClassList("hidden");

        Debug.Log($"[ShelfMgr] Opened for: {cabinet.cabinetName}");

        BuildGenreFilter();
        BuildShelfTabs();
        RefreshInvList();
        RefreshShelfView();
        UpdateTransferBtns();
        HideSelectedInfo();
    }

    public void Close()
    {
        _panel?.AddToClassList("hidden");
        _cabinet = null;
        _isOpen  = false;
        BookInfoCardController.Instance?.Hide();
    }

    public bool IsOpen => _isOpen;

    // ── Genre Filter ─────────────────────────────────────────────

    private void BuildGenreFilter()
    {
        if (_genreFilterBar == null) return;
        _genreFilterBar.Clear();

        var allBtn = new Button { text = "ALL" };
        allBtn.AddToClassList("shelf-mgr__genre-btn");
        if (_activeGenreFilter == null) allBtn.AddToClassList("active");
        allBtn.RegisterCallback<ClickEvent>(_ => SetGenreFilter(null));
        _genreFilterBar.Add(allBtn);

        foreach (BookGenre g in System.Enum.GetValues(typeof(BookGenre)))
        {
            var genre = g;
            string label = g.ToString().Length >= 3 ? g.ToString()[..3].ToUpper() : g.ToString().ToUpper();
            var btn = new Button { text = label, tooltip = g.ToString() };
            btn.AddToClassList("shelf-mgr__genre-btn");
            if (_activeGenreFilter == g) btn.AddToClassList("active");
            btn.RegisterCallback<ClickEvent>(_ => SetGenreFilter(genre));
            _genreFilterBar.Add(btn);
        }
    }

    private void SetGenreFilter(BookGenre? genre)
    {
        _activeGenreFilter = genre;
        if (_genreFilterBar == null) return;
        var btns = _genreFilterBar.Children().OfType<Button>().ToList();
        for (int i = 0; i < btns.Count; i++)
        {
            btns[i].RemoveFromClassList("active");
            bool isAll   = i == 0 && genre == null;
            bool isMatch = i > 0 && genre.HasValue &&
                (BookGenre)System.Enum.GetValues(typeof(BookGenre)).GetValue(i - 1) == genre.Value;
            if (isAll || isMatch) btns[i].AddToClassList("active");
        }
        RefreshInvList();
    }

    // ── Shelf Tabs ───────────────────────────────────────────────

    private void BuildShelfTabs()
    {
        if (_shelfTabs == null || _cabinet == null) return;
        _shelfTabs.Clear();

        for (int i = 0; i < _cabinet.shelves.Count; i++)
        {
            int   idx   = i;
            var   shelf = _cabinet.shelves[i];
            float fill  = shelf != null ? shelf.GetFillRatio() : 0f;

            var tab = new Button { text = $"S{i + 1}" };
            tab.AddToClassList("shelf-mgr__shelf-tab");
            if (i == _currentShelfIdx) tab.AddToClassList("active");
            tab.tooltip = shelf != null
                ? $"Полиця {i+1} · {shelf.GetBookCount()} книг · {fill:P0}"
                : $"Полиця {i+1}";

            tab.RegisterCallback<ClickEvent>(_ =>
            {
                _currentShelfIdx  = idx;
                _selectedShelfIdx = -1;
                RefreshShelfTabs();
                RefreshShelfView();
                UpdateTransferBtns();
                HideSelectedInfo();
            });
            _shelfTabs.Add(tab);
        }
        UpdatePageNum();
    }

    private void RefreshShelfTabs()
    {
        if (_shelfTabs == null) return;
        var tabs = _shelfTabs.Children().OfType<Button>().ToList();
        for (int i = 0; i < tabs.Count; i++)
        {
            tabs[i].RemoveFromClassList("active");
            if (i == _currentShelfIdx) tabs[i].AddToClassList("active");
            if (_cabinet != null && i < _cabinet.shelves.Count && _cabinet.shelves[i] != null)
            {
                var s = _cabinet.shelves[i];
                tabs[i].tooltip = $"Полиця {i+1} · {s.GetBookCount()} · {s.GetFillRatio():P0}";
            }
        }
        UpdatePageNum();
    }

    // ── Inventory List ───────────────────────────────────────────

    private void RefreshInvList()
    {
        if (_invList == null) return;
        _invList.Clear();

        var all = InventoryManager.Instance?.GetSortedInventory(SortType.ByTitle);
        if (all == null) { if (_invCount != null) _invCount.text = "0"; return; }

        var filtered = _activeGenreFilter.HasValue
            ? all.Where(b => BookDatabase.Instance?.GetBook(b.templateID)?.genre == _activeGenreFilter).ToList()
            : all;

        if (_invCount != null) _invCount.text = filtered.Count.ToString();

        foreach (var book in filtered)
        {
            var tpl = BookDatabase.Instance?.GetBook(book.templateID);
            if (tpl == null) continue;
            _invList.Add(BuildInvItem(book, tpl));
        }
    }

    private VisualElement BuildInvItem(BookInstance book, BookTemplate tpl)
    {
        var row = new VisualElement();
        row.AddToClassList("shelf-mgr__inv-item");

        var icon = new VisualElement();
        icon.AddToClassList("shelf-mgr__inv-item__icon");
        ApplyBookIcon(icon, tpl);

        var info  = new VisualElement(); info.AddToClassList("shelf-mgr__inv-item__info");
        var title = new Label(tpl.title); title.AddToClassList("shelf-mgr__inv-item__title");
        var meta  = new Label($"{tpl.rarity} · {tpl.genre} · ${tpl.sellPrice:F0}");
        meta.AddToClassList("shelf-mgr__inv-item__meta");
        info.Add(title); info.Add(meta);
        row.Add(icon); row.Add(info);

        var shelf = GetCurrentShelf();
        if (shelf != null && !shelf.CanFitBook(tpl))
        {
            row.style.opacity = 0.4f;
            row.tooltip = "Не вміщується на цю полицю";
        }

        var cb = book; var ct = tpl;
        row.RegisterCallback<MouseEnterEvent>(_ => BookInfoCardController.Instance?.Show(ct));
        row.RegisterCallback<MouseLeaveEvent>(_ => BookInfoCardController.Instance?.Hide());
        row.RegisterCallback<ClickEvent>(_ =>
        {
            _selectedInvBook  = cb;
            _selectedShelfIdx = -1;
            RefreshInvSelection();
            UpdateTransferBtns();
            ShowSelectedInfo(ct);
        });
        return row;
    }

    private void RefreshInvSelection()
    {
        if (_invList == null) return;
        var all = InventoryManager.Instance?.GetSortedInventory(SortType.ByTitle);
        if (all == null) return;
        var filtered = _activeGenreFilter.HasValue
            ? all.Where(b => BookDatabase.Instance?.GetBook(b.templateID)?.genre == _activeGenreFilter).ToList()
            : all;
        var items = _invList.Children().ToList();
        for (int i = 0; i < items.Count && i < filtered.Count; i++)
        {
            items[i].RemoveFromClassList("selected");
            if (_selectedInvBook != null && filtered[i].instanceID == _selectedInvBook.instanceID)
                items[i].AddToClassList("selected");
        }
    }

    // ── Shelf View ───────────────────────────────────────────────

    private void RefreshShelfView()
    {
        if (_shelfViewContent == null) return;
        _shelfViewContent.Clear();

        var shelf = GetCurrentShelf();
        if (shelf == null) return;

        var books  = shelf.GetAllBookData();
        int count  = books?.Count ?? 0;
        float fW   = shelf.GetFreeWidth();
        float tW   = shelf.GetShelfWorldWidth();

        if (_shelfName != null) _shelfName.text = $"Полиця {_currentShelfIdx + 1}";
        if (_shelfInfo != null) _shelfInfo.text = $"{count} книг · {(tW > 0 ? fW / tW * 100f : 0f):F0}% вільно";

        if (books != null)
        {
            for (int i = 0; i < books.Count; i++)
            {
                int idx = i; var e = books[i];
                var tpl = BookDatabase.Instance?.GetBook(e.templateID);
                if (tpl == null) continue;
                _shelfViewContent.Add(BuildSpine(idx, tpl, e.isReserved));
            }
        }

        // 2 placeholder-и якщо є місце
        if (fW > 0.03f)
        {
            for (int i = 0; i < 2; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("shelf-book-spine");
                slot.style.backgroundColor = new StyleColor(new Color(0.6f, 0.45f, 0.3f, 0.18f));
                _shelfViewContent.Add(slot);
            }
        }
        RefreshShelfTabs();
    }

    private VisualElement BuildSpine(int index, BookTemplate tpl, bool isReserved)
    {
        var spine = new VisualElement();
        spine.AddToClassList("shelf-book-spine");
        spine.style.backgroundColor = new StyleColor(GetRarityColor(tpl.rarity));
        if (index == _selectedShelfIdx) spine.AddToClassList("selected");
        if (isReserved) spine.style.opacity = 0.45f;

        var title = new Label(tpl.title);
        title.AddToClassList("shelf-book-spine__title");
        spine.Add(title);

        var ct = tpl; int ci = index;
        spine.RegisterCallback<MouseEnterEvent>(_ => BookInfoCardController.Instance?.Show(ct));
        spine.RegisterCallback<MouseLeaveEvent>(_ => BookInfoCardController.Instance?.Hide());
        spine.RegisterCallback<ClickEvent>(_ =>
        {
            if (isReserved) return;
            _selectedShelfIdx = ci; _selectedInvBook = null;
            RefreshSpineSelection(); RefreshInvSelection();
            UpdateTransferBtns(); ShowSelectedInfo(ct);
        });
        return spine;
    }

    private void RefreshSpineSelection()
    {
        if (_shelfViewContent == null) return;
        var spines = _shelfViewContent.Children()
            .Where(c => c.ClassListContains("shelf-book-spine")).ToList();
        for (int i = 0; i < spines.Count; i++)
        {
            spines[i].RemoveFromClassList("selected");
            if (i == _selectedShelfIdx) spines[i].AddToClassList("selected");
        }
    }

    // ── Transfer ─────────────────────────────────────────────────

    private void TransferOneToShelf()
    {
        if (_selectedInvBook == null) return;
        var shelf = GetCurrentShelf();
        var tpl   = BookDatabase.Instance?.GetBook(_selectedInvBook.templateID);
        if (shelf == null || tpl == null) return;
        if (!shelf.CanFitBook(tpl)) { Debug.Log("[ShelfMgr] Не вміщується."); return; }
        InventoryManager.Instance?.RemoveBook(_selectedInvBook);
        shelf.PlaceBook(_selectedInvBook);
        _selectedInvBook = null;
        HideSelectedInfo(); Refresh();
    }

    private void TransferAllToShelf()
    {
        var shelf = GetCurrentShelf();
        if (shelf == null) return;
        var all = InventoryManager.Instance?.GetSortedInventory(SortType.ByTitle);
        if (all == null) return;
        var toMove = _activeGenreFilter.HasValue
            ? all.Where(b => BookDatabase.Instance?.GetBook(b.templateID)?.genre == _activeGenreFilter).ToList()
            : new List<BookInstance>(all);
        int moved = 0;
        foreach (var book in toMove)
        {
            var tpl = BookDatabase.Instance?.GetBook(book.templateID);
            if (tpl == null || !shelf.CanFitBook(tpl)) continue;
            InventoryManager.Instance?.RemoveBook(book);
            shelf.PlaceBook(book);
            moved++;
        }
        if (moved > 0) Debug.Log($"[ShelfMgr] {moved} книг → полиця");
        HideSelectedInfo(); Refresh();
    }

    private void TransferOneToInv()
    {
        if (_selectedShelfIdx < 0) return;
        var shelf = GetCurrentShelf();
        if (shelf == null) return;
        var book = shelf.TakeBookAt(_selectedShelfIdx);
        if (book != null) InventoryManager.Instance?.AddExistingBook(book);
        _selectedShelfIdx = -1;
        HideSelectedInfo(); Refresh();
    }

    private void TransferAllToInv()
    {
        var shelf = GetCurrentShelf();
        if (shelf == null) return;
        for (int i = shelf.GetBookCount() - 1; i >= 0; i--)
        {
            var b = shelf.TakeBookAt(i);
            if (b != null) InventoryManager.Instance?.AddExistingBook(b);
        }
        _selectedShelfIdx = -1;
        HideSelectedInfo(); Refresh();
    }

    // ── Helpers ──────────────────────────────────────────────────

    private void Refresh() { RefreshInvList(); RefreshShelfView(); UpdateTransferBtns(); }

    private void UpdateTransferBtns()
    {
        var shelf = GetCurrentShelf();

        bool canToShelf = _selectedInvBook != null && shelf != null;
        if (canToShelf)
        {
            var tpl = BookDatabase.Instance?.GetBook(_selectedInvBook.templateID);
            canToShelf = tpl != null && shelf.CanFitBook(tpl);
        }
        _btnToShelf?.SetEnabled(canToShelf);

        var inv = InventoryManager.Instance?.GetSortedInventory(SortType.ByTitle);
        _btnAllToShelf?.SetEnabled(inv != null && inv.Count > 0 && shelf != null);
        _btnToInv     ?.SetEnabled(_selectedShelfIdx >= 0 && shelf != null);
        _btnAllToInv  ?.SetEnabled(shelf != null && shelf.GetBookCount() > 0);
    }

    private void ShowSelectedInfo(BookTemplate tpl)
    {
        if (_selectedInfo == null || tpl == null) return;
        _selectedInfo.RemoveFromClassList("hidden");
        if (_selIcon  != null && tpl.icon != null)
            _selIcon.style.backgroundImage = new StyleBackground(tpl.icon);
        if (_selTitle != null) _selTitle.text = tpl.title;
        if (_selMeta  != null) _selMeta.text  = $"{tpl.rarity} · {tpl.genre} · ${tpl.sellPrice:F0}";
    }

    private void HideSelectedInfo() => _selectedInfo?.AddToClassList("hidden");

    private void UpdatePageNum()
    {
        if (_pageNum == null || _cabinet == null) return;
        _pageNum.text = $"полиця {_currentShelfIdx + 1} / {_cabinet.shelves.Count}";
    }

    private Shelf GetCurrentShelf()
    {
        if (_cabinet == null || _currentShelfIdx >= _cabinet.shelves.Count) return null;
        return _cabinet.shelves[_currentShelfIdx];
    }

    // ── Static Helpers ───────────────────────────────────────────

    private static void ApplyBookIcon(VisualElement c, BookTemplate tpl)
    {
        if (c == null || tpl == null) return;
        c.Clear();
        if (tpl.icon != null) c.style.backgroundImage = new StyleBackground(tpl.icon);
        else c.Add(new Label(tpl.genre switch
        {
            BookGenre.Fantasy   => "🧙", BookGenre.Horror  => "💀",
            BookGenre.Mystery   => "🔍", BookGenre.Classic => "📜",
            BookGenre.SciFi     => "🚀", BookGenre.Biography => "👤",
            BookGenre.Academic  => "🎓", _ => "📖"
        }));
    }

    private static Color GetRarityColor(BookRarity r) => r switch
    {
        BookRarity.Common    => new Color(0.55f, 0.50f, 0.42f),
        BookRarity.Uncommon  => new Color(0.30f, 0.50f, 0.32f),
        BookRarity.Rare      => new Color(0.24f, 0.42f, 0.65f),
        BookRarity.Epic      => new Color(0.48f, 0.24f, 0.65f),
        BookRarity.Legendary => new Color(0.70f, 0.55f, 0.18f),
        _                    => new Color(0.55f, 0.50f, 0.42f)
    };
}