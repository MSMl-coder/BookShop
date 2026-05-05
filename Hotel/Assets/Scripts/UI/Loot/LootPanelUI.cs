// Assets/Scripts/UI/Loot/LootPanelUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class LootPanelUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset cardTemplate;

    private VisualElement _panel;
    private VisualElement _lootContainer;
    private Label _statBooks;
    private Label _statMoney;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _panel         = root.Q<VisualElement>("EndDayPanel");
        _lootContainer = root.Q<VisualElement>("LootContainer");
        _statBooks     = root.Q<Label>("StatBooks");
        _statMoney     = root.Q<Label>("StatMoney");

        Hide();
    }

    public void Show()
    {
        if (_panel == null) return;
        _panel.style.display  = DisplayStyle.Flex;
        _panel.pickingMode    = PickingMode.Position;

        UpdateStats();
        BuildCards();
        Debug.Log("[LootPanelUI] Shown.");
    }

    public void Hide()
    {
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.None;
        _panel.pickingMode   = PickingMode.Ignore;
    }

    private void UpdateStats()
    {
        if (EconomyManager.Instance == null) return;
        if (_statBooks != null) _statBooks.text = $"Books sold: {EconomyManager.Instance.BooksSoldToday}";
        if (_statMoney != null) _statMoney.text = $"Earned: ${EconomyManager.Instance.MoneyEarnedToday}";
    }

    private void BuildCards()
    {
        if (_lootContainer == null || cardTemplate == null) return;
        _lootContainer.Clear();

        List<LootCardTemplate> cards = LootManager.Instance?.GetCurrentPool();
        if (cards == null || cards.Count == 0)
        {
            Debug.LogWarning("[LootPanelUI] No cards to display.");
            return;
        }

        foreach (var card in cards)
        {
            VisualElement cardUI = cardTemplate.Instantiate();

            var nameLabel = cardUI.Q<Label>("CardName");
            var costLabel = cardUI.Q<Label>("CardCost");
            var pickBtn   = cardUI.Q<Button>("PickButton");

            if (nameLabel != null) nameLabel.text = card.cardName;
            if (costLabel != null) costLabel.text = $"${card.cost}";

            if (pickBtn != null)
            {
                bool canAfford = EconomyManager.Instance?.Money >= card.cost;
                pickBtn.SetEnabled(canAfford);
                pickBtn.text = canAfford ? "PICK" : "Not enough $";

                // Захоплюємо card у локальну змінну для closure
                LootCardTemplate capturedCard = card;
                pickBtn.clicked += () => OnCardPicked(capturedCard);
            }

            _lootContainer.Add(cardUI);
        }
    }

    private void OnCardPicked(LootCardTemplate card)
    {
        if (EconomyManager.Instance?.SpendMoney(card.cost) == true)
        {
            LootManager.Instance?.SelectCard(card);
            BuildCards(); // Оновлюємо після вибору
            UpdateStats();
        }
    }
}