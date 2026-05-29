// ═══════════════════════════════════════════════════════════
// DecorModalController.cs — Decor bottom-strip modal
// Path: Assets/Scripts/UI/Bookshop/Controllers/DecorModalController.cs
//
// Left: category sidebar (vertical).
// Right top: horizontal scroll of item cards for selected category.
// Right bottom: editor (color, rotate, place/cancel) — slides up when item selected.
//
// Integration: InventoryManager.GetAllUnlockedTemplates() for available furniture.
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class DecorModalController : MonoBehaviour
{
    private VisualElement _root;
    private BookshopUIController _master;

    private ScrollView _categoriesList;
    private ScrollView _itemsList;
    private VisualElement _editor;
    private Label _pageTitle;

    private Label _editorIcon;
    private Label _editorName;
    private Label _editorMeta;
    private VisualElement _swatchesContainer;
    private Button _btnCancel, _btnPlace, _btnRotateLeft, _btnRotateRight;

    private PropClass? _activeCategory = null;
    private PropTemplate _selectedItem = null;

    public void Initialize(VisualElement root, BookshopUIController master)
    {
        _root = root;
        _master = master;

        _categoriesList = root.Q<ScrollView>("DecorCategories");
        _itemsList      = root.Q<ScrollView>("DecorItems");
        _editor         = root.Q<VisualElement>("DecorEditor");
        _pageTitle      = root.Q<Label>("DecorPageTitle");

        _editorIcon = root.Q<Label>("DecorEditorIcon");
        _editorName = root.Q<Label>("DecorEditorName");
        _editorMeta = root.Q<Label>("DecorEditorMeta");
        _swatchesContainer = root.Q<VisualElement>("DecorColorSwatches");

        _btnCancel = root.Q<Button>("DecorCancel");
        _btnPlace  = root.Q<Button>("DecorPlace");
        _btnRotateLeft  = root.Q<Button>("DecorRotateLeft");
        _btnRotateRight = root.Q<Button>("DecorRotateRight");

        if (_btnCancel != null) _btnCancel.clicked += CloseEditor;
        if (_btnPlace  != null) _btnPlace.clicked  += OnPlace;
        if (_btnRotateLeft  != null) _btnRotateLeft.clicked  += () => Rotate(-90f);
        if (_btnRotateRight != null) _btnRotateRight.clicked += () => Rotate(90f);

        BuildColorSwatches();

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnFurnitureChanged += RefreshItems;

        BuildCategories();
        RefreshItems();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnFurnitureChanged -= RefreshItems;
    }

    // ─────────────────────────────────────────────
    #region Categories
    // ─────────────────────────────────────────────

    private void BuildCategories()
    {
        if (_categoriesList == null) return;
        _categoriesList.Clear();

        // "All" item
        _categoriesList.Add(MakeCategoryRow("🪑", "All", null, isActive: _activeCategory == null));

        // PropClass enum values
        foreach (PropClass cls in System.Enum.GetValues(typeof(PropClass)))
        {
            string icon = GetClassIcon(cls);
            string name = GetClassName(cls);
            int count = CountByClass(cls);
            _categoriesList.Add(MakeCategoryRow(icon, name, cls, isActive: _activeCategory == cls, count));
        }
    }

    private VisualElement MakeCategoryRow(string icon, string name, PropClass? cls, bool isActive = false, int count = 0)
    {
        var row = new VisualElement();
        row.AddToClassList("decor-cat-row");
        if (isActive) row.AddToClassList("active");

        var iconEl = new VisualElement();
        iconEl.AddToClassList("decor-cat-row__icon");
        iconEl.Add(new Label(icon));
        row.Add(iconEl);

        var nameEl = new Label(name);
        nameEl.AddToClassList("decor-cat-row__name");
        row.Add(nameEl);

        if (count > 0)
        {
            var numEl = new Label(count.ToString());
            numEl.AddToClassList("decor-cat-row__num");
            row.Add(numEl);
        }

        row.RegisterCallback<ClickEvent>(_ =>
        {
            _activeCategory = cls;
            BuildCategories();
            RefreshItems();
            if (_pageTitle != null) _pageTitle.text = name;
        });

        return row;
    }

    private int CountByClass(PropClass cls)
    {
        var all = InventoryManager.Instance?.GetAllUnlockedTemplates();
        if (all == null) return 0;
        return all.Count(t => t != null && t.propClass == cls);
    }

    private string GetClassIcon(PropClass cls) => cls.ToString().ToLower() switch
    {
        "shelf" or "cabinet" => "🗄",
        "island" or "table"  => "🪑",
        "lamp" or "lighting" => "💡",
        "plant"              => "🪴",
        "decor"              => "🖼",
        _ => "📜"
    };

    private string GetClassName(PropClass cls)
    {
        // Show in English
        return cls.ToString();
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Items
    // ─────────────────────────────────────────────

    public void RefreshItems()
    {
        if (_itemsList == null) return;
        _itemsList.Clear();

        var templates = InventoryManager.Instance?.GetAllUnlockedTemplates();
        if (templates == null) return;

        var filtered = _activeCategory.HasValue
            ? templates.Where(t => t.propClass == _activeCategory.Value)
            : templates;

        foreach (var t in filtered)
            _itemsList.Add(MakeItemCard(t));
    }

    private VisualElement MakeItemCard(PropTemplate template)
    {
        if (_master?.DecorItemCardTemplate == null)
            return new Label(template.propName);

        var card = _master.DecorItemCardTemplate.Instantiate().ElementAt(0);
        if (_selectedItem == template) card.AddToClassList("selected");

        var iconLabel = card.Q<Label>("DecorItemIcon");
        var nameLabel = card.Q<Label>("DecorItemName");
        var priceLabel = card.Q<Label>("DecorItemPrice");

        if (iconLabel != null) iconLabel.text = "🪑"; // TODO: from template.icon
        if (nameLabel != null) nameLabel.text = template.propName;
        if (priceLabel != null) priceLabel.text = "—"; // template has no price field

        PropTemplate captured = template;
        card.RegisterCallback<ClickEvent>(_ => SelectItem(captured));

        return card;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Editor
    // ─────────────────────────────────────────────

    private void SelectItem(PropTemplate template)
    {
        _selectedItem = template;
        RefreshItems(); // re-render to show "selected" class

        if (_editor != null) _editor.RemoveFromClassList("hidden");
        if (_editorName != null) _editorName.text = template.propName;
        if (_editorMeta != null) _editorMeta.text = $"ID-{template.propID}";
        if (_editorIcon != null) _editorIcon.text = "🪑";
    }

    public void CloseEditor()
    {
        _selectedItem = null;
        if (_editor != null) _editor.AddToClassList("hidden");
        RefreshItems();
    }

    private void OnPlace()
    {
        if (_selectedItem == null) return;
        // Hook into EditModeManager / placement system
        // EditModeManager.Instance?.StartPlacement(_selectedItem);
        BookshopUIController.Instance?.Toast?.Show("🪑", "Furniture placed", ToastType.Good);
        CloseEditor();
    }

    private void Rotate(float degrees)
    {
        // Hook into placement system rotation
        BookshopUIController.Instance?.Toast?.Show("↻", $"Rotated {degrees:F0}°", ToastType.Info);
    }

    private void BuildColorSwatches()
    {
        if (_swatchesContainer == null) return;
        _swatchesContainer.Clear();

        var colors = new[] {
            "#6b4226", "#3d2515", "#8b6520", "#6b2a2a", "#3a4868"
        };

        for (int i = 0; i < colors.Length; i++)
        {
            var swatch = new VisualElement();
            swatch.AddToClassList("decor-editor__swatch");
            if (i == 0) swatch.AddToClassList("active");

            if (ColorUtility.TryParseHtmlString(colors[i], out var col))
                swatch.style.backgroundColor = col;

            VisualElement captured = swatch;
            swatch.RegisterCallback<ClickEvent>(_ =>
            {
                foreach (var s in _swatchesContainer.Children())
                    s.RemoveFromClassList("active");
                captured.AddToClassList("active");
            });

            _swatchesContainer.Add(swatch);
        }
    }

    #endregion
}
