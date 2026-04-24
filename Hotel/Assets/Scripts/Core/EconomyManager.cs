using UnityEngine;
using System;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    [Header("Wallet")]
    [SerializeField] private int _currentMoney = 500; // Стартовий капітал

    [Header("Daily Stats")]
    public int booksSoldToday = 0;
    public int moneyEarnedToday = 0;

    public event Action<int> OnMoneyChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public int Money => _currentMoney;

    public void AddMoney(int amount)
    {
        _currentMoney += amount;
        moneyEarnedToday += amount;
        OnMoneyChanged?.Invoke(_currentMoney);
        Debug.Log($"[Economy] Зароблено: {amount}. Баланс: {_currentMoney}");
    }

    public bool SpendMoney(int amount)
    {
        if (_currentMoney >= amount)
        {
            _currentMoney -= amount;
            OnMoneyChanged?.Invoke(_currentMoney);
            Debug.Log($"[Economy] Витрачено: {amount}. Баланс: {_currentMoney}");
            return true;
        }
        Debug.LogWarning("[Economy] Недостатньо грошей!");
        return false;
    }

    public void RecordBookSold(float price)
    {
        booksSoldToday++;
        AddMoney(Mathf.RoundToInt(price));
    }

    public void ResetDailyStats()
    {
        booksSoldToday = 0;
        moneyEarnedToday = 0;
    }
}