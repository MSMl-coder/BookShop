// Assets/Scripts/UI/Bookshop/Controllers/DayStatsController.cs
// НОВИЙ (Фаза 2):
//   Екран підсумків дня — показується під час GameState.DayStats.
//   Відображає: день, зароблено, продано книг, оренда, баланс після.
//   Кнопка "Далі" → GameLoopManager.FinishDayStats()
//
//   UXML: потрібен елемент "DayStatsOverlay" (дивись патч нижче).
//
//   UNITY SETUP:
//   1. Додати компонент DayStatsController на BookshopUI GameObject
//   2. Підключити в BookshopUIController (дивись патч)

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class DayStatsController : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Затримка перед показом (щоб toast встиг з'явитись)")]
    [SerializeField] private float showDelay = 0.3f;

    [Tooltip("Тривалість fade-in")]
    [SerializeField] private float fadeIn = 0.35f;

    // ── UI ───────────────────────────────────────────────────────
    private VisualElement _overlay;
    private Label         _dayLabel;
    private Label         _earnedLabel;
    private Label         _soldLabel;
    private Label         _rentLabel;
    private Label         _balanceLabel;
    private Label         _debtWarning;
    private Button        _continueBtn;

    private Coroutine _showCoroutine;

    // ── Initialize ───────────────────────────────────────────────

    public void Initialize(VisualElement root)
    {
        _overlay      = root.Q<VisualElement>("DayStatsOverlay");
        _dayLabel     = root.Q<Label>("DayStatsDay");
        _earnedLabel  = root.Q<Label>("DayStatsEarned");
        _soldLabel    = root.Q<Label>("DayStatsSold");
        _rentLabel    = root.Q<Label>("DayStatsRent");
        _balanceLabel = root.Q<Label>("DayStatsBalance");
        _debtWarning  = root.Q<Label>("DayStatsDebtWarning");
        _continueBtn  = root.Q<Button>("DayStatsContinueBtn");

        if (_overlay == null)
        {
            Debug.LogWarning("[DayStats] DayStatsOverlay не знайдено в UXML.");
            return;
        }

        _overlay.style.display = DisplayStyle.None;
        _overlay.style.opacity = 0f;

        if (_continueBtn != null)
            _continueBtn.clicked += OnContinueClicked;

        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += OnStateChanged;

        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnRentApplied += OnRentApplied;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= OnStateChanged;
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnRentApplied -= OnRentApplied;
    }

    // ── Handlers ─────────────────────────────────────────────────

    private DailyRentResult? _lastRentResult;

    private void OnRentApplied(DailyRentResult result)
    {
        // Зберігаємо результат — покажемо коли відкриється екран
        _lastRentResult = result;
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.DayStats)
        {
            if (_showCoroutine != null) StopCoroutine(_showCoroutine);
            _showCoroutine = StartCoroutine(ShowRoutine());
        }
        else
        {
            Hide();
        }
    }

    private void OnContinueClicked()
    {
        GameLoopManager.Instance?.FinishDayStats();
    }

    // ── Show/Hide ────────────────────────────────────────────────

    private IEnumerator ShowRoutine()
    {
        yield return new WaitForSeconds(showDelay);

        PopulateData();

        _overlay.style.display = DisplayStyle.Flex;

        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.deltaTime;
            _overlay.style.opacity = Mathf.Clamp01(elapsed / fadeIn);
            yield return null;
        }
        _overlay.style.opacity = 1f;
        _showCoroutine = null;
    }

    private void Hide()
    {
        if (_overlay == null) return;
        _overlay.style.display = DisplayStyle.None;
        _overlay.style.opacity = 0f;
    }

    // ── Data ─────────────────────────────────────────────────────

    private void PopulateData()
    {
        var eco = EconomyManager.Instance;
        var loop = GameLoopManager.Instance;
        if (eco == null || loop == null) return;

        int day      = loop.CurrentDay;
        int earned   = eco.MoneyEarnedToday;
        int sold     = eco.BooksSoldToday;
        int rent     = _lastRentResult?.RentAmount ?? eco.DailyRent;
        int balance  = eco.Money;
        bool inDebt  = eco.IsInDebt;
        int debtDay  = eco.DebtDaysCurrent;
        int debtMax  = eco.DebtDaysAllowed;

        if (_dayLabel     != null) _dayLabel.text     = $"ДЕНЬ {day} ЗАВЕРШЕНО";
        if (_earnedLabel  != null) _earnedLabel.text  = $"+{earned}₴";
        if (_soldLabel    != null) _soldLabel.text    = $"{sold}";
        if (_rentLabel    != null) _rentLabel.text    = $"-{rent}₴";
        if (_balanceLabel != null)
        {
            _balanceLabel.text = $"{balance}₴";
            _balanceLabel.style.color = balance < 0
                ? new StyleColor(new Color(0.9f, 0.3f, 0.2f))
                : StyleKeyword.Null;
        }

        // Попередження про борг
        if (_debtWarning != null)
        {
            if (inDebt && debtDay > 0)
            {
                _debtWarning.style.display = DisplayStyle.Flex;
                _debtWarning.text = $"⚠️ БОРГ: {debtDay}/{debtMax} дні. Погасіть до закінчення або збанкрутуєте!";
            }
            else
            {
                _debtWarning.style.display = DisplayStyle.None;
            }
        }
    }
}