// Assets/Scripts/UI/Loot/LootPanelUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class LootPanelUI : MonoBehaviour
{
    [SerializeField] private UIDocument      uiDocument;
    [SerializeField] private VisualTreeAsset cardTemplate;

    private VisualElement _container;
    private Label _statBooks;
    private Label _statMoney;
    private Label _statVisitors;
    private Label _picksLabel;
    private Label _subtitle;


   private static void SetLabel(VisualElement root, string name, string text)
        => root.Q<Label>(name)?label.text = "...";

    private void OnEnable()
    {
        var root     = uiDocument.rootVisualElement;
        _container   = root.Q<VisualElement>("LootContainer");
        _statBooks   = root.Q<Label>("StatBooks");
        _statMoney   = root.Q<Label>("StatMoney");
        _statVisitors = root.Q<Label>("StatVisitors");
        _picksLabel  = root.Q<Label>("PicksLabel");
        _subtitle    = root.Q<Label>("EndDaySubtitle");
    }

    public void Show()
    {
        UpdateStats();
        BuildCards();
    }

    private void UpdateStats()
    {
        if (EconomyManager.Instance == null) return;

        if (_statBooks   != null) _statBooks.text    = EconomyManager.Instance.BooksSoldToday.ToString();
        if (_statMoney   != null) _statMoney.text    = $"{EconomyManager.Instance.MoneyEarnedToday:F0} грн";
        if (_subtitle    != null) _subtitle.text     = $"День {GameLoopManager.Instance?.CurrentDay ?? 1}";
        UpdatePicksLabel();
    }

    private void UpdatePicksLabel()
    {
        if (_picksLabel != null && LootManager.Instance != null)
            _picksLabel.text = $"Виборів залишилось: {LootManager.Instance.PicksRemaining}";
    }

    private void BuildCards()
    {
        if (_container == null || cardTemplate == null) return;
        _container.Clear();

        List<LootCardTemplate> cards = LootManager.Instance?.GetCurrentPool();
        if (cards == null || cards.Count == 0)
        {
            Debug.LogWarning("[LootPanelUI] Пул карток порожній.");
            return;
        }

        foreach (var card in cards)
        {
            VisualElement cardUI = cardTemplate.Instantiate().ElementAt(0);

            // Заповнення назв з LootCardItem.uxml
            SetLabel(cardUI, "CardIcon", GetCardIcon(card.type));
            SetLabel(cardUI, "CardName", card.cardName);
            SetLabel(cardUI, "CardDesc", card.description);
            SetLabel(cardUI, "CardType", GetCardTypeName(card.type));

            var costLabel = cardUI.Q<Label>("CardCost");
            if (costLabel != null)
            {
                costLabel.text = card.cost <= 0 ? "БЕЗКОШТОВНО" : $"{card.cost} грн";
                if (card.cost <= 0) costLabel.AddToClassList("free");
                else costLabel.RemoveFromClassList("free");
            }

            // Золота картка
            if (card.isGold) cardUI.AddToClassList("gold");

            var pickBtn = cardUI.Q<Button>("PickButton");
            if (pickBtn != null)
            {
                bool canAfford = EconomyManager.Instance?.Money >= card.cost;
                bool canPick   = LootManager.Instance?.CanPick ?? false;
                pickBtn.SetEnabled(canAfford && canPick);

                LootCardTemplate captured = card;
                pickBtn.clicked += () => OnCardPicked(captured);
            }

            _container.Add(cardUI);
        }
    }

    private void OnCardPicked(LootCardTemplate card)
    {
        if (EconomyManager.Instance?.SpendMoney(card.cost) != true && card.cost > 0) return;

        LootManager.Instance?.SelectCard(card);
        UpdatePicksLabel();
        BuildCards(); // Перебудовуємо після вибору
    }

    private static string GetCardIcon(LootCardType type) => type switch
    {
        LootCardType.FurnitureUpgrade => "🪑",
        LootCardType.MoneyBonus       => "💰",
        LootCardType.BookPack         => "📚",
       // LootCardType.PrestigeBonus    => "⭐",
        _ => "🎁"
    };

    private static string GetCardTypeName(LootCardType type) => type switch
    {
        LootCardType.FurnitureUpgrade => "МЕБЛІ",
        LootCardType.MoneyBonus       => "МОНЕТИ",
        LootCardType.BookPack         => "КНИГИ",
     //   LootCardType.PrestigeBonus    => "ПРЕСТИЖ",
        _ => "БОНУС"
    };

}
