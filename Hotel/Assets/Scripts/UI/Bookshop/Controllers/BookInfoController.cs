// ═══════════════════════════════════════════════════════════
// BookInfoController.cs — Slide-in book info card
// Path: Assets/Scripts/UI/Bookshop/Controllers/BookInfoController.cs
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;

public class BookInfoController : MonoBehaviour
{
    public static BookInfoController Instance { get; private set; }

    private VisualElement _card;
    private Label _title;
    private Label _author;
    private Label _price;
    private Label _condition;
    private Label _bonusText;
    private Label _num;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    public void Initialize(VisualElement root)
    {
        _card      = root.Q<VisualElement>("BookInfoCard");
        _title     = root.Q<Label>("BookInfoTitle");
        _author    = root.Q<Label>("BookInfoAuthor");
        _price     = root.Q<Label>("BookInfoPrice");
        _condition = root.Q<Label>("BookInfoCondition");
        _bonusText = root.Q<Label>("BookInfoBonusText");
        _num       = root.Q<Label>("BookInfoNum");
    }

    public void Show()
    {
        if (_card != null)
        {
            _card.RemoveFromClassList("hidden");
            _card.AddToClassList("visible");
        }
    }

    public void Show(BookTemplate template)
    {
        if (template == null || _card == null) return;
        if (_title  != null) _title.text  = $"«{template.title}»";
        if (_author != null) _author.text = $"{template.author} · {template.writingYear}";
        if (_price  != null) _price.text  = $"$ {template.sellPrice:F0}";
        if (_condition != null) _condition.text = "Excellent";
        if (_num    != null) _num.text    = $"№ {template.bookID?.Substring(0, System.Math.Min(6, template.bookID?.Length ?? 0))}";
        if (_bonusText != null) _bonusText.text = GetBonusText(template);

        Show();
    }

    public void Hide()
    {
        if (_card == null) return;
        _card.RemoveFromClassList("visible");
        _card.AddToClassList("hidden");
    }

    private string GetBonusText(BookTemplate t)
    {
        // Simple bonus rules based on rarity
        return t.rarity switch
        {
            BookRarity.Common    => "+5% Mood",
            BookRarity.Uncommon  => "+10% Endurance",
            BookRarity.Rare      => "+15% Reputation",
            BookRarity.Epic      => "+20% Wisdom",
            BookRarity.Legendary => "+30% Prestige",
            _ => "—"
        };
    }
}
