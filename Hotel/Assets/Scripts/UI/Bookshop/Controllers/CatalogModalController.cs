// ═══════════════════════════════════════════════════════════
// CatalogModalController.cs — Catalog modal logic
// Path: Assets/Scripts/UI/Bookshop/Controllers/CatalogModalController.cs
//
// Author tabs at top, scrollable sections below.
// Each section: header + book cards row + collection arrow + bonus card.
// Books are grouped by author; collection bonus shown on right.
//
// Uses: CollectionTracker (if available) for progress.
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class CatalogModalController : MonoBehaviour
{
    private VisualElement _root;
    private BookshopUIController _master;

    private VisualElement _tabsContainer;
    private ScrollView _content;
    private Label _authorsCount;
    private Label _booksCount;

    private string _selectedAuthor = "ALL"; // "ALL" or specific author name

    public void Initialize(VisualElement root, BookshopUIController master)
    {
        _root = root;
        _master = master;

        _tabsContainer = root.Q<VisualElement>("CatAuthorTabs");
        _content       = root.Q<ScrollView>("CatContent");
        _authorsCount  = root.Q<Label>("CatAuthorsCount");
        _booksCount    = root.Q<Label>("CatBooksCount");

        BuildAuthorTabs();
        Refresh();
    }

    private void BuildAuthorTabs()
    {
        if (_tabsContainer == null) return;
        _tabsContainer.Clear();

        var authors = GetAllAuthors();

        // "All" tab
        _tabsContainer.Add(MakeTab("All", "ALL", isActive: true));
        foreach (var a in authors)
            _tabsContainer.Add(MakeTab(a, a));

        if (_authorsCount != null) _authorsCount.text = authors.Count.ToString();
    }

    private Button MakeTab(string label, string authorKey, bool isActive = false)
    {
        var btn = new Button(() => SelectAuthor(authorKey, label));
        btn.text = label;
        btn.AddToClassList("cat-author-tab");
        if (isActive) btn.AddToClassList("active");
        return btn;
    }

    private void SelectAuthor(string authorKey, string label)
    {
        _selectedAuthor = authorKey;

        // Update tab active state
        foreach (var child in _tabsContainer.Children())
        {
            if (child is Button btn)
            {
                btn.RemoveFromClassList("active");
                if (btn.text == label) btn.AddToClassList("active");
            }
        }
        Refresh();
    }

    private List<string> GetAllAuthors()
    {
        var db = BookDatabase.Instance;
        if (db == null || db.allBooks == null) return new List<string>();
        return db.allBooks
            .Where(b => b != null && !string.IsNullOrEmpty(b.author))
            .Select(b => b.author)
            .Distinct()
            .OrderBy(a => a)
            .ToList();
    }

    public void Refresh()
    {
        if (_content == null) return;
        _content.Clear();

        var allBooks = BookDatabase.Instance?.allBooks;
        if (allBooks == null) return;

        IEnumerable<IGrouping<string, BookTemplate>> grouped;
        if (_selectedAuthor == "ALL")
        {
            grouped = allBooks
                .Where(b => b != null)
                .GroupBy(b => b.author ?? "Unknown")
                .OrderBy(g => g.Key);
        }
        else
        {
            grouped = allBooks
                .Where(b => b != null && b.author == _selectedAuthor)
                .GroupBy(b => b.author);
        }

        int totalBooks = 0;
        int ownedBooks = 0;

        foreach (var grp in grouped)
        {
            _content.Add(MakeAuthorSection(grp.Key, grp.ToList(), out int owned, out int total));
            totalBooks += total;
            ownedBooks += owned;
        }

        if (_booksCount != null) _booksCount.text = $"{ownedBooks}/{totalBooks}";
    }

    private VisualElement MakeAuthorSection(string authorName, List<BookTemplate> books, out int owned, out int total)
    {
        total = books.Count;
        var ownedBookIds = InventoryManager.Instance?.GetSortedInventory(SortType.ByTitle)
            .Select(b => b.templateID).Distinct().ToHashSet();
        owned = books.Count(b => ownedBookIds != null && ownedBookIds.Contains(b.bookID));

        if (_master?.CatalogAuthorSectionTemplate == null)
            return new Label($"{authorName} ({owned}/{total})");

        var section = _master.CatalogAuthorSectionTemplate.Instantiate().ElementAt(0);

        // Header
        var nameLabel = section.Q<Label>("AuthorName");
        var metaLabel = section.Q<Label>("AuthorMeta");
        if (nameLabel != null) nameLabel.text = authorName;
        if (metaLabel != null) metaLabel.text = $"{owned} / {total} COLLECTED";

        // Book cards
        var container = section.Q<VisualElement>("BooksContainer");
        if (container != null)
        {
            foreach (var b in books)
            {
                bool isOwned = ownedBookIds != null && ownedBookIds.Contains(b.bookID);
                container.Add(MakeBookCard(b, isOwned));
            }
        }

        // Bonus card
        var bonusTitle  = section.Q<Label>("BonusTitle");
        var bonusTarget = section.Q<Label>("BonusTarget");
        var bonusStatus = section.Q<Label>("BonusStatus");
        var bonusCard   = section.Q<VisualElement>("BonusCard");

        if (bonusTitle != null)  bonusTitle.text  = "+35%\nreputation";
        if (bonusTarget != null) bonusTarget.text = $"{owned} / {total}";
        if (bonusStatus != null)
        {
            if (owned == 0)         bonusStatus.text = "BEGINNING";
            else if (owned >= total) bonusStatus.text = "COMPLETED";
            else                     bonusStatus.text = "IN PROGRESS";
        }
        if (bonusCard != null && owned == 0)
            bonusCard.AddToClassList("locked");

        return section;
    }

    private VisualElement MakeBookCard(BookTemplate template, bool isOwned)
    {
        if (_master?.CatalogBookCardTemplate == null)
            return new Label(template.title);

        var card = _master.CatalogBookCardTemplate.Instantiate().ElementAt(0);
        if (!isOwned) card.AddToClassList("missing");
        else          card.AddToClassList(GetRarityClass(template.rarity));

        var iconText = card.Q<Label>("CatBookIconText");
        var title    = card.Q<Label>("CatBookTitle");
        var genre    = card.Q<Label>("CatBookGenre");
        var rarity   = card.Q<Label>("CatBookRarity");
        var price    = card.Q<Label>("CatBookPrice");

        if (iconText != null) iconText.text = isOwned ? "📕" : "📓";
        if (title    != null) title.text    = $"«{template.title}»";
        if (genre    != null) genre.text    = template.genre.ToString();
        if (rarity   != null)
        {
            rarity.text = isOwned ? template.rarity.ToString() : "?";
            rarity.ClearClassList();
            rarity.AddToClassList("inv-pill");
            if (isOwned) rarity.AddToClassList(GetRarityPillClass(template.rarity));
        }
        if (price    != null) price.text = isOwned ? $"$ {template.sellPrice:F0}" : "—";

        return card;
    }

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
}
