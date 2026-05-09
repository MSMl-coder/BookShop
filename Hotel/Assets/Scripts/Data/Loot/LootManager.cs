// Assets/Scripts/Data/Loot/LootManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LootManager : MonoBehaviour
{
    public static LootManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private List<LootCardTemplate> allAvailableCards;
    [SerializeField] private int cardsToDraw    = 5;
    [SerializeField] private int maxPicksPerDay = 3;

    [Header("Book Pack Settings")]
    [SerializeField] private int booksPerPack = 3; // скільки книг дає BookPack

    private List<LootCardTemplate> _currentDailyPool = new();
    private int _picksRemaining = 0;

    public int  PicksRemaining => _picksRemaining;
    public bool CanPick        => _picksRemaining > 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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

    private void HandleStateChange(GameState state)
    {
        if (state == GameState.LootPhase)
            GenerateLootPool();
        else if (state == GameState.Preparation)
            _currentDailyPool.Clear();
    }

    public List<LootCardTemplate> GetCurrentPool()
    {
        if (_currentDailyPool == null || _currentDailyPool.Count == 0)
            GenerateLootPool();
        return _currentDailyPool;
    }

    private void GenerateLootPool()
    {
        if (allAvailableCards == null || allAvailableCards.Count == 0)
        {
            Debug.LogError("[LootManager] allAvailableCards порожній!");
            return;
        }

        _picksRemaining = maxPicksPerDay;

        _currentDailyPool = allAvailableCards
            .OrderBy(_ => Random.value)
            .Take(cardsToDraw)
            .ToList();

        Debug.Log($"[LootManager] Пул: {_currentDailyPool.Count} карток, виборів: {_picksRemaining}");
    }

    public void SelectCard(LootCardTemplate card)
    {
        if (!CanPick)
        {
            Debug.LogWarning("[LootManager] Ліміт виборів вичерпано.");
            return;
        }

        ApplyCardEffect(card);
        _picksRemaining--;

        Debug.Log($"[LootManager] Обрано: {card.cardName}. Залишилось: {_picksRemaining}");
    }

    private void ApplyCardEffect(LootCardTemplate card)
    {
        if (card == null) return;

        switch (card.type)
        {
            case LootCardType.FurnitureUpgrade:
                if (card.furniturePayload == null)
                {
                    Debug.LogWarning($"[LootManager] {card.cardName}: furniturePayload не призначено!");
                    return;
                }
                // UnlockFurniture тепер додає FurnitureInstance в новий інвентар
                InventoryManager.Instance?.UnlockFurniture(card.furniturePayload);
                Debug.Log($"[LootManager] Меблі отримано: {card.furniturePayload.furnitureName}");
                break;

            case LootCardType.MoneyBonus:
                EconomyManager.Instance?.AddMoney(card.moneyPayload);
                Debug.Log($"[LootManager] Гроші отримано: +{card.moneyPayload}");
                break;

            case LootCardType.BookPack:
                ApplyBookPack(card);
                break;
        }
    }

    private void ApplyBookPack(LootCardTemplate card)
    {
        var db = BookDatabase.Instance;
        if (db == null || db.allBooks == null || db.allBooks.Count == 0)
        {
            Debug.LogWarning("[LootManager] BookPack: BookDatabase порожній або не ініціалізований.");
            return;
        }

        // Вибираємо випадкові книги з бази
        int count = Mathf.Min(booksPerPack, db.allBooks.Count);
        var shuffled = db.allBooks
            .Where(b => b != null)
            .OrderBy(_ => Random.value)
            .Take(count)
            .ToList();

        foreach (var bookTemplate in shuffled)
            InventoryManager.Instance?.AddBook(bookTemplate.bookID);

        Debug.Log($"[LootManager] BookPack: додано {shuffled.Count} книг в інвентар");
    }
}