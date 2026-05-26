// Assets/Scripts/Core/GameLoop/GameLoopManager.cs
// ОНОВЛЕНО (Фаза 2):
//   - EndWorkDay() → ApplyDailyRent() → DayStats
//   - OnBankruptcy: зупиняє гру (заглушка — TODO: екран банкрутства)
//   - FinishDayStats() → LootPhase
//   - ShopCloseHour = 19

using UnityEngine;
using System;

public class GameLoopManager : MonoBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }
    public int       CurrentDay   { get; private set; } = 1;

    [Header("WorkDay Settings")]
    [Tooltip("Година закриття крамниці. О цій годині WorkDay завершується автоматично.")]
    [SerializeField] private int shopCloseHour = 19;
    public int ShopCloseHour => shopCloseHour;

    // ── Події ───────────────────────────────────────────────────
    public event Action<GameState> OnStateChanged;
    public event Action<int>       OnDayEnded;
    public event Action<int>       OnNewDayStarted;
    public event Action            OnDayReset;
    public event Action            OnBankruptcy;

    // ── Unity ───────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Підписуємось на банкрутство від EconomyManager
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnBankruptcy += HandleBankruptcy;

        ChangeState(GameState.Preparation);
    }

    private void OnDestroy()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnBankruptcy -= HandleBankruptcy;
    }

    // ── Core ─────────────────────────────────────────────────────

    public void ChangeState(GameState newState)
    {
        Debug.Log($"[GameLoop] {CurrentState} → {newState}  (День {CurrentDay})");
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }

    // ── Цикл дня ─────────────────────────────────────────────────

    /// Preparation → WorkDay (вручну: гравець відкриває крамницю)
    public void StartWorkDay()
    {
        if (CurrentState != GameState.Preparation)
        {
            Debug.LogWarning($"[GameLoop] StartWorkDay ігнорується: стан {CurrentState}");
            return;
        }
        ChangeState(GameState.WorkDay);
    }

    /// WorkDay → DayStats
    /// Спочатку списуємо оренду, потім переходимо в DayStats.
    /// Якщо банкрутство — HandleBankruptcy зупинить цикл.
    public void EndWorkDay()
    {
        if (CurrentState != GameState.WorkDay)
        {
            Debug.LogWarning($"[GameLoop] EndWorkDay ігнорується: стан {CurrentState}");
            return;
        }

        OnDayEnded?.Invoke(CurrentDay);

        // Списуємо оренду — якщо банкрутство, HandleBankruptcy викличеться через подію
        var result = EconomyManager.Instance?.ApplyDailyRent();

        // Якщо банкрутство — не переходимо далі (HandleBankruptcy вже спрацював)
        if (result.HasValue && result.Value.IsBankrupt) return;

        ChangeState(GameState.DayStats);

        // DayStatsController слухає OnStateChanged і показує екран.
        // Гравець натискає "Далі" → FinishDayStats() викликається звідти.
    }

    /// DayStats → LootPhase
    public void FinishDayStats()
    {
        if (CurrentState != GameState.DayStats)
        {
            Debug.LogWarning($"[GameLoop] FinishDayStats ігнорується: стан {CurrentState}");
            return;
        }
        CurrentDay++;
        ChangeState(GameState.LootPhase);
    }

    /// LootPhase → Preparation
    public void StartNewDay()
    {
        if (CurrentState != GameState.LootPhase)
        {
            Debug.LogWarning($"[GameLoop] StartNewDay ігнорується: стан {CurrentState}");
            return;
        }
        EconomyManager.Instance?.ResetDailyStats();
        OnDayReset?.Invoke();
        OnNewDayStarted?.Invoke(CurrentDay);
        ChangeState(GameState.Preparation);
    }

    /// Пропустити нагороди
    public void SkipLootPhase()
    {
        if (CurrentState != GameState.LootPhase) return;
        StartNewDay();
    }

    // ── Bankruptcy ───────────────────────────────────────────────

    private void HandleBankruptcy()
    {
        Debug.LogError("[GameLoop] БАНКРУТСТВО — гра зупинена.");
        OnBankruptcy?.Invoke();
        // TODO: показати екран банкрутства
        // Поки просто зупиняємо Time
        Time.timeScale = 0f;
    }

    // ── Debug ────────────────────────────────────────────────────

    [ContextMenu("Test: End Work Day")]
    public void TestEndDay()
    {
        if (CurrentState == GameState.WorkDay) EndWorkDay();
        else Debug.LogWarning($"[GameLoop] TEST: не в WorkDay (стан: {CurrentState})");
    }

    [ContextMenu("Test: Force Bankruptcy")]
    public void TestBankruptcy()
    {
        EconomyManager.Instance?.SetMoney(-(EconomyManager.Instance.DailyRent * EconomyManager.Instance.DebtDaysAllowed + 1));
        for (int i = 0; i < EconomyManager.Instance.DebtDaysAllowed; i++)
            EconomyManager.Instance?.ApplyDailyRent();
    }
}