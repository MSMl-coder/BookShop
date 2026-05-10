// Assets/Scripts/Tutorial/TutorialManager.cs  [Фаза 1 — фінальна версія]
// ВИПРАВЛЕННЯ:
//   - EconomyManager.OnBookSold НЕ існує — подія видалена
//   - Тригер OnFirstSale вже викликається в EconomyManager.RecordBookSold()
//     через TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnFirstSale)
//     → дублювання тут не потрібне, підписка прибрана
//   - InitTutorial НЕ викликається з Awake — тільки через OnStateChanged
//   - blockInput реалізовано через InputBlocker
//   - highlightTarget реалізовано через TutorialHighlight

using UnityEngine;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    private HashSet<string> _completedSteps = new HashSet<string>();
    private TutorialStep    _currentStep;
    private bool            _allDone;

    public bool IsTutorialActive => _currentStep != null;
    public bool IsTutorialDone   => _allDone;

    public event System.Action<TutorialStep> OnStepStarted;
    public event System.Action<string>       OnStepCompleted;
    public event System.Action               OnAllCompleted;

    private const string PREFS_KEY = "TutorialProgress_v2";

    // ── Unity ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadProgress();
        // ✅ ФІКС: InitTutorial НЕ викликається тут — тільки через OnStateChanged
    }

    private void OnEnable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += OnStateChanged;

        // ✅ ФІКС: EconomyManager.OnBookSold НЕ існує.
        // RecordBookSold() вже викликає TryTrigger(OnFirstSale) напряму — тут не потрібно.
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= OnStateChanged;
    }

    // ── Public API ─────────────────────────────────────────────

    public void TryTrigger(TutorialTrigger trigger)
    {
        if (_allDone) return;

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

        if (_currentStep.blockInput)
            InputBlocker.SetBlocked(false);

        TutorialHighlight.ClearHighlight();

        _completedSteps.Add(_currentStep.stepID);
        string completedID = _currentStep.stepID;
        _currentStep = null;

        OnStepCompleted?.Invoke(completedID);
        SaveProgress();

        if (AreAllCompleted())
        {
            _allDone = true;
            OnAllCompleted?.Invoke();
            Debug.Log("[Tutorial] ✅ Всі кроки пройдено!");
        }
    }

    /// Пропустити весь туторіал (тільки весь, не окремий крок)
    public void SkipAll()
    {
        if (_currentStep != null)
        {
            if (_currentStep.blockInput) InputBlocker.SetBlocked(false);
            TutorialHighlight.ClearHighlight();
        }

        foreach (var step in steps)
            _completedSteps.Add(step.stepID);

        _currentStep = null;
        _allDone     = true;
        SaveProgress();
        OnAllCompleted?.Invoke();
        Debug.Log("[Tutorial] Туторіал пропущено.");
    }

    public void ResetProgress()
    {
        _completedSteps.Clear();
        _currentStep = null;
        _allDone     = false;
        PlayerPrefs.DeleteKey(PREFS_KEY);
        Debug.Log("[Tutorial] Progress reset.");
    }

    public bool IsCompleted(string stepID) => _completedSteps.Contains(stepID);

    // ── Private ────────────────────────────────────────────────

    private void OnStateChanged(GameState state)
    {
        if (_allDone) return;

        // Стартовий тригер — перший Preparation
        if (state == GameState.Preparation)
            TryTrigger(TutorialTrigger.OnGameStart);
        else if (state == GameState.LootPhase)
            TryTrigger(TutorialTrigger.OnDayEnd);
    }

    private void ShowStep(TutorialStep step)
    {
        _currentStep = step;

        if (step.blockInput)
            InputBlocker.SetBlocked(true);

        if (!string.IsNullOrEmpty(step.highlightTarget))
            TutorialHighlight.Highlight(step.highlightTarget);

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

        _allDone = AreAllCompleted();
        Debug.Log($"[Tutorial] Завантажено {_completedSteps.Count} кроків. Done={_allDone}");
    }
}