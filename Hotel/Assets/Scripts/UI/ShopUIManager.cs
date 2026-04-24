using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

public class ShopUIManager : MonoBehaviour
{
    [Header("UI Resources")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset bookItemTemplate;
    [SerializeField] private VisualTreeAsset lootCardTemplate; // Шаблон картки нагороди

    private bool _isInventoryOpen = false;
    private Shelf _selectedShelf;
    private Cabinet _selectedCabinet;
    private List<Cabinet> _allCabinets = new List<Cabinet>();

    // Основні панелі
    private VisualElement _inventoryPanel; 
    private VisualElement _cabinetPanel;   
    private VisualElement _leftPanel;
    private VisualElement _rightPanel;
    
    // Елементи екрана завершення дня (Loot Phase)
    private VisualElement _endOfDayScreen;
    private VisualElement _lootContainer;

    // Списки та мітки
    private ScrollView _inventoryGrid;
    private ScrollView _cabinetList;
    private ScrollView _shelfList;
    private Label _selectedShelfLabel;

    void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // 1. Пошук стандартних панелей інвентарю
        _inventoryPanel = root.Q<VisualElement>("InventoryPanel");
        _cabinetPanel = root.Q<VisualElement>("CabinetPanel");
        _inventoryGrid = root.Q<ScrollView>("InventoryGrid");
        _cabinetList = root.Q<ScrollView>("CabinetList");
        _shelfList = root.Q<ScrollView>("ShelfList");
        _selectedShelfLabel = root.Q<Label>("SelectedShelfLabel");
        _leftPanel = root.Q<VisualElement>("LeftPanel");
        _rightPanel = root.Q<VisualElement>("RightPanel");
        _endOfDayScreen = root.Q<VisualElement>("EndDayPanel"); 
        _lootContainer = root.Q<VisualElement>("LootContainer");

        // Прив'язка кнопок у Footer
        var footer = root.Q<VisualElement>("Footer");
        if (footer != null)
        {
            footer.Q<Button>("BtnPushOne")?.RegisterCallback<ClickEvent>(evt => PushOneToSelected());
            footer.Q<Button>("BtnPushAll")?.RegisterCallback<ClickEvent>(evt => PushAllToSelected());
            footer.Q<Button>("BtnPopOne")?.RegisterCallback<ClickEvent>(evt => PopOneFromSelected());
            footer.Q<Button>("BtnPopAll")?.RegisterCallback<ClickEvent>(evt => PopAllFromSelected());
        }

        root.Q<Button>("BtnOpenInventory")?.RegisterCallback<ClickEvent>(evt => ToggleInventory());

        // Підписки на менеджери
        InventoryManager.Instance.OnInventoryChanged += RefreshInventory;
        
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += HandleGameStateChanged;

        // Початкове налаштування
        _allCabinets = Object.FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude).ToList();
 
        RefreshCabinetList();
        CloseAllPanels();

        Debug.Log("[UI] Головний ShopUIManager успішно ініціалізовано.");
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseAllPanels();
        }
    }

    #region Керування Станами (Loot Phase)

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.LootPhase)
        {
            CloseAllPanels(); // Закриваємо інвентар, якщо він був відкритий
            ShowEndOfDayScreen(true);
        }
        else
        {
            ShowEndOfDayScreen(false);
        }
    }

    private void ShowEndOfDayScreen(bool show)
    {
        if (_endOfDayScreen == null) return;

        _endOfDayScreen.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        
        // ВАЖЛИВО: дозволяємо кліки тільки коли екран активний
        _endOfDayScreen.pickingMode = show ? PickingMode.Position : PickingMode.Ignore;

        if (show)
        {
            RefreshLootStats();
            RefreshLootCards();
        }
    }

    private void RefreshLootStats()
    {
        _endOfDayScreen.Q<Label>("StatBooks").text = $"Продано книг: {EconomyManager.Instance.booksSoldToday}";
        _endOfDayScreen.Q<Label>("StatMoney").text = $"Заробіток: ${EconomyManager.Instance.moneyEarnedToday}";
    }

    private void RefreshLootCards()
    {
        // Якщо контейнер не знайдено, просто виходимо, не викликаючи помилку
        if (_lootContainer == null) 
        {
            Debug.LogError("[ShopUI] Спроба оновити картки, але LootContainer рівний NULL. Перевір імена в UI Builder.");
            return;
        }

        // Перевірка префаба (ScriptableObject картки)
        if (lootCardTemplate == null)
        {
            Debug.LogError("[ShopUI] Loot Card Template не призначено в інспекторі Unity!");
            return;
        }

        _lootContainer.Clear();

        // Перевірка наявності LootManager
        if (LootManager.Instance == null) return;

        var cards = LootManager.Instance.GetCurrentPool();
        if (cards == null) return;

        foreach (var card in cards)
        {
            var cardUI = lootCardTemplate.Instantiate();
            
            // Безпечне призначення тексту
            var nameLabel = cardUI.Q<Label>("CardName");
            if (nameLabel != null) nameLabel.text = card.cardName;

            var costLabel = cardUI.Q<Label>("CardCost");
            if (costLabel != null) costLabel.text = $"${card.cost}";

            Button btn = cardUI.Q<Button>("PickButton");
            if (btn != null)
            {
                btn.SetEnabled(EconomyManager.Instance.Money >= card.cost);
                btn.clicked += () => {
                    if (EconomyManager.Instance.SpendMoney(card.cost))
                    {
                        LootManager.Instance.SelectCard(card);
                        RefreshLootCards();
                    }
                };
            }
            _lootContainer.Add(cardUI);
        }
    }

    #endregion

    #region Керування Інвентарем

