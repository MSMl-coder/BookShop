// Assets/Scripts/UI/Cabinet/CabinetPanelUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class CabinetPanelUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private ScrollView _cabinetList;
    private ScrollView _shelfList;
    private Label      _selectedLabel;

    private Cabinet _selectedCabinet;
    private Shelf   _selectedShelf;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _cabinetList   = root.Q<ScrollView>("CabinetList");
        _shelfList     = root.Q<ScrollView>("ShelfList");
        _selectedLabel = root.Q<Label>("SelectedShelfLabel");

        root.Q<Button>("BtnPushOne")?.RegisterCallback<ClickEvent>(_ => PushOne());
        root.Q<Button>("BtnPushAll")?.RegisterCallback<ClickEvent>(_ => PushAll());
        root.Q<Button>("BtnPopOne") ?.RegisterCallback<ClickEvent>(_ => PopOne());
        root.Q<Button>("BtnPopAll") ?.RegisterCallback<ClickEvent>(_ => PopAll());
    }

    public void Open(Cabinet cabinet)
    {
        _selectedCabinet = cabinet;
        _selectedShelf   = null;
        BuildCabinetList();
    }

    private void BuildCabinetList()
    {
        if (_cabinetList == null) return;
        _cabinetList.Clear();

        var all = FindObjectsByType<Cabinet>(FindObjectsSortMode.None);
        foreach (var cab in all)
        {
            var item = MakeCabinetItem(cab);
            _cabinetList.Add(item);
        }

        // Будуємо полиці для вибраної шафи
        if (_selectedCabinet != null)
            BuildShelfList(_selectedCabinet);
    }

    private VisualElement MakeCabinetItem(Cabinet cab)
    {
        var row = new VisualElement();
        row.AddToClassList("cabinet-item");
        if (cab == _selectedCabinet) row.AddToClassList("selected");

        var name  = new Label(cab.cabinetName);   name.AddToClassList("cabinet-item__name");
        var count = new Label($"{cab.shelves.Count} полиці"); count.AddToClassList("cabinet-item__count");
        row.Add(name); row.Add(count);

        row.RegisterCallback<ClickEvent>(_ =>
        {
            _selectedCabinet = cab;
            _selectedShelf   = null;
            BuildCabinetList();
        });
        return row;
    }

    private void BuildShelfList(Cabinet cabinet)
    {
        if (_shelfList == null) return;
        _shelfList.Clear();

        for (int i = 0; i < cabinet.shelves.Count; i++)
        {
            int idx   = i;
            Shelf shelf = cabinet.shelves[i];

            var item = new VisualElement();
            item.AddToClassList("shelf-item");
            if (shelf == _selectedShelf) item.AddToClassList("selected");

            var nameL = new Label($"Полиця {idx + 1}"); nameL.AddToClassList("shelf-item__name");

            // Fill bar
            var barBg  = new VisualElement(); barBg.AddToClassList("shelf-fill-bg");
            var barFill = new VisualElement(); barFill.AddToClassList("shelf-fill-bar");
            float pct = shelf.GetFillRatio() * 100f;
            barFill.style.width = Length.Percent(pct);
            barBg.Add(barFill);

            var cnt = new Label($"{shelf.GetBookCount()} книг"); cnt.AddToClassList("shelf-item__count");

            item.Add(nameL); item.Add(barBg); item.Add(cnt);
            item.RegisterCallback<ClickEvent>(_ => SelectShelf(shelf, idx + 1));

            _shelfList.Add(item);
        }
    }

    private void SelectShelf(Shelf shelf, int number)
    {
        _selectedShelf = shelf;
        if (_selectedLabel != null)
            _selectedLabel.text = $"{_selectedCabinet?.cabinetName} · Полиця {number}  [{shelf.GetBookCount()} книг]";
        BuildShelfList(_selectedCabinet);
    }

    private void PushOne()  { if (_selectedShelf != null) InventoryManager.Instance?.PushOneToShelf(_selectedShelf); RefreshLabel(); }
    private void PushAll()  { if (_selectedShelf != null) InventoryManager.Instance?.PushAllToShelf(_selectedShelf); RefreshLabel(); }

    private void PopOne()
    {
        if (_selectedShelf == null) return;
        var b = _selectedShelf.TakeLastBook();
        if (b != null) InventoryManager.Instance?.AddExistingBook(b);
        RefreshLabel();
    }

    private void PopAll()
    {
        if (_selectedShelf == null) return;
        BookInstance b;
        do { b = _selectedShelf.TakeLastBook(); if (b != null) InventoryManager.Instance?.AddExistingBook(b); } while (b != null);
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (_selectedShelf == null || _selectedLabel == null) return;
        int n = _selectedCabinet?.shelves.IndexOf(_selectedShelf) + 1 ?? 0;
        _selectedLabel.text = $"{_selectedCabinet?.cabinetName} · Полиця {n}  [{_selectedShelf.GetBookCount()} книг]";
        BuildShelfList(_selectedCabinet);
    }
}
