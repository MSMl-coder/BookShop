// Assets/Scripts/UI/v2/GameHUD/GameHUDController.cs
//
// Left nav: transparent floating buttons.
// Hover  = slide right (USS transition on translate).
// Active = class "nav-btn--active" зафіксовує позицію висунутою.
// При кліку на іншу кнопку — попередня повертається.

using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-15)]
public class GameHUDController : MonoBehaviour
{
    public static GameHUDController Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    // Clock
    private Label _clockHH;
    private Label _clockMM;

    // Speed
    private Button _speedBtn1;
    private Button _speedBtn2;
    private Button _speedBtn3;

    // Nav buttons
    private Button _btnAwards;
    private Button _btnBookHunters;
    private Button _btnDecorations;
    private Button _btnCatalog;
    private Button _btnInventory;

    // Currently active nav button
    private Button _activeNavBtn;

    private const string CLASS_SPEED_ACTIVE = "speed-btn--active";
    private const string CLASS_NAV_ACTIVE   = "nav-btn--active";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        var root = uiDocument?.rootVisualElement;
        if (root == null) return;

        _clockHH = root.Q<Label>("ClockHH");
        _clockMM = root.Q<Label>("ClockMM");

        _speedBtn1 = root.Q<Button>("SpeedBtn1");
        _speedBtn2 = root.Q<Button>("SpeedBtn2");
        _speedBtn3 = root.Q<Button>("SpeedBtn3");
        _speedBtn1?.RegisterCallback<ClickEvent>(_ => SetSpeed(1));
        _speedBtn2?.RegisterCallback<ClickEvent>(_ => SetSpeed(2));
        _speedBtn3?.RegisterCallback<ClickEvent>(_ => SetSpeed(3));

        _btnAwards      = root.Q<Button>("BtnAwards");
        _btnBookHunters = root.Q<Button>("BtnBookHunters");
        _btnDecorations = root.Q<Button>("BtnDecorations");
        _btnCatalog     = root.Q<Button>("BtnCatalog");
        _btnInventory   = root.Q<Button>("BtnInventory");

        // Bind nav buttons — кожна зберігає свій active стан
        BindNavBtn(_btnAwards,      () => { /* Awards panel */ });
        BindNavBtn(_btnBookHunters, () => { /* BookHunters panel */ });
        BindNavBtn(_btnDecorations, () => UIScreenManager.Instance?.ToggleEditMode());
        BindNavBtn(_btnCatalog,     () => { /* Catalog panel */ });
        BindNavBtn(_btnInventory,   () => BookshopUIController.Instance?.OpenInventoryFromShelf());

        UpdateClock(7, 0);
    }

    // ── Public API ────────────────────────────────────────────────

    public void UpdateClock(int hours, int minutes)
    {
        if (_clockHH != null) _clockHH.text = hours.ToString("D2");
        if (_clockMM != null) _clockMM.text = minutes.ToString("D2");
    }

    // ── Nav ───────────────────────────────────────────────────────

    /// Реєструє клік на nav кнопку:
    /// - додає nav-btn--active (фіксує slide-right)
    /// - прибирає з попередньої активної
    /// - виконує action
    private void BindNavBtn(Button btn, System.Action action)
    {
        if (btn == null) return;
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            SetActiveNav(btn);
            action?.Invoke();
        });
    }

    private void SetActiveNav(Button btn)
    {
        // Прибираємо з поточної активної
        if (_activeNavBtn != null && _activeNavBtn != btn)
            _activeNavBtn.RemoveFromClassList(CLASS_NAV_ACTIVE);

        // Якщо клікнули на вже активну — деактивуємо (toggle)
        if (_activeNavBtn == btn)
        {
            btn.RemoveFromClassList(CLASS_NAV_ACTIVE);
            _activeNavBtn = null;
        }
        else
        {
            btn.AddToClassList(CLASS_NAV_ACTIVE);
            _activeNavBtn = btn;
        }
    }

    // ── Speed ─────────────────────────────────────────────────────

    private void SetSpeed(int speed)
    {
        _speedBtn1?.EnableInClassList(CLASS_SPEED_ACTIVE, speed == 1);
        _speedBtn2?.EnableInClassList(CLASS_SPEED_ACTIVE, speed == 2);
        _speedBtn3?.EnableInClassList(CLASS_SPEED_ACTIVE, speed == 3);
        Debug.Log($"[GameHUD] Speed: {speed}x");
    }
}
