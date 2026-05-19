// ═══════════════════════════════════════════════════════════
// ClubCardController.cs — Club card with flip animation
// Path: Assets/Scripts/UI/Bookshop/Controllers/ClubCardController.cs
//
// Wires to: GameLoopManager.OnStateChanged, EconomyManager.OnMoneyChanged,
//           GameLoopManager.OnNewDayStarted
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;

public class ClubCardController : MonoBehaviour
{
    private VisualElement _clubCard;
    private Label _dayLabel;
    private Label _moneyLabel;
    private Label _phaseLabel;
    private Label _clubMembers;
    private Label _clubReputation;

    // Phase names (English to match v3 prototype)
    private static readonly string[] PhaseNames =
    {
        "PREPARATION", // GameState.Preparation
        "WORK DAY",    // GameState.WorkDay
        "REWARDS",     // GameState.LootPhase
    };

    public void Initialize(VisualElement root)
    {
        _clubCard       = root.Q<VisualElement>("ClubCard");
        _dayLabel       = root.Q<Label>("DayLabel");
        _moneyLabel     = root.Q<Label>("MoneyLabel");
        _phaseLabel     = root.Q<Label>("PhaseLabel");
        _clubMembers    = root.Q<Label>("ClubMembers");
        _clubReputation = root.Q<Label>("ClubReputation");

        // Flip on click
        if (_clubCard != null)
            _clubCard.RegisterCallback<ClickEvent>(_ => ToggleFlip());

        // Subscribe to managers
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged += UpdateMoney;
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged += UpdatePhase;
            GameLoopManager.Instance.OnNewDayStarted += UpdateDay;
        }

        // Initial values
        UpdateMoney(EconomyManager.Instance?.Money ?? 0);
        UpdateDay(GameLoopManager.Instance?.CurrentDay ?? 1);
        UpdatePhase(GameLoopManager.Instance?.CurrentState ?? GameState.Preparation);
    }

    private void OnDisable()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged -= UpdateMoney;
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged -= UpdatePhase;
            GameLoopManager.Instance.OnNewDayStarted -= UpdateDay;
        }
    }

    private void ToggleFlip()
    {
        if (_clubCard == null) return;
        if (_clubCard.ClassListContains("flipped"))
            _clubCard.RemoveFromClassList("flipped");
        else
            _clubCard.AddToClassList("flipped");
    }

    private void UpdateMoney(int amount)
    {
        if (_moneyLabel != null) _moneyLabel.text = $"$ {amount:N0}";
    }

    private void UpdateDay(int day)
    {
        if (_dayLabel != null) _dayLabel.text = day.ToString("D2");
    }

    private void UpdatePhase(GameState state)
    {
        if (_phaseLabel != null)
        {
            int idx = (int)state;
            _phaseLabel.text = idx < PhaseNames.Length ? PhaseNames[idx] : state.ToString().ToUpper();
        }
    }

    public void SetClubStats(int members, int reputation)
    {
        if (_clubMembers != null) _clubMembers.text = members.ToString();
        if (_clubReputation != null) _clubReputation.text = reputation.ToString();
    }
}
