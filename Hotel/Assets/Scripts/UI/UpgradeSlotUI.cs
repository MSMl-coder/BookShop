// Assets/Scripts/UI/Upgrade/UpgradeSlotUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

/// Модальне вікно апгрейду шафи (UpgradeOverlay у MainShopUI.uxml).
///
/// Відкривається з PlayerInteraction при кліку на FurnitureSlot
/// у стані GameState.Preparation.
///
/// UNITY SETUP:
///   1. Компонент на тому ж GameObject що ShopUIManager.
///   2. UIDocument — той самий MainShopUI.
///   3. У PlayerInteraction.HandleRaycast розкоментувати виклик:
///      UpgradeSlotUI.Instance?.Open(slot);
public class UpgradeSlotUI : MonoBehaviour
{
    public static UpgradeSlotUI Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    // ── Elements ──
    private VisualElement _overlay;
    private Label         _slotNameLabel;
    private VisualElement _currentIcon;
    private Label         _currentName;
    private Label         _currentClass;
    private ScrollView    _optionsList;
    private Button        _confirmBtn;
    private Button        _cancelBtn;
    private Button        _closeBtn;

    // ── State ──
    private FurnitureSlot     _targetSlot;
    private FurnitureTemplate _selectedTemplate;
    private List<FurnitureTemplate> _availableOptions = new();

