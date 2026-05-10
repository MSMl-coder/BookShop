// Assets/Scripts/Tutorial/TutorialManager.cs
// ФАЗА 1 — фіксований TutorialManager
//
// ВИПРАВЛЕНО (з ТЗ):
//   ПРОБЛЕМА: дубль — InitTutorial викликається і з Awake, і з GameLoopManager.
//   РІШЕННЯ: прибрано виклик з Awake; InitTutorial викликається ТІЛЬКИ з GameLoopManager
//             через подію OnStateChanged (GameState.Preparation → перший день).
//
//   ПРОБЛЕМА: поле blockInput існує але логіка не реалізована.
//   РІШЕННЯ: реалізовано через InputBlocker.SetBlocked(true/false) — блокує
//             InteractionRouter та інші системи вводу під час кроку.
//
//   ПРОБЛЕМА: поле highlightTarget існує але підсвітка не реалізована.
//   РІШЕННЯ: реалізовано TutorialHighlight.Highlight(elementName) — обводка/пульсація
//             цільового UI елемента або 3D-об'єкта.
//
//   ПРОБЛЕМА: тригер OnFirstSale ніколи не спрацьовував.
//   РІШЕННЯ: підключено до EconomyManager.OnBookSold.
//
//   РІШЕННЯ кнопки "НЕ буде": туторіал проходиться ОДИН РАЗ, кнопка "Пропустити"
//             є але не дає пропустити окремий крок — лише весь туторіал.

using UnityEngine;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    private HashSet<string> _completedSteps = new HashSet<string>();
    private TutorialStep    _currentStep;

    public bool IsTutorialActive  => _currentStep != null;
    public bool IsTutorialDone    => _allDone;
    private bool _allDone;

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
        // ✅ ФІКС: InitTutorial НЕ викликається тут
        // Туторіал стартує через OnEnable → підписка на події
    }

    private void OnEnable()
    {
        // ✅ ФІКС: підписуємось на GameLoopManager — туторіал стартує при першому Preparation
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += OnStateChanged;

        // ✅ ФІКС: тригер продажу — підключаємо до EconomyManager
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnBookSold += OnBookSold;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= OnStateChanged;

        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnBookSold -= OnBookSold;
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

        // Знімаємо блокування вводу
        if (_currentStep.blockInput)
            InputBlocker.SetBlocked(false);

        // Знімаємо підсвітку
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

    /// Пропустити весь туторіал (кнопка "Пропустити" — тільки весь, не окремий крок)
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

    /// Скинути прогрес (для дебагу)
    public void ResetProgress()
    {
        _completedSteps.Clear();
        _currentStep = null;
        _allDone     = false;
        PlayerPrefs.DeleteKey(PREFS_KEY);
        Debug.Log("[Tutorial] Прогрес скинуто.");
    }

    public bool IsCompleted(string stepID) => _completedSteps.Contains(stepID);

    // ── Private ────────────────────────────────────────────────

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Preparation && !_allDone)
        {
            // Стартовий тригер — перший день
            TryTrigger(TutorialTrigger.OnGameStart);
        }
        else if (state == GameState.LootPhase && !_allDone)
        {
            TryTrigger(TutorialTrigger.OnDayEnd);
        }
    }

    private void OnBookSold(BookInstance book)
    {
        // ✅ ФІКС: тригер продажу тепер підключений
        TryTrigger(TutorialTrigger.OnFirstSale);
    }

    private void ShowStep(TutorialStep step)
    {
        _currentStep = step;

        // ✅ ФІКС: блокування вводу якщо потрібно
        if (step.blockInput)
            InputBlocker.SetBlocked(true);

        // ✅ ФІКС: підсвічування цільового елемента
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
        Debug.Log($"[Tutorial] Завантажено {_completedSteps.Count} пройдених кроків. Done={_allDone}");
    }
}