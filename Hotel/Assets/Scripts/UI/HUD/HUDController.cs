// Assets/Scripts/UI/HUD/HUDController.cs
using UnityEngine;
using UnityEngine.UIElements;

/// Оновлює HUD-елементи (гроші, день, престиж, фаза)
/// на основі подій від менеджерів.
///
/// UNITY SETUP:
/// Додай на той самий GameObject що і ShopUIManager.
public class HUDController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private Label _dayLabel;
    private Label _phaseLabel;
    private Label _moneyLabel;
    private Label _prestigeValue;
    private VisualElement _prestigeFill;

    private static readonly string[] PhaseNames =
    {
        "ПІДГОТОВКА",   // GameState.Preparation
        "ТОРГІВЛЯ",     // GameState.WorkDay
        "НАГОРОДИ"      // GameState.LootPhase
    };

    private void OnEnable()
    {
        if (uiDocument == null) return;

        var root    = uiDocument.rootVisualElement;
        _dayLabel   = root.Q<Label>("DayLabel");
        _phaseLabel = root.Q<Label>("PhaseLabel");
        _moneyLabel = root.Q<Label>("MoneyLabel");
        _prestigeValue = root.Q<Label>("PrestigeValue");
        _prestigeFill  = root.Q<VisualElement>("PrestigeBarFill");

        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged += UpdateMoney;
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += UpdatePhase;

        // Початковий стан
        UpdateMoney(EconomyManager.Instance?.Money ?? 0);
        UpdateDay(GameLoopManager.Instance?.CurrentDay ?? 1);
        UpdatePhase(GameLoopManager.Instance?.CurrentState ?? GameState.Preparation);
    }

    private void OnDisable()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged -= UpdateMoney;
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= UpdatePhase;
    }

    // ── Оновлення ──

    private void UpdateMoney(int amount)
    {
        if (_moneyLabel != null)
            _moneyLabel.text = amount.ToString("N0");
    }

    private void UpdateDay(int day)
    {
        if (_dayLabel != null)
            _dayLabel.text = $"ДЕНЬ {day}";
    }

    private void UpdatePhase(GameState state)
    {
        UpdateDay(GameLoopManager.Instance?.CurrentDay ?? 1);

        if (_phaseLabel != null)
        {
            int idx = (int)state;
            _phaseLabel.text = idx < PhaseNames.Length ? PhaseNames[idx] : state.ToString().ToUpper();
        }
    }

    /// Викликай ззовні коли змінюється престиж
    public void UpdatePrestige(int current, int max)
    {
        if (_prestigeValue != null)
            _prestigeValue.text = $"{current} / {max}";

        if (_prestigeFill != null)
        {
            float pct = max > 0 ? Mathf.Clamp01((float)current / max) * 100f : 0f;
            _prestigeFill.style.width = Length.Percent(pct);
        }
    }
}
