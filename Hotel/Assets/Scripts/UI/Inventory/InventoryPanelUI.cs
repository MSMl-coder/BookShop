// Assets/Scripts/UI/Inventory/InventoryPanelUI.cs
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryPanelUI : MonoBehaviour
{
    [SerializeField] private UIDocument       uiDocument;
    [SerializeField] private VisualTreeAsset  bookItemTemplate;

    private ScrollView _grid;
    private Label      _countLabel;
    private SortType   _currentSort = SortType.ByTitle;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _grid       = root.Q<ScrollView>("InventoryGrid");
        _countLabel = root.Q<Label>("InvCount");

        root.Q<Button>("BtnSortTitle") ?.RegisterCallback<ClickEvent>(_ => SetSort(SortType.ByTitle,  "BtnSortTitle",  root));
        root.Q<Button>("BtnSortAuthor")?.RegisterCallback<ClickEvent>(_ => SetSort(SortType.ByAuthor, "BtnSortAuthor", root));
        root.Q<Button>("BtnSortPrice") ?.RegisterCallback<ClickEvent>(_ => SetSort(SortType.ByPrice,  "BtnSortPrice",  root));
        root.Q<Button>("BtnSortGenre") ?.RegisterCallback<ClickEvent>(_ => SetSort(SortType.ByGenre,  "BtnSortGenre",  root));
        root.Q<Button>("BtnSortRarity")?.RegisterCallback<ClickEvent>(_ => SetSort(SortType.ByRarity, "BtnSortRarity", root));

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += Refresh;
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
    }

    public void Refresh()
    {
        if (_grid == null) return;
        _grid.Clear();

        var books = InventoryManager.Instance?.GetSortedInventory(_currentSort);
        if (books == null) return;

        if (_countLabel != null)
            _countLabel.text = $"{books.Count} книг";

        foreach (var book in books)
        {
            if (bookItemTemplate == null) break;
            var item = new InventoryItemUI(
                book,
                bookItemTemplate,
                onHoverEnter: t => ShopUIManager.Instance?.ShowBookInfo(t),
                onHoverExit:  () => ShopUIManager.Instance?.HideBookInfo()
            );
            if (item.Root != null) _grid.Add(item.Root);
        }
    }

    private void SetSort(SortType sort, string btnName, VisualElement root)
    {
        _currentSort = sort;
        foreach (var b in new[]{"BtnSortTitle","BtnSortAuthor","BtnSortPrice","BtnSortGenre","BtnSortRarity"})
            root.Q<Button>(b)?.RemoveFromClassList("active");
        root.Q<Button>(btnName)?.AddToClassList("active");
        Refresh();
    }
}
