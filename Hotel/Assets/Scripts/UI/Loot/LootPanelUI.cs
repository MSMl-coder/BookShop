// Assets/Scripts/UI/Loot/LootPanelUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// Екран нагород кінця дня.
/// Підписується на GameLoopManager.OnStateChanged.
public class LootPanelUI : MonoBehaviour
{
    [SerializeField] private UIDocument      uiDocument;
    [SerializeField] private VisualTreeAsset cardTemplate;

    // ── Elements ──
    private VisualElement _panel;
    private VisualElement _lootContainer;
    private Label _statBooks;
    private Label _statMoney;
    private Label _picksLabel;
    private Label _subtitle;
    private Button _skipBtn;

    private void OnEnable()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        _panel         = root.Q<VisualElement>("EndDayPanel");
        _lootContainer = root.Q<VisualElement>("LootContainer");
        _statBooks     = root.Q<Label>("StatBooks");
        _statMoney     = root.Q<Label>("StatMoney");
        _picksLabel    = root.Q<Label>("PicksLabel");
        _subtitle      = root.Q<Label>("EndDaySubtitle");
        _skipBtn       = root.Q<Button>("BtnSkipLoot");

        if (_panel == null)
        {
            Debug.LogWarning("[LootPanelUI] 'EndDayPanel' не знайдено у UXML.");
            return;
        }

        if (_skipBtn != null)
            _skipBtn.clicked += () => GameLoopManager.Instance?.SkipLootPhase();

        Hide();

        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    // ─────────────────────────────────────────────
    #region State
    // ─────────────────────────────────────────────

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.LootPhase) Show();
        else                              Hide();
    }

    private void Show()
    {
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.Flex;
        _panel.pickingMode   = PickingMode.Position;
        UpdateStats();
        BuildCards();
    }

    private void Hide()
    {
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.None;
        _panel.pickingMode   = PickingMode.Ignore;
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Stats
    // ─────────────────────────────────────────────

    private void UpdateStats()
    {
        int day = GameLoopManager.Instance?.CurrentDay ?? 1;
        if (_subtitle != null) _subtitle.text = $"День {day - 1}";

        // ВИПРАВЛЕНО: було booksSoldToday (lowercase) → правильно BooksSoldToday (PascalCase)
        if (_statBooks != null)
            _statBooks.text = (EconomyManager.Instance?.BooksSoldToday ?? 0).ToString();

        // ВИПРАВЛЕНО: було moneyEarnedToday (lowercase) → правильно MoneyEarnedToday (PascalCase)
        if (_statMoney != null)
        {
            int earned = EconomyManager.Instance?.MoneyEarnedToday ?? 0;
            _statMoney.text = $"{earned} грн";
        }

        UpdatePicksLabel();
    }

    private void UpdatePicksLabel()
    {
        if (_picksLabel == null) return;
        int picks = LootManager.Instance?.PicksRemaining ?? 0;
        _picksLabel.text = picks > 0
            ? $"Виборів залишилось: {picks}"
            : "Всі нагороди отримано!";
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Cards
    // ─────────────────────────────────────────────

    private void BuildCards()
    {
        if (_lootContainer == null || cardTemplate == null) return;
        _lootContainer.Clear();

        var cards = LootManager.Instance?.GetCurrentPool();
        if (cards == null || cards.Count == 0)
        {
            Debug.Log("[LootPanelUI] Немає карток у пулі. Починаємо новий день.");
            GameLoopManager.Instance?.StartNewDay();
            return;
        }

        bool canPick = LootManager.Instance?.CanPick ?? false;

        foreach (var card in cards)
        {
            var cardEl = cardTemplate.Instantiate().ElementAt(0);

            SetLabel(cardEl, "CardName",        card.cardName);
            SetLabel(cardEl, "CardDesc",        card.description);
            SetLabel(cardEl, "CardDescription", card.description);

            var iconEl = cardEl.Q<VisualElement>("CardIcon");
            if (iconEl != null && card.icon != null)
                iconEl.style.backgroundImage = new StyleBackground(card.icon);

            if (card.isGold) cardEl.AddToClassList("gold");

            var costLabel = cardEl.Q<Label>("CardCost");
            if (costLabel != null)
            {
                costLabel.text = card.cost <= 0 ? "БЕЗКОШТОВНО" : $"{card.cost} грн";
                if (card.cost <= 0) costLabel.AddToClassList("free");
            }

            var pickBtn = cardEl.Q<Button>("PickButton");
            if (pickBtn != null)
            {
                int money    = EconomyManager.Instance?.Money ?? 0;
                bool canPay  = card.cost <= 0 || money >= card.cost;
                bool enabled = canPick && canPay;

                pickBtn.SetEnabled(enabled);
                pickBtn.text = !canPick  ? "ОБРАНО"
                             : !canPay   ? "Мало грошей"
                             :             "ОБРАТИ";

                var captured = card;
                pickBtn.clicked += () => OnCardPicked(captured);
            }

            _lootContainer.Add(cardEl);
        }

        UpdatePicksLabel();
    }

    private void OnCardPicked(LootCardTemplate card)
    {
        if (card.cost > 0)
        {
            bool paid = EconomyManager.Instance?.SpendMoney(card.cost) ?? false;
            if (!paid) return;
        }

        LootManager.Instance?.SelectCard(card);
        BuildCards();
        UpdatePicksLabel();

        if (LootManager.Instance != null && !LootManager.Instance.CanPick)
        {
            Debug.Log("[LootPanelUI] Всі вибори використано → StartNewDay()");
            GameLoopManager.Instance?.StartNewDay();
        }
    }

    #endregion

    // ─────────────────────────────────────────────
    private static void SetLabel(VisualElement root, string name, string text)
    {
        var label = root.Q<Label>(name);
        if (label != null) label.text = text;
    }
}
