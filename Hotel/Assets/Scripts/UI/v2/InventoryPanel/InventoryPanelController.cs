// Assets/Scripts/UI/Components/InventoryPanel/InventoryPanelController.cs
//
// Standalone компонент панелі інвентарю.
// Відображає книги з InventoryManager у grid 3 колонки.
// Звук при прокручуванні через UIHoverSound.
//
// API:
//   Show()  — відкрити панель
//   Hide()  — закрити
//   Toggle()— перемкнути
//
// V2 (5 червня): картки тепер у wrapper-структурі з drop-shadow
// (як genre-card у NPCInspector). Класи: book-card-wrap → shadow + card.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryPanelController
{
    private const string CLS_HIDDEN   = "hidden";
    private const string CLS_ACTIVE   = "inv-cat-btn--active";
    private const string CLS_SELECTED = "book-card--selected";   // NEW: book-card not inv-book-card

    // ── UI refs ───────────────────────────────────────────────────
    private readonly VisualElement _root;
    private readonly VisualElement _categoryBar;
    private readonly VisualElement _subcategoryBar;
    private readonly TextField     _searchField;
    private readonly ScrollView    _bookGrid;

    // ── State ─────────────────────────────────────────────────────
    private bool      _isVisible;
    private BookGenre? _activeGenre;
    private string    _searchQuery = "";
    private BookTemplate _selectedTemplate;
    private VisualElement _selectedCard;

    // Scroll sound throttle
    private float _lastScrollSoundTime = -1f;
    private const float SCROLL_SOUND_COOLDOWN = 0.12f;

    // ── Constructor ───────────────────────────────────────────────
    public InventoryPanelController(VisualElement templateContainer)
    {
        _root = templateContainer.Q<VisualElement>("InventoryPanel") ?? templateContainer;

        _categoryBar    = _root.Q<VisualElement>("CategoryBar");
        _subcategoryBar = _root.Q<VisualElement>("SubcategoryBar");
        _searchField    = _root.Q<TextField>("SearchField");
        _bookGrid       = _root.Q<ScrollView>("BookGrid");

        var closeBtn = _root.Q<Button>("BtnClose");
        closeBtn?.RegisterCallback<ClickEvent>(_ => Hide());

        _searchField?.RegisterValueChangedCallback(evt =>
        {
            _searchQuery = evt.newValue ?? "";
            RefreshGrid();
        });

        // Scroll sound
        _bookGrid?.RegisterCallback<WheelEvent>(_ => PlayScrollSound());

        // Підписка на зміни інвентарю
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += RefreshGrid;

        BuildCategoryBar();
        BuildSubcategoryBar();

        _root.AddToClassList(CLS_HIDDEN);
        _root.pickingMode = PickingMode.Ignore;
    }

    // ── Public API ────────────────────────────────────────────────

    public bool IsVisible => _isVisible;

    public void Show()
    {
        RefreshGrid();
        _root.RemoveFromClassList(CLS_HIDDEN);
        _root.pickingMode = PickingMode.Position;
        _isVisible = true;
    }

    public void Hide()
    {
        _root.AddToClassList(CLS_HIDDEN);
        _root.pickingMode = PickingMode.Ignore;
        _isVisible = false;
    }

    public void Toggle()
    {
        if (_isVisible) Hide(); else Show();
    }

    public void Dispose()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshGrid;
    }

    // ── Category bar ──────────────────────────────────────────────

    private void BuildCategoryBar()
    {
        if (_categoryBar == null) return;
        _categoryBar.Clear();

        // "All" + кожен жанр
        AddCategoryBtn(null, "All");
        foreach (BookGenre g in System.Enum.GetValues(typeof(BookGenre)))
            AddCategoryBtn(g, g.ToString());
    }

    private void AddCategoryBtn(BookGenre? genre, string label)
    {
        var btn = new Button();
        btn.AddToClassList("inv-cat-btn");
        btn.tooltip = label;
        if (genre == null) btn.AddToClassList(CLS_ACTIVE); // "All" active by default
        UIHoverSound.RegisterButton(btn);

        BookGenre? captured = genre;
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            _activeGenre = captured;
            // Оновлюємо активну кнопку
            foreach (var c in _categoryBar.Children().OfType<Button>())
                c.EnableInClassList(CLS_ACTIVE, c == btn);
            BuildSubcategoryBar();
            RefreshGrid();
        });
        _categoryBar.Add(btn);
    }

    // ── Subcategory bar ───────────────────────────────────────────

    private void BuildSubcategoryBar()
    {
        if (_subcategoryBar == null) return;
        _subcategoryBar.Clear();

        // A/B sort buttons
        var btnA = new Button { text = "A" };
        btnA.AddToClassList("inv-subcat-btn");
        btnA.AddToClassList("inv-subcat-btn--active");
        UIHoverSound.RegisterButton(btnA);
        _subcategoryBar.Add(btnA);

        var btnB = new Button { text = "B" };
        btnB.AddToClassList("inv-subcat-btn");
        UIHoverSound.RegisterButton(btnB);
        _subcategoryBar.Add(btnB);

        // Rarity filter badges
        var rarities = new[] { "C", "U", "R", "E", "L" };
        foreach (var r in rarities)
        {
            var badge = new Button { text = r };
            badge.AddToClassList("inv-badge-btn");
            UIHoverSound.RegisterButton(badge);
            _subcategoryBar.Add(badge);
        }
    }

    // ── Book grid ─────────────────────────────────────────────────

    private void RefreshGrid()
    {
        if (_bookGrid == null) return;
        _bookGrid.Clear();
        _selectedCard = null;
        _selectedTemplate = null;

        var books = GetFilteredBooks();
        foreach (var tpl in books)
        {
            var wrapper = BuildBookCardWrapper(tpl);
            _bookGrid.Add(wrapper);
        }
    }

    private IEnumerable<BookTemplate> GetFilteredBooks()
    {
        var inventory = InventoryManager.Instance?.GetSortedInventory(SortType.ByTitle);
        if (inventory == null) return Enumerable.Empty<BookTemplate>();

        var templates = inventory
            .Select(b => BookDatabase.Instance?.GetBook(b.templateID))
            .Where(t => t != null)
            .Distinct();

        if (_activeGenre.HasValue)
            templates = templates.Where(t => t.genre == _activeGenre.Value);

        if (!string.IsNullOrEmpty(_searchQuery))
            templates = templates.Where(t =>
                t.title.ToLower().Contains(_searchQuery.ToLower()) ||
                (t.author?.ToLower().Contains(_searchQuery.ToLower()) ?? false));

        return templates;
    }

    // ════════════════════════════════════════════════════════════════
    //  WRAPPER-СТРУКТУРА як у genre-card з NPCInspector
    //
    //  <book-card-wrap>           ← positioning context (relative)
    //    <book-card-shadow/>      ← absolute, translate 4px 5px (тінь)
    //    <book-card>              ← absolute, поверх тіні
    //      <book-card__icon/>
    //      <book-card__dot/>      ← клас рарності визначає колір
    //      <book-card__title/>
    //      <book-card__price/>
    //  </book-card-wrap>
    //
    //  Стилі — у Shared/BookCard.uss (всі класи .book-card*).
    //  Click & hover — на самій card (shadow має pickingMode=Ignore).
    //  Повертається WRAPPER (це flex-item у grid).
    // ════════════════════════════════════════════════════════════════
    private VisualElement BuildBookCardWrapper(BookTemplate tpl)
    {
        // ── wrapper ── flex-item у сітці, positioning context для shadow+card
        var wrap = new VisualElement();
        wrap.AddToClassList("book-card-wrap");
        wrap.pickingMode = PickingMode.Position;

        // ── shadow ── ідентично .genre-card-shadow з NPCInspector.uss
        var shadow = new VisualElement();
        shadow.AddToClassList("book-card-shadow");
        shadow.pickingMode = PickingMode.Ignore;
        wrap.Add(shadow);                          // ПЕРШИЙ → рендериться ЗА карткою

        // ── card ── видима кремова коробка
        var card = new VisualElement();
        card.AddToClassList("book-card");
        card.pickingMode = PickingMode.Position;
        wrap.Add(card);                            // ДРУГИЙ → поверх shadow

        // Слот обкладинки/іконки (опційно — заповнити через style.backgroundImage)
        var cover = new VisualElement();
        cover.AddToClassList("book-card__icon");
        cover.pickingMode = PickingMode.Ignore;
        card.Add(cover);

        // Крапка рарності (клас визначає колір через .book-card__dot.uncommon etc)
        var dot = new VisualElement();
        dot.AddToClassList("book-card__dot");
        dot.AddToClassList(GetRarityClass(tpl.rarity));
        dot.pickingMode = PickingMode.Ignore;
        card.Add(dot);

        // Назва (труncується через white-space:nowrap у CSS)
        var title = new Label(tpl.title ?? "");
        title.AddToClassList("book-card__title");
        title.pickingMode = PickingMode.Ignore;
        card.Add(title);

        // Ціна
        var price = new Label($"${tpl.sellPrice:0}");
        price.AddToClassList("book-card__price");
        price.pickingMode = PickingMode.Ignore;
        card.Add(price);

        // Interactions — реєструємо на CARD, не на wrap.
        // Так hover/click спрацьовують лише на видимій частині (а не на ширшому
        // bounding box wrapper'а, який включає margin-right/bottom для тіні).
        UIHoverSound.RegisterHover(card);
        card.RegisterCallback<ClickEvent>(_ => SelectCard(card, tpl));

        return wrap;
    }

    private void SelectCard(VisualElement card, BookTemplate tpl)
    {
        // Скидаємо попереднє виділення
        _selectedCard?.RemoveFromClassList(CLS_SELECTED);
        UIHoverSound.PlayClick();

        if (_selectedCard == card)
        {
            // Повторний клік — скидаємо
            _selectedCard = null;
            _selectedTemplate = null;
            return;
        }

        _selectedCard = card;
        _selectedTemplate = tpl;
        card.AddToClassList(CLS_SELECTED);

        // Показати BookInfoCard
        BookInfoCardController.Instance?.Show(tpl);
    }

    // ── Scroll sound ─────────────────────────────────────────────

    private void PlayScrollSound()
    {
        if (Time.unscaledTime - _lastScrollSoundTime < SCROLL_SOUND_COOLDOWN) return;
        _lastScrollSoundTime = Time.unscaledTime;
        UIHoverSound.PlayHover();
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static string GetRarityClass(BookRarity rarity) => rarity switch
    {
        BookRarity.Common    => "common",
        BookRarity.Uncommon  => "uncommon",
        BookRarity.Rare      => "rare",
        BookRarity.Epic      => "epic",
        BookRarity.Legendary => "legendary",
        _                    => "common"
    };
}