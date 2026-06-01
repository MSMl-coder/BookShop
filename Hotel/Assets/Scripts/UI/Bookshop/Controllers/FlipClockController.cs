// Assets/Scripts/UI/Bookshop/Controllers/FlipClockController.cs
// ФІКС: Годинник не скидався після закінчення дня.
//   FlipClockController ніколи не підписувався на GameLoopManager.OnNewDayStarted —
//   _totalMin продовжував накопичуватись між днями.
//   Новий день починався з того часу де зупинився попередній (або ще пізніше).
//
// ВИПРАВЛЕННЯ:
//   [1] Підписка на GameLoopManager.OnNewDayStarted у Initialize
//   [2] OnNewDayStarted → ResetToStartTime() → SetTime(startHour, startMinute)
//   [3] Також підписка на OnDayReset (якщо потрібен скид при переходах між фазами)
//   [4] Відписка в OnDestroy

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class FlipClockController : MonoBehaviour
{
    // ─── Nested types ────────────────────────────────────────────

    private class Digit
    {
        public VisualElement Panel    { get; }
        public VisualElement Flap     { get; }
        public Label         FlapLabel { get; }
        public Label         TopLabel  { get; }
        public Label         BottomLabel { get; }
        public int  Current   { get; set; } = -1;
        public bool Animating { get; set; }

        public bool IsValid => Panel != null && Flap != null && FlapLabel != null;

        public Digit(VisualElement root, string name)
        {
            Panel       = root.Q<VisualElement>(name);
            Flap        = Panel?.Q<VisualElement>("Flap");
            FlapLabel   = Flap?.Q<Label>();
            TopLabel    = Panel?.Q<Label>("Top");
            BottomLabel = Panel?.Q<Label>("Bottom");
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
    [SerializeField] private int   startHour   = 12;
    [SerializeField] private int   startMinute = 43;
    [SerializeField] private float gameMinutesPerSec = 0.5f;

    [Header("Animation")]
    [SerializeField] private float flipDuration = 0.22f;

    // ─── State ───────────────────────────────────────────────────

    private Digit _h1, _h2, _m1, _m2;
    private float _totalMin;
    private float _speed  = 1f;
    private bool  _paused = false;
    private int   _lastH  = -1, _lastM = -1;

    private Button _btnPause, _btnPlay, _btnFast, _btnSettings;

    public float TotalGameMinutes => _totalMin;
    public float Speed    => _paused ? 0f : _speed;
    public bool  IsPaused => _paused;

    public static FlipClockController Instance { get; private set; }

    // ─── Initialize ──────────────────────────────────────────────

    public void Initialize(VisualElement root, BookshopUIController master)
    {
        if (Instance == null) Instance = this;
        else Debug.LogWarning("[FlipClock] Multiple instances detected!");

        _totalMin = startHour * 60f + startMinute;

        _h1 = new Digit(root, "FlipH1");
        _h2 = new Digit(root, "FlipH2");
        _m1 = new Digit(root, "FlipM1");
        _m2 = new Digit(root, "FlipM2");

        _btnPause    = root.Q<Button>("BtnPause");
        _btnPlay     = root.Q<Button>("BtnPlay");
        _btnFast     = root.Q<Button>("BtnFast");
        _btnSettings = root.Q<Button>("BtnSettings");

        if (_btnPause    != null) _btnPause.clicked    += () => SetSpeed(0);
        if (_btnPlay     != null) _btnPlay.clicked     += () => SetSpeed(1);
        if (_btnFast     != null) _btnFast.clicked     += () => SetSpeed(2);
        if (_btnSettings != null) _btnSettings.clicked += () =>
            Debug.Log("[FlipClock] Settings");

        // ✅ ФІКС: підписуємось на початок нового дня → скидаємо годинник
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnNewDayStarted += OnNewDayStarted;
            GameLoopManager.Instance.OnDayReset       += OnDayReset;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        // ✅ Відписуємось щоб уникнути memory leak
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnNewDayStarted -= OnNewDayStarted;
            GameLoopManager.Instance.OnDayReset       -= OnDayReset;
        }

        if (Instance == this) Instance = null;
    }

    // ─── Event handlers ──────────────────────────────────────────

    /// ✅ ФІКС: скидаємо час на початку кожного нового дня.
    /// OnNewDayStarted → LootPhase завершено, починається новий день (Preparation).
    private void OnNewDayStarted(int day)
    {
        ResetToStartTime();
        Debug.Log($"[FlipClock] День {day}: годинник скинуто → {startHour:D2}:{startMinute:D2}");
    }

    /// OnDayReset — додатковий guard на випадок якщо порядок подій зміниться.
    private void OnDayReset()
    {
        // OnNewDayStarted вже скинув, але якщо раптом не спрацював — скидаємо тут
        if (GameHour >= (GameLoopManager.Instance?.ShopCloseHour ?? 19))
            ResetToStartTime();
    }

    // ─── Public API ──────────────────────────────────────────────

    public int GameHour   => (int)(_totalMin / 60f) % 24;
    public int GameMinute => (int)_totalMin % 60;

    public void SetTime(int h, int m)
    {
        _totalMin = h * 60f + m;
        Refresh();
    }

    /// Скинути до стартового часу (startHour:startMinute).
    public void ResetToStartTime()
    {
        // Скидаємо час і паузуємо годинник (перезапуститься при відкритті магазину)
        SetTime(startHour, startMinute);
        // НЕ паузуємо тут — PhaseWidgetController або гравець відкриє магазин і запустить
    }

    // ─── Unity Update ─────────────────────────────────────────────

    private void Update()
    {
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

    // ─── Animations ──────────────────────────────────────────────

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
        if (!d.IsValid || d.Animating) yield break;
        d.Animating = true;

        if (d.FlapLabel   != null) d.FlapLabel.text   = d.Current.ToString();
        if (d.TopLabel    != null) d.TopLabel.text    = next.ToString();
        if (d.BottomLabel != null) d.BottomLabel.text = d.Current.ToString();

        float elapsed = 0f;
        while (elapsed < flipDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flipDuration);
            if (d.Flap != null) d.Flap.style.height = Mathf.Lerp(28f, 0f, t);
            yield return null;
        }

        d.Current = next;
        if (d.BottomLabel != null) d.BottomLabel.text = next.ToString();
        if (d.Flap        != null)
        {
            d.Flap.style.height = 28f;
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

    // ─── Speed control ────────────────────────────────────────────

    private void SetSpeed(int mode)
    {
        _btnPause?.RemoveFromClassList("active");
        _btnPlay?.RemoveFromClassList("active");
        _btnFast?.RemoveFromClassList("active");

        switch (mode)
        {
            case 0:
                _paused = true;
                _speed  = 1f;
                _btnPause?.AddToClassList("active");
                Time.timeScale = 0f;
                break;
            case 1:
                _paused = false;
                _speed  = 1f;
                _btnPlay?.AddToClassList("active");
                Time.timeScale = 1f;
                break;
            case 2:
                _paused = false;
                _speed  = 3f;
                _btnFast?.AddToClassList("active");
                Time.timeScale = 3f;
                Debug.Log("[FlipClock] Speed x3");
                break;
        }
    }
}