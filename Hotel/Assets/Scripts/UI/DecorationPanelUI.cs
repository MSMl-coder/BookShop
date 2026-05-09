// Assets/Scripts/UI/Decoration/DecorationPanelUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

/// Меню декорування магазину.
///
/// UNITY SETUP:
/// 1. DecorationPanel.uxml підключений до MainShopUI.uxml
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
    private FurnitureClass?  _activeFilter = null;
    private bool             _filterPlaced = false;
    private FurnitureTemplate _selected;
    private bool             _isOpen = false;

    // Замість поля _isEditMode — завжди читаємо з менеджера
    private bool InEditMode =>
        EditModeManager.Instance != null && EditModeManager.Instance.IsEditMode;

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

        // Реєструємо всі шаблони в InventoryManager (авто-розблокування unlockedByDefault)
        InventoryManager.Instance?.InitFurnitureDatabase(allFurniture);

        // Підписка на зміни інвентарю → перебудова гриду
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnFurnitureChanged += BuildGrid;

        CacheElements(root);
        BindButtons(root);

        SetDisplay(_panel, false);
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnFurnitureChanged -= BuildGrid;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Setup
    // ─────────────────────────────────────────────

    private void CacheElements(VisualElement root)
    {
        _panel         = root.Q<VisualElement>("DecorationPanel");
        _grid          = root.Q<ScrollView>("DecoGrid");
        _unlockedCount = root.Q<Label>("DecoUnlockedCount");

        _detailEmpty       = root.Q<VisualElement>("DecoDetailEmpty");
        _detailContent     = root.Q<VisualElement>("DecoDetailContent");
        _detailIcon        = root.Q<VisualElement>("DecoDetailIcon");
        _detailName        = root.Q<Label>("DecoDetailName");
        _detailClass       = root.Q<Label>("DecoDetailClass");
        _detailBonus       = root.Q<Label>("DecoDetailBonus");
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
        root.Q<Button>("BtnCloseDecoration")?.RegisterCallback<ClickEvent>(_ => Close());

        BindFilter(root, "DecoFilterAll",    null,                        false);
        BindFilter(root, "DecoFilterShelf",  FurnitureClass.WallShelf,    false);
        BindFilter(root, "DecoFilterIsland", FurnitureClass.CenterIsland, false);
        BindFilter(root, "DecoFilterDecor",  FurnitureClass.Decor,        false);
        BindFilterPlaced(root, "DecoFilterPlaced");

        if (_actionBtn != null)
        {
            _actionBtn.clicked -= OnActionBtnClicked; // захист від подвійної підписки
            _actionBtn.clicked += OnActionBtnClicked;
        }
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

    /// Відкрити панель (зберігає поточний InEditMode стан)
    public void Open()
    {
        _isOpen = true;
        SetDisplay(_panel, true);
        BuildGrid();
        ShowDetailEmpty();

        if (_actionBtn != null)
            _actionBtn.text = InEditMode ? "РОЗМІСТИТИ" : "ДЕТАЛІ";
    }

    /// Відкрити явно в EditMode (для кнопки BtnOpenDecoration коли IsEditMode=true)
    public void OpenInEditMode()
    {
        Open(); // InEditMode читається з менеджера — додаткових дій не треба
    }

    /// Повністю закрити панель
    public void Close()
    {
        _isOpen = false;
        SetDisplay(_panel, false);
        _selected = null;
    }

    /// Сховати (без скидання стану EditMode) — після початку розміщення
    private void Hide()
    {
        _isOpen = false;
        SetDisplay(_panel, false);
        _selected = null;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Grid
    // ─────────────────────────────────────────────

    public void BuildGridPublic() => BuildGrid();

    private void BuildGrid()
    {
        if (_grid == null || furnitureCardTemplate == null) return;
        _grid.Clear();

        // Всі розблоковані шаблони через InventoryManager
        var allUnlocked = InventoryManager.Instance?.GetAllUnlockedTemplates()
                          ?? new List<FurnitureTemplate>();

        // Фільтрація
        IEnumerable<FurnitureTemplate> filtered = allUnlocked;

        if (_activeFilter.HasValue)
            filtered = filtered.Where(f => f.furnitureClass == _activeFilter.Value);

        if (_filterPlaced)
            filtered = filtered.Where(f =>
                PlacementRegistry.Instance?.GetAll()
                    .Any(e => e.instance.templateID == f.furnitureID) == true);

        if (_unlockedCount != null)
            _unlockedCount.text = $"{allUnlocked.Count} предметів";

        foreach (var item in filtered)
            _grid.Add(MakeCard(item, isUnlocked: true));

        // Заблоковані предмети (є в allFurniture але не в інвентарі)
        foreach (var item in allFurniture)
        {
            if (item == null) continue;
            if (allUnlocked.Any(u => u.furnitureID == item.furnitureID)) continue;
            if (_activeFilter.HasValue && item.furnitureClass != _activeFilter.Value) continue;
            if (_filterPlaced) continue; // заблоковані ніколи не розміщені

            _grid.Add(MakeCard(item, isUnlocked: false));
        }
    }

    private VisualElement MakeCard(FurnitureTemplate item, bool isUnlocked)
    {
        var card = furnitureCardTemplate.CloneTree();
        card.AddToClassList("furniture-card");

        if (!isUnlocked) card.AddToClassList("locked");
        if (_selected == item) card.AddToClassList("selected");

        // Визначаємо чи розміщений через Registry (не через SO)
        bool isPlaced = PlacementRegistry.Instance?.GetAll()
            .Any(e => e.instance.templateID == item.furnitureID) == true;

        if (isPlaced) card.AddToClassList("placed");

        // ── Іконка ──
        var iconEl = card.Q<VisualElement>("FurnitureIcon");
        if (iconEl != null && item.icon != null)
            iconEl.style.backgroundImage = new StyleBackground(item.icon);

        // ── Назва ──
        var nameL = card.Q<Label>("FurnitureName");
        if (nameL != null) nameL.text = item.furnitureName;

        // ── Бонус ──
        var bonusBadge = card.Q<VisualElement>("BonusBadge");
        var bonusL     = card.Q<Label>("BonusText");
        bool hasBonus  = !string.IsNullOrEmpty(item.bonusDescription);
        if (bonusBadge != null)
            bonusBadge.style.display = hasBonus ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasBonus && bonusL != null)
            bonusL.text = TruncateBonus(item.bonusDescription);

        // ── Статус ──
        var statusL = card.Q<Label>("FurnitureStatus");
        if (statusL != null)
        {
            if (!isUnlocked) statusL.text = "ЗАБЛОКОВАНО";
            else if (isPlaced) statusL.text = "В ЗАЛІ ✓";
            else statusL.text = "В ЗАПАСІ";
        }

        // ── Клік ──
        FurnitureTemplate captured = item;
        bool capturedLocked = !isUnlocked;
        card.RegisterCallback<ClickEvent>(_ => OnCardClick(captured, capturedLocked));

        return card;
    }

    private void OnCardClick(FurnitureTemplate item, bool isLocked)
    {
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
        if (_detailName  != null) _detailName.text = item.furnitureName;
        if (_detailClass != null)
            _detailClass.text = ClassNames.TryGetValue(item.furnitureClass, out var cn)
                ? cn : item.furnitureClass.ToString();

        // ── Бонус ──
        bool hasBonus = !string.IsNullOrEmpty(item.bonusDescription);
        SetDisplay(_detailBonusBlock, hasBonus);
        if (hasBonus && _detailBonus != null)
            _detailBonus.text = item.bonusDescription;

        // ── Умова розблокування ──
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
        if (hasSet) BuildSetProgress(item.setID);

        // ── Кнопка дії — ЄДИНИЙ блок ──
        UpdateActionButton(item, isLocked);
    }

    /// Оновлює текст і стан кнопки залежно від контексту
    private void UpdateActionButton(FurnitureTemplate item, bool isLocked)
    {
        if (_actionBtn == null) return;

        if (isLocked)
        {
            _actionBtn.text = "ЗАБЛОКОВАНО";
            _actionBtn.SetEnabled(false);
            _actionBtn.RemoveFromClassList("remove");
            return;
        }

        // Перевіряємо розміщення через Registry, не через SO
        bool isPlaced = PlacementRegistry.Instance?.GetAll()
            .Any(e => e.instance.templateID == item.furnitureID) == true;

        if (InEditMode)
        {
            _actionBtn.text = isPlaced ? "ПЕРЕМІСТИТИ" : "РОЗМІСТИТИ";
            _actionBtn.SetEnabled(true);
            _actionBtn.RemoveFromClassList("remove");
        }
        else
        {
            // Поза EditMode — кнопка недоступна (розміщення тільки в EditMode)
            _actionBtn.text = isPlaced ? "РОЗМІЩЕНО" : "ДЕТАЛІ";
            _actionBtn.SetEnabled(false);
            _actionBtn.RemoveFromClassList("remove");
        }
    }

    private void BuildSetProgress(string setID)
    {
        if (_detailSetProgress == null) return;
        _detailSetProgress.Clear();

        var setItems    = allFurniture.Where(f => f.setID == setID).ToList();
        var allUnlocked = InventoryManager.Instance?.GetAllUnlockedTemplates()
                          ?? new List<FurnitureTemplate>();

        if (_detailSetName != null)
        {
            int collected = setItems.Count(f => allUnlocked.Any(u => u.furnitureID == f.furnitureID));
            _detailSetName.text = $"Набір: {setID} ({collected}/{setItems.Count})";
        }

        foreach (var f in setItems)
        {
            var dot = new VisualElement();
            dot.AddToClassList("set-dot");
            bool has = allUnlocked.Any(u => u.furnitureID == f.furnitureID);
            dot.AddToClassList(has ? "collected" : "missing");
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
        if (_selected == null)
        {
            Debug.LogWarning("[DecoUI] Кнопка натиснута але _selected = null");
            return;
        }

        Debug.Log($"[DecoUI] Кнопка: {_selected.furnitureName}, InEditMode={InEditMode}");

        if (!InEditMode) return; // поза EditMode кнопка заблокована через SetEnabled

        // Перевіряємо чи є нерозміщений екземпляр для переміщення/розміщення
        bool isPlaced = PlacementRegistry.Instance?.GetAll()
            .Any(e => e.instance.templateID == _selected.furnitureID) == true;

        if (isPlaced)
        {
            // "ПЕРЕМІСТИТИ" — знаходимо GO і піднімаємо
            // GetAll() повертає IEnumerable<(GameObject go, FurnitureInstance instance)>
            // Шукаємо через foreach щоб уникнути nullable tuple проблеми
            GameObject targetGo = null;
            foreach (var (go, inst) in PlacementRegistry.Instance?.GetAll()
                                       ?? System.Linq.Enumerable.Empty<(GameObject, FurnitureInstance)>())
            {
                if (inst.templateID == _selected.furnitureID) { targetGo = go; break; }
            }

            if (targetGo != null)
            {
                var placedObj = targetGo.GetComponentInChildren<PlacedObject>()
                             ?? targetGo.GetComponent<PlacedObject>();
                if (placedObj != null)
                    PlacementController.Instance?.PickUpExisting(placedObj);
            }
        }
        else
        {
            // "РОЗМІСТИТИ" — починаємо розміщення нового екземпляра
            PlacementController.Instance?.BeginPlacement(_selected);
        }

        Hide();
    }

    private void RemoveFurniture(FurnitureTemplate item)
    {
        if (PlacementRegistry.Instance == null) return;

        GameObject targetGo = null;
        foreach (var (go, inst) in PlacementRegistry.Instance.GetAll())
        {
            if (inst.templateID == item.furnitureID) { targetGo = go; break; }
        }

        if (targetGo != null)
        {
            PlacementRegistry.Instance.Unregister(targetGo);
            Destroy(targetGo);
        }

        BuildGrid();
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Helpers
    // ─────────────────────────────────────────────

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