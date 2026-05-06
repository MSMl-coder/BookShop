// Assets/Scripts/Core/Economy/EconomyManager.cs
using UnityEngine;
using System;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    [Header("Wallet")]
    [SerializeField] private int _startingMoney = 500;

    // Поточний баланс — приватний, доступ через публічний геттер
    private int _currentMoney;
    

    // Статистика поточного дня
    public int BooksSoldToday { get; private set; }
    public int MoneyEarnedToday { get; private set; }

    public int Money => _currentMoney;

    public event Action<int> OnMoneyChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _currentMoney = _startingMoney;
    }

    // ВИПРАВЛЕНО: додано SetMoney для Save/Load
    public void SetMoney(int amount)
    {
        _currentMoney = Mathf.Max(0, amount);
        OnMoneyChanged?.Invoke(_currentMoney);
        
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        _currentMoney += amount;
        MoneyEarnedToday += amount;
        OnMoneyChanged?.Invoke(_currentMoney);
        Debug.Log($"[Economy] +{amount}. Balance: {_currentMoney}");
    }

    public bool SpendMoney(int amount)
    {
        if (amount <= 0) return false;

        if (_currentMoney < amount)
        {
            Debug.LogWarning($"[Economy] Not enough money! Need: {amount}, Have: {_currentMoney}");
            return false;
        }

        _currentMoney -= amount;
        OnMoneyChanged?.Invoke(_currentMoney);
        Debug.Log($"[Economy] -{amount}. Balance: {_currentMoney}");
        return true;
    }

    public void RecordBookSold(float price)
    {
        BooksSoldToday++;
        AddMoney(Mathf.RoundToInt(price));
        TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnFirstSale);
    }

    public void ResetDailyStats()
    {
        BooksSoldToday = 0;
        MoneyEarnedToday = 0;
        Debug.Log("[Economy] Daily stats reset.");
    }
}