public void OpenCabinetUI(Cabinet cabinet)
{
    Debug.Log($"[ShopUI] Спроба відкрити інтерфейс шафи: {cabinet.name}");
    
    _selectedCabinet = cabinet;
    _isInventoryOpen = true;

    // 1. Вмикаємо батьківські контейнери (якщо вони є)
    if (_leftPanel != null) _leftPanel.style.display = DisplayStyle.Flex;
    if (_rightPanel != null) _rightPanel.style.display = DisplayStyle.Flex;

    // 2. Вмикаємо самі панелі
    if (_inventoryPanel != null) _inventoryPanel.style.display = DisplayStyle.Flex;
    if (_cabinetPanel != null) _cabinetPanel.style.display = DisplayStyle.Flex;

    // 3. Оновлюємо вміст
    SelectCabinet(cabinet); 
    RefreshInventory();
    
    Debug.Log("[ShopUI] Інтерфейс шафи має бути видимим.");
}

    public void CloseAllPanels()
    {
        if (_inventoryPanel != null) _inventoryPanel.style.display = DisplayStyle.None;
        if (_cabinetPanel != null) _cabinetPanel.style.display = DisplayStyle.None;
        if (_leftPanel != null) _leftPanel.style.display = DisplayStyle.None;
        if (_rightPanel != null) _rightPanel.style.display = DisplayStyle.None;
        if (_endOfDayScreen != null) _endOfDayScreen.style.display = DisplayStyle.None;
        _isInventoryOpen = false;
    }

    public void ToggleInventory()
    {
        _isInventoryOpen = !_isInventoryOpen;
        var state = _isInventoryOpen ? DisplayStyle.Flex : DisplayStyle.None;

        if (_leftPanel != null) _leftPanel.style.display = state;
        if (_inventoryPanel != null) _inventoryPanel.style.display = state;
        
        if (_isInventoryOpen) RefreshInventory();
    }

    #endregion

    #region Списки та Кнопки (Полиці/Інвентар)

    private void RefreshCabinetList()
    {
        if (_cabinetList == null) return;
        _cabinetList.Clear();
        foreach (var cabinet in _allCabinets)
        {
            Button btn = new Button { text = cabinet.cabinetName };
            btn.style.height = 40;
            btn.clicked += () => SelectCabinet(cabinet);
            _cabinetList.Add(btn);
        }
    }

    private void SelectCabinet(Cabinet cabinet)
    {
        _selectedCabinet = cabinet;
        _shelfList?.Clear();
        _selectedShelf = null;

        for (int i = 0; i < cabinet.shelves.Count; i++)
        {
            int index = i;
            Button btn = new Button { text = $"Поличка {index + 1}" };
            btn.style.height = 40;
            btn.clicked += () => SelectShelf(cabinet.shelves[index], index + 1);
            _shelfList.Add(btn);
        }
    }

    private void SelectShelf(Shelf shelf, int number)
    {
        _selectedShelf = shelf;
        if (_selectedShelfLabel != null)
            _selectedShelfLabel.text = $"{_selectedCabinet.cabinetName}: Поличка {number}";
    }

    public void RefreshInventory()
    {
        if (_inventoryGrid == null || !_isInventoryOpen) return;
        _inventoryGrid.Clear();
        
        var books = InventoryManager.Instance.GetSortedInventory(SortType.ByTitle);
        foreach (var book in books)
        {
            var itemUI = new InventoryItemUI(book, bookItemTemplate);
            _inventoryGrid.Add(itemUI.Root);
        }
    }

    private void PushOneToSelected() { if (_selectedShelf != null) InventoryManager.Instance.PushOneToShelf(_selectedShelf); }
    private void PushAllToSelected() { if (_selectedShelf != null) InventoryManager.Instance.PushAllToShelf(_selectedShelf); }
    private void PopOneFromSelected() { 
        if (_selectedShelf == null) return;
        BookInstance data = _selectedShelf.TakeLastBook();
        if (data != null) InventoryManager.Instance.AddExistingBook(data);
    }
    private void PopAllFromSelected() 
{ 
    if (_selectedShelf == null) return;

    // Створюємо тимчасову змінну для перевірки
    BookInstance lastPoppedBook;
    
    // Поки метод TakeLastBook() повертає книгу (не null), додаємо її в інвентар
    do 
    {
        lastPoppedBook = _selectedShelf.TakeLastBook();
        if (lastPoppedBook != null)
        {
            InventoryManager.Instance.AddExistingBook(lastPoppedBook);
        }
    } while (lastPoppedBook != null);

    Debug.Log("[UI] Всі книги з полиці перенесено в інвентар.");
}
    #endregion

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleGameStateChanged;
    }
}