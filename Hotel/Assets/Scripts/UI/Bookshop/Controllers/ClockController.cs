// ═══════════════════════════════════════════════════════════
// ClockController.cs — Game time controls
// Path: Assets/Scripts/UI/Bookshop/Controllers/ClockController.cs
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;

public class ClockController : MonoBehaviour
{
    [Header("Game Time")]
    [SerializeField] private int startHour = 12;
    [SerializeField] private int startMinute = 43;
    [SerializeField] private float gameMinutesPerSec = 0.5f;

    private Label _digits;
    private Button _btnPause, _btnPlay, _btnFast, _btnSettings;
    private BookshopUIController _master;

    private float _gMin;
    private float _speed = 1f;
    private bool _paused = false;

    public void Initialize(VisualElement root, BookshopUIController master)
    {
        _master = master;
        _digits = root.Q<Label>("ClockDigits");
        _btnPause = root.Q<Button>("BtnPause");
        _btnPlay  = root.Q<Button>("BtnPlay");
        _btnFast  = root.Q<Button>("BtnFast");
        _btnSettings = root.Q<Button>("BtnSettings");

        if (_btnPause != null) _btnPause.clicked += () => SetSpeed(0);
        if (_btnPlay  != null) _btnPlay.clicked  += () => SetSpeed(1);
        if (_btnFast  != null) _btnFast.clicked  += () => SetSpeed(2);
        if (_btnSettings != null) _btnSettings.clicked += OnSettings;

        _gMin = startHour * 60 + startMinute;
    }

    private void Update()
    {
        if (!_paused) _gMin = (_gMin + _speed * gameMinutesPerSec * Time.deltaTime * 10f) % (24 * 60);
        if (_digits != null)
        {
            int h = ((int)_gMin / 60) % 24;
            int m = (int)_gMin % 60;
            _digits.text = $"{h:D2}:{m:D2}";
        }
    }

    private void SetSpeed(int s)
    {
        _speed = s == 0 ? 1f : (s == 2 ? 3f : 1f);
        _paused = (s == 0);

        _btnPause?.RemoveFromClassList("active");
        _btnPlay?.RemoveFromClassList("active");
        _btnFast?.RemoveFromClassList("active");

        switch (s)
        {
            case 0: _btnPause?.AddToClassList("active"); _master?.Toast?.Show("⏸", "Time paused", ToastType.Warn); break;
            case 1: _btnPlay?.AddToClassList("active"); break;
            case 2: _btnFast?.AddToClassList("active"); _master?.Toast?.Show("⏩", "×3 speed", ToastType.Info); break;
        }
    }

    private void OnSettings()
    {
        _master?.Toast?.Show("⚙", "Settings coming soon", ToastType.Info);
        // Optional: open SaveLoadPanelUI here
        // SaveLoadPanelUI.Instance?.Open(SaveLoadMode.Save);
    }
}
