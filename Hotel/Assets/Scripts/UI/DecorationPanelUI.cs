// Assets/Scripts/UI/Decoration/DecorationPanelUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

/// Меню декорування магазину.
///
/// UNITY SETUP:
/// 1. DecorationPanel.uxml підключений до MainShopUI.uxml
///    (або окремий UIDocument Sort Order 100)
/// 2. Цей скрипт — на тому ж GameObject що ShopUIManager
/// 3. allFurniture — призначити всі FurnitureTemplate з проекту
/// 4. furnitureCardTemplate — FurnitureItemCard.uxml
///
/// ПОТІК:
/// BtnOpenDecoration → Toggle() → BuildGrid() → OnCardClick() → ShowDetail()
///                                                             → OnActionBtn()
public class DecorationPanelUI : MonoBehaviour
{
    public static DecorationPanelUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private UIDocument      uiDocument;
    [SerializeField] private VisualTreeAsset furnitureCardTemplate;

    [Header("Data — призначити всі FurnitureTemplate")]
    [SerializeField] private List<FurnitureTemplate> allFurniture = new();

    // ── Visual Elements ──
    private VisualElement _panel;
    private ScrollView    _grid;
    private Label         _unlockedCount;

    // Деталі
    private VisualElement _detailEmpty;
    private VisualElement _detailContent;
    private VisualElement _detailIcon;
    private Label         _detailName;
    private Label         _detailClass;
    private Label         _detailBonus;
    private VisualElement _detailBonusBlock;
    private Label         _detailUnlock;
    private VisualElement _detailUnlockBlock;
    private Label         _detailSetName;
    private VisualElement _detailSetBlock;
    private VisualElement _detailSetProgress;
    private Button        _actionBtn;

    // ── State ──
    private FurnitureClass? _activeFilter = null;   // null = всі
    private bool            _filterPlaced = false;
    private FurnitureTemplate _selected;
    private bool _isOpen = false;

    // Назви класів українською
    private static readonly Dictionary<FurnitureClass, string> ClassNames = new()
    {
        { FurnitureClass.WallShelf,    "НАСТІННА ПОЛИЦЯ" },
        { FurnitureClass.CenterIsland, "ОСТРІВНИЙ СТЕНД" },
        { FurnitureClass.Decor,        "ДЕКОР"           },
    };

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
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        CacheElements(root);
        BindButtons(root);

        // Стартово — закрита
        SetDisplay(_panel, false);
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Setup
    // ─────────────────────────────────────────────

    private void CacheElements(VisualElement root)
    {
        _panel          = root.Q<VisualElement>("DecorationPanel");
        _grid           = root.Q<ScrollView>("DecoGrid");
        _unlockedCount  = root.Q<Label>("DecoUnlockedCount");

        _detailEmpty    = root.Q<VisualElement>("DecoDetailEmpty");
        _detailContent  = root.Q<VisualElement>("DecoDetailContent");
        _detailIcon     = root.Q<VisualElement>("DecoDetailIcon");
        _detailName     = root.Q<Label>("DecoDetailName");
        _detailClass    = root.Q<Label>("DecoDetailClass");
        _detailBonus    = root.Q<Label>("DecoDetailBonus");
        _detailBonusBlock  = root.Q<VisualElement>("DecoDetailBonusBlock");
        _detailUnlock      = root.Q<Label>("DecoDetailUnlock");
        _detailUnlockBlock = root.Q<VisualElement>("DecoDetailUnlockBlock");
        _detailSetName     = root.Q<Label>("DecoDetailSetName");
        _detailSetBlock    = root.Q<VisualElement>("DecoDetailSetBlock");
        _detailSetProgress = root.Q<VisualElement>("DecoSetProgress");
        _actionBtn         = root.Q<Button>("DecoActionBtn");

        if (_panel == null)
            Debug.LogWarning("[DecoUI] Елемент 'DecorationPanel' не знайдено у UXML.");
    }

    private void BindButtons(VisualElement root)
    {
        // Закрити
        root.Q<Button>("BtnCloseDecoration")?.RegisterCallback<ClickEvent>(_ => Close());

        // Фільтри
        BindFilter(root, "DecoFilterAll",    null,                      false);
        BindFilter(root, "DecoFilterShelf",  FurnitureClass.WallShelf,  false);
        BindFilter(root, "DecoFilterIsland", FurnitureClass.CenterIsland, false);
        BindFilter(root, "DecoFilterDecor",  FurnitureClass.Decor,      false);
        BindFilterPlaced(root, "DecoFilterPlaced");

        // Кнопка дії
        if (_actionBtn != null)
            _actionBtn.clicked += OnActionBtnClicked;
    }

    private void BindFilter(VisualElement root, string btnName,
                            FurnitureClass? filterClass, bool placed)
    {
        var btn = root.Q<Button>(btnName);
        if (btn == null) return;

        btn.clicked += () =>
        {
            _activeFilter = filterClass;
            _filterPlaced = placed;

            // Скидаємо active на всіх фільтрах
            foreach (var name in new[]
                { "DecoFilterAll","DecoFilterShelf","DecoFilterIsland",
                  "DecoFilterDecor","DecoFilterPlaced" })
                root.Q<Button>(name)?.RemoveFromClassList("active");

            btn.AddToClassList("active");
            BuildGrid();
        };
    }

