// ═══════════════════════════════════════════════════════════════════
// ClubCardController.cs — Club card with CSS opacity fade flip
// Path: Assets/Scripts/UI/Bookshop/Controllers/ClubCardController.cs
//
// APPROACH: Unity UI Toolkit doesn't support 3D transforms or preserve-3d.
// Instead we use a 2-step opacity cross-fade:
//   1. Fade out current face (opacity 1→0, 0.18s)
//   2. Hide current, show next face, fade in (opacity 0→1, 0.18s)
//
// Both faces always exist in UXML at position: absolute, same size.
// The "hidden" class in USS sets display:none.
// We temporarily set opacity via style before toggling hidden.
// ═══════════════════════════════════════════════════════════════════

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class ClubCardController : MonoBehaviour
{
    [Header("Flip Animation")]
    [SerializeField] private float fadeDuration = 0.18f;

    private bool _isFlipped   = false;
    private bool _isAnimating = false;

    // Card wrapper (click target)
    private VisualElement _clubCard;
    // Two face panels
    private VisualElement _frontFace;
    private VisualElement _backFace;

    // Back face labels
    private Label _moneyLabel;
    private Label _levelLabel;
    private Label _phaseLabel;
    // Front face labels
    private Label _clubMembers;
    private Label _clubReputation;
    private Label _shopNameLabel;
    private Button _editNameBtn;

    private BookshopUIController _master;

    private static readonly string[] PhaseNames = { "PREPARATION", "WORK DAY", "REWARDS" };

    // ─────────────────────────────────────────────
    public void Initialize(VisualElement root, BookshopUIController master)
    {
        _master = master;

        _clubCard  = root.Q<VisualElement>("ClubCard");
        _frontFace = root.Q<VisualElement>("ClubCardFront");
        _backFace  = root.Q<VisualElement>("ClubCardBack");

        // Front labels
        _clubMembers    = root.Q<Label>("ClubMembers");
        _clubReputation = root.Q<Label>("ClubReputation");
        _shopNameLabel  = root.Q<Label>("ShopNameLabel");
        _editNameBtn    = root.Q<Button>("BtnEditShopName");

        // Back labels
        _moneyLabel = root.Q<Label>("MoneyLabel");
        _levelLabel = root.Q<Label>("DayLabel");
        _phaseLabel = root.Q<Label>("PhaseLabel");

        // Click the CARD (not the edit button) → flip
        if (_clubCard != null)
            _clubCard.RegisterCallback<ClickEvent>(OnCardClick);

        // Edit button
        if (_editNameBtn != null)
            _editNameBtn.clicked += () => _master?.Toast?.Show("✎", "Tap to rename shop", ToastType.Info);

        // Subscribe to game events
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged += UpdateMoney;
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged  += UpdatePhase;
            GameLoopManager.Instance.OnNewDayStarted += UpdateLevel;
        }

        // Initial data
        UpdateMoney(EconomyManager.Instance?.Money ?? 1240);
        UpdateLevel(GameLoopManager.Instance?.CurrentDay ?? 1);
        UpdatePhase(GameLoopManager.Instance?.CurrentState ?? GameState.Preparation);

        // Start on front face
        SetFaceInstant(showFront: true);
    }

    private void OnDisable()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged -= UpdateMoney;
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged  -= UpdatePhase;
            GameLoopManager.Instance.OnNewDayStarted -= UpdateLevel;
        }
    }

    // ─────────────────────────────────────────────
    // Click handler — ignore if clicking edit button
    // ─────────────────────────────────────────────
    private void OnCardClick(ClickEvent e)
    {
        // If user clicked the edit button, don't flip
        if (_editNameBtn != null && e.target == _editNameBtn) return;
        if (_editNameBtn != null && ((VisualElement)e.target).ClassListContains("club-card__edit-btn")) return;
        RequestFlip();
    }

    // ─────────────────────────────────────────────
    // Data bindings
    // ─────────────────────────────────────────────
    private void UpdateMoney(int amount)
    {
        if (_moneyLabel != null) _moneyLabel.text = $"$ {amount:N0}";
    }

    private void UpdateLevel(int day)
    {
        if (_levelLabel != null) _levelLabel.text = day.ToString("D2");
    }

    private void UpdatePhase(GameState state)
    {
        int idx = (int)state;
        if (_phaseLabel != null)
            _phaseLabel.text = idx < PhaseNames.Length ? PhaseNames[idx] : state.ToString().ToUpper();
    }

    public void SetClubStats(int members, int rep)
    {
        if (_clubMembers    != null) _clubMembers.text    = members.ToString();
        if (_clubReputation != null) _clubReputation.text = rep.ToString();
    }

    public void SetShopName(string name)
    {
        if (_shopNameLabel != null) _shopNameLabel.text = $"«{name}»";
    }

    // ─────────────────────────────────────────────
    // Flip — opacity cross-fade
    // ─────────────────────────────────────────────
    public void RequestFlip()
    {
        if (_isAnimating) return;
        StartCoroutine(DoFade());
    }

    private IEnumerator DoFade()
    {
        _isAnimating = true;
        bool toBack = !_isFlipped;

        VisualElement fromFace = toBack ? _frontFace : _backFace;
        VisualElement toFace   = toBack ? _backFace  : _frontFace;

        // Make sure "to" face is visible but transparent, then show it
        toFace.RemoveFromClassList("hidden");
        toFace.style.opacity = 0f;

        float elapsed = 0f;

        // Fade out "from", fade in "to" simultaneously
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            fromFace.style.opacity = 1f - t;
            toFace.style.opacity   = t;
            yield return null;
        }

        // Finalise
        fromFace.style.opacity = 0f;
        toFace.style.opacity   = 1f;
        fromFace.AddToClassList("hidden");

        _isFlipped    = toBack;
        _isAnimating  = false;
    }

    private void SetFaceInstant(bool showFront)
    {
        if (_frontFace == null || _backFace == null) return;

        if (showFront)
        {
            _frontFace.RemoveFromClassList("hidden");
            _frontFace.style.opacity = 1f;
            _backFace.AddToClassList("hidden");
            _backFace.style.opacity = 0f;
        }
        else
        {
            _backFace.RemoveFromClassList("hidden");
            _backFace.style.opacity = 1f;
            _frontFace.AddToClassList("hidden");
            _frontFace.style.opacity = 0f;
        }
    }
}
