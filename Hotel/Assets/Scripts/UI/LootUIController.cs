using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Collections;

public class LootUIController : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _screenRoot;
    private VisualElement _lootContainer;
    
    [Header("UI Resources")]
    [SerializeField] private VisualTreeAsset cardTemplate; 

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        // Використовуємо корутину для безпечної підписки після ініціалізації всіх синглтонів
        StopAllCoroutines();
        StartCoroutine(DelayedSubscribe());
    }

    private IEnumerator DelayedSubscribe()
    {
        // Чекаємо, поки GameLoopManager з'явиться в сцені
        yield return new WaitUntil(() => GameLoopManager.Instance != null);
        
        GameLoopManager.Instance.OnStateChanged += HandleStateChanged;
        Debug.Log("[LootUI] Підписка на ігровий цикл активна.");

        // Ініціалізуємо посилання на елементи UXML
        SetupUIReferences();
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void SetupUIReferences()
    {
        if (_uiDocument == null || _uiDocument.rootVisualElement == null) return;

        var root = _uiDocument.rootVisualElement;

        // "ScreenRoot" має бути назвою самого верхнього контейнера у вашому UXML
        _screenRoot = root.Q<VisualElement>("EndDayPanel");
        
        if (_screenRoot != null)
        {
            // Приховуємо при старті
            _screenRoot.style.display = DisplayStyle.None;
            _lootContainer = _screenRoot.Q<VisualElement>("LootContainer");
            Debug.Log("[LootUI] Посилання на UI елементи успішно встановлені.");
        }
        else
        {
            Debug.LogError("[LootUI] Не знайдено елемент 'ScreenRoot'. Перевірте назву в UI Builder!");
        }
    }

    private void HandleStateChanged(GameState state)
    {
        Debug.Log($"[LootUI] Стан змінився на: {state}");
        
        if (state == GameState.LootPhase)
        {
            ShowEndOfDayScreen();
        }
        else
        {
            if (_screenRoot != null)
                _screenRoot.style.display = DisplayStyle.None;
        }
    }

    private void ShowEndOfDayScreen()
    {
        if (_screenRoot == null) SetupUIReferences();
        if (_screenRoot == null) return;

        // Робимо вікно видимим
        _screenRoot.style.display = DisplayStyle.Flex;
        Debug.Log("[LootUI] Відображення екрана завершення дня.");

        // Оновлюємо текстову статистику (переконайтеся, що імена збігаються з UXML)
        var booksLabel = _screenRoot.Q<Label>("StatBooks");
        var moneyLabel = _screenRoot.Q<Label>("StatMoney");

        if (booksLabel != null) 
            booksLabel.text = $"Продано книг: {EconomyManager.Instance.BooksSoldToday}";
        
        if (moneyLabel != null) 
            moneyLabel.text = $"Заробіток: ${EconomyManager.Instance.MoneyEarnedToday}";

        // Генеруємо картки
        RefreshLootGrid();
    }

    public void RefreshLootGrid()
    {
        if (_lootContainer == null) return;
        
        _lootContainer.Clear();
        List<LootCardTemplate> cards = LootManager.Instance.GetCurrentPool();
        
        Debug.Log($"[LootUI] Створення карток. Кількість у пулі: {cards.Count}");

        foreach (var card in cards)
        {
            // Створюємо екземпляр картки з шаблону
            VisualElement cardInstance = cardTemplate.Instantiate();
            
            // Наповнюємо даними
            cardInstance.Q<Label>("CardName").text = card.cardName;
            
            var costLabel = cardInstance.Q<Label>("CardCost");
            if (costLabel != null) costLabel.text = $"${card.cost}";
            
            Button pickBtn = cardInstance.Q<Button>("PickButton");
            
            if (pickBtn != null)
            {
                // Вимикаємо кнопку, якщо грошей замало
                if (EconomyManager.Instance.Money < card.cost)
                {
                    pickBtn.SetEnabled(false);
                    pickBtn.text = "Мало коштів";
                }

                pickBtn.clicked += () => 
                {
                    if (EconomyManager.Instance.SpendMoney(card.cost))
                    {
                        LootManager.Instance.SelectCard(card);
                        // Після вибору оновлюємо всю сітку, щоб перевірити доступність інших карток
                        RefreshLootGrid();
                    }
                };
            }

            _lootContainer.Add(cardInstance);
        }
    }
}