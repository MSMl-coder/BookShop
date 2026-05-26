// Assets/Scripts/UI/Bookshop/Controllers/PhaseBarController.cs
// ОНОВЛЕНО (Фаза 2):
//   - 4 сегменти: PREP / WORK / STATS / LOOT
//   - DayStats сегмент: PhaseSegStats / PhaseFillStats
//   - WorkDay прогрес: від часу відкриття до ShopCloseHour (динамічно)
//   - EditMode відображається як Preparation (підстан)

using UnityEngine;
using UnityEngine.UIElements;

public class PhaseBarController : MonoBehaviour
{
    [Header("Phase Durations (game-minutes) — для Prep та Loot")]
    [SerializeField] private float prepDurationMin  = 120f;
    [SerializeField] private float statsDurationMin = 30f;
    [SerializeField] private float lootDurationMin  = 60f;

    [Header("References")]
    [SerializeField] private FlipClockController clockCtrl;

    // ── UI ───────────────────────────────────────────────────────
    private VisualElement _phaseBar, _pointer, _pointerRow;

    private VisualElement _segPrep,  _segWork,  _segStats,  _segLoot;
    private VisualElement _fillPrep, _fillWork, _fillStats, _fillLoot;

    // ── State ────────────────────────────────────────────────────
    private GameState _currentPhase  = GameState.Preparation;
    private float     _phaseStartMin = 0f;
    private float     _shopOpenMin   = 0f;
    private float     _shopCloseMin  = 19f * 60f;

    // ── Bar cache ────────────────────────────────────────────────
    private float _barWidth = 0f;

    // ── Кількість сегментів ──────────────────────────────────────
    private const int SEG_COUNT = 4; // PREP / WORK / STATS / LOOT

    public void Initialize(VisualElement root, FlipClockController clock)
    {
        clockCtrl = clock;

        _phaseBar   = root.Q<VisualElement>("PhaseBar");
        _pointerRow = root.Q<VisualElement>("PhasePointerRow");
        _pointer    = root.Q<VisualElement>("PhasePointer");

        _segPrep  = root.Q<VisualElement>("PhaseSegPrep");
        _segWork  = root.Q<VisualElement>("PhaseSegWork");
        _segStats = root.Q<VisualElement>("PhaseSegStats");
        _segLoot  = root.Q<VisualElement>("PhaseSegLoot");

        _fillPrep  = root.Q<VisualElement>("PhaseFillPrep");
        _fillWork  = root.Q<VisualElement>("PhaseFillWork");
        _fillStats = root.Q<VisualElement>("PhaseFillStats");
        _fillLoot  = root.Q<VisualElement>("PhaseFillLoot");

        if (GameLoopManager.Instance != null)
        {
            _shopCloseMin = GameLoopManager.Instance.ShopCloseHour * 60f;
            GameLoopManager.Instance.OnStateChanged += OnPhaseChanged;
        }

        if (_phaseBar != null)
            _phaseBar.RegisterCallback<GeometryChangedEvent>(e => _barWidth = e.newRect.width);

        OnPhaseChanged(GameLoopManager.Instance?.CurrentState ?? GameState.Preparation);
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= OnPhaseChanged;
    }

    // ── Update ───────────────────────────────────────────────────

    private void Update()
    {
        if (clockCtrl == null || _pointer == null) return;

        float currentMin = clockCtrl.TotalGameMinutes;
        float elapsed    = currentMin - _phaseStartMin;
        float duration   = GetDuration(_currentPhase, currentMin);
        float t          = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f;

        SetFill(_currentPhase, t);

        // Рух стрілки в межах активного сегменту
        float segLeft = GetSegmentLeftFraction(_currentPhase);
        float ptrFrac = segLeft + t * (1f / SEG_COUNT);

        if (_pointerRow != null && _barWidth > 0f)
        {
            float px = ptrFrac * _barWidth - 7f;
            _pointer.style.marginLeft = Mathf.Clamp(px, 0f, _barWidth - 14f);
        }
    }

    // ── Phase change ─────────────────────────────────────────────

    private void OnPhaseChanged(GameState newState)
    {
        // EditMode — підстан Preparation, бар не змінюємо
        if (newState == GameState.EditMode) return;

        _currentPhase  = newState;
        _phaseStartMin = clockCtrl != null ? clockCtrl.TotalGameMinutes : 0f;

        if (newState == GameState.WorkDay)
            _shopOpenMin = _phaseStartMin;

        SetActive(_segPrep,  newState == GameState.Preparation);
        SetActive(_segWork,  newState == GameState.WorkDay);
        SetActive(_segStats, newState == GameState.DayStats);
        SetActive(_segLoot,  newState == GameState.LootPhase);

        // Fill попередніх сегментів
        if (newState != GameState.Preparation) SetFill(GameState.Preparation, 1f);
        if (newState != GameState.WorkDay)     SetFill(GameState.WorkDay,
            newState == GameState.DayStats || newState == GameState.LootPhase ? 1f : 0f);
        if (newState != GameState.DayStats)    SetFill(GameState.DayStats,
            newState == GameState.LootPhase ? 1f : 0f);
        if (newState != GameState.LootPhase)   SetFill(GameState.LootPhase, 0f);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private void SetActive(VisualElement seg, bool active)
    {
        if (seg == null) return;
        if (active) seg.AddToClassList("active");
        else        seg.RemoveFromClassList("active");
    }

    private void SetFill(GameState phase, float t)
    {
        var fill = phase switch
        {
            GameState.Preparation => _fillPrep,
            GameState.WorkDay     => _fillWork,
            GameState.DayStats    => _fillStats,
            GameState.LootPhase   => _fillLoot,
            _                     => null
        };
        if (fill == null) return;
        fill.style.width = new StyleLength(new Length(Mathf.Clamp01(t) * 100f, LengthUnit.Percent));
    }

    /// WorkDay: від часу відкриття до ShopCloseHour (динамічно)
    private float GetDuration(GameState phase, float currentMin) => phase switch
    {
        GameState.Preparation => prepDurationMin,
        GameState.WorkDay     => Mathf.Max(1f, _shopCloseMin - _shopOpenMin),
        GameState.DayStats    => statsDurationMin,
        GameState.LootPhase   => lootDurationMin,
        _                     => 60f
    };

    /// Ліва межа сегменту як частка від ширини бару (0..1)
    private float GetSegmentLeftFraction(GameState phase) => phase switch
    {
        GameState.Preparation => 0f,
        GameState.WorkDay     => 1f / SEG_COUNT,
        GameState.DayStats    => 2f / SEG_COUNT,
        GameState.LootPhase   => 3f / SEG_COUNT,
        _                     => 0f
    };
}