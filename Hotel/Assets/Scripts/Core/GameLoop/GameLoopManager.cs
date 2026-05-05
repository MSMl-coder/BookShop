// Assets/Scripts/Core/GameLoop/GameLoopManager.cs
using UnityEngine;
using System;

public class GameLoopManager : MonoBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }
    public int CurrentDay { get; private set; } = 1;

    public event Action<GameState> OnStateChanged;
    public event Action<int> OnDayStarted;
    public event Action<int> OnDayEnded;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        ChangeState(GameState.Preparation);
        TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnGameStart);
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        Debug.Log($"[GameLoop] Day {CurrentDay} → {newState}");
    }

    public void StartWorkDay()
    {
        if (CurrentState != GameState.Preparation) return;

        OnDayStarted?.Invoke(CurrentDay);
        ChangeState(GameState.WorkDay);
    }

    public void EndWorkDay()
    {
        if (CurrentState != GameState.WorkDay)
        {
            Debug.LogWarning($"[GameLoop] Cannot end WorkDay from state: {CurrentState}");
            return;
        }

        OnDayEnded?.Invoke(CurrentDay);
        EconomyManager.Instance?.ResetDailyStats();
        GameStateSerializer.Instance?.QuickSave();
        ChangeState(GameState.LootPhase);
        CurrentDay++;
    }

    // Editor тест — доступний тільки в Editor
    [ContextMenu("DEBUG: Skip to LootPhase")]
    public void Debug_SkipToLoot()
    {
        OnDayEnded?.Invoke(CurrentDay);
        EconomyManager.Instance?.ResetDailyStats();
        ChangeState(GameState.LootPhase);
        CurrentDay++;
    }
}