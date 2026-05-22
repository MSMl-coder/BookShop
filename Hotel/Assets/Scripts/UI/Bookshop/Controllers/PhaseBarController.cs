// ═══════════════════════════════════════════════════════════════════
// PhaseBarController.cs — Phase progress bar + moving pointer
// Path: Assets/Scripts/UI/Bookshop/Controllers/PhaseBarController.cs
//
// The phase bar shows 3 segments (PREP / WORK / LOOT) like a stained-glass
// window. The pointer (▼) moves left→right across the ACTIVE segment as
// the phase progresses, then jumps to the start of the next segment.
//
// Each segment has an inner fill strip (phase-segment__fill) whose width
// grows from 0% to 100% as the phase progresses.
//
// Phase durations are set per-phase in Inspector or read from GameLoopManager.
// ═══════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;

public class PhaseBarController : MonoBehaviour
{
    // ─── Phase duration config ───
    [Header("Phase Durations (game-minutes)")]
    [SerializeField] private float prepDurationMin  = 120f;  // 2h preparation
    [SerializeField] private float workDurationMin  = 480f;  // 8h work day
    [SerializeField] private float lootDurationMin  = 60f;   // 1h loot phase

    [Header("References")]
    [SerializeField] private FlipClockController clockCtrl;

    // ─── UI Elements ───
    private VisualElement _phaseBar;
    private VisualElement _pointer;
    private VisualElement _pointerRow;

    private VisualElement _segPrep, _segWork, _segLoot;
    private VisualElement _fillPrep, _fillWork, _fillLoot;

    // ─── State ───
    private GameState _currentPhase  = GameState.Preparation;
    private float     _phaseStartMin = 0f;   // game-minute when current phase began

    // ─── Bar layout cache ───
    private float _barWidth  = 0f;
    private float _segWidth  = 0f;  // approx: barWidth / 3 ignoring dividers

    public void Initialize(VisualElement root, FlipClockController clock)
    {
        clockCtrl   = clock;
        _phaseBar   = root.Q<VisualElement>("PhaseBar");
        _pointerRow = root.Q<VisualElement>("PhasePointerRow");
        _pointer    = root.Q<VisualElement>("PhasePointer");

        _segPrep  = root.Q<VisualElement>("PhaseSegPrep");
        _segWork  = root.Q<VisualElement>("PhaseSegWork");
        _segLoot  = root.Q<VisualElement>("PhaseSegLoot");

        _fillPrep = root.Q<VisualElement>("PhaseFillPrep");
        _fillWork = root.Q<VisualElement>("PhaseFillWork");
        _fillLoot = root.Q<VisualElement>("PhaseFillLoot");

        // Subscribe to phase changes
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += OnPhaseChanged;

        // Init
        OnPhaseChanged(GameLoopManager.Instance?.CurrentState ?? GameState.Preparation);

        // Cache bar width after layout
        if (_phaseBar != null)
            _phaseBar.RegisterCallback<GeometryChangedEvent>(OnBarLayout);
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= OnPhaseChanged;
    }

    private void OnBarLayout(GeometryChangedEvent e)
    {
        _barWidth = e.newRect.width;
        _segWidth = (_barWidth - 4f) / 3f; // 4px = 2 dividers × 2px
    }

    // ─────────────────────────────────────────────
    // Update — move pointer + fill active segment
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (clockCtrl == null || _pointer == null) return;

        float currentMin  = clockCtrl.TotalGameMinutes;
        float elapsedMin  = currentMin - _phaseStartMin;
        float phaseDur    = GetDuration(_currentPhase);

        float t = phaseDur > 0f ? Mathf.Clamp01(elapsedMin / phaseDur) : 0f;

        // Update fill strip width for current phase
        SetFill(_currentPhase, t);

        // Move pointer within the active segment
        // Segment offsets (approximate, assuming equal thirds):
        float segOffset = GetSegmentLeftPct(_currentPhase);
        // pointer position = segOffset + t * (1/3 of bar)
        float ptrPct = segOffset + t * (1f / 3f);

        if (_pointerRow != null && _barWidth > 0f)
        {
            // We position via margin-left in pixels
            float ptrPx = ptrPct * _barWidth - 7f; // 7px = half pointer width
            _pointer.style.marginLeft = Mathf.Clamp(ptrPx, 0f, _barWidth - 14f);
        }

        // Auto-advance phase when done (if not driven by GameLoopManager)
        if (t >= 1f && GameLoopManager.Instance == null)
        {
            AdvancePhase();
        }
    }

    // ─────────────────────────────────────────────
    // Phase change
    // ─────────────────────────────────────────────

    private void OnPhaseChanged(GameState newState)
    {
        _currentPhase  = newState;
        _phaseStartMin = clockCtrl != null ? clockCtrl.TotalGameMinutes : 0f;

        // Active class on segments
        SetActive(_segPrep, newState == GameState.Preparation);
        SetActive(_segWork, newState == GameState.WorkDay);
        SetActive(_segLoot, newState == GameState.LootPhase);

        // Reset fills for inactive segments
        if (newState != GameState.Preparation) SetFill(GameState.Preparation, 1f); // fully filled
        if (newState != GameState.WorkDay)     SetFill(GameState.WorkDay,     newState == GameState.LootPhase ? 1f : 0f);
        if (newState != GameState.LootPhase)   SetFill(GameState.LootPhase,   0f);
    }

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
            GameState.LootPhase   => _fillLoot,
            _                     => null
        };
        if (fill == null) return;
        fill.style.width = new StyleLength(new Length(Mathf.Clamp01(t) * 100f, LengthUnit.Percent));
    }

    private float GetDuration(GameState phase) => phase switch
    {
        GameState.Preparation => prepDurationMin,
        GameState.WorkDay     => workDurationMin,
        GameState.LootPhase   => lootDurationMin,
        _                     => 60f
    };

    // Left edge of segment as fraction of total bar width (0..1)
    private float GetSegmentLeftPct(GameState phase) => phase switch
    {
        GameState.Preparation => 0f,
        GameState.WorkDay     => 1f / 3f,
        GameState.LootPhase   => 2f / 3f,
        _                     => 0f
    };

    private void AdvancePhase()
    {
        _currentPhase = _currentPhase switch
        {
            GameState.Preparation => GameState.WorkDay,
            GameState.WorkDay     => GameState.LootPhase,
            GameState.LootPhase   => GameState.Preparation,
            _                     => GameState.Preparation
        };
        OnPhaseChanged(_currentPhase);
    }
}
