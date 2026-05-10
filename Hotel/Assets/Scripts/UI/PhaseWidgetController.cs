// Assets/Scripts/UI/Phase/PhaseWidgetController.cs
// ВИПРАВЛЕННЯ v2:
//   - Захист від подвійної підписки _mainBtn.clicked (унsubscribe перед subscribe)
//   - Додано WorkDayTimer: таймер зворотного відліку під час WorkDay
//     налаштовується через Inspector (workDayDuration, default 180 секунд)
//   - Таймер показується в PhaseTimer label
//   - По закінченню таймера → GameLoopManager.EndWorkDay() автоматично

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class PhaseWidgetController : MonoBehaviour
{
    public static PhaseWidgetController Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    [Header("WorkDay Timer")]
    [Tooltip("Тривалість робочого дня в секундах")]
    [SerializeField] private float workDayDuration = 180f;

    private Button        _mainBtn;
    private Label         _indicatorLabel;
    private Label         _timerLabel;
    private Label         _dayLabel;
    private Label         _phaseLabel;
    private VisualElement _dot0, _dot1, _dot2;

    private GameState _currentState;
    private Coroutine _timerCoroutine;

    private static readonly string[] BtnTexts      = { "ВІДКРИТИ МАГАЗИН", "ЗАВЕРШИТИ ДЕНЬ", "НАГОРОДИ..." };
    private static readonly string[] IndicatorTexts = { "ПІДГОТОВКА", "ТОРГІВЛЯ", "НАГОРОДИ" };
    private static readonly string[] BtnClasses     = { "open-shop", "end-day", "inactive" };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void OnEnable()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        _mainBtn        = root.Q<Button>("PhaseMainBtn");
        _indicatorLabel = root.Q<Label>("PhaseIndicatorLabel");
        _timerLabel     = root.Q<Label>("PhaseTimer");
        _dayLabel       = root.Q<Label>("DayLabel");
        _phaseLabel     = root.Q<Label>("PhaseLabel");
        _dot0 = root.Q<VisualElement>("PhaseDot0");
        _dot1 = root.Q<VisualElement>("PhaseDot1");
        _dot2 = root.Q<VisualElement>("PhaseDot2");

        // ВИПРАВЛЕНО: unsubscribe перед subscribe — захист від подвійної підписки
        if (_mainBtn != null)
        {
            _mainBtn.clicked -= OnMainBtnClicked;
            _mainBtn.clicked += OnMainBtnClicked;
        }

        var loop = GameLoopManager.Instance;
        if (loop != null)
        {
            loop.OnStateChanged  -= UpdateState;
            loop.OnStateChanged  += UpdateState;
            loop.OnNewDayStarted -= UpdateDayLabel;
            loop.OnNewDayStarted += UpdateDayLabel;
            UpdateState(loop.CurrentState);
            UpdateDayLabel(loop.CurrentDay);
        }
    }

    private void OnDisable()
    {
        if (_mainBtn != null) _mainBtn.clicked -= OnMainBtnClicked;

        var loop = GameLoopManager.Instance;
        if (loop != null)
        {
            loop.OnStateChanged  -= UpdateState;
            loop.OnNewDayStarted -= UpdateDayLabel;
        }

        StopTimer();
    }

    // ── State ───────────────────────────────────────────────────

    private void UpdateState(GameState state)
    {
        _currentState = state;
        int idx = (int)state;

        if (_mainBtn != null)
        {
            _mainBtn.text = idx < BtnTexts.Length ? BtnTexts[idx] : state.ToString();
            _mainBtn.SetEnabled(state != GameState.LootPhase);
            foreach (var cls in BtnClasses) _mainBtn.RemoveFromClassList(cls);
            if (idx < BtnClasses.Length) _mainBtn.AddToClassList(BtnClasses[idx]);
        }

        if (_indicatorLabel != null)
            _indicatorLabel.text = idx < IndicatorTexts.Length ? IndicatorTexts[idx] : "";

        if (_phaseLabel != null)
            _phaseLabel.text = idx < IndicatorTexts.Length ? IndicatorTexts[idx] : "";

        UpdateDots(state);

        // Таймер
        StopTimer();
        if (state == GameState.WorkDay)
            _timerCoroutine = StartCoroutine(WorkDayTimerRoutine());
        else if (_timerLabel != null)
            _timerLabel.text = "";
    }

    private void UpdateDayLabel(int day)
    {
        if (_dayLabel != null) _dayLabel.text = $"ДЕНЬ {day}";
    }

    private void UpdateDots(GameState state)
    {
        var dots = new[] { _dot0, _dot1, _dot2 };
        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null) continue;
            dots[i].RemoveFromClassList("active");
            dots[i].RemoveFromClassList("done");
            if (i == (int)state)     dots[i].AddToClassList("active");
            else if (i < (int)state) dots[i].AddToClassList("done");
        }
    }

    // ── Button ──────────────────────────────────────────────────

    private void OnMainBtnClicked()
    {
        switch (_currentState)
        {
            case GameState.Preparation:
                if (!HasBooksOnShelves()) { ShowWarning("Розмістіть книги на полицях!"); return; }
                GameLoopManager.Instance?.StartWorkDay();
                break;

            case GameState.WorkDay:
                // Ручне завершення дня — зупиняємо таймер і завершуємо
                StopTimer();
                GameLoopManager.Instance?.EndWorkDay();
                break;
        }
    }

    private static bool HasBooksOnShelves()
    {
        foreach (var cab in Object.FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude))
            foreach (var shelf in cab.shelves)
                if (shelf != null && shelf.GetBookCount() > 0) return true;
        return false;
    }

    // ── WorkDay Timer ───────────────────────────────────────────

    private IEnumerator WorkDayTimerRoutine()
    {
        float remaining = workDayDuration;

        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            remaining  = Mathf.Max(0f, remaining);

            if (_timerLabel != null)
            {
                int mins = Mathf.FloorToInt(remaining / 60f);
                int secs = Mathf.FloorToInt(remaining % 60f);
                _timerLabel.text = $"{mins:D2}:{secs:D2}";

                // Червоний колір коли менше 30 секунд
                _timerLabel.style.color = remaining <= 30f
                    ? new StyleColor(new Color(0.9f, 0.3f, 0.2f))
                    : StyleKeyword.Null;
            }

            yield return null;
        }

        // Таймер закінчився — завершуємо день автоматично
        Debug.Log("[PhaseWidget] WorkDay timer expired → EndWorkDay");
        if (_currentState == GameState.WorkDay)
            GameLoopManager.Instance?.EndWorkDay();
    }

    private void StopTimer()
    {
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }
    }

    // ── Warning ─────────────────────────────────────────────────

    private void ShowWarning(string text)
    {
        if (_timerLabel == null) return;
        _timerLabel.text = text;
        _timerLabel.style.color = new StyleColor(new Color(0.9f, 0.3f, 0.2f));
        StartCoroutine(ClearWarning());
    }

    private IEnumerator ClearWarning()
    {
        yield return new WaitForSeconds(3f);
        if (_timerLabel != null && _currentState != GameState.WorkDay)
        {
            _timerLabel.text = "";
            _timerLabel.style.color = StyleKeyword.Null;
        }
    }

    public void SetTimerText(string text)
    {
        if (_timerLabel != null) _timerLabel.text = text;
    }
}