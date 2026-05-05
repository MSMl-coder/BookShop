// Assets/Scripts/UI/Cabinet/CabinetPanelUI.cs
using UnityEngine;
using UnityEngine.UIElements;

public class CabinetPanelUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement _panel;
    private ScrollView _cabinetList;
    private ScrollView _shelfList;
    private Label _selectedShelfLabel;

    private Cabinet _selectedCabinet;
    private Shelf _selectedShelf;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _panel             = root.Q<VisualElement>("CabinetPanel");
        _cabinetList       = root.Q<ScrollView>("CabinetList");
        _shelfList         = root.Q<ScrollView>("ShelfList");
        _selectedShelfLabel = root.Q<Label>("SelectedShelfLabel");

        var footer = root.Q<VisualElement>("Footer");
        footer?.Q<Button>("BtnPushOne")?.RegisterCallback<ClickEvent>(_ => PushOne());
        footer?.Q<Button>("BtnPushAll")?.RegisterCallback<ClickEvent>(_ => PushAll());
        footer?.Q<Button>("BtnPopOne") ?.RegisterCallback<ClickEvent>(_ => PopOne());
        footer?.Q<Button>("BtnPopAll") ?.RegisterCallback<ClickEvent>(_ => PopAll());

        Hide();
    }

    public void Open(Cabinet cabinet)
    {
        _selectedCabinet = cabinet;
        _selectedShelf = null;

        if (_panel != null) _panel.style.display = DisplayStyle.Flex;
        BuildShelfList(cabinet);
    }

    public void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    private void BuildShelfList(Cabinet cabinet)
    {
        _shelfList?.Clear();
        _cabinetList?.Clear();

        // Назва шафи
        var header = new Label(cabinet.cabinetName);
        header.style.fontSize = 18;
        _cabinetList?.Add(header);

        // Список полиць
        for (int i = 0; i < cabinet.shelves.Count; i++)
        {
            int index = i;
            var btn = new Button { text = $"Shelf {index + 1}  [{cabinet.shelves[i].GetBookCount()} books]" };
            btn.style.height = 40;
            btn.clicked += () => SelectShelf(cabinet.shelves[index], index + 1);
            _shelfList?.Add(btn);
        }
    }

    private void SelectShelf(Shelf shelf, int number)
    {
        _selectedShelf = shelf;
        if (_selectedShelfLabel != null)
            _selectedShelfLabel.text = $"{_selectedCabinet?.cabinetName}: Shelf {number}";
    }

    private void PushOne()
    {
        if (_selectedShelf != null)
            InventoryManager.Instance?.PushOneToShelf(_selectedShelf);
        RefreshShelfLabel();
    }

    private void PushAll()
    {
        if (_selectedShelf != null)
            InventoryManager.Instance?.PushAllToShelf(_selectedShelf);
        RefreshShelfLabel();
    }

    private void PopOne()
    {
        if (_selectedShelf == null) return;
        BookInstance book = _selectedShelf.TakeLastBook();
        if (book != null) InventoryManager.Instance?.AddExistingBook(book);
        RefreshShelfLabel();
    }

    private void PopAll()
    {
        if (_selectedShelf == null) return;
        BookInstance book;
        do
        {
            book = _selectedShelf.TakeLastBook();
            if (book != null) InventoryManager.Instance?.AddExistingBook(book);
        }
        while (book != null);
        RefreshShelfLabel();
    }

    private void RefreshShelfLabel()
    {
        if (_selectedShelf == null || _selectedShelfLabel == null) return;
        // Оновлюємо лічильник книг
        int shelfIndex = _selectedCabinet?.shelves.IndexOf(_selectedShelf) ?? 0;
        _selectedShelfLabel.text =
            $"{_selectedCabinet?.cabinetName}: Shelf {shelfIndex + 1}  [{_selectedShelf.GetBookCount()} books]";
    }
}