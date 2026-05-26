// Assets/Scripts/Core/Economy/EconomyManager.cs
// ОНОВЛЕНО (Фаза 2):
//   - dailyRent: фіксована сума списується при DayStats
//   - Борг: якщо баланс < 0 після списання — починається відлік днів боргу
//   - debtDaysAllowed: максимум днів з боргом до банкрутства
//   - ApplyDailyRent(): викликається з GameLoopManager при EndWorkDay
//   - OnBankruptcy: подія для GameLoop/UI
//   - DailyRentResult: результат списання (для DayStats екрану)

using UnityEngine;
using System;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    // ── Wallet ───────────────────────────────────────────────────
    [Header("Wallet")]
    [SerializeField] private int _startingMoney = 500;

    private int _currentMoney;
    public int Money => _currentMoney;

    // ── Daily Rent ───────────────────────────────────────────────
    [Header("Daily Rent")]
    [Tooltip("Фіксована сума оренди що списується кожного дня при DayStats.")]
    [SerializeField] private int dailyRent = 100;

    [Tooltip("Максимум днів з боргом до банкрутства.")]
    [SerializeField] private int debtDaysAllowed = 3;

    public int DailyRent        => dailyRent;
    public int DebtDaysAllowed  => debtDaysAllowed;
    public int DebtDaysCurrent  { get; private set; } = 0;
    public bool IsInDebt        => _currentMoney < 0;

    // ── Daily Stats ──────────────────────────────────────────────
    public int BooksSoldToday    { get; private set; }
    public int MoneyEarnedToday  { get; private set; }
    public int RentPaidLastDay   { get; private set; }  // для DayStats екрану
    public int BalanceAfterRent  { get; private set; }  // для DayStats екрану

    // ── Події ────────────────────────────────────────────────────
    public event Action<int>            OnMoneyChanged;
    public event Action<DailyRentResult> OnRentApplied;   // для DayStats UI
    public event Action                  OnBankruptcy;     // для GameLoop

    // ── Unity ────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _currentMoney = _startingMoney;
    }

    // ── Money ────────────────────────────────────────────────────

    public void SetMoney(int amount)
    {
        _currentMoney = amount; // дозволяємо від'ємне (борг)
        OnMoneyChanged?.Invoke(_currentMoney);
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        _currentMoney   += amount;
        MoneyEarnedToday += amount;
        OnMoneyChanged?.Invoke(_currentMoney);
        Debug.Log($"[Economy] +{amount}. Баланс: {_currentMoney}");
    }

    public bool SpendMoney(int amount)
    {
        if (amount <= 0) return false;
        if (_currentMoney < amount)
        {
            Debug.LogWarning($"[Economy] Недостатньо коштів! Потрібно: {amount}, Є: {_currentMoney}");
            return false;
        }
        _currentMoney -= amount;
        OnMoneyChanged?.Invoke(_currentMoney);
        Debug.Log($"[Economy] -{amount}. Баланс: {_currentMoney}");
        return true;
    }

    public void RecordBookSold(float price, string bookTitle = "")
    {
            if (GameLoopManager.Instance?.CurrentState != GameState.WorkDay)
            {
                Debug.LogWarning("[Economy] RecordBookSold поза WorkDay — ігнорується.");
                return;
            }
            BooksSoldToday++;
            int rounded = Mathf.RoundToInt(price);
            AddMoney(rounded);
            TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnFirstSale);
            if (!string.IsNullOrEmpty(bookTitle))
                NotificationSystem.NotifyBookSold(bookTitle, rounded);
        }

    // ── Daily Rent ───────────────────────────────────────────────

    /// Викликається з GameLoopManager.EndWorkDay() перед переходом у DayStats.
    /// Списує оренду, оновлює борг, перевіряє банкрутство.
    public DailyRentResult ApplyDailyRent()
    {
        int balanceBefore = _currentMoney;
        _currentMoney    -= dailyRent;
        RentPaidLastDay   = dailyRent;
        BalanceAfterRent  = _currentMoney;

        OnMoneyChanged?.Invoke(_currentMoney);
        Debug.Log($"[Economy] Оренда -{dailyRent}. Баланс: {balanceBefore} → {_currentMoney}");

        // Борг
        if (_currentMoney < 0)
        {
            DebtDaysCurrent++;
            Debug.LogWarning($"[Economy] БОРГ! День боргу: {DebtDaysCurrent}/{debtDaysAllowed}. Баланс: {_currentMoney}");
        }
        else
        {
            // Борг погашено
            if (DebtDaysCurrent > 0)
                Debug.Log($"[Economy] Борг погашено.");
            DebtDaysCurrent = 0;
        }

        var result = new DailyRentResult(
            rentAmount:    dailyRent,
            balanceBefore: balanceBefore,
            balanceAfter:  _currentMoney,
            debtDay:       DebtDaysCurrent,
            debtAllowed:   debtDaysAllowed,
            isBankrupt:    DebtDaysCurrent >= debtDaysAllowed
        );

        OnRentApplied?.Invoke(result);

        if (result.IsBankrupt)
        {
            Debug.LogError($"[Economy] БАНКРУТСТВО! {debtDaysAllowed} дні підряд з боргом.");
            OnBankruptcy?.Invoke();
        }

        return result;
    }

    // ── Reset ────────────────────────────────────────────────────

    public void ResetDailyStats()
    {
        BooksSoldToday   = 0;
        MoneyEarnedToday = 0;
        Debug.Log("[Economy] Денна статистика скинута.");
    }
}

// ── DailyRentResult ──────────────────────────────────────────────
/// Передається в OnRentApplied і DayStats UI
public readonly struct DailyRentResult
{
    public readonly int  RentAmount;
    public readonly int  BalanceBefore;
    public readonly int  BalanceAfter;
    public readonly int  DebtDay;       // 0 = немає боргу, 1..N = N-й день боргу
    public readonly int  DebtAllowed;
    public readonly bool IsBankrupt;
    public readonly bool IsInDebt => BalanceAfter < 0;

    public DailyRentResult(int rentAmount, int balanceBefore, int balanceAfter,
                           int debtDay, int debtAllowed, bool isBankrupt)
    {
        RentAmount    = rentAmount;
        BalanceBefore = balanceBefore;
        BalanceAfter  = balanceAfter;
        DebtDay       = debtDay;
        DebtAllowed   = debtAllowed;
        IsBankrupt    = isBankrupt;
    }
}

// ── ПАТЧ: RecordBookSold з notification ──────────────────────────
// Замінити існуючий RecordBookSold в EconomyManager на цей варіант:
//
//    public void RecordBookSold(float price, string bookTitle = "")
//    {
//        if (GameLoopManager.Instance?.CurrentState != GameState.WorkDay)
//        {
//            Debug.LogWarning("[Economy] RecordBookSold поза WorkDay — ігнорується.");
//            return;
//        }
//        BooksSoldToday++;
//        int rounded = Mathf.RoundToInt(price);
//        AddMoney(rounded);
//        TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnFirstSale);
//        if (!string.IsNullOrEmpty(bookTitle))
//            NotificationSystem.NotifyBookSold(bookTitle, rounded);
//    }