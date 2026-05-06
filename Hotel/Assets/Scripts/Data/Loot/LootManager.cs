// Assets/Scripts/Data/Loot/LootManager.cs
// ОНОВЛЕНО: додано public PicksRemaining, CanPick;
//           прибрано прямий виклик ChangeState — тепер це робить LootPanelUI через GameLoopManager.StartNewDay()
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LootManager : MonoBehaviour
{
    public static LootManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private List<LootCardTemplate> allAvailableCards;
    [SerializeField] private int cardsToDraw   = 5;
    [SerializeField] private int maxPicksPerDay = 3;

    private List<LootCardTemplate> _currentDailyPool = new();
    private int _picksRemaining = 0;

    // ── Public ──
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

    // ─────────────────────────────────────────────

    private void HandleStateChange(GameState state)
    {
        if (state == GameState.LootPhase)
            GenerateLootPool();
        else if (state == GameState.Preparation)
            _currentDailyPool.Clear(); // чистимо після нового дня
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

        // ВАЖЛИВО: НЕ викликаємо ChangeState тут.
        // LootPanelUI перевіряє CanPick після виклику і сам викликає GameLoopManager.StartNewDay().
    }

    private void ApplyCardEffect(LootCardTemplate card)
    {
        if (card == null) return;

        switch (card.type)
        {
            case LootCardType.FurnitureUpgrade:
                if (InventoryManager.Instance != null && card.furniturePayload != null)
                    InventoryManager.Instance.UnlockFurniture(card.furniturePayload);
                break;

            case LootCardType.MoneyBonus:
                if (EconomyManager.Instance != null)
                    EconomyManager.Instance.AddMoney(card.moneyPayload);
                break;

            case LootCardType.BookPack:
                Debug.Log("[LootManager] BookPack — не реалізовано.");
                break;
        }
    }
}
