// Assets/Scripts/UI/Upgrade/UpgradeSlotUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

/// Модальне вікно апгрейду шафи (UpgradeOverlay у MainShopUI.uxml).
public class UpgradeSlotUI : MonoBehaviour
{
    public static UpgradeSlotUI Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _overlay;
    private Label         _slotNameLabel;
    private VisualElement _currentIcon;
    private Label         _currentName;
    private Label         _currentClass;
    private ScrollView    _optionsList;
    private Button        _confirmBtn;
    private Button        _cancelBtn;
    private Button        _closeBtn;

    private FurnitureSlot     _targetSlot;
    private FurnitureTemplate _selectedTemplate;
    private List<FurnitureTemplate> _availableOptions = new();

    private static readonly Dictionary<FurnitureClass, string> ClassNames = new()
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

        _overlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _overlay) Close();
        });

        _overlay.AddToClassList("hidden");
    }

    // ─────────────────────────────────────────────
    #region Public API
    // ─────────────────────────────────────────────

    public void Open(FurnitureSlot slot)
    {
        if (slot == null || _overlay == null) return;

        _targetSlot       = slot;
        _selectedTemplate = null;
        _availableOptions = GetOptionsForSlot(slot);

        FillCurrentInfo(slot);
        BuildOptionsList();
        UpdateConfirmButton();

        _overlay.RemoveFromClassList("hidden");
        Debug.Log($"[UpgradeUI] Відкрито для слота: {slot.name}, опцій: {_availableOptions.Count}");
    }

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
        if (_slotNameLabel != null)
            _slotNameLabel.text = slot.name;

        // ВИПРАВЛЕНО: замість рефлексії використовуємо публічний геттер CurrentTemplate
        // Раніше: typeof(FurnitureSlot).GetField("currentTemplate", NonPublic|Instance) — крихко і повільно
        var current = slot.CurrentTemplate;

        if (current != null)
        {
            if (_currentIcon != null && current.icon != null)
                _currentIcon.style.backgroundImage = new StyleBackground(current.icon);

            if (_currentName != null)  _currentName.text  = current.furnitureName;
            if (_currentClass != null)
                _currentClass.text = ClassNames.TryGetValue(current.furnitureClass, out var cn) ? cn : "";
        }
        else
        {
            if (_currentName  != null) _currentName.text  = "Порожньо";
            if (_currentClass != null)
                _currentClass.text = ClassNames.TryGetValue(slot.allowedClass, out var cn) ? cn : "";
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
            emptyLabel.style.color     = new StyleColor(new UnityEngine.Color(0.6f, 0.45f, 0.3f));
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

        var icon = new VisualElement();
        icon.AddToClassList("upgrade-option__icon");
        if (tmpl.icon != null)
            icon.style.backgroundImage = new StyleBackground(tmpl.icon);
        row.Add(icon);

        var info = new VisualElement();
        info.AddToClassList("upgrade-option__info");

        var nameL = new Label(tmpl.furnitureName);
        nameL.AddToClassList("upgrade-option__name");
        info.Add(nameL);

        // ВИПРАВЛЕНО: замість рефлексії для читання bonusDescription — читаємо напряму
        // bonusDescription є public полем у виправленому FurnitureTemplate
        if (!string.IsNullOrEmpty(tmpl.bonusDescription))
        {
            var bonusL = new Label(tmpl.bonusDescription);
            bonusL.AddToClassList("upgrade-option__bonus");
            info.Add(bonusL);
        }

        row.Add(info);

        var priceL = new Label($"{tmpl.basePrice} ₴");
        priceL.AddToClassList("upgrade-option__price");
        row.Add(priceL);

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
        BuildOptionsList();
        UpdateConfirmButton();
    }

    private void UpdateConfirmButton()
    {
        if (_confirmBtn == null) return;

                bool hasSelection = _selectedTemplate != null;
                bool canAfford    = hasSelection &&
                        (EconomyManager.Instance?.Money ?? 0) >= 
                        _selectedTemplate.basePrice;  // ← Краш якщо hasSelection=false!

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
        Debug.Log($"[UpgradeUI] Апгрейд: {_selectedTemplate.furnitureName} за {_selectedTemplate.basePrice} ₴");
        Close();
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Helpers
    // ─────────────────────────────────────────────

    private List<FurnitureTemplate> GetOptionsForSlot(FurnitureSlot slot)
    {
        if (InventoryManager.Instance == null) return new List<FurnitureTemplate>();

        var unlocked = InventoryManager.Instance.GetUnlockedFurnitureByClass(slot.allowedClass);

        // ВИПРАВЛЕНО: замість рефлексії для читання currentTemplate — використовуємо геттер
        var current = slot.CurrentTemplate;

        return unlocked.Where(f => f != current).ToList();
    }

    #endregion
}
