// Assets/Scripts/UI/BookInfo/BookInfoCardController.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

/// Керує "бібліотечною карткою" (BookInfoCard.uxml) що висувається
/// з правого краю екрана при hover на книгу.
///
/// UNITY SETUP:
/// 1. Додай BookInfoCard.uxml як дочірній елемент MainShopUI.uxml
///    АБО на окремий UIDocument з вищим Sort Order (101).
/// 2. Повісь цей скрипт на той самий GameObject що ShopUIManager.
/// 3. Виклик: BookInfoCardController.Instance.Show(template) / Hide()
public class BookInfoCardController : MonoBehaviour
{
    public static BookInfoCardController Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    // ── Visual Elements ──
    private VisualElement _card;
    private VisualElement _genreBar;
    private Label _titleLabel;
    private Label _rarityLabel;
    private Label _authorLabel;
    private Label _yearLabel;
    private Label _genreLabel;
    private Label _priceLabel;
    private Label _descLabel;
    private VisualElement _descBlock;
    private VisualElement _traitsContainer;
    private VisualElement _collectionBlock;
    private Label _collectionName;
    private Button _sellBtn;

    // ── State ──
    private BookTemplate _currentTemplate;
    private bool _isVisible = false;

    // ── Genre bar CSS classes ──
    private static readonly Dictionary<BookEnums.BookGenre, string> GenreBarClasses
        = new Dictionary<BookEnums.BookGenre, string>
    {
        { BookEnums.BookGenre.Fantasy,   "fantasy"   },
        { BookEnums.BookGenre.Horror,    "horror"    },
        { BookEnums.BookGenre.Mystery,   "mystery"   },
        { BookEnums.BookGenre.Classic,   "classic"   },
        { BookEnums.BookGenre.SciFi,     "scifi"     },
        { BookEnums.BookGenre.Biography, "biography" },
        { BookEnums.BookGenre.Academic,  "academic"  },
    };

    // ── Rarity badge classes ──
    private static readonly string[] RarityClasses =
        { "common", "uncommon", "rare", "epic", "legendary" };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void OnEnable()
    {
        if (uiDocument == null) { Debug.LogError("[BookInfoCard] UIDocument не призначено!"); return; }

        var root = uiDocument.rootVisualElement;
        CacheElements(root);
        BindButtons();

        // Стартовий стан — прихована
        _card?.AddToClassList("hidden");
        _card?.RemoveFromClassList("visible");
    }

    // ───────────────────────────────────────────
    #region Cache & Bind
    // ───────────────────────────────────────────

    private void CacheElements(VisualElement root)
    {
        _card            = root.Q<VisualElement>("BookInfoCard");
        _genreBar        = root.Q<VisualElement>("InfoCardGenreBar");
        _titleLabel      = root.Q<Label>("InfoCardTitle");
        _rarityLabel     = root.Q<Label>("InfoCardRarity");
        _authorLabel     = root.Q<Label>("InfoCardAuthor");
        _yearLabel       = root.Q<Label>("InfoCardYear");
        _genreLabel      = root.Q<Label>("InfoCardGenre");
        _priceLabel      = root.Q<Label>("InfoCardPrice");
        _descLabel       = root.Q<Label>("InfoCardDesc");
        _descBlock       = root.Q<VisualElement>("InfoCardDescBlock");
        _traitsContainer = root.Q<VisualElement>("InfoCardTraits");
        _collectionBlock = root.Q<VisualElement>("InfoCardCollectionBlock");
        _collectionName  = root.Q<Label>("InfoCardCollectionName");
        _sellBtn         = root.Q<Button>("InfoCardSellBtn");

        if (_card == null)
            Debug.LogWarning("[BookInfoCard] Елемент 'BookInfoCard' не знайдено у UXML. " +
                             "Переконайся що BookInfoCard.uxml підключено до MainShopUI.uxml.");
    }

    private void BindButtons()
    {
        if (_sellBtn != null)
            _sellBtn.clicked += OnSellClicked;
    }

    #endregion

    // ───────────────────────────────────────────
    #region Public API
    // ───────────────────────────────────────────

    /// Показати картку для шаблону книги
    public void Show(BookTemplate template)
    {
        if (template == null || _card == null) return;
        _currentTemplate = template;

        FillData(template);

        if (!_isVisible)
        {
            _isVisible = true;
            _card.RemoveFromClassList("hidden");
            _card.AddToClassList("visible");
        }
    }

    /// Сховати картку
    public void Hide()
    {
        if (!_isVisible || _card == null) return;
        _isVisible = false;
        _card.RemoveFromClassList("visible");
        _card.AddToClassList("hidden");
        _currentTemplate = null;
    }

    #endregion

    // ───────────────────────────────────────────
    #region Fill Data
    // ───────────────────────────────────────────

    private void FillData(BookTemplate t)
    {
        // Назва
        if (_titleLabel != null)
            _titleLabel.text = t.title;

        // Рідкість
        if (_rarityLabel != null)
        {
            _rarityLabel.text = GetRarityUkrainian(t.rarity);
            foreach (var c in RarityClasses) _rarityLabel.RemoveFromClassList(c);
            _rarityLabel.AddToClassList(t.rarity.ToString().ToLower());
        }

        // Автор
        if (_authorLabel != null)
            _authorLabel.text = string.IsNullOrEmpty(t.author) ? "Невідомо" : t.author;

        // Рік
        if (_yearLabel != null)
            _yearLabel.text = t.writingYear > 0 ? t.writingYear.ToString() : "—";

        // Жанр
        if (_genreLabel != null)
            _genreLabel.text = GetGenreUkrainian(t.genre);

        // Ціна
        if (_priceLabel != null)
            _priceLabel.text = $"{t.sellPrice:F0} грн";

        // Смуга жанру
        if (_genreBar != null)
        {
            foreach (var kv in GenreBarClasses) _genreBar.RemoveFromClassList(kv.Value);
            if (GenreBarClasses.TryGetValue(t.genre, out string cls))
                _genreBar.AddToClassList(cls);
        }

        // Опис (якщо є поле description в BookTemplate)
        FillDescription(t);

        // Трейти (НЕСЛУХНЯНІСТЬ та ін.)
        FillTraits(t);

        // Колекція
        FillCollection(t);
    }

    private void FillDescription(BookTemplate t)
    {
        if (_descBlock == null) return;

        // Перевіряємо чи є поле опису через рефлексію або напряму
        // Якщо в BookTemplate є поле 'description' — використовуємо його
        // Якщо немає — ховаємо блок
#if UNITY_EDITOR
        var field = typeof(BookTemplate).GetField("description",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            string desc = field.GetValue(t) as string;
            bool hasDesc = !string.IsNullOrWhiteSpace(desc);
            _descBlock.style.display = hasDesc ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasDesc && _descLabel != null) _descLabel.text = desc;
        }
        else
        {
            _descBlock.style.display = DisplayStyle.None;
        }
#else
        _descBlock.style.display = DisplayStyle.None;
#endif
    }

    private void FillTraits(BookTemplate t)
    {
        if (_traitsContainer == null) return;
        _traitsContainer.Clear();

        // Перевіряємо поле isDisobedient
        var disobField = typeof(BookTemplate).GetField("isDisobedient",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (disobField != null && (bool)(disobField.GetValue(t) ?? false))
        {
            var badge = new Label("⚡ НЕСЛУХНЯНА");
            badge.AddToClassList("trait-badge");
            badge.AddToClassList("disobedient");
            badge.tooltip = "Ця книга може самовільно переміщатись на інші полиці";
            _traitsContainer.Add(badge);
        }
    }

    private void FillCollection(BookTemplate t)
    {
        if (_collectionBlock == null) return;

        // Шукаємо колекцію де є ця книга
        var colField = typeof(BookTemplate).GetField("collectionId",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (colField != null)
        {
            string colId = colField.GetValue(t) as string;
            if (!string.IsNullOrEmpty(colId))
            {
                _collectionBlock.RemoveFromClassList("hidden");
                if (_collectionName != null)
                    _collectionName.text = $"Колекція: {colId}";
                return;
            }
        }

        _collectionBlock.AddToClassList("hidden");
    }

    #endregion

    // ───────────────────────────────────────────
    #region Actions
    // ───────────────────────────────────────────

    private void OnSellClicked()
    {
        if (_currentTemplate == null) return;

        // Знаходимо першу книгу цього типу в інвентарі та продаємо
        var inventory = InventoryManager.Instance;
        if (inventory == null) return;

        // Шукаємо екземпляр
        var books = inventory.GetSortedInventory(SortType.ByTitle);
        BookInstance toSell = null;
        foreach (var b in books)
        {
            if (b.templateID == _currentTemplate.bookID)
            {
                toSell = b;
                break;
            }
        }

        if (toSell == null)
        {
            Debug.LogWarning($"[BookInfoCard] Книга {_currentTemplate.title} не знайдена в інвентарі.");
            return;
        }

        // Продаємо через EconomyManager
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddMoney((int)_currentTemplate.sellPrice);
            inventory.RemoveBook(toSell);
            Debug.Log($"[BookInfoCard] Продано: {_currentTemplate.title} за {_currentTemplate.sellPrice} грн");
        }

        Hide();
    }

    #endregion

    // ───────────────────────────────────────────
    #region Helpers
    // ───────────────────────────────────────────

    private static string GetRarityUkrainian(BookEnums.BookRarity rarity) => rarity switch
    {
        BookEnums.BookRarity.Common    => "ЗВИЧАЙНА",
        BookEnums.BookRarity.Uncommon  => "НЕЗВИЧАЙНА",
        BookEnums.BookRarity.Rare      => "РІДКІСНА",
        BookEnums.BookRarity.Epic      => "ЕПІЧНА",
        BookEnums.BookRarity.Legendary => "ЛЕГЕНДАРНА",
        _                              => rarity.ToString().ToUpper()
    };

    private static string GetGenreUkrainian(BookEnums.BookGenre genre) => genre switch
    {
        BookEnums.BookGenre.Fantasy   => "Фентезі",
        BookEnums.BookGenre.Horror    => "Жахи",
        BookEnums.BookGenre.Mystery   => "Детектив",
        BookEnums.BookGenre.Classic   => "Класика",
        BookEnums.BookGenre.SciFi     => "Наукова фантастика",
        BookEnums.BookGenre.Biography => "Біографія",
        BookEnums.BookGenre.Academic  => "Академічна",
        _                             => genre.ToString()
    };

    #endregion
}
