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
// ПІДКЛЮЧЕННЯ:
//   Так само як NPCInspectorMount — окремий UXML компонент,
//   монтується в GameHUDController.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryPanelController
{
    private const string CLS_HIDDEN   = "hidden";
    private const string CLS_ACTIVE   = "inv-cat-btn--active";
    private const string CLS_SELECTED = "book-card--selected";   // book-card (not inv-book-card)

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
    private VisualElement _searchDots;   // 4 dots placeholder — hidden when typing

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
        if (_bookGrid != null)
        {
            _bookGrid.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            _bookGrid.RegisterCallback<GeometryChangedEvent>(_ => ApplyScrollbarStyles());
        }

        _searchDots = _root.Q<VisualElement>("SearchDots");

        var closeBtn = _root.Q<Button>("BtnClose");
        closeBtn?.RegisterCallback<ClickEvent>(_ => Hide());

        _searchField?.RegisterValueChangedCallback(evt =>
        {
            _searchQuery = evt.newValue ?? "";
            RefreshGrid();
            // Dots: show when empty, hide when typing
            _searchDots?.EnableInClassList("hidden", !string.IsNullOrEmpty(evt.newValue));
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

        // Збираємо унікальні шаблони
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
    //  WRAPPER-СТРУКТУРА: book-card-wrap → shadow + card
    //  Тінь ідентична genre-card-shadow з NPCInspector.uss.
    //  Стилі: Shared/BookCard.uss (всі .book-card* класи).
    // ════════════════════════════════════════════════════════════════
    private VisualElement BuildBookCardWrapper(BookTemplate tpl)
    {
        // Wrapper — flex-item у сітці, positioning context для shadow і card
        var wrap = new VisualElement();
        wrap.AddToClassList("book-card-wrap");
        wrap.pickingMode = PickingMode.Position;

        // Shadow — рендериться ПЕРШИМ (за карткою)
        var shadow = new VisualElement();
        shadow.AddToClassList("book-card-shadow");
        shadow.pickingMode = PickingMode.Ignore;
        wrap.Add(shadow);

        // Card — поверх shadow
        var card = new VisualElement();
        card.AddToClassList("book-card");
        card.pickingMode = PickingMode.Position;
        wrap.Add(card);

        // Іконка жанру (слот, заповнити через style.backgroundImage)
        var cover = new VisualElement();
        cover.AddToClassList("book-card__icon");
        cover.pickingMode = PickingMode.Ignore;
        card.Add(cover);

        // Крапка рарності
        var dot = new VisualElement();
        dot.AddToClassList("book-card__dot");
        dot.AddToClassList(GetRarityClass(tpl.rarity));
        dot.pickingMode = PickingMode.Ignore;
        card.Add(dot);

        // Назва
        var title = new Label(tpl.title ?? "");
        title.AddToClassList("book-card__title");
        title.pickingMode = PickingMode.Ignore;
        card.Add(title);

        // Ціна
        var price = new Label($"${tpl.sellPrice:0}");
        price.AddToClassList("book-card__price");
        price.pickingMode = PickingMode.Ignore;
        card.Add(price);

        // Hover та click на card (не на wrap — щоб не зачіпати shadow-відступ)
        UIHoverSound.RegisterHover(card);
        card.RegisterCallback<ClickEvent>(_ => SelectCard(card, tpl));

        return wrap;  // ← wrap є flex-item у BookGrid
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
        UIHoverSound.PlayHover(); // використовуємо той же звук, можна замінити на окремий
    }

    // ── Helpers ───────────────────────────────────────────────────

    // ── Scrollbar — inline C# стилі (надійніше ніж USS при GUID-проблемах) ──
    private void ApplyScrollbarStyles()
    {
        var scroller = _bookGrid?.Q<VisualElement>("unity-vertical-scroller");
        if (scroller == null) return;

        scroller.style.width   = scroller.style.minWidth = scroller.style.maxWidth = 10f;
        scroller.style.marginLeft  = 8f;  scroller.style.marginRight = 4f;
        scroller.style.marginTop   = scroller.style.marginBottom = 10f;
        scroller.style.borderTopWidth = scroller.style.borderBottomWidth =
        scroller.style.borderLeftWidth = scroller.style.borderRightWidth = 0;
        scroller.style.backgroundColor = StyleKeyword.None;

        var tracker = scroller.Q<VisualElement>("unity-tracker");
        if (tracker != null)
        {
            tracker.style.backgroundColor = new StyleColor(new Color(218/255f, 213/255f, 200/255f));
            tracker.style.borderTopLeftRadius    = tracker.style.borderTopRightRadius    =
            tracker.style.borderBottomLeftRadius = tracker.style.borderBottomRightRadius = 3f;
            tracker.style.borderTopWidth = tracker.style.borderBottomWidth =
            tracker.style.borderLeftWidth = tracker.style.borderRightWidth = 0;
            tracker.style.width = 6f;
        }

        var dragger = scroller.Q<VisualElement>("unity-dragger");
        if (dragger != null)
        {
            dragger.style.backgroundColor = new StyleColor(new Color(150/255f, 144/255f, 132/255f));
            dragger.style.borderTopLeftRadius    = dragger.style.borderTopRightRadius    =
            dragger.style.borderBottomLeftRadius = dragger.style.borderBottomRightRadius = 3f;
            dragger.style.borderTopWidth = dragger.style.borderBottomWidth =
            dragger.style.borderLeftWidth = dragger.style.borderRightWidth = 0;
            dragger.style.minHeight = 36f;
            dragger.style.width = 6f;
        }

        foreach (var btn in scroller.Query<Button>().Build())
        {
            btn.style.display  = DisplayStyle.None;
            btn.style.width    = btn.style.height    = 0;
            btn.style.minWidth = btn.style.minHeight = 0;
            btn.style.maxWidth = btn.style.maxHeight = 0;
            btn.style.marginTop = btn.style.marginBottom = 
            btn.style.marginLeft = btn.style.marginRight = 0;
            btn.style.paddingTop = btn.style.paddingBottom = 
            btn.style.paddingLeft = btn.style.paddingRight = 0;
        }
    }

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