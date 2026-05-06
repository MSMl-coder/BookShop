// Assets/Scripts/UI/Phase/PhaseWidgetController.cs
// ОНОВЛЕНО: підписка на OnNewDayStarted; HUD DayLabel/PhaseLabel синхронізовані
using UnityEngine;
using UnityEngine.UIElements;

public class PhaseWidgetController : MonoBehaviour
{
    public static PhaseWidgetController Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    private Button        _mainBtn;
    private Label         _indicatorLabel;
    private Label         _timerLabel;
    private Label         _dayLabel;
    private Label         _phaseLabel;
    private VisualElement _dot0, _dot1, _dot2;

    private GameState _currentState;

    private static readonly string[] BtnTexts =
        { "ВІДКРИТИ МАГАЗИН", "ЗАВЕРШИТИ ДЕНЬ", "НАГОРОДИ..." };
    private static readonly string[] IndicatorTexts =
        { "ПІДГОТОВКА", "ТОРГІВЛЯ", "НАГОРОДИ" };
    private static readonly string[] BtnClasses =
        { "open-shop", "end-day", "inactive" };

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

        if (_mainBtn != null) _mainBtn.clicked += OnMainBtnClicked;

        var loop = GameLoopManager.Instance;
        if (loop != null)
        {
            loop.OnStateChanged  += UpdateState;
            loop.OnNewDayStarted += day => UpdateDayLabel(day);
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
            loop.OnNewDayStarted -= day => UpdateDayLabel(day);
        }
    }

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
        if (_timerLabel != null) _timerLabel.text = "";
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

    private void OnMainBtnClicked()
    {
        switch (_currentState)
        {
            case GameState.Preparation:
                if (!HasBooksOnShelves()) { ShowWarning("Розмістіть книги на полицях!"); return; }
                GameLoopManager.Instance?.StartWorkDay();
                break;
            case GameState.WorkDay:
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

    private void ShowWarning(string text)
    {
        if (_timerLabel == null) return;
        _timerLabel.text = text;
        _timerLabel.style.color = new StyleColor(new Color(0.9f, 0.3f, 0.2f));
        StartCoroutine(ClearWarning());
    }

    private System.Collections.IEnumerator ClearWarning()
    {
        yield return new WaitForSeconds(3f);
        if (_timerLabel != null) { _timerLabel.text = ""; _timerLabel.style.color = StyleKeyword.Null; }
    }

    public void SetTimerText(string text) { if (_timerLabel != null) _timerLabel.text = text; }
}