    // Назви класів українською
    private static readonly System.Collections.Generic.Dictionary<FurnitureClass, string> ClassNames = new()
    {
        { FurnitureClass.WallShelf,    "НАСТІННА ПОЛИЦЯ" },
        { FurnitureClass.CenterIsland, "ОСТРІВНИЙ СТЕНД" },
        { FurnitureClass.Decor,        "ДЕКОР"           },
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void OnEnable()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        _overlay      = root.Q<VisualElement>("UpgradeOverlay");
        _slotNameLabel = root.Q<Label>("UpgradeSlotName");
        _currentIcon  = root.Q<VisualElement>("UpgradeCurrentIcon");
        _currentName  = root.Q<Label>("UpgradeCurrentName");
        _currentClass = root.Q<Label>("UpgradeCurrentClass");
        _optionsList  = root.Q<ScrollView>("UpgradeOptionsList");
        _confirmBtn   = root.Q<Button>("UpgradeBtnConfirm");
        _cancelBtn    = root.Q<Button>("UpgradeBtnCancel");
        _closeBtn     = root.Q<Button>("UpgradeBtnClose");

        if (_overlay == null)
        {
            Debug.LogWarning("[UpgradeUI] 'UpgradeOverlay' не знайдено у UXML.");
            return;
        }

        _confirmBtn?.RegisterCallback<ClickEvent>(_ => ConfirmUpgrade());
        _cancelBtn ?.RegisterCallback<ClickEvent>(_ => Close());
        _closeBtn  ?.RegisterCallback<ClickEvent>(_ => Close());

        // Клік на backdrop закриває модал
        _overlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _overlay) Close();
        });

        _overlay.AddToClassList("hidden");
    }

    // ─────────────────────────────────────────────
    #region Public API
    // ─────────────────────────────────────────────

    /// Відкрити меню для вказаного слота
    public void Open(FurnitureSlot slot)
    {
        if (slot == null || _overlay == null) return;

        _targetSlot      = slot;
        _selectedTemplate = null;

        // Збираємо доступні апгрейди: розблоковані + того ж класу
        _availableOptions = GetOptionsForSlot(slot);

        FillCurrentInfo(slot);
        BuildOptionsList();
        UpdateConfirmButton();

        _overlay.RemoveFromClassList("hidden");

        Debug.Log($"[UpgradeUI] Відкрито для слота: {slot.name}, опцій: {_availableOptions.Count}");
    }

    /// Закрити модал
    public void Close()
    {
        _overlay?.AddToClassList("hidden");
        _targetSlot       = null;
        _selectedTemplate = null;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Build UI
    // ─────────────────────────────────────────────

    private void FillCurrentInfo(FurnitureSlot slot)
    {
        // Назва слота
        if (_slotNameLabel != null)
            _slotNameLabel.text = slot.name;

        // Поточний шаблон через рефлексію (currentTemplate — SerializeField, не public)
        var currentField = typeof(FurnitureSlot).GetField("currentTemplate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var current = currentField?.GetValue(slot) as FurnitureTemplate;

        if (current != null)
        {
            if (_currentIcon != null && current.icon != null)
                _currentIcon.style.backgroundImage = new StyleBackground(current.icon);

            if (_currentName != null) _currentName.text = current.furnitureName;
            if (_currentClass != null)
                _currentClass.text = ClassNames.TryGetValue(current.furnitureClass, out var cn) ? cn : "";
        }
        else
        {
            if (_currentName != null) _currentName.text  = "Порожньо";
            if (_currentClass != null) _currentClass.text =
                ClassNames.TryGetValue(slot.allowedClass, out var cn) ? cn : "";
        }
    }

    private void BuildOptionsList()
    {
        if (_optionsList == null) return;
        _optionsList.Clear();

        int playerMoney = EconomyManager.Instance?.Money ?? 0;

        if (_availableOptions.Count == 0)
        {
            var emptyLabel = new Label("Немає доступних апгрейдів.\nРозблокуйте меблі в Декорі.");
            emptyLabel.style.color     = new StyleColor(new Color(0.6f, 0.45f, 0.3f));
            emptyLabel.style.fontSize  = 12;
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            emptyLabel.style.whiteSpace = WhiteSpace.Normal;
            emptyLabel.style.paddingTop = emptyLabel.style.paddingBottom = 12;
            emptyLabel.style.paddingLeft = emptyLabel.style.paddingRight = 8;
            _optionsList.Add(emptyLabel);
            return;
        }

        foreach (var tmpl in _availableOptions)
        {
            bool canAfford = playerMoney >= tmpl.basePrice;
            var row = MakeOptionRow(tmpl, canAfford);
            _optionsList.Add(row);
        }
    }

    private VisualElement MakeOptionRow(FurnitureTemplate tmpl, bool canAfford)
    {
        var row = new VisualElement();
        row.AddToClassList("upgrade-option");
        if (!canAfford) row.AddToClassList("cant-afford");
        if (tmpl == _selectedTemplate) row.AddToClassList("selected");

        // Іконка
        var icon = new VisualElement();
        icon.AddToClassList("upgrade-option__icon");
        if (tmpl.icon != null)
            icon.style.backgroundImage = new StyleBackground(tmpl.icon);
        row.Add(icon);

        // Інфо
        var info = new VisualElement();
        info.AddToClassList("upgrade-option__info");

        var nameL = new Label(tmpl.furnitureName);
        nameL.AddToClassList("upgrade-option__name");
        info.Add(nameL);

        // Бонус з нового поля (якщо є) або заглушка
        string bonusText = GetBonusText(tmpl);
        if (!string.IsNullOrEmpty(bonusText))
        {
            var bonusL = new Label(bonusText);
            bonusL.AddToClassList("upgrade-option__bonus");
            info.Add(bonusL);
        }

        row.Add(info);

        // Ціна
        var priceL = new Label($"{tmpl.basePrice} ₴");
        priceL.AddToClassList("upgrade-option__price");
        row.Add(priceL);

        // Клік
        FurnitureTemplate captured = tmpl;
        row.RegisterCallback<ClickEvent>(_ =>
        {
            if (!canAfford) return;
            SelectOption(captured);
        });

        return row;
    }

    private void SelectOption(FurnitureTemplate tmpl)
    {
        _selectedTemplate = tmpl;

        // Перебудовуємо список щоб оновити selected-клас
        BuildOptionsList();
        UpdateConfirmButton();
    }

    private void UpdateConfirmButton()
    {
        if (_confirmBtn == null) return;

        bool hasSelection = _selectedTemplate != null;
        bool canAfford    = hasSelection &&
                            (EconomyManager.Instance?.Money ?? 0) >= _selectedTemplate.basePrice;

        _confirmBtn.SetEnabled(hasSelection && canAfford);

        if (!hasSelection)
            _confirmBtn.text = "ВСТАНОВИТИ";
        else if (!canAfford)
            _confirmBtn.text = "МАЛО ГРОШЕЙ";
        else
            _confirmBtn.text = $"ВСТАНОВИТИ  {_selectedTemplate.basePrice} ₴";
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Confirm
    // ─────────────────────────────────────────────

    private void ConfirmUpgrade()
    {
        if (_targetSlot == null || _selectedTemplate == null) return;
        if (EconomyManager.Instance == null) return;

        bool paid = EconomyManager.Instance.SpendMoney(_selectedTemplate.basePrice);
        if (!paid)
        {
            Debug.LogWarning("[UpgradeUI] Недостатньо грошей для апгрейду.");
            return;
        }

        _targetSlot.UpgradeFurniture(_selectedTemplate);

        Debug.Log($"[UpgradeUI] Апгрейд виконано: {_selectedTemplate.furnitureName} " +
                  $"за {_selectedTemplate.basePrice} ₴");

        Close();
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Helpers
    // ─────────────────────────────────────────────

    /// Повертає розблоковані меблі відповідного класу (крім вже встановленого)
    private List<FurnitureTemplate> GetOptionsForSlot(FurnitureSlot slot)
    {
        if (InventoryManager.Instance == null) return new List<FurnitureTemplate>();

        var unlocked = InventoryManager.Instance.GetUnlockedFurnitureByClass(slot.allowedClass);

        // Виключаємо поточний шаблон щоб не пропонувати встановити те саме
        var currentField = typeof(FurnitureSlot).GetField("currentTemplate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var current = currentField?.GetValue(slot) as FurnitureTemplate;

        return unlocked.Where(f => f != current).ToList();
    }

    /// Безпечно читає bonusDescription з FurnitureTemplate
    /// (поле додано нашим патчем — може не бути в оригінальному файлі)
    private static string GetBonusText(FurnitureTemplate tmpl)
    {
        var field = typeof(FurnitureTemplate).GetField("bonusDescription",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(tmpl) as string ?? "";
    }

    #endregion
}
