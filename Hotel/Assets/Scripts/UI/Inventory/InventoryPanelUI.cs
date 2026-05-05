// Assets/Scripts/UI/Inventory/InventoryPanelUI.cs
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryPanelUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset bookItemTemplate;

    private VisualElement _panel;
    private ScrollView _grid;
    public bool IsVisible { get; private set; }

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _panel = root.Q<VisualElement>("InventoryPanel");
        _grid  = root.Q<ScrollView>("InventoryGrid");

        // Кнопки сортування
        root.Q<Button>("BtnSortTitle") ?.RegisterCallback<ClickEvent>(_ => Refresh(SortType.ByTitle));
        root.Q<Button>("BtnSortAuthor")?.RegisterCallback<ClickEvent>(_ => Refresh(SortType.ByAuthor));
        root.Q<Button>("BtnSortPrice") ?.RegisterCallback<ClickEvent>(_ => Refresh(SortType.ByPrice));
        root.Q<Button>("BtnSortGenre") ?.RegisterCallback<ClickEvent>(_ => Refresh(SortType.ByGenre));

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += () => Refresh(SortType.ByTitle);

        Hide();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= () => Refresh(SortType.ByTitle);
    }

    public void Show()
    {
        IsVisible = true;
        if (_panel != null) _panel.style.display = DisplayStyle.Flex;
        Refresh(SortType.ByTitle);
    }

    public void Hide()
    {
        IsVisible = false;
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    private void Refresh(SortType sort)
    {
        if (_grid == null || !IsVisible) return;
        _grid.Clear();

        var books = InventoryManager.Instance?.GetSortedInventory(sort);
        if (books == null) return;

        foreach (var book in books)
        {
            var item = new InventoryItemUI(book, bookItemTemplate);
            if (item.Root != null) _grid.Add(item.Root);
        }
    }
}