// Assets/Scripts/Core/GameLoop/GameLoopManager.cs
// ФІКС: Додано DEBUG_SetDay() — безпечна заміна рефлексії в DebugGameController.SetStartDay()
//   і відновлення дня в GameStateSerializer.ApplySaveData().
//   Рефлексія typeof(GameLoopManager).GetProperty("CurrentDay")?.SetValue(...) ЛАМАЛА
//   IL2CPP build (AOT не підтримує SetValue на private setter).

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

    /// Preparation → WorkDay
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
    public void EndWorkDay()
    {
        if (CurrentState != GameState.WorkDay)
        {
            Debug.LogWarning($"[GameLoop] EndWorkDay ігнорується: стан {CurrentState}");
            return;
        }

        OnDayEnded?.Invoke(CurrentDay);

        var result = EconomyManager.Instance?.ApplyDailyRent();
        if (result.HasValue && result.Value.IsBankrupt) return;

        ChangeState(GameState.DayStats);
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
        Time.timeScale = 0f;
    }

    // ── Debug API ────────────────────────────────────────────────

    /// ✅ ФІКС: Замінює небезпечну рефлексію в DebugGameController і GameStateSerializer.
    /// В production build — лише для відновлення дня при завантаженні збереження.
    /// В Editor / Development Build — також для тестування через DebugGameController.
    public void DEBUG_SetDay(int day)
    {
        CurrentDay = Mathf.Max(1, day);
        Debug.Log($"[GameLoop] Day встановлено: {CurrentDay}");
    }

    // ── ContextMenu test helpers ─────────────────────────────────

    [ContextMenu("Test: End Work Day")]
    public void TestEndDay()
    {
        if (CurrentState == GameState.WorkDay) EndWorkDay();
        else Debug.LogWarning($"[GameLoop] TEST: не в WorkDay (стан: {CurrentState})");
    }

    [ContextMenu("Test: Force Bankruptcy")]
    public void TestBankruptcy()
    {
        if (EconomyManager.Instance == null) return;
        EconomyManager.Instance.SetMoney(
            -(EconomyManager.Instance.DailyRent * EconomyManager.Instance.DebtDaysAllowed + 1));
        for (int i = 0; i < EconomyManager.Instance.DebtDaysAllowed; i++)
            EconomyManager.Instance.ApplyDailyRent();
    }
}