using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LootManager : MonoBehaviour
{
    public static LootManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private List<LootCardTemplate> allAvailableCards;
    [SerializeField] private int cardsToDraw = 5;
    [SerializeField] private int maxPicksPerDay = 3;

    private List<LootCardTemplate> _currentDailyPool = new List<LootCardTemplate>();
    private int _picksRemaining = 0;

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
            // Не знищуємо при переході між сценами, якщо потрібно
            // DontDestroyOnLoad(gameObject); 
        }
        else 
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += HandleStateChange;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleStateChange;
    }

    /// <summary>
    /// Повертає список карток для поточного дня. 
    /// Якщо пул порожній (наприклад, примусовий виклик), генерує його.
    /// </summary>
    public List<LootCardTemplate> GetCurrentPool()
    {
        if (_currentDailyPool == null || _currentDailyPool.Count == 0)
        {
            Debug.Log("[LootManager] Пул порожній, запускаю екстрену генерацію.");
            GenerateLootPool();
        }
        return _currentDailyPool;
    }

    private void HandleStateChange(GameState state)
    {
        if (state == GameState.LootPhase)
        {
            GenerateLootPool();
        }
    }

    private void GenerateLootPool()
    {
        if (allAvailableCards == null || allAvailableCards.Count == 0)
        {
            Debug.LogError("[LootManager] Список allAvailableCards порожній! Додай ScriptableObjects в інспекторі.");
            return;
        }

        _picksRemaining = maxPicksPerDay;

        // Перемішуємо та беремо задану кількість карток
        _currentDailyPool = allAvailableCards
            .OrderBy(x => Random.value)
            .Take(cardsToDraw)
            .ToList();

        Debug.Log($"[LootManager] Згенеровано пул: {_currentDailyPool.Count} карток. Доступно виборів: {_picksRemaining}");
    }

    public void SelectCard(LootCardTemplate card)
    {
        if (_picksRemaining <= 0) 
        {
            Debug.LogWarning("[LootManager] Спроба обрати картку, але ліміт вичерпано!");
            return;
        }

        Debug.Log($"[LootManager] Обрано картку: {card.cardName}");
        ApplyCardEffect(card);
        
        _picksRemaining--;

        if (_picksRemaining <= 0)
        {
            Debug.Log("[LootManager] Всі вибори зроблено. Перехід до фази підготовки.");
            // Очищуємо пул, щоб він не висів у пам'яті
            _currentDailyPool.Clear();
            
            if (GameLoopManager.Instance != null)
                GameLoopManager.Instance.ChangeState(GameState.Preparation);
        }
    }

    private void ApplyCardEffect(LootCardTemplate card)
    {
        if (card == null) return;

        switch (card.type)
        {
            case LootCardType.FurnitureUpgrade:
                if (InventoryManager.Instance != null && card.furniturePayload != null)
            {
                InventoryManager.Instance.UnlockFurniture(card.furniturePayload);
                // Використовуємо .name (це ім'я файлу ассета в Unity)
                Debug.Log($"[LootManager] Розблоковано меблі: {card.furniturePayload.name}");
            }
            break;

            case LootCardType.MoneyBonus:
                if (EconomyManager.Instance != null)
                {
                    EconomyManager.Instance.AddMoney(card.moneyPayload);
                    Debug.Log($"[LootManager] Нараховано бонус: ${card.moneyPayload}");
                }
                break;
                
            case LootCardType.BookPack:
                // Якщо захочеш додавати паки книг у майбутньому
                Debug.Log("[LootManager] Ефект паку книг ще не реалізовано.");
                break;
        }
    }
}