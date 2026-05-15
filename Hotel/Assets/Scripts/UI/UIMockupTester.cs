// Assets/Scripts/Editor/UIMockupTester.cs
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Скрипт для швидкого тестування реактивності UI без реальної логіки гри.
/// Дозволяє перевірити: фліп картки, наповнення інвентарю, зміну престижу.
/// </summary>
public class UIMockupTester : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    
    // Посилання на елементи, які ми будемо "смикати"
    private VisualElement _tagAFlipper;
    private VisualElement _inventoryPanel;
    private Label _moneyLabel;
    private ProgressBar _prestigeBar;

    private bool _isFlipped = false;

    private void OnEnable()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        // Шукаємо елементи за іменами з твоїх UXML
        _tagAFlipper = root.Q<VisualElement>("TagAFlipper");
        _inventoryPanel = root.Q<VisualElement>("InventoryPanel");
        _moneyLabel = root.Q<Label>("MoneyLabel");
        
        // Реєструємо тестові кліки
        root.Q<Button>("TagAAssembly")?.RegisterCallback<ClickEvent>(evt => SimulateCardFlip());
        
        Debug.Log("[Mockup] Tester initialized. Press 'T' to add fake money, 'I' to toggle inventory.");
    }

    private void Update()
    {
        // Гарячі клавіші для тестів
        if (Input.GetKeyDown(KeyCode.T)) SimulateAddMoney();
        if (Input.GetKeyDown(KeyCode.I)) ToggleInventory();
    }

    private void SimulateCardFlip()
    {
        if (_tagAFlipper == null) return;
        
        _isFlipped = !_isFlipped;
        // Додаємо/видаляємо клас анімації (має бути в USS)
        if (_isFlipped)
            _tagAFlipper.AddToClassList("flipped");
        else
            _tagAFlipper.RemoveFromClassList("flipped");
            
        Debug.Log($"[Mockup] Card flipped: {_isFlipped}");
    }

    private void SimulateAddMoney()
    {
        if (_moneyLabel == null) return;
        int current = int.Parse(_moneyLabel.text.Replace(" ", ""));
        _moneyLabel.text = (current + 150).ToString("N0");
        Debug.Log("[Mockup] Added 150 money.");
    }

    private void ToggleInventory()
    {
        if (_inventoryPanel == null) return;
        bool isVisible = _inventoryPanel.style.display == DisplayStyle.Flex;
        _inventoryPanel.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
    }
}