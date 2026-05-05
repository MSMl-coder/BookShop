// Assets/Scripts/Tutorial/TutorialManager.cs
using UnityEngine;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    private HashSet<string> _completedSteps = new HashSet<string>();
    private TutorialStep _currentStep;

    public bool IsTutorialActive => _currentStep != null;

    public event System.Action<TutorialStep> OnStepStarted;
    public event System.Action<string> OnStepCompleted;
    public event System.Action OnAllCompleted;

    private const string PREFS_KEY = "TutorialProgress";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        LoadProgress();
    }

    // --- Public API ---

    public void TryTrigger(TutorialTrigger trigger)
    {
        foreach (var step in steps)
        {
            if (step.trigger == trigger && !_completedSteps.Contains(step.stepID))
            {
                ShowStep(step);
                return;
            }
        }
    }

    public void CompleteCurrentStep()
    {
        if (_currentStep == null) return;

        _completedSteps.Add(_currentStep.stepID);
        string completedID = _currentStep.stepID;
        _currentStep = null;

        OnStepCompleted?.Invoke(completedID);
        SaveProgress();

        // Перевіряємо чи всі кроки пройдено
        if (AreAllCompleted())
            OnAllCompleted?.Invoke();
    }

    // НОВИЙ: Пропустити всі кроки туторіалу
    public void SkipAll()
    {
        foreach (var step in steps)
            _completedSteps.Add(step.stepID);

        _currentStep = null;
        SaveProgress();
        OnAllCompleted?.Invoke();
        Debug.Log("[Tutorial] All steps skipped.");
    }

    // НОВИЙ: Скинути прогрес туторіалу
    public void ResetProgress()
    {
        _completedSteps.Clear();
        _currentStep = null;
        PlayerPrefs.DeleteKey(PREFS_KEY);
        Debug.Log("[Tutorial] Progress reset.");
    }

    public bool IsCompleted(string stepID) => _completedSteps.Contains(stepID);

    // --- Private ---

    private void ShowStep(TutorialStep step)
    {
        _currentStep = step;
        OnStepStarted?.Invoke(step);
        Debug.Log($"[Tutorial] → {step.stepID}: {step.message}");
    }

    private bool AreAllCompleted()
    {
        foreach (var step in steps)
            if (!_completedSteps.Contains(step.stepID)) return false;
        return true;
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetString(PREFS_KEY, string.Join(",", _completedSteps));
        PlayerPrefs.Save();
    }

    private void LoadProgress()
    {
        string saved = PlayerPrefs.GetString(PREFS_KEY, "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var id in saved.Split(','))
            if (!string.IsNullOrEmpty(id)) _completedSteps.Add(id);

        Debug.Log($"[Tutorial] Loaded {_completedSteps.Count} completed steps.");
    }
}