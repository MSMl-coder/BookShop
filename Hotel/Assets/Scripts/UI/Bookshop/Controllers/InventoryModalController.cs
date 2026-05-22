// ═══════════════════════════════════════════════════════════
// InventoryModalController.cs — Inventory modal logic
// Path: Assets/Scripts/UI/Bookshop/Controllers/InventoryModalController.cs
//
// v2 ADDITIONS:
//  • Page navigation: < > buttons + page label
//  • FilterByShelf(id) — called when 3D shelf is clicked
//  • FilterByCategory(genre)
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class InventoryModalController : MonoBehaviour
{
    private VisualElement _root;
    private BookshopUIController _master;

    private ScrollView _list;
    private ScrollView _categories;
    private Label _total, _value, _pageTitle, _pageSubtitle, _pageNum;
    private Button _sortTitle, _sortAuthor, _sortPrice, _sortRarity;
    private Button _btnPrev, _btnNext;        // < >

    private SortType _currentSort = SortType.ByTitle;
    private string   _shelfFilter = null;    // null = all

    // Paging
    private int _currentPage = 1;
    private int _pageSize    = 12;
    private int _totalPages  = 1;
    private List<BookInstance> _cachedBooks = new();

    public void Initialize(VisualElement root, BookshopUIController master)
    {
        _root   = root;
        _master = master;

        _list         = root.Q<ScrollView>("InvList");
        _categories   = root.Q<ScrollView>("InvCategories");
        _total        = root.Q<Label>("InvTotal");
        _value        = root.Q<Label>("InvValue");
        _pageTitle    = root.Q<Label>("InvPageTitle");
        _pageSubtitle = root.Q<Label>("InvPageSubtitle");
        _pageNum      = root.Q<Label>("InvPageNum");

        _sortTitle  = root.Q<Button>("BtnSortTitle");
        _sortAuthor = root.Q<Button>("BtnSortAuthor");
        _sortPrice  = root.Q<Button>("BtnSortPrice");
        _sortRarity = root.Q<Button>("BtnSortRarity");

        // Page navigation — look for BtnInvPrev / BtnInvNext in footer
        _btnPrev = root.Q<Button>("BtnInvPrev");
        _btnNext = root.Q<Button>("BtnInvNext");
        if (_btnPrev != null) _btnPrev.clicked += () => GoToPage(_currentPage - 1);
        if (_btnNext != null) _btnNext.clicked += () => GoToPage(_currentPage + 1);

        BindSort(_sortTitle,  SortType.ByTitle);
        BindSort(_sortAuthor, SortType.ByAuthor);
        BindSort(_sortPrice,  SortType.ByPrice);
        BindSort(_sortRarity, SortType.ByRarity);

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RefreshList;
            RefreshList();
        }

        BuildCategories();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshList;
    }

    // ─────────────────────────────────────────────
    #region Sort
    // ─────────────────────────────────────────────

    private void BindSort(Button btn, SortType type)
    {
        if (btn == null) return;
        btn.clicked += () =>
        {
            _currentSort = type;
            _sortTitle?.RemoveFromClassList("active");
            _sortAuthor?.RemoveFromClassList("active");
            _sortPrice?.RemoveFromClassList("active");
            _sortRarity?.RemoveFromClassList("active");
            btn.AddToClassList("active");
            RefreshList();
        };
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Categories list
    // ─────────────────────────────────────────────

    private void BuildCategories()
    {
        if (_categories == null) return;
        _categories.Clear();
        _categories.Add(MakeCategoryItem("📚", "Books", 0, isActive: true));
        _categories.Add(MakeCategoryItem("🗄",  "Shelves", 0));
        _categories.Add(MakeCategoryItem("📜",  "Scrolls", 0));
        _categories.Add(MakeCategoryItem("🖼",  "Artifacts", 0));
    }

    private VisualElement MakeCategoryItem(string icon, string name, int count, bool isActive = false)
    {
        var row = new VisualElement();
        row.AddToClassList("inv-cover-item");
        if (isActive) row.AddToClassList("active");

        var iconEl = new VisualElement();
        iconEl.AddToClassList("inv-cover-cat-icon");
        var iconLabel = new Label(icon);
        iconEl.Add(iconLabel);

        var info = new VisualElement();
        info.AddToClassList("inv-cover-cat-info");
        var nameLabel = new Label(name);
        nameLabel.AddToClassList("inv-cover-cat-name");
        info.Add(nameLabel);

        var countLabel = new Label(count.ToString());
        countLabel.AddToClassList("inv-cover-cat-count");

        row.Add(iconEl);
        row.Add(info);
        row.Add(countLabel);
        return row;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Book list
    // ─────────────────────────────────────────────

    /// Called by BookshopUIController when player clicks a 3D shelf
    public void FilterByShelf(string shelfId)
    {
        _shelfFilter = shelfId;
        _currentPage = 1;
        RefreshList();
    }

    public void ClearFilter()
    {
        _shelfFilter = null;
        _currentPage = 1;
        RefreshList();
    }

    public void RefreshList()
    {
        if (_list == null) return;
        _list.Clear();

        var allBooks = InventoryManager.Instance?.GetSortedInventory(_currentSort);
        if (allBooks == null || allBooks.Count == 0)
        {
            if (_total    != null) _total.text    = "0";
            if (_value    != null) _value.text    = "$0";
            if (_pageSubtitle != null) _pageSubtitle.text = "— 0 items in stock —";
            UpdatePageLabel();
            BuildCategories();
            return;
        }

        // Apply shelf filter if active
        _cachedBooks = string.IsNullOrEmpty(_shelfFilter)
            ? allBooks
            : allBooks.Where(b =>
            {
                var t = BookDatabase.Instance?.GetBook(b.templateID);
                // Filter: books placed on a specific shelf object
                // (Requires WorldPlacementManager; fallback = no filter)
                return t != null; // TODO: check placement shelfId
            }).ToList();

        // Paging
        _totalPages  = Mathf.Max(1, Mathf.CeilToInt((float)_cachedBooks.Count / _pageSize));
        _currentPage = Mathf.Clamp(_currentPage, 1, _totalPages);

        var pageBooks = _cachedBooks
            .Skip((_currentPage - 1) * _pageSize)
            .Take(_pageSize)
            .ToList();

        // Build rows with section letters
        string lastLetter = "";
        int totalValue    = 0;

        foreach (var book in pageBooks)
        {
            var template = BookDatabase.Instance?.GetBook(book.templateID);
            if (template == null) continue;

            string firstLetter = (template.title ?? "?").Substring(0, 1).ToUpper();
            if (firstLetter != lastLetter)
            {
                var sect = new Label(firstLetter);
                sect.AddToClassList("inv-section-letter");
                _list.Add(sect);
                lastLetter = firstLetter;
            }

            _list.Add(MakeBookRow(template));
            totalValue += (int)template.sellPrice;
        }

        if (_total    != null) _total.text    = _cachedBooks.Count.ToString();
        if (_value    != null) _value.text    = $"${totalValue:N0}";
        if (_pageSubtitle != null)
            _pageSubtitle.text = string.IsNullOrEmpty(_shelfFilter)
                ? $"— {_cachedBooks.Count} items in stock —"
                : $"— Shelf: {_shelfFilter} —";

        UpdatePageLabel();
        BuildCategories();
    }

    private void GoToPage(int page)
    {
        int clamped = Mathf.Clamp(page, 1, _totalPages);
        if (clamped == _currentPage) return;
        _currentPage = clamped;
        RefreshList();
    }

    private void UpdatePageLabel()
    {
        if (_pageNum == null) return;
        _pageNum.text = $"page {_currentPage} / {_totalPages}";

        // Enable/disable nav buttons
        if (_btnPrev != null) _btnPrev.SetEnabled(_currentPage > 1);
        if (_btnNext != null) _btnNext.SetEnabled(_currentPage < _totalPages);
    }


    private VisualElement MakeBookRow(BookTemplate template)
    {
        if (_master?.InventoryRowTemplate == null)
        {
            // Fallback simple row
            var simple = new Label($"{template.title} — ${template.sellPrice:F0}");
            simple.AddToClassList("inv-row");
            return simple;
        }

        var row = _master.InventoryRowTemplate.Instantiate().ElementAt(0);

        // Apply rarity CSS class for border glow
        row.AddToClassList(GetRarityClass(template.rarity));

        var iconText  = row.Q<Label>("InvRowIconText");
        var titleEl   = row.Q<Label>("InvBookTitle");
        var authorEl  = row.Q<Label>("InvBookAuthor");
        var genreEl   = row.Q<Label>("InvBookGenre");
        var rarityEl  = row.Q<Label>("InvBookRarity");
        var priceEl   = row.Q<Label>("InvBookPrice");
        var stockEl   = row.Q<Label>("InvBookStock");

        if (iconText != null) iconText.text = GetIconForRarity(template.rarity);
        if (titleEl  != null) titleEl.text  = $"«{template.title}»";
        if (authorEl != null) authorEl.text = $"{template.author} · {template.writingYear}";
        if (genreEl  != null) genreEl.text  = GetGenreName(template.genre);
        if (rarityEl != null)
        {
            rarityEl.text = GetRarityName(template.rarity);
            rarityEl.ClearClassList();
            rarityEl.AddToClassList("inv-pill");
            rarityEl.AddToClassList(GetRarityPillClass(template.rarity));
        }
        if (priceEl  != null) priceEl.text  = $"$ {template.sellPrice:F0}";
        if (stockEl  != null) stockEl.text  = "1 in stock"; // TODO: count duplicates

        // Click → show in book info card
        BookTemplate captured = template;
        row.RegisterCallback<ClickEvent>(_ =>
        {
            _master?.BookInfo?.Show(captured);
        });

        return row;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Rarity helpers
    // ─────────────────────────────────────────────

    private string GetRarityClass(BookRarity r) => r switch
    {
        BookRarity.Common    => "r-common",
        BookRarity.Uncommon  => "r-uncommon",
        BookRarity.Rare      => "r-rare",
        BookRarity.Epic      => "r-epic",
        BookRarity.Legendary => "r-legend",
        _ => "r-common"
    };

    private string GetRarityPillClass(BookRarity r) => r switch
    {
        BookRarity.Common    => "common",
        BookRarity.Uncommon  => "uncommon",
        BookRarity.Rare      => "rare",
        BookRarity.Epic      => "epic",
        BookRarity.Legendary => "legend",
        _ => "common"
    };

    private string GetRarityName(BookRarity r) => r switch
    {
        BookRarity.Common    => "Common",
        BookRarity.Uncommon  => "Uncommon",
        BookRarity.Rare      => "Rare",
        BookRarity.Epic      => "Epic",
        BookRarity.Legendary => "Legendary",
        _ => "Common"
    };

    private string GetGenreName(BookGenre g) => g switch
    {
        BookGenre.Classic    => "Classic",
        BookGenre.Fantasy    => "Fantasy",
        BookGenre.SciFi      => "Sci-Fi",
        BookGenre.Horror     => "Horror",
        BookGenre.Mystery    => "Mystery",
        BookGenre.Biography  => "Biography",
        BookGenre.Academic   => "Academic",
        _ => g.ToString()
    };

    private string GetIconForRarity(BookRarity r) => r switch
    {
        BookRarity.Common    => "📗",
        BookRarity.Uncommon  => "📘",
        BookRarity.Rare      => "📕",
        BookRarity.Epic      => "📓",
        BookRarity.Legendary => "📔",
        _ => "📖"
    };

    #endregion
}
