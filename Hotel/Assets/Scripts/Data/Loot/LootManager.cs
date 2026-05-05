// Assets/Scripts/Data/Loot/LootManager.cs
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

    // ВИПРАВЛЕНО: флаг щоб відрізнити "пул не згенерований" від "пул порожній"
    private bool _poolGenerated = false;

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
    }

    // ВИПРАВЛЕНО: GetCurrentPool не генерує пул як side effect
    public List<LootCardTemplate> GetCurrentPool()
    {
        if (!_poolGenerated)
            Debug.LogWarning("[LootManager] Pool not generated yet. Call happens before LootPhase?");

        return new List<LootCardTemplate>(_currentDailyPool);
    }

    public int PicksRemaining => _picksRemaining;
    public bool CanPick => _picksRemaining > 0;

    private void GenerateLootPool()
    {
        if (allAvailableCards == null || allAvailableCards.Count == 0)
        {
            Debug.LogError("[LootManager] allAvailableCards is empty!");
            return;
        }

        _picksRemaining = maxPicksPerDay;
        _currentDailyPool = allAvailableCards
            .OrderBy(_ => Random.value)
            .Take(cardsToDraw)
            .ToList();

        _poolGenerated = true;
        Debug.Log($"[LootManager] Pool generated: {_currentDailyPool.Count} cards. Picks: {_picksRemaining}");
    }

    public void SelectCard(LootCardTemplate card)
    {
        if (!CanPick)
        {
            Debug.LogWarning("[LootManager] No picks remaining!");
            return;
        }

        if (!_currentDailyPool.Contains(card))
        {
            Debug.LogWarning("[LootManager] Card not in current pool!");
            return;
        }

        ApplyCardEffect(card);
        _currentDailyPool.Remove(card);
        _picksRemaining--;

        Debug.Log($"[LootManager] Card selected: {card.cardName}. Picks left: {_picksRemaining}");

        if (_picksRemaining <= 0)
        {
            _poolGenerated = false;
            GameLoopManager.Instance?.ChangeState(GameState.Preparation);
        }
    }

    private void ApplyCardEffect(LootCardTemplate card)
    {
        switch (card.type)
        {
            case LootCardType.FurnitureUpgrade:
                if (card.furniturePayload != null)
                    InventoryManager.Instance?.UnlockFurniture(card.furniturePayload);
                break;

            case LootCardType.MoneyBonus:
                EconomyManager.Instance?.AddMoney(card.moneyPayload);
                break;

            case LootCardType.BookPack:
                Debug.Log("[LootManager] BookPack effect not yet implemented.");
                break;
        }
    }
}