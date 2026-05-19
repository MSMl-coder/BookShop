// ═══════════════════════════════════════════════════════════
// CalendarController.cs — Calendar display
// Path: Assets/Scripts/UI/Bookshop/Controllers/CalendarController.cs
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;

public class CalendarController : MonoBehaviour
{
    [Header("Calendar Config")]
    [SerializeField] private string monthName = "September";
    [SerializeField] private string yearLabel = "YEAR I · AUTUMN";
    [SerializeField] private int daysInWeek = 7;

    private Label _monthLabel;
    private Label _yearLabel;
    private Label _bigDay;
    private Label _weekday;
    private Label _dayMeta;
    private VisualElement _weekStrip;

    private static readonly string[] Weekdays =
        { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

    public void Initialize(VisualElement root)
    {
        _monthLabel = root.Q<Label>("CalMonthName");
        _yearLabel  = root.Q<Label>("CalYear");
        _bigDay     = root.Q<Label>("CalBigDay");
        _weekday    = root.Q<Label>("CalWeekday");
        _dayMeta    = root.Q<Label>("CalDayMeta");
        _weekStrip  = root.Q<VisualElement>("CalWeekStrip");

        if (_monthLabel != null) _monthLabel.text = monthName;
        if (_yearLabel  != null) _yearLabel.text = yearLabel;

        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnNewDayStarted += UpdateDay;
            UpdateDay(GameLoopManager.Instance.CurrentDay);
        }
        else
        {
            UpdateDay(1);
        }
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnNewDayStarted -= UpdateDay;
    }

    public void UpdateDay(int day)
    {
        if (_bigDay  != null) _bigDay.text = day.ToString();
        if (_weekday != null) _weekday.text = Weekdays[(day - 1) % 7];
        if (_dayMeta != null)
        {
            int daysToWeekend = 5 - ((day - 1) % 7);
            _dayMeta.text = daysToWeekend > 0
                ? $"To weekend · {daysToWeekend} day{(daysToWeekend == 1 ? "" : "s")}"
                : "Weekend";
        }
        BuildWeekStrip(day);
    }

    private void BuildWeekStrip(int currentDay)
    {
        if (_weekStrip == null) return;
        _weekStrip.Clear();
        int weekStart = ((currentDay - 1) / 7) * 7 + 1;
        for (int i = 0; i < daysInWeek; i++)
        {
            int dayNum = weekStart + i;
            var tick = new Label(dayNum.ToString());
            tick.AddToClassList("cal-tick");
            if (dayNum < currentDay)      tick.AddToClassList("past");
            else if (dayNum == currentDay) tick.AddToClassList("today");
            _weekStrip.Add(tick);
        }
    }
}
