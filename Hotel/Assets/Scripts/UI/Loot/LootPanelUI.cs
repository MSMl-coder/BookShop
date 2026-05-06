// Assets/Scripts/UI/Loot/LootPanelUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// Керує екраном кінця дня та loot-картками.
/// FIX 1: switch expression (type => ...) замінено на класичний switch statement
///         Причина: Unity може не підтримувати C# 8 switch expressions
///         залежно від .NET target у Player Settings
/// FIX 2: назви елементів відповідають реальному LootCardItem.uxml:
///         CardName, CardIcon, CardDescription, PickButton
public class LootPanelUI : MonoBehaviour
{
    [SerializeField] private UIDocument      uiDocument;
    [SerializeField] private VisualTreeAsset cardTemplate;

    private VisualElement _endDayPanel;
    private VisualElement _lootContainer;
    private Label _statBooks;
    private Label _statMoney;
    private Label _picksLabel;
    private Label _subtitle;

    private void OnEnable()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        _endDayPanel   = root.Q<VisualElement>("EndDayPanel");
        _lootContainer = root.Q<VisualElement>("LootContainer");
        _statBooks     = root.Q<Label>("StatBooks");
        _statMoney     = root.Q<Label>("StatMoney");
        _picksLabel    = root.Q<Label>("PicksLabel");
        _subtitle      = root.Q<Label>("EndDaySubtitle");

        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.LootPhase) Show();
        else Hide();
    }

    // ── Public ──

    public void Show()
    {
        if (_endDayPanel != null)
        {
            _endDayPanel.style.display = DisplayStyle.Flex;
            _endDayPanel.pickingMode   = PickingMode.Position;
        }
        UpdateStats();
        BuildCards();
    }

    public void Hide()
    {
        if (_endDayPanel != null)
        {
            _endDayPanel.style.display = DisplayStyle.None;
            _endDayPanel.pickingMode   = PickingMode.Ignore;
        }
    }

    // ── Stats ──

    private void UpdateStats()
    {
        int day = (GameLoopManager.Instance != null) ? GameLoopManager.Instance.CurrentDay : 1;
        if (_subtitle  != null) _subtitle.text = "День " + day;

        if (_statBooks != null)
            _statBooks.text = (EconomyManager.Instance != null)
                ? EconomyManager.Instance.BooksSoldToday.ToString()
                : "0";

        if (_statMoney != null)
            _statMoney.text = (EconomyManager.Instance != null)
                ? EconomyManager.Instance.MoneyEarnedToday.ToString("F0") + " грн"
                : "0 грн";

        UpdatePicksLabel();
    }

    private void UpdatePicksLabel()
    {
        if (_picksLabel == null) return;
        int picks = (LootManager.Instance != null) ? LootManager.Instance.PicksRemaining : 0;
        _picksLabel.text = "Виборів залишилось: " + picks;
    }

    // ── Cards ──

    private void BuildCards()
    {
        if (_lootContainer == null || cardTemplate == null) return;
        _lootContainer.Clear();

        List<LootCardTemplate> cards = null;
        if (LootManager.Instance != null)
            cards = LootManager.Instance.GetCurrentPool();

        if (cards == null || cards.Count == 0)
        {
            Debug.LogWarning("[LootPanelUI] Пул карток порожній.");
            return;
        }

        foreach (LootCardTemplate card in cards)
        {
            VisualElement cardEl = cardTemplate.Instantiate().ElementAt(0);

            // Назви відповідають реальному LootCardItem.uxml
            SetLabel(cardEl, "CardName",        card.cardName);
            SetLabel(cardEl, "CardDescription", card.description);

            // Іконка
            if (card.icon != null)
            {
                VisualElement iconEl = cardEl.Q<VisualElement>("CardIcon");
                if (iconEl != null)
                    iconEl.style.backgroundImage = new StyleBackground(card.icon);
            }

            // Золота картка
            if (card.isGold)
                cardEl.AddToClassList("gold");

            // Кнопка вибору
            Button pickBtn = cardEl.Q<Button>("PickButton");
            if (pickBtn != null)
            {
                int money   = (EconomyManager.Instance != null) ? EconomyManager.Instance.Money : 0;
                bool canPay  = (card.cost <= 0) || (money >= card.cost);
                bool canPick = (LootManager.Instance != null) && LootManager.Instance.CanPick;

                pickBtn.SetEnabled(canPay && canPick);

                if (!canPay && card.cost > 0)
                    pickBtn.text = "Мало коштів";
                else
                    pickBtn.text = "ОБРАТИ";

                LootCardTemplate captured = card;
                pickBtn.clicked += delegate { OnCardPicked(captured); };
            }

            _lootContainer.Add(cardEl);
        }
    }

    private void OnCardPicked(LootCardTemplate card)
    {
        bool paid = false;
        if (card.cost <= 0)
        {
            paid = true;
        }
        else if (EconomyManager.Instance != null)
        {
            paid = EconomyManager.Instance.SpendMoney(card.cost);
        }

        if (!paid) return;

        if (LootManager.Instance != null)
            LootManager.Instance.SelectCard(card);

        UpdateStats();
        BuildCards();
    }

    // ── Helpers ──

    // ВИПРАВЛЕНО: класичний switch statement замість switch expression (=>)
    // Switch expression синтаксис "type switch { X => Y }" потребує C# 8.0
    // і викликав помилки CS1003 "':' expected" та CS1525 "Invalid expression term ';'"
    private static string GetCardTypeName(LootCardType type)
    {
        switch (type)
        {
            case LootCardType.FurnitureUpgrade: return "МЕБЛІ";
            case LootCardType.MoneyBonus:       return "МОНЕТИ";
            case LootCardType.BookPack:         return "КНИГИ";
            default:                            return "БОНУС";
        }
    }

    private static void SetLabel(VisualElement root, string elementName, string text)
    {
        Label label = root.Q<Label>(elementName);
        if (label != null) label.text = text;
    }
}
