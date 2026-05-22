// ═══════════════════════════════════════════════════════════════════
// FlipClockController.cs  v2 — matches fixed USS layout
// Path: Assets/Scripts/UI/Bookshop/Controllers/FlipClockController.cs
//
// DIGIT STRUCTURE (matches BookshopUI.uss):
//   .flip-digit
//     .flip-digit__bottom  — permanent bottom half (current value)
//       Label              — full 56px label, shifted up -28px to clip bottom
//     .flip-digit__top     — permanent top half (next value)
//       Label              — full 56px label at top: 0, clips to 28px
//     .flip-digit__flap    — animated: starts at height 28px, shrinks to 0
//       Label              — shows current value (folds away revealing top-next)
//     .flip-digit__gap     — decorative center line
//
// ANIMATION per digit change (currentVal → nextVal):
//   Step 1: set TopNext  = nextVal   (quietly, under flap)
//   Step 2: animate Flap height 28 → 0  (flap folds down, next revealed in top)
//   Step 3: set BottomCur = nextVal  (quietly, now bottom matches top)
//           set Flap height back to 28, set FlapLabel = nextVal
//   (No step 4 needed — bottom already updated)
// ═══════════════════════════════════════════════════════════════════

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class FlipClockController : MonoBehaviour
{
    // ─── Inner: one flip panel ───────────────────────────────────
    private class Digit
    {
        public readonly string     Id;
        public readonly VisualElement Panel;
        public readonly VisualElement Flap;
        public readonly Label         FlapLabel;
        public readonly Label         TopLabel;    // top half
        public readonly Label         BottomLabel; // bottom half

        public int  Current     = -1;
        public bool Animating   = false;

        public Digit(VisualElement root, string id)
        {
            Id     = id;
            Panel  = root.Q<VisualElement>(id);
            if (Panel == null) { Debug.LogWarning($"[FlipClock] Panel '{id}' not found"); return; }

            var flap   = Panel.Q<VisualElement>(id + "Flap");
            var top    = Panel.Q<VisualElement>(className: "flip-digit__top");
            var bottom = Panel.Q<VisualElement>(className: "flip-digit__bottom");

            Flap        = flap;
            FlapLabel   = flap?.Q<Label>(id + "FlapLabel");
            TopLabel    = top?.Q<Label>(id + "TopNext");
            BottomLabel = bottom?.Q<Label>(id + "BottomCur");
        }

        public bool IsValid => Panel != null && Flap != null && FlapLabel != null;

        public void SetInstant(int value)
        {
            Current = value;
            string s = value.ToString();
            if (FlapLabel   != null) FlapLabel.text   = s;
            if (TopLabel    != null) TopLabel.text    = s;
            if (BottomLabel != null) BottomLabel.text = s;
            // Reset flap to full height
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

    // Public read for PhaseBarController
    public float TotalGameMinutes => _totalMin;

    // ─────────────────────────────────────────────────────────────
    public void Initialize(VisualElement root, BookshopUIController master)
    {
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

        Refresh();
    }

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

    // ─────────────────────────────────────────────────────────────
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

        // Prepare: show next in top (under flap), keep current in bottom
        if (d.TopLabel  != null) d.TopLabel.text  = next.ToString();
        // Flap shows current value (it will fold away)
        if (d.FlapLabel != null) d.FlapLabel.text = d.Current.ToString();

        // Animate flap: height 28 → 0 (fold down, revealing top with next)
        float elapsed = 0f;
        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flipDuration);
            float h = Mathf.Lerp(28f, 0f, t);
            if (d.Flap != null) d.Flap.style.height = h;
            // Darken flap as it folds
            byte bright = (byte)Mathf.RoundToInt(Mathf.Lerp(24, 8, t));
            if (d.Flap != null)
                d.Flap.style.backgroundColor = new StyleColor(
                    new Color32(bright, (byte)(bright + 14), (byte)(bright + 18), 255));
            yield return null;
        }

        // Crossover: update bottom, restore flap (now showing next from top)
        d.Current = next;
        if (d.BottomLabel != null) d.BottomLabel.text = next.ToString();
        if (d.FlapLabel   != null) d.FlapLabel.text   = next.ToString();
        if (d.Flap        != null)
        {
            d.Flap.style.height = 28f;
            d.Flap.style.backgroundColor = StyleKeyword.Null; // reset to USS color
        }

        d.Animating = false;
    }

    private void Refresh()
    {
        int h = (int)(_totalMin / 60f) % 24;
        int m = (int)_totalMin % 60;
        _h1.SetInstant(h / 10);
        _h2.SetInstant(h % 10);
        _m1.SetInstant(m / 10);
        _m2.SetInstant(m % 10);
        _lastH = h; _lastM = m;
    }

    private void SetSpeed(int mode)
    {
        _btnPause?.RemoveFromClassList("active");
        _btnPlay?.RemoveFromClassList("active");
        _btnFast?.RemoveFromClassList("active");

        switch (mode)
        {
            case 0: _paused = true;  _speed = 1f; _btnPause?.AddToClassList("active"); break;
            case 1: _paused = false; _speed = 1f; _btnPlay?.AddToClassList("active");  break;
            case 2: _paused = false; _speed = 3f; _btnFast?.AddToClassList("active");
                Debug.Log("[FlipClock] Speed x3"); break;
        }
    }

    public int GameHour   => (int)(_totalMin / 60f) % 24;
    public int GameMinute => (int)_totalMin % 60;
    public void SetTime(int h, int m) { _totalMin = h * 60f + m; Refresh(); }
}
