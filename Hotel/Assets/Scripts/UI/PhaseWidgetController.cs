// Assets/Scripts/UI/Phase/PhaseWidgetController.cs
// ОНОВЛЕНО (Фаза 2):
//   - Прибрано UIDocument + Awake/OnEnable — тепер Initialize(root, clock) як всі контролери
//   - Додати на BookshopUI GameObject + підключити в BookshopUIController
//   - WorkDayClockRoutine: стежить за FlipClock.GameHour, о ShopCloseHour → EndWorkDay
//   - PhaseWidget видимий тільки в Preparation/WorkDay/DayStats/LootPhase
//   - DayStats: кнопка прихована (заглушка)
//   - 4 dots: PREP / WORK / STATS / LOOT

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class PhaseWidgetController : MonoBehaviour
{
    public static PhaseWidgetController Instance { get; private set; }

    // ── Buttons / Labels ────────────────────────────────────────
    private Button        _mainBtn;
    private Label         _indicatorLabel;
    private Label         _timerLabel;
    private VisualElement _phaseWidget;

    // Dots: 0=Prep, 1=Work, 2=Stats, 3=Loot
    private VisualElement _dot0, _dot1, _dot2, _dot3;

    private GameState             _currentState;
    private Coroutine             _timerCoroutine;
    private FlipClockController   _clock;

    private static readonly string[] BtnTexts = {
        "ВІДКРИТИ МАГАЗИН",   // Preparation
        "ЗАВЕРШИТИ ДЕНЬ",      // WorkDay
        "...",                 // DayStats (заглушка)
        "НАГОРОДИ...",         // LootPhase
    };
    private static readonly string[] IndicatorTexts = {
        "ПІДГОТОВКА", "ТОРГІВЛЯ", "ПІДСУМКИ", "НАГОРОДИ"
    };
    private static readonly string[] BtnClasses = {
        "open-shop", "end-day", "inactive", "inactive"
    };

    // ── Initialize (викликається з BookshopUIController) ─────────
    public void Initialize(VisualElement root, FlipClockController clock)
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { return; }

        _clock = clock;

        _phaseWidget    = root.Q<VisualElement>("PhaseWidget");
        _mainBtn        = root.Q<Button>("PhaseMainBtn");
        _indicatorLabel = root.Q<Label>("PhaseIndicatorLabel");
        _timerLabel     = root.Q<Label>("PhaseTimer");
        _dot0           = root.Q<VisualElement>("PhaseDot0");
        _dot1           = root.Q<VisualElement>("PhaseDot1");
        _dot2           = root.Q<VisualElement>("PhaseDot2");
        _dot3           = root.Q<VisualElement>("PhaseDot3");

        if (_mainBtn != null)
        {
            _mainBtn.clicked -= OnMainBtnClicked;
            _mainBtn.clicked += OnMainBtnClicked;
        }

        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged  -= OnStateChanged;
            GameLoopManager.Instance.OnStateChanged  += OnStateChanged;
            GameLoopManager.Instance.OnNewDayStarted -= OnNewDay;
            GameLoopManager.Instance.OnNewDayStarted += OnNewDay;
            OnStateChanged(GameLoopManager.Instance.CurrentState);
        }

        // Показуємо PhaseWidget
        if (_phaseWidget != null)
            _phaseWidget.style.display = DisplayStyle.Flex;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged  -= OnStateChanged;
            GameLoopManager.Instance.OnNewDayStarted -= OnNewDay;
        }
        StopTimer();
    }

    // ── State ────────────────────────────────────────────────────

    private void OnStateChanged(GameState state)
    {
        _currentState = state;
        StopTimer();

        int idx = state switch
        {
            GameState.Preparation => 0,
            GameState.EditMode    => 0, // EditMode = підстан Prep
            GameState.WorkDay     => 1,
            GameState.DayStats    => 2,
            GameState.LootPhase   => 3,
            _                     => 0
        };

        // Кнопка
        if (_mainBtn != null)
        {
            _mainBtn.text = BtnTexts[idx];
            foreach (var cls in BtnClasses) _mainBtn.RemoveFromClassList(cls);
            _mainBtn.AddToClassList(BtnClasses[idx]);
            _mainBtn.SetEnabled(idx == 0 || idx == 1);
        }

        // Лейбл
        if (_indicatorLabel != null)
            _indicatorLabel.text = IndicatorTexts[idx];

        // Dots
        UpdateDots(idx);

        // Таймер
        if (state == GameState.WorkDay)
            _timerCoroutine = StartCoroutine(WorkDayClockRoutine());
        else if (_timerLabel != null)
            _timerLabel.text = "";
    }

    private void OnNewDay(int day) { /* лейбл дня якщо потрібно */ }

    private void UpdateDots(int activeIdx)
    {
        var dots = new[] { _dot0, _dot1, _dot2, _dot3 };
        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null) continue;
            dots[i].RemoveFromClassList("active");
            dots[i].RemoveFromClassList("done");
            if (i < activeIdx)       dots[i].AddToClassList("done");
            else if (i == activeIdx) dots[i].AddToClassList("active");
        }
    }

    // ── Button ───────────────────────────────────────────────────

    private void OnMainBtnClicked()
    {
        switch (_currentState)
        {
            case GameState.Preparation:
            case GameState.EditMode:
                if (!HasBooksOnShelves()) { ShowWarning("Розмістіть книги на полицях!"); return; }
                GameLoopManager.Instance?.StartWorkDay();
                break;

            case GameState.WorkDay:
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

    // ── WorkDay Clock Routine ────────────────────────────────────

    private IEnumerator WorkDayClockRoutine()
    {
        int closeHour = GameLoopManager.Instance?.ShopCloseHour ?? 19;

        while (_currentState == GameState.WorkDay)
        {
            if (_clock == null)
                _clock = Object.FindFirstObjectByType<FlipClockController>();

            if (_clock != null)
            {
                int h = _clock.GameHour;
                int m = _clock.GameMinute;

                if (h >= closeHour)
                {
                    Debug.Log($"[PhaseWidget] {closeHour}:00 — крамниця закривається → EndWorkDay");
                    GameLoopManager.Instance?.EndWorkDay();
                    yield break;
                }

                if (_timerLabel != null)
                {
                    int rem = (closeHour * 60) - (h * 60 + m);
                    _timerLabel.text = $"{rem / 60:D2}:{rem % 60:D2}";
                    _timerLabel.style.color = rem <= 60
                        ? new StyleColor(new Color(0.9f, 0.3f, 0.2f))
                        : StyleKeyword.Null;
                }
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private void StopTimer()
    {
        if (_timerCoroutine != null) { StopCoroutine(_timerCoroutine); _timerCoroutine = null; }
        if (_timerLabel != null)     { _timerLabel.text = ""; _timerLabel.style.color = StyleKeyword.Null; }
    }

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
}