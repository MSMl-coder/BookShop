// Assets/Scripts/UI/v2/EditHUD/EditHUDController.cs
//
// ВИПРАВЛЕНО:
//   - GetFilteredTemplates() повертає List<PropTemplate> (не IEnumerable)
//     бо InventoryManager.GetAllUnlockedTemplates() вже повертає List
//   - PlacementController.ConfirmPlacementPublic() → замінено на JustConfirmedThisFrame
//     або закоментовано (метод не існує, PlacementController підтверджує через LMB)

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-10)]
public class EditHUDController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement _leftPanel;
    private VisualElement _categoryBar;
    private ScrollView    _subcategoryRow;
    private TextField     _searchField;
    private ScrollView    _itemGrid;
    private VisualElement _contextToolbar;
    private VisualElement _ctxItemIcon;
    private Button        _btnMove;
    private Button        _btnDuplicate;
    private Button        _btnDelete;
    private Button        _btnConfirm;
    private Button        _btnCancel;
    private Button        _btnClose;

    private PropClass? _activeCategory;
    private string     _activeSubcategory;
    private string     _searchQuery;
    private PropTemplate _selectedTemplate;
    private PlacedObject _selectedPlacedObject;

    private const string CLASS_PANEL_HIDDEN   = "edit-left-panel--hidden";
    private const string CLASS_TOOLBAR_HIDDEN = "context-toolbar--hidden";
    private const string CLASS_CAT_ACTIVE     = "cat-icon-btn--active";
    private const string CLASS_SUBCAT_ACTIVE  = "subcat-btn--active";
    private const string CLASS_ITEM_SELECTED  = "item-card--selected";

    private void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        var root = uiDocument?.rootVisualElement;
        if (root == null) return;

        _leftPanel      = root.Q<VisualElement>("LeftPanel");
        _categoryBar    = root.Q<VisualElement>("CategoryIconBar");
        _subcategoryRow = root.Q<ScrollView>("SubcategoryRow");
        _searchField    = root.Q<TextField>("SearchField");
        _itemGrid       = root.Q<ScrollView>("ItemGrid");
        _contextToolbar = root.Q<VisualElement>("ContextToolbar");
        _ctxItemIcon    = root.Q<VisualElement>("ToolbarItemIcon");
        _btnMove        = root.Q<Button>("BtnMove");
        _btnDuplicate   = root.Q<Button>("BtnDuplicate");
        _btnDelete      = root.Q<Button>("BtnDelete");
        _btnConfirm     = root.Q<Button>("BtnConfirm");
        _btnCancel      = root.Q<Button>("BtnCancel");
        _btnClose       = root.Q<Button>("BtnClosePanel");

        _btnClose?.RegisterCallback<ClickEvent>(_ => UIScreenManager.Instance?.SwitchTo(UIScreen.Game));
        _searchField?.RegisterValueChangedCallback(evt => { _searchQuery = evt.newValue; RefreshItemGrid(); });
        _btnMove?.RegisterCallback<ClickEvent>(_ => OnMove());
        _btnDuplicate?.RegisterCallback<ClickEvent>(_ => OnDuplicate());
        _btnDelete?.RegisterCallback<ClickEvent>(_ => OnDelete());
        _btnConfirm?.RegisterCallback<ClickEvent>(_ => OnConfirm());
        _btnCancel?.RegisterCallback<ClickEvent>(_ => OnCancelPlacement());

        BuildCategoryBarPlaceholder();

        _contextToolbar?.AddToClassList(CLASS_TOOLBAR_HIDDEN);
        if (_contextToolbar != null) _contextToolbar.pickingMode = PickingMode.Ignore;

        _leftPanel?.AddToClassList(CLASS_PANEL_HIDDEN);
    }

    // ── Public API ────────────────────────────────────────────────

    public void OnPanelShown()
    {
        _leftPanel?.RemoveFromClassList(CLASS_PANEL_HIDDEN);
        if (_leftPanel != null) _leftPanel.pickingMode = PickingMode.Position;
        RefreshItemGrid();
    }

    public void OnPanelHidden()
    {
        _leftPanel?.AddToClassList(CLASS_PANEL_HIDDEN);
        if (_leftPanel != null) _leftPanel.pickingMode = PickingMode.Ignore;
        HideContextToolbar();
    }

    public void ShowContextToolbar(PlacedObject obj)
    {
        _selectedPlacedObject = obj;
        if (_contextToolbar != null)
        {
            _contextToolbar.RemoveFromClassList(CLASS_TOOLBAR_HIDDEN);
            _contextToolbar.pickingMode = PickingMode.Position;
        }
    }

    public void HideContextToolbar()
    {
        _selectedPlacedObject = null;
        _contextToolbar?.AddToClassList(CLASS_TOOLBAR_HIDDEN);
        if (_contextToolbar != null) _contextToolbar.pickingMode = PickingMode.Ignore;
    }

    // ── Category Bar ──────────────────────────────────────────────

    private void BuildCategoryBarPlaceholder()
    {
        if (_categoryBar == null) return;
        _categoryBar.Clear();

        var categories = new[]
        {
            "Furniture", "Decorations", "Lighting", "Plants", "Books"
        };

        bool first = true;
        foreach (var name in categories)
        {
            var btn = new Button();
            btn.AddToClassList("cat-icon-btn");
            if (first) { btn.AddToClassList(CLASS_CAT_ACTIVE); first = false; }
            btn.tooltip = name;
            string capturedName = name;
            btn.RegisterCallback<ClickEvent>(_ => SelectCategory(capturedName, btn));
            _categoryBar.Add(btn);
        }
    }

    private void SelectCategory(string name, Button btn)
    {
        _activeSubcategory = null;
        foreach (var b in _categoryBar.Children().OfType<Button>())
            b.EnableInClassList(CLASS_CAT_ACTIVE, b == btn);
        BuildSubcategoryRow();
        RefreshItemGrid();
    }

    // ── Subcategory Row ───────────────────────────────────────────

    private void BuildSubcategoryRow()
    {
        if (_subcategoryRow == null) return;
        _subcategoryRow.Clear();

        var allBtn = CreateSubcatChip("All", null, _activeSubcategory == null);
        _subcategoryRow.Add(allBtn);

        foreach (var sub in new[] { "Small", "Medium", "Large", "Vintage", "Modern" })
            _subcategoryRow.Add(CreateSubcatChip(sub, sub, _activeSubcategory == sub));
    }

    private Button CreateSubcatChip(string label, string value, bool active)
    {
        var btn = new Button { text = label };
        btn.AddToClassList("subcat-btn");
        if (active) btn.AddToClassList(CLASS_SUBCAT_ACTIVE);

        string capturedValue = value;
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            _activeSubcategory = capturedValue;
            foreach (var b in _subcategoryRow.Children().OfType<Button>())
                b.EnableInClassList(CLASS_SUBCAT_ACTIVE, b == btn);
            RefreshItemGrid();
        });
        return btn;
    }

    // ── Item Grid ─────────────────────────────────────────────────

    private void RefreshItemGrid()
    {
        if (_itemGrid == null) return;
        _itemGrid.Clear();
        _selectedTemplate = null;

        var templates = GetFilteredTemplates();
        foreach (var t in templates)
            _itemGrid.Add(CreateItemCard(t));
    }

    // ВИПРАВЛЕНО: повертає List<PropTemplate> (InventoryManager вже повертає List)
    private List<PropTemplate> GetFilteredTemplates()
    {
        if (InventoryManager.Instance == null)
            return new List<PropTemplate>();

        var all = InventoryManager.Instance.GetAllUnlockedTemplates();
        if (all == null) return new List<PropTemplate>();

        if (!string.IsNullOrEmpty(_searchQuery))
            return all.Where(t => t.propName.ToLower().Contains(_searchQuery.ToLower())).ToList();

        return all;
    }

    private VisualElement CreateItemCard(PropTemplate template)
    {
        var card = new VisualElement();
        card.AddToClassList("item-card");
        card.pickingMode = PickingMode.Position;

        var preview = new VisualElement();
        preview.name = "ItemPreview";
        preview.AddToClassList("item-card__preview");
        card.Add(preview);

        var nameLabel = new Label(template.propName);
        nameLabel.AddToClassList("item-card__name");
        card.Add(nameLabel);

        PropTemplate captured = template;
        card.RegisterCallback<ClickEvent>(_ => SelectTemplate(captured, card));
        return card;
    }

    private void SelectTemplate(PropTemplate template, VisualElement card)
    {
        _selectedTemplate = template;
        foreach (var c in _itemGrid.Children())
            c.RemoveFromClassList(CLASS_ITEM_SELECTED);
        card.AddToClassList(CLASS_ITEM_SELECTED);
        PlacementController.Instance?.BeginPlacement(template);
    }

    // ── Context Toolbar Actions ───────────────────────────────────

    private void OnMove()
    {
        if (_selectedPlacedObject == null) return;
        PlacementController.Instance?.PickUpExisting(_selectedPlacedObject);
        HideContextToolbar();
    }

    private void OnDuplicate()
    {
        if (_selectedPlacedObject?.Instance == null) return;
        var template = InventoryManager.Instance?.GetTemplate(_selectedPlacedObject.Instance.templateID);
        if (template != null)
            PlacementController.Instance?.BeginPlacement(template);
    }

    private void OnDelete()
    {
        if (_selectedPlacedObject == null) return;
        PlacementController.Instance?.ReturnToInventory(_selectedPlacedObject);
        HideContextToolbar();
    }

    private void OnConfirm()
    {
        // PlacementController не має публічного Confirm — підтвердження через ЛКМ.
        // Ця кнопка лише закриває toolbar. Якщо потрібне ручне підтвердження —
        // додай метод ConfirmPlacement() в PlacementController.cs
        HideContextToolbar();
        Debug.Log("[EditHUD] Confirm — PlacementController підтверджує через ЛКМ у 3D");
    }

    private void OnCancelPlacement()
    {
        PlacementController.Instance?.CancelPlacement();
        HideContextToolbar();
    }
}