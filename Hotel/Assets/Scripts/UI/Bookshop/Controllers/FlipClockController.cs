// Assets/Scripts/UI/Bookshop/Controllers/FlipClockController.cs
// v3 — ЗМІНИ:
//   [1] Digit конструктор: правильні id-prefixed імена UXML (FlipH1Flap, FlipH1TopNext...)
//   [2] SetSpeed: відновлено Time.timeScale = 0 для паузи (боти зупиняються як і було)
//   [3] Keyboard shortcuts: Space = пауза/відновлення, 1 = ×1, 2 = ×3, 3 = ×6
//   [4] _lastActiveMode: Space відновлює останній активний режим
//   [5] OnNewDayStarted: скидання годинника при початку нового дня
//   [6] Публічний BlockKeyboard для TextField фокусу

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class FlipClockController : MonoBehaviour
{
    // ─── Inner: один цифровий блок ───────────────────────────────
    private class Digit
    {
        public readonly string        Id;
        public readonly VisualElement Panel;
        public readonly VisualElement Flap;
        public readonly Label         FlapLabel;
        public readonly Label         TopLabel;
        public readonly Label         BottomLabel;

        public int  Current   = -1;
        public bool Animating = false;

        // IsValid: всі необхідні елементи знайдені
        public bool IsValid => Panel != null && Flap != null && FlapLabel != null;

        public Digit(VisualElement root, string id)
        {
            Id    = id;
            Panel = root.Q<VisualElement>(id);

            if (Panel == null)
            {
                Debug.LogWarning($"[FlipClock] Panel '{id}' не знайдено у UXML");
                return;
            }

            // ✅ [1] id-prefixed імена: FlipH1 → FlipH1Flap, FlipH1FlapLabel,
            //                                    FlipH1TopNext, FlipH1BottomCur
            var flap   = Panel.Q<VisualElement>(id + "Flap");
            var top    = Panel.Q<VisualElement>(className: "flip-digit__top");
            var bottom = Panel.Q<VisualElement>(className: "flip-digit__bottom");

            Flap        = flap;
            FlapLabel   = flap?.Q<Label>(id + "FlapLabel");
            TopLabel    = top?.Q<Label>(id + "TopNext");
            BottomLabel = bottom?.Q<Label>(id + "BottomCur");

            if (!IsValid)
                Debug.LogWarning($"[FlipClock] Digit '{id}': " +
                                 $"Flap={Flap != null} FlapLabel={FlapLabel != null} " +
                                 $"Top={TopLabel != null} Bottom={BottomLabel != null}");
        }

        public void SetInstant(int value)
        {
            Current = value;
            string s = value.ToString();
            if (FlapLabel   != null) FlapLabel.text   = s;
            if (TopLabel    != null) TopLabel.text    = s;
            if (BottomLabel != null) BottomLabel.text = s;
            if (Flap != null) Flap.style.height = 28f;
        }
    }

    // ─── Config ──────────────────────────────────────────────────
    [Header("Time")]
    [SerializeField] private int   startHour         = 12;
    [SerializeField] private int   startMinute       = 43;
    [SerializeField] private float gameMinutesPerSec = 0.5f;

    [Header("Animation")]
    [SerializeField] private float flipDuration = 0.22f;

    // ─── Швидкості ────────────────────────────────────────────────
    // Mode 0: пауза    → Time.timeScale = 0
    // Mode 1: ×1       → Time.timeScale = 1  [BtnPlay / клавіша 1]
    // Mode 2: ×3       → Time.timeScale = 3  [BtnFast / клавіша 2]
    // Mode 3: ×6       → Time.timeScale = 6  [тільки клавіша 3]
    private static readonly float[] TIMESCALES   = { 0f, 1f, 3f };
    private static readonly float[] CLOCK_SPEEDS = { 1f, 1f, 3f };

    // ─── State ───────────────────────────────────────────────────
    private Digit  _h1, _h2, _m1, _m2;
    private float  _totalMin;
    private float  _speed       = 1f;
    private bool   _paused      = false;
    private int    _currentMode = 1;   // активний режим (1 = ×1 за замовчуванням)
    private int    _lastActiveMode = 1;// ✅ [4] для Space toggle
    private int    _lastH = -1, _lastM = -1;

    private Button _btnPause, _btnPlay, _btnFast;

    // ─── Public ──────────────────────────────────────────────────
    public float TotalGameMinutes => _totalMin;
    public float Speed    => _paused ? 0f : _speed;
    public bool  IsPaused => _paused;
    public int   GameHour   => (int)(_totalMin / 60f) % 24;
    public int   GameMinute => (int)_totalMin % 60;

    /// Встановити true поки TextField у фокусі — блокує клавіатурні шорткати.
    public static bool BlockKeyboard { get; set; } = false;

    public static FlipClockController Instance { get; private set; }

    // ─── Initialize ──────────────────────────────────────────────

    public void Initialize(VisualElement root, BookshopUIController master)
    {
        if (Instance == null) Instance = this;
        else Debug.LogWarning("[FlipClock] Duplicate instance!");

        _totalMin = startHour * 60f + startMinute;

        _h1 = new Digit(root, "FlipH1");
        _h2 = new Digit(root, "FlipH2");
        _m1 = new Digit(root, "FlipM1");
        _m2 = new Digit(root, "FlipM2");

        _btnPause = root.Q<Button>("BtnPause");
        _btnPlay  = root.Q<Button>("BtnPlay");
        _btnFast  = root.Q<Button>("BtnFast");
        var _btnSettings = root.Q<Button>("BtnSettings");

        if (_btnPause    != null) _btnPause.clicked    += () => SetSpeed(0);
        if (_btnPlay     != null) _btnPlay.clicked     += () => SetSpeed(1);
        if (_btnFast     != null) _btnFast.clicked     += () => SetSpeed(2);
        if (_btnSettings != null) _btnSettings.clicked += OnSettingsClicked;

        // ✅ [5] Скидаємо годинник при початку нового дня
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnNewDayStarted += OnNewDayStarted;
            GameLoopManager.Instance.OnDayReset       += OnDayReset;
        }

        // Стартуємо у режимі ×1 (BtnPlay active)
        ApplyModeVisuals(1);
        Refresh();

        Debug.Log($"[FlipClock] Init: {startHour:D2}:{startMinute:D2} | " +
                  $"H1={_h1.IsValid} H2={_h2.IsValid} M1={_m1.IsValid} M2={_m2.IsValid}");
    }

    private void OnDestroy()
    {
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnNewDayStarted -= OnNewDayStarted;
            GameLoopManager.Instance.OnDayReset       -= OnDayReset;
        }
        if (Instance == this) Instance = null;
    }

    // ─── Unity Update ─────────────────────────────────────────────

    private void Update()
    {
        HandleKeyboard();

        if (_paused) return;

        _totalMin += _speed * gameMinutesPerSec * Time.deltaTime;
        if (_totalMin >= 1440f) _totalMin -= 1440f;

        int h = (int)(_totalMin / 60f) % 24;
        int m = (int)_totalMin % 60;

        if (h != _lastH || m != _lastM)
        {
            TriggerFlips(h, m);
            _lastH = h;
            _lastM = m;
        }
    }

    // ─── Keyboard shortcuts ───────────────────────────────────────
    // ✅ [3] Space = пауза/відновлення | 1 = ×1 | 2 = ×3 | 3 = ×6

    private void HandleKeyboard()
    {
        var kb = Keyboard.current;
        if (kb == null || BlockKeyboard) return;

        if      (kb.spaceKey.wasPressedThisFrame) TogglePause();
        else if (kb.digit1Key.wasPressedThisFrame) SetSpeed(1);
        else if (kb.digit2Key.wasPressedThisFrame) SetSpeed(2);
        
    }

    /// ✅ [4] Space toggle: якщо на паузі → відновлює останній активний режим.
    private void TogglePause()
    {
        if (_paused)
            SetSpeed(_lastActiveMode);
        else
            SetSpeed(0);
    }

    // ─── SetSpeed ─────────────────────────────────────────────────

    public void SetSpeed(int mode)
    {
        mode = Mathf.Clamp(mode, 0, 2);

        // Запам'ятовуємо останній ненульовий режим для Space toggle
        if (mode != 0) _lastActiveMode = mode;

        _currentMode    = mode;
        _paused         = (mode == 0);
        _speed          = CLOCK_SPEEDS[mode];
        Time.timeScale  = TIMESCALES[mode];  // ✅ [2] пауза зупиняє ботів (timeScale=0)

        ApplyModeVisuals(mode);

        Debug.Log($"[FlipClock] Speed mode {mode}: " +
                  $"timeScale={Time.timeScale} clockSpeed={_speed}×");
    }

    /// Оновлює .active класи на кнопках відповідно до поточного режиму.
    private void ApplyModeVisuals(int mode)
    {
        _btnPause?.RemoveFromClassList("active");
        _btnPlay?.RemoveFromClassList("active");
        _btnFast?.RemoveFromClassList("active");

        switch (mode)
        {
            case 0: _btnPause?.AddToClassList("active"); break;
            case 1: _btnPlay?.AddToClassList("active");  break;
            case 2: _btnFast?.AddToClassList("active");  break;
            
        }
    }

    // ─── Day events ──────────────────────────────────────────────

    private void OnNewDayStarted(int day)
    {
        ResetToStartTime();
        Debug.Log($"[FlipClock] День {day}: скинуто → {startHour:D2}:{startMinute:D2}");
    }

    private void OnDayReset()
    {
        if (GameHour >= (GameLoopManager.Instance?.ShopCloseHour ?? 19))
            ResetToStartTime();
    }

    private void OnSettingsClicked() => Debug.Log("[FlipClock] Settings");

    // ─── Public API ──────────────────────────────────────────────

    public void SetTime(int h, int m)
    {
        _totalMin = h * 60f + m;
        Refresh();
    }

    public void ResetToStartTime() => SetTime(startHour, startMinute);

    // ─── Flip animation ──────────────────────────────────────────

    private void TriggerFlips(int h, int m)
    {
        int h1 = h / 10, h2 = h % 10, m1 = m / 10, m2 = m % 10;
        if (_h1.Current != h1) StartCoroutine(Flip(_h1, h1));
        if (_h2.Current != h2) StartCoroutine(Flip(_h2, h2));
        if (_m1.Current != m1) StartCoroutine(Flip(_m1, m1));
        if (_m2.Current != m2) StartCoroutine(Flip(_m2, m2));
    }

    private IEnumerator Flip(Digit d, int next)
    {
        if (!d.IsValid || d.Animating) { d.SetInstant(next); yield break; }
        d.Animating = true;

        if (d.TopLabel  != null) d.TopLabel.text  = next.ToString();
        if (d.FlapLabel != null) d.FlapLabel.text = d.Current.ToString();

        float elapsed = 0f;
        while (elapsed < flipDuration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled — щоб анімація йшла навіть на паузі
            float t = Mathf.Clamp01(elapsed / flipDuration);
            if (d.Flap != null) d.Flap.style.height = Mathf.Lerp(28f, 0f, t);

            byte bright = (byte)Mathf.RoundToInt(Mathf.Lerp(24, 8, t));
            if (d.Flap != null)
                d.Flap.style.backgroundColor = new StyleColor(
                    new Color32(bright, (byte)(bright + 14), (byte)(bright + 18), 255));
            yield return null;
        }

        d.Current = next;
        if (d.BottomLabel != null) d.BottomLabel.text = next.ToString();
        if (d.FlapLabel   != null) d.FlapLabel.text   = next.ToString();
        if (d.Flap != null)
        {
            d.Flap.style.height          = 28f;
            d.Flap.style.backgroundColor = StyleKeyword.Null;
        }
        d.Animating = false;
    }

    private void Refresh()
    {
        int h = (int)(_totalMin / 60f) % 24;
        int m = (int)_totalMin % 60;
        _h1?.SetInstant(h / 10);
        _h2?.SetInstant(h % 10);
        _m1?.SetInstant(m / 10);
        _m2?.SetInstant(m % 10);
        _lastH = h;
        _lastM = m;
    }
}