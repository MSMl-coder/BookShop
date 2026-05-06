// Assets/Scripts/Core/GameLoopManager.cs
// ОНОВЛЕНО: додано ResetDay(), SkipLoot(), OnNewDayStarted event
using UnityEngine;
using System;

 

public class GameLoopManager : MonoBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }
    public int CurrentDay { get; private set; } = 1;

    // Існуючі події
    public event Action<GameState> OnStateChanged;
    public event Action<int> OnDayEnded;

    // НОВІ події
    public event Action<int> OnNewDayStarted;   // day number — для UI "День N"
    public event Action      OnDayReset;        // скинути всі денні лічильники

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        ChangeState(GameState.Preparation);
    }

    // ── Стани ──────────────────────────────────────

    public void ChangeState(GameState newState)
    {
        Debug.Log($"[GameLoop] {CurrentState} → {newState}  (День {CurrentDay})");
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }

    // ── Головний ігровий цикл ──────────────────────

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

    /// WorkDay → LootPhase  (викликається кнопкою "Завершити день")
    public void EndWorkDay()
    {
        if (CurrentState != GameState.WorkDay)
        {
            Debug.LogWarning($"[GameLoop] EndWorkDay ігнорується: стан {CurrentState}");
            return;
        }

        OnDayEnded?.Invoke(CurrentDay);
        CurrentDay++;
        ChangeState(GameState.LootPhase);
    }

    /// LootPhase → Preparation  (після вибору всіх нагород або skip)
    /// Викликається з LootManager.SelectCard() або кнопкою "Пропустити"
    public void StartNewDay()
    {
        if (CurrentState != GameState.LootPhase)
        {
            Debug.LogWarning($"[GameLoop] StartNewDay ігнорується: стан {CurrentState}");
            return;
        }

        // Скидаємо денну статистику
        EconomyManager.Instance?.ResetDailyStats();
        OnDayReset?.Invoke();

        OnNewDayStarted?.Invoke(CurrentDay);
        ChangeState(GameState.Preparation);
    }

    /// Пропустити фазу нагород і одразу почати новий день
    public void SkipLootPhase()
    {
        if (CurrentState != GameState.LootPhase) return;
        Debug.Log("[GameLoop] Гравець пропускає нагороди.");
        StartNewDay();
    }

    // ── Debug / Test ───────────────────────────────

    [ContextMenu("Test: End Work Day")]
    public void TestEndDay()
    {
        Debug.Log("[GameLoop] TEST: завершуємо день примусово.");
        OnDayEnded?.Invoke(CurrentDay);
        CurrentDay++;
        ChangeState(GameState.LootPhase);
    }

    [ContextMenu("Test: Skip to Next State")]
    public void TestSkipState()
    {
        int next = ((int)CurrentState + 1) % 3;
        ChangeState((GameState)next);
    }
}
