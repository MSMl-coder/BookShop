using UnityEngine;
using System;

public enum GameState { Preparation, WorkDay, LootPhase }

public class GameLoopManager : MonoBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }
    public int CurrentDay { get; private set; } = 1;

    public event Action<GameState> OnStateChanged;
    public event Action<int> OnDayEnded;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        ChangeState(GameState.Preparation);
    }

    // ТЕСТОВИЙ МЕТОД: Тепер він працює завжди
    [ContextMenu("Test: End Work Day")]
    public void TestEndDay() 
    {
        Debug.Log("[GameLoop] Тестовий виклик завершення дня...");
        
        // Для тесту ми ігноруємо перевірку if (CurrentState == GameState.WorkDay)
        OnDayEnded?.Invoke(CurrentDay);
        ChangeState(GameState.LootPhase);
        CurrentDay++;
    }

    public void ChangeState(GameState newState)
    {
        Debug.Log($"[GameLoop] Спроба змінити стан з {CurrentState} на {newState}");
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }

    public void StartWorkDay()
    {
        if (CurrentState == GameState.Preparation)
            ChangeState(GameState.WorkDay);
    }

    // РЕАЛЬНИЙ МЕТОД (для гри)
    public void EndWorkDay()
    {
        if (CurrentState == GameState.WorkDay)
        {
            OnDayEnded?.Invoke(CurrentDay);
            ChangeState(GameState.LootPhase);
            CurrentDay++;
        }
        else
        {
            Debug.LogWarning($"[GameLoop] Неможливо завершити робочий день, бо зараз стан: {CurrentState}");
        }
    }
}