    private void BindFilterPlaced(VisualElement root, string btnName)
    {
        var btn = root.Q<Button>(btnName);
        if (btn == null) return;

        btn.clicked += () =>
        {
            _activeFilter = null;
            _filterPlaced = true;

            foreach (var name in new[]
                { "DecoFilterAll","DecoFilterShelf","DecoFilterIsland",
                  "DecoFilterDecor","DecoFilterPlaced" })
                root.Q<Button>(name)?.RemoveFromClassList("active");
            btn.AddToClassList("active");
            BuildGrid();
        };
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Public API
    // ─────────────────────────────────────────────

    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    public void Open()
    {
        _isOpen = true;
        SetDisplay(_panel, true);
        BuildGrid();
        ShowDetailEmpty();
    }

    public void Close()
    {
        _isOpen = false;
        SetDisplay(_panel, false);
        _selected = null;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Grid
    // ─────────────────────────────────────────────

    private void BuildGrid()
    {
        if (_grid == null || furnitureCardTemplate == null) return;
        _grid.Clear();

        var unlocked  = InventoryManager.Instance?.GetUnlockedFurnitureByClass(FurnitureClass.WallShelf)
                        ?? new List<FurnitureTemplate>();
        // Отримуємо всі розблоковані (всіх класів разом)
        var allUnlocked = GetAllUnlocked();

        // Оновлюємо лічильник
        if (_unlockedCount != null)
            _unlockedCount.text = $"{allUnlocked.Count} / {allFurniture.Count} предметів";

        // Фільтрація
        var filtered = allFurniture.AsEnumerable();

        if (_filterPlaced)
            filtered = filtered.Where(f => f.IsPlaced);
        else if (_activeFilter.HasValue)
            filtered = filtered.Where(f => f.furnitureClass == _activeFilter.Value);

        // Сортування: доступні першими, всередині — за назвою
        var sorted = filtered
            .OrderByDescending(f => allUnlocked.Contains(f) || f.unlockedByDefault)
            .ThenBy(f => f.furnitureName)
            .ToList();

        foreach (var item in sorted)
        {
            bool isUnlocked = item.unlockedByDefault || allUnlocked.Contains(item);
            var card = MakeCard(item, isUnlocked);
            _grid.Add(card);
        }
    }

    private VisualElement MakeCard(FurnitureTemplate item, bool isUnlocked)
    {
        VisualElement card = furnitureCardTemplate.Instantiate().ElementAt(0);

        // ── Стан ──
        if (!isUnlocked)
            card.AddToClassList("locked");
        else if (item.IsPlaced)
            card.AddToClassList("placed");

        if (item == _selected)
            card.AddToClassList("selected");

        // ── Іконка ──
        var iconEl = card.Q<VisualElement>("FurnitureIcon");
        if (iconEl != null && item.icon != null)
            iconEl.style.backgroundImage = new StyleBackground(item.icon);

        // ── Назва ──
        var nameL = card.Q<Label>("FurnitureName");
        if (nameL != null) nameL.text = item.furnitureName;

        // ── Бонус (скорочено) ──
        var bonusL = card.Q<Label>("BonusText");
        var bonusBadge = card.Q<VisualElement>("BonusBadge");
        if (bonusL != null && bonusBadge != null)
        {
            bool hasBonus = !string.IsNullOrEmpty(item.bonusDescription);
            bonusBadge.style.display = hasBonus ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasBonus) bonusL.text = TruncateBonus(item.bonusDescription);
        }

        // ── Статус в футері ──
        var statusL = card.Q<Label>("FurnitureStatus");
        if (statusL != null)
        {
            if (!isUnlocked)      statusL.text = "ЗАБЛОКОВАНО";
            else if (item.IsPlaced) statusL.text = "В ЗАЛІ ✓";
            else                    statusL.text = "В ЗАПАСІ";
        }

        // ── Клік ──
        FurnitureTemplate captured = item;
        bool capturedLocked = !isUnlocked;
        card.RegisterCallback<ClickEvent>(_ => OnCardClick(captured, capturedLocked, card));

        return card;
    }

    private void OnCardClick(FurnitureTemplate item, bool isLocked, VisualElement clickedCard)
    {
        // Знімаємо selected з попередньої картки — перебудовуємо грід
        _selected = item;
        BuildGrid();
        ShowDetail(item, isLocked);
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Detail Panel
    // ─────────────────────────────────────────────

    private void ShowDetailEmpty()
    {
        SetDisplay(_detailEmpty, true);
        _detailContent?.AddToClassList("hidden");
    }

    private void ShowDetail(FurnitureTemplate item, bool isLocked)
    {
        SetDisplay(_detailEmpty, false);
        _detailContent?.RemoveFromClassList("hidden");

        // ── Іконка ──
        if (_detailIcon != null)
        {
            _detailIcon.style.backgroundImage = item.icon != null
                ? new StyleBackground(item.icon)
                : new StyleBackground();
        }

        // ── Назва + клас ──
        if (_detailName  != null) _detailName.text  = item.furnitureName;
        if (_detailClass != null) _detailClass.text =
            ClassNames.TryGetValue(item.furnitureClass, out var cn) ? cn : item.furnitureClass.ToString();

        // ── Бонус ──
        bool hasBonus = !string.IsNullOrEmpty(item.bonusDescription);
        SetDisplay(_detailBonusBlock, hasBonus);
        if (hasBonus && _detailBonus != null)
            _detailBonus.text = item.bonusDescription;

        // ── Умова розблокування (тільки locked) ──
        bool hasUnlock = isLocked && !string.IsNullOrEmpty(item.unlockCondition);
        if (_detailUnlockBlock != null)
        {
            if (hasUnlock) _detailUnlockBlock.RemoveFromClassList("hidden");
            else           _detailUnlockBlock.AddToClassList("hidden");
        }
        if (hasUnlock && _detailUnlock != null)
            _detailUnlock.text = item.unlockCondition;

        // ── Сет ──
        bool hasSet = !string.IsNullOrEmpty(item.setID);
        if (_detailSetBlock != null)
        {
            if (hasSet) _detailSetBlock.RemoveFromClassList("hidden");
            else        _detailSetBlock.AddToClassList("hidden");
        }
        if (hasSet)
            BuildSetProgress(item.setID);

        // ── Кнопка дії ──
        if (_actionBtn != null)
        {
            if (isLocked)
            {
                _actionBtn.text = "ЗАБЛОКОВАНО";
                _actionBtn.SetEnabled(false);
                _actionBtn.RemoveFromClassList("remove");
            }
            else if (item.IsPlaced)
            {
                _actionBtn.text = "ПРИБРАТИ";
                _actionBtn.SetEnabled(true);
                _actionBtn.AddToClassList("remove");
            }
            else
            {
                _actionBtn.text = "РОЗМІСТИТИ";
                _actionBtn.SetEnabled(true);
                _actionBtn.RemoveFromClassList("remove");
            }
        }
    }

    private void BuildSetProgress(string setID)
    {
        if (_detailSetProgress == null) return;
        _detailSetProgress.Clear();

        var setItems = allFurniture.Where(f => f.setID == setID).ToList();
        var allUnlocked = GetAllUnlocked();

        if (_detailSetName != null)
            _detailSetName.text = $"Набір: {setID} ({setItems.Count(f => allUnlocked.Contains(f))}/{setItems.Count})";

        foreach (var f in setItems)
        {
            var dot = new VisualElement();
            dot.AddToClassList("set-dot");
            dot.AddToClassList(allUnlocked.Contains(f) ? "collected" : "missing");
            dot.tooltip = f.furnitureName;
            _detailSetProgress.Add(dot);
        }
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Action Button
    // ─────────────────────────────────────────────

    private void OnActionBtnClicked()
    {
        if (_selected == null) return;

        if (_selected.IsPlaced)
            RemoveFurniture(_selected);
        else
            PlaceFurniture(_selected);

        BuildGrid();
        ShowDetail(_selected, isLocked: false);
    }

    private void PlaceFurniture(FurnitureTemplate item)
    {
        // Знаходимо перший вільний FurnitureSlot відповідного класу
        var slots = Object.FindObjectsByType<FurnitureSlot>(FindObjectsInactive.Exclude);
        FurnitureSlot target = null;

        foreach (var slot in slots)
        {
            if (slot.allowedClass == item.furnitureClass)
            {
                target = slot;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[DecoUI] Немає вільного слота для {item.furnitureClass}");
            return;
        }

        target.UpgradeFurniture(item);
        item.IsPlaced = true;
        Debug.Log($"[DecoUI] Розміщено: {item.furnitureName}");
    }

    private void RemoveFurniture(FurnitureTemplate item)
    {
        // Знаходимо слот де стоїть цей предмет і скидаємо
        // (спрощена логіка — у повній версії треба зберігати посилання на слот)
        item.IsPlaced = false;
        Debug.Log($"[DecoUI] Прибрано: {item.furnitureName}");
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Helpers
    // ─────────────────────────────────────────────

    /// Повертає всі розблоковані меблі з усіх класів
    private List<FurnitureTemplate> GetAllUnlocked()
    {
        var result = new List<FurnitureTemplate>();
        foreach (FurnitureClass cls in System.Enum.GetValues(typeof(FurnitureClass)))
        {
            var list = InventoryManager.Instance?.GetUnlockedFurnitureByClass(cls);
            if (list != null) result.AddRange(list);
        }
        return result;
    }

    private static string TruncateBonus(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length > 22 ? text.Substring(0, 20) + "…" : text;
    }

    private static void SetDisplay(VisualElement el, bool show)
    {
        if (el != null) el.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    #endregion
}
