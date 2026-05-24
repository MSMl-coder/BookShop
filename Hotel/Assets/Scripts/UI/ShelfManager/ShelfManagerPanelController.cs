// Assets/Scripts/UI/ShelfManager/ShelfManagerPanelController.cs  v2.4
// ФІКСИ кнопок:
//   › — після Transfer скидаємо sel коректно, індекс не збивається
//   » — snapshot списку до ітерації, RemoveBook не змінює колекцію під час foreach
//   ‹ — TakeBookAt за правильним індексом, перевіряємо bounds
//   « — рахуємо GetBookCount() до циклу, не після кожного TakeBookAt

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class ShelfManagerPanelController : MonoBehaviour
{
    public static ShelfManagerPanelController Instance { get; private set; }
    [SerializeField] private UIDocument uiDocument;

    // ── VE ─────────────────────────────────────────────────────
    private VisualElement _panel;
    private VisualElement _invList;
    private VisualElement _genreFilter;
    private VisualElement _allShelvesContent;
    private VisualElement _selectedInfo;
    private VisualElement _selIcon;
    private Label         _selTitle, _selMeta, _shelfInfo, _invCount;
    private Button        _btnToShelf, _btnAllToShelf, _btnToInv, _btnAllToInv;
    private bool          _uiReady;

    // ── State ───────────────────────────────────────────────────
    private Cabinet      _cabinet;
    private BookInstance _selectedInvBook  = null;
    private int          _targetShelfRow   = 0;   // ціль для › і »
    private int          _selectedShelfRow = -1;  // полиця вибраної книги з полиці
    private int          _selectedShelfIdx = -1;  // індекс книги в shelf._books
    private BookGenre?   _genreActive      = null;
    private bool         _isOpen;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (uiDocument == null) { Debug.LogError("[ShelfMgr] UIDocument не призначено!"); return; }
        var root = uiDocument.rootVisualElement;
        if (root == null) return;
        Cache(root);
        BindBtns(root);
        _uiReady = true;
        _panel?.AddToClassList("hidden");
        Debug.Log($"[ShelfMgr] Ready. Panel={_panel != null}");
    }

    // ── Cache ───────────────────────────────────────────────────
    private void Cache(VisualElement root)
    {
        _panel        = root.Q("ShelfManagerPanel");
        _invList      = root.Q("ShelfMgrInvList");
        _genreFilter  = root.Q("ShelfMgrGenreFilter");
        _selectedInfo = root.Q("ShelfMgrSelectedInfo");
        _selIcon      = root.Q("ShelfMgrSelIcon");
        _selTitle     = root.Q<Label>("ShelfMgrSelTitle");
        _selMeta      = root.Q<Label>("ShelfMgrSelMeta");
        _shelfInfo    = root.Q<Label>("ShelfMgrShelfInfo");
        _invCount     = root.Q<Label>("ShelfMgrInvCount");
        _btnToShelf   = root.Q<Button>("BtnTransferOneToShelf");
        _btnAllToShelf= root.Q<Button>("BtnTransferAllToShelf");
        _btnToInv     = root.Q<Button>("BtnTransferOneToInv");
        _btnAllToInv  = root.Q<Button>("BtnTransferAllToInv");

        var sv = root.Q<ScrollView>("ShelfMgrAllShelvesView");
        _allShelvesContent = sv?.Q<VisualElement>(className:"unity-scroll-view__content-container") ?? sv;

        if (_panel == null) Debug.LogError("[ShelfMgr] ShelfManagerPanel не знайдено в UXML!");
    }

    private void BindBtns(VisualElement root)
    {
        root.Q<Button>("ShelfMgrCloseBtn")  ?.RegisterCallback<ClickEvent>(_ => Close());
        root.Q<Button>("ShelfMgrCloseStamp")?.RegisterCallback<ClickEvent>(_ => Close());
        _btnToShelf?   .RegisterCallback<ClickEvent>(_ => TransferOneToShelf());
        _btnAllToShelf?.RegisterCallback<ClickEvent>(_ => TransferAllToShelf());
        _btnToInv?     .RegisterCallback<ClickEvent>(_ => TransferOneToInv());
        _btnAllToInv?  .RegisterCallback<ClickEvent>(_ => TransferAllToInv());
    }

    // ── Public ──────────────────────────────────────────────────
    public bool IsOpen => _isOpen;

    public void Open(Cabinet cabinet)
    {
        if (!_uiReady || _panel == null || cabinet == null) return;

        _cabinet         = cabinet;
        _selectedInvBook = null;
        _targetShelfRow  = 0;
        _selectedShelfRow= -1;
        _selectedShelfIdx= -1;
        _genreActive     = null;
        _isOpen          = true;

        if (_shelfInfo != null) _shelfInfo.text = "";

        _panel.style.display = DisplayStyle.Flex;
        _panel.RemoveFromClassList("hidden");

        BuildGenreFilter();
        RefreshInvList();
        RefreshAllShelves();
        UpdateBtns();
        HideSel();

        Debug.Log($"[ShelfMgr] Opened: {cabinet.cabinetName}");
    }

    public void Close()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
        _panel?.AddToClassList("hidden");
        _cabinet = null; _isOpen = false;
        BookInfoCardController.Instance?.Hide();
    }

    // ── Genre filter ────────────────────────────────────────────
    private void BuildGenreFilter()
    {
        if (_genreFilter == null) return;
        _genreFilter.Clear();
        AddGenreBtn("ALL", null);
        foreach (BookGenre g in System.Enum.GetValues(typeof(BookGenre)))
        {
            var genre = g;
            string lbl = g.ToString().Length >= 3 ? g.ToString()[..3].ToUpper() : g.ToString().ToUpper();
            AddGenreBtn(lbl, genre, g.ToString());
        }
    }

    private void AddGenreBtn(string text, BookGenre? genre, string tip = "")
    {
        var btn = new Button { text = text };
        if (!string.IsNullOrEmpty(tip)) btn.tooltip = tip;
        btn.AddToClassList("shelf-mgr__genre-btn");
        if (_genreActive == genre) btn.AddToClassList("active");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            _genreActive = genre;
            RefreshGenreBtns();
            RefreshInvList();
        });
        _genreFilter.Add(btn);
    }

    private void RefreshGenreBtns()
    {
        if (_genreFilter == null) return;
        var btns  = _genreFilter.Children().OfType<Button>().ToList();
        var genres = System.Enum.GetValues(typeof(BookGenre));
        for (int i = 0; i < btns.Count; i++)
        {
            btns[i].RemoveFromClassList("active");
            bool match = i == 0 ? _genreActive == null
                : (_genreActive.HasValue && i-1 < genres.Length
                   && (BookGenre)genres.GetValue(i-1) == _genreActive.Value);
            if (match) btns[i].AddToClassList("active");
        }
    }

    // ── Inventory ───────────────────────────────────────────────
    private void RefreshInvList()
    {
        if (_invList == null) return;
        _invList.Clear();

        var all = InventoryManager.Instance?.GetSortedInventory(SortType.ByTitle);
        if (all == null) { if (_invCount != null) _invCount.text = "0"; return; }

        var list = _genreActive.HasValue
            ? all.Where(b => GetTpl(b)?.genre == _genreActive).ToList() : all;

        if (_invCount != null) _invCount.text = list.Count.ToString();
        foreach (var book in list)
        {
            var tpl = GetTpl(book);
            if (tpl != null) _invList.Add(BuildInvRow(book, tpl));
        }
    }

    private VisualElement BuildInvRow(BookInstance book, BookTemplate tpl)
    {
        var row = new VisualElement();
        row.AddToClassList("shelf-mgr__inv-item");

        var icon = new VisualElement(); icon.AddToClassList("shelf-mgr__inv-item__icon");
        ApplyIcon(icon, tpl);

        var info = new VisualElement(); info.AddToClassList("shelf-mgr__inv-item__info");
        var ttl  = new Label(tpl.title); ttl.AddToClassList("shelf-mgr__inv-item__title");
        var meta = new Label($"{tpl.rarity} · ${tpl.sellPrice:F0}");
        meta.AddToClassList("shelf-mgr__inv-item__meta");
        info.Add(ttl); info.Add(meta);
        row.Add(icon); row.Add(info);

        var cb = book; var ct = tpl;
        row.RegisterCallback<MouseEnterEvent>(_ => BookInfoCardController.Instance?.Show(ct));
        row.RegisterCallback<MouseLeaveEvent>(_ => BookInfoCardController.Instance?.Hide());
        row.RegisterCallback<ClickEvent>(_ =>
        {
            _selectedInvBook  = cb;
            _selectedShelfRow = -1;
            _selectedShelfIdx = -1;
            RefreshInvSel();
            RefreshSpineSel();
            UpdateBtns();
            ShowSel(ct);
        });
        return row;
    }

    private void RefreshInvSel()
    {
        if (_invList == null) return;
        foreach (var c in _invList.Children()) c.RemoveFromClassList("selected");
        if (_selectedInvBook == null) return;

        var all = InventoryManager.Instance?.GetSortedInventory(SortType.ByTitle);
        if (all == null) return;
        var list = _genreActive.HasValue
            ? all.Where(b => GetTpl(b)?.genre == _genreActive).ToList() : all;
        var items = _invList.Children().ToList();
        for (int i = 0; i < items.Count && i < list.Count; i++)
            if (list[i].instanceID == _selectedInvBook.instanceID)
                items[i].AddToClassList("selected");
    }

    // ── All shelves ─────────────────────────────────────────────
    private void RefreshAllShelves()
    {
        if (_allShelvesContent == null || _cabinet == null) return;
        _allShelvesContent.Clear();

        for (int si = 0; si < _cabinet.shelves.Count; si++)
        {
            var shelf = _cabinet.shelves[si];
            if (shelf == null) continue;

            int   idx    = si;
            var   books  = shelf.GetAllBookData();
            int   count  = books?.Count ?? 0;
            float freeW  = shelf.GetFreeWidth();
            float totW   = shelf.GetShelfWorldWidth();
            float freePct= totW > 0 ? freeW / totW * 100f : 0f;

            // Рядок: [бірка | вміст]
            var row = new VisualElement();
            row.AddToClassList("shelf-mgr-v2__shelf-row");
            if (idx == _targetShelfRow) row.AddToClassList("selected");

            // ── Бірка ──────────────────────────────────────────
            var tag = new VisualElement();
            tag.AddToClassList("shelf-mgr-v2__shelf-tag");
            if (idx == _targetShelfRow) tag.AddToClassList("selected-tag");

            var hole = new VisualElement();
            hole.AddToClassList("shelf-mgr-v2__shelf-tag-hole");

            var tagNum = new Label($"S{si+1}");
            tagNum.AddToClassList("shelf-mgr-v2__shelf-tag-num");

            tag.Add(hole);
            tag.Add(tagNum);
            tag.RegisterCallback<ClickEvent>(_ =>
            {
                _targetShelfRow = idx;
                RefreshAllShelves();
                UpdateBtns();
                UpdateShelfHeader();
            });
            row.Add(tag);

            // ── Вміст полиці ───────────────────────────────────
            var contentEl = new VisualElement();
            contentEl.AddToClassList("shelf-mgr-v2__shelf-content");

            var head = new VisualElement();
            head.AddToClassList("shelf-mgr-v2__shelf-head");
            var headLbl  = new Label($"SHELF  {si+1}"); headLbl.AddToClassList("shelf-mgr-v2__shelf-head-label");
            var headMeta = new Label($"{count} books · {freePct:F0}% free"); headMeta.AddToClassList("shelf-mgr-v2__shelf-head-meta");
            head.Add(headLbl); head.Add(headMeta);
            contentEl.Add(head);

            var spineRow = new VisualElement();
            spineRow.AddToClassList("shelf-mgr-v2__spine-row");

            if (books != null)
                for (int bi = 0; bi < books.Count; bi++)
                {
                    var entry = books[bi];
                    var tpl   = BookDatabase.Instance?.GetBook(entry.templateID);
                    if (tpl != null) spineRow.Add(BuildSpine(idx, bi, tpl, entry.isReserved));
                }

            // Порожні слоти (декоративні, після книг)
            if (freeW > 0.005f)
            {
                int em = Mathf.Min(5, Mathf.CeilToInt(freeW / 0.022f));
                for (int e = 0; e < em; e++)
                {
                    var empty = new VisualElement();
                    empty.AddToClassList("shelf-spine-empty");
                    spineRow.Add(empty);
                }
            }

            contentEl.Add(spineRow);
            row.Add(contentEl);
            _allShelvesContent.Add(row);
        }

        UpdateShelfHeader();
    }

    private void UpdateShelfHeader()
    {
        if (_shelfInfo == null || _cabinet == null) return;
        if (_targetShelfRow >= 0 && _targetShelfRow < _cabinet.shelves.Count)
        {
            var s = _cabinet.shelves[_targetShelfRow];
            if (s != null)
            {
                float fw = s.GetFreeWidth(); float tw = s.GetShelfWorldWidth();
                _shelfInfo.text = $"Target: SHELF {_targetShelfRow+1}  ·  {(tw>0?fw/tw*100f:0f):F0}% free";
            }
        }
        else _shelfInfo.text = "";
    }

    private VisualElement BuildSpine(int si, int bi, BookTemplate tpl, bool reserved)
    {
        var spine = new VisualElement();
        spine.AddToClassList("shelf-book-spine");
        spine.style.backgroundColor = new StyleColor(RarCol(tpl.rarity));
        if (reserved) spine.style.opacity = 0.4f;
        if (_selectedShelfRow == si && _selectedShelfIdx == bi) spine.AddToClassList("selected");

        var ttl = new Label(tpl.title); ttl.AddToClassList("shelf-book-spine__title");
        spine.Add(ttl);

        var ct = tpl;
        int captSi = si, captBi = bi;
        spine.RegisterCallback<MouseEnterEvent>(_ => BookInfoCardController.Instance?.Show(ct));
        spine.RegisterCallback<MouseLeaveEvent>(_ => BookInfoCardController.Instance?.Hide());
        spine.RegisterCallback<ClickEvent>(_ =>
        {
            if (reserved) return;
            _selectedShelfRow = captSi;
            _selectedShelfIdx = captBi; // індекс в GetAllBookData() = індекс в shelf._books
            _selectedInvBook  = null;
            RefreshSpineSel();
            RefreshInvSel();
            UpdateBtns();
            ShowSel(ct);
            Debug.Log($"[ShelfMgr] Selected book: shelf={captSi} idx={captBi} '{ct.title}'");
        });
        return spine;
    }

    private void RefreshSpineSel()
    {
        if (_allShelvesContent == null) return;
        var rows = _allShelvesContent.Children()
            .Where(c => c.ClassListContains("shelf-mgr-v2__shelf-row")).ToList();
        for (int si = 0; si < rows.Count; si++)
        {
            var contentEl = rows[si].Children()
                .FirstOrDefault(c => c.ClassListContains("shelf-mgr-v2__shelf-content"));
            if (contentEl == null) continue;
            var spineRow = contentEl.Children()
                .FirstOrDefault(c => c.ClassListContains("shelf-mgr-v2__spine-row"));
            if (spineRow == null) continue;
            var spines = spineRow.Children()
                .Where(c => c.ClassListContains("shelf-book-spine")).ToList();
            for (int bi = 0; bi < spines.Count; bi++)
            {
                spines[bi].RemoveFromClassList("selected");
                if (si == _selectedShelfRow && bi == _selectedShelfIdx)
                    spines[bi].AddToClassList("selected");
            }
        }
    }

    // ════════════════════════════════════════
    // TRANSFER LOGIC — всі баги виправлені
    // ════════════════════════════════════════

    // ›  Перенести вибрану книгу з інвентарю → вибрана полиця
    private void TransferOneToShelf()
    {
        if (_selectedInvBook == null)
        {
            Debug.LogWarning("[ShelfMgr] › : жодна книга не вибрана в інвентарі");
            return;
        }

        var shelf = GetTargetShelf();
        if (shelf == null) { Debug.LogWarning("[ShelfMgr] › : немає цільової полиці"); return; }

        var tpl = GetTpl(_selectedInvBook);
        if (tpl == null) { Debug.LogWarning("[ShelfMgr] › : template не знайдено"); return; }

        if (!shelf.CanFitBook(tpl))
        {
            Debug.LogWarning($"[ShelfMgr] › : '{tpl.title}' не вміщується на SHELF {_targetShelfRow+1}");
            return;
        }

        var book = _selectedInvBook; // зберігаємо перед скиданням
        InventoryManager.Instance?.RemoveBook(book);
        shelf.PlaceBook(book);

        Debug.Log($"[ShelfMgr] › : '{tpl.title}' → SHELF {_targetShelfRow+1}");
        _selectedInvBook = null;
        HideSel();
        Refresh();
    }

    // »  Перенести всі книги з інвентарю → полиці
    private void TransferAllToShelf()
    {
        if (_cabinet == null) return;

        // ФІКС: snapshot перед ітерацією щоб RemoveBook не ламав колекцію
        var all = InventoryManager.Instance?.GetSortedInventory(SortType.ByTitle);
        if (all == null || all.Count == 0) return;

        var snapshot = _genreActive.HasValue
            ? all.Where(b => GetTpl(b)?.genre == _genreActive).ToList()
            : new List<BookInstance>(all);

        int moved = 0;
        foreach (var book in snapshot)
        {
            var tpl = GetTpl(book);
            if (tpl == null) continue;

            // Спочатку вибрана полиця, потім будь-яка вільна
            Shelf shelf = null;
            var target = GetTargetShelf();
            if (target != null && target.CanFitBook(tpl))
                shelf = target;
            else
                shelf = _cabinet.shelves.FirstOrDefault(s => s != null && s.CanFitBook(tpl));

            if (shelf == null) continue;

            // ФІКС: перевіряємо що книга ще в інвентарі перед видаленням
            bool removed = InventoryManager.Instance != null &&
                           InventoryManager.Instance.GetSortedInventory(SortType.ByTitle)
                               .Any(b => b.instanceID == book.instanceID);
            if (!removed) continue;

            InventoryManager.Instance.RemoveBook(book);
            shelf.PlaceBook(book);
            moved++;
        }

        Debug.Log($"[ShelfMgr] » : {moved} книг → полиці");
        _selectedInvBook = null;
        HideSel();
        Refresh();
    }

    // ‹  Повернути вибрану книгу з полиці → інвентар
    private void TransferOneToInv()
    {
        if (_selectedShelfRow < 0 || _selectedShelfIdx < 0)
        {
            Debug.LogWarning("[ShelfMgr] ‹ : жодна книга не вибрана на полиці");
            return;
        }
        if (_cabinet == null || _selectedShelfRow >= _cabinet.shelves.Count) return;

        var shelf = _cabinet.shelves[_selectedShelfRow];
        if (shelf == null) return;

        int bookCount = shelf.GetBookCount();
        if (_selectedShelfIdx >= bookCount)
        {
            Debug.LogWarning($"[ShelfMgr] ‹ : idx={_selectedShelfIdx} >= count={bookCount}");
            _selectedShelfRow = -1; _selectedShelfIdx = -1;
            HideSel(); Refresh();
            return;
        }

        var book = shelf.TakeBookAt(_selectedShelfIdx);
        if (book != null)
        {
            InventoryManager.Instance?.AddExistingBook(book);
            Debug.Log($"[ShelfMgr] ‹ : '{book.templateID}' → інвентар");
        }
        else
        {
            Debug.LogWarning($"[ShelfMgr] ‹ : TakeBookAt({_selectedShelfIdx}) повернув null");
        }

        _selectedShelfRow = -1;
        _selectedShelfIdx = -1;
        HideSel();
        Refresh();
    }

    // «  Повернути всі книги з полиць → інвентар
    private void TransferAllToInv()
    {
        if (_cabinet == null) return;

        int total = 0;
        foreach (var shelf in _cabinet.shelves)
        {
            if (shelf == null) continue;

            // ФІКС: рахуємо кількість до циклу, ітеруємо у зворотному порядку
            int count = shelf.GetBookCount();
            for (int i = count - 1; i >= 0; i--)
            {
                var b = shelf.TakeBookAt(i);
                if (b != null)
                {
                    InventoryManager.Instance?.AddExistingBook(b);
                    total++;
                }
            }
        }

        Debug.Log($"[ShelfMgr] « : {total} книг → інвентар");
        _selectedShelfRow = -1;
        _selectedShelfIdx = -1;
        HideSel();
        Refresh();
    }

    // ── Helpers ─────────────────────────────────────────────────
    private void Refresh()
    {
        RefreshInvList();
        RefreshAllShelves();
        UpdateBtns();
    }

    private void UpdateBtns()
    {
        var tShelf = GetTargetShelf();
        var tpl    = _selectedInvBook != null ? GetTpl(_selectedInvBook) : null;

        // › : є вибрана книга в інвентарі + вибрана полиця вміщує
        bool canOne = _selectedInvBook != null && tShelf != null
                      && tpl != null && tShelf.CanFitBook(tpl);
        _btnToShelf?.SetEnabled(canOne);

        // » : є хоч щось в інвентарі + є хоч одна полиця
        var inv = InventoryManager.Instance?.GetSortedInventory(SortType.ByTitle);
        bool hasInv = inv != null && inv.Count > 0;
        bool hasShelf = _cabinet?.shelves.Any(s => s != null) ?? false;
        _btnAllToShelf?.SetEnabled(hasInv && hasShelf);

        // ‹ : є вибрана книга на полиці + індекс валідний
        bool canOneInv = _selectedShelfRow >= 0 && _selectedShelfIdx >= 0
                         && _cabinet != null
                         && _selectedShelfRow < _cabinet.shelves.Count
                         && _cabinet.shelves[_selectedShelfRow] != null
                         && _selectedShelfIdx < _cabinet.shelves[_selectedShelfRow].GetBookCount();
        _btnToInv?.SetEnabled(canOneInv);

        // « : є хоч одна книга на будь-якій полиці
        bool anyOnShelf = _cabinet?.shelves.Any(s => s != null && s.GetBookCount() > 0) ?? false;
        _btnAllToInv?.SetEnabled(anyOnShelf);
    }

    private Shelf GetTargetShelf()
    {
        if (_cabinet == null) return null;
        if (_targetShelfRow >= 0 && _targetShelfRow < _cabinet.shelves.Count)
            return _cabinet.shelves[_targetShelfRow];
        return _cabinet.shelves.FirstOrDefault(s => s != null);
    }

    private void ShowSel(BookTemplate tpl)
    {
        if (_selectedInfo == null || tpl == null) return;
        _selectedInfo.RemoveFromClassList("hidden");
        if (_selIcon  != null && tpl.icon != null) _selIcon.style.backgroundImage = new StyleBackground(tpl.icon);
        if (_selTitle != null) _selTitle.text = tpl.title;
        if (_selMeta  != null) _selMeta.text  = $"{tpl.rarity}  ·  {tpl.genre}  ·  ${tpl.sellPrice:F0}";
    }
    private void HideSel() => _selectedInfo?.AddToClassList("hidden");

    private static BookTemplate GetTpl(BookInstance b) => BookDatabase.Instance?.GetBook(b?.templateID);

    private static void ApplyIcon(VisualElement c, BookTemplate tpl)
    {
        c.Clear();
        if (tpl.icon != null) c.style.backgroundImage = new StyleBackground(tpl.icon);
        else c.Add(new Label(tpl.genre switch
        {
            BookGenre.Fantasy   => "🧙", BookGenre.Horror    => "💀",
            BookGenre.Mystery   => "🔍", BookGenre.Classic   => "📜",
            BookGenre.SciFi     => "🚀", BookGenre.Biography => "👤",
            BookGenre.Academic  => "🎓", _ => "📖"
        }));
    }

    private static Color RarCol(BookRarity r) => r switch
    {
        BookRarity.Common    => new Color(0.42f, 0.40f, 0.33f),
        BookRarity.Uncommon  => new Color(0.29f, 0.48f, 0.29f),
        BookRarity.Rare      => new Color(0.23f, 0.41f, 0.66f),
        BookRarity.Epic      => new Color(0.48f, 0.23f, 0.66f),
        BookRarity.Legendary => new Color(0.72f, 0.56f, 0.16f),
        _                    => new Color(0.42f, 0.40f, 0.33f)
    };
}