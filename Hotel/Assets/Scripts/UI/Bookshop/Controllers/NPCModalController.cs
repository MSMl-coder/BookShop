// ═══════════════════════════════════════════════════════════
// NPCModalController.cs — NPC dialogue modal
// Path: Assets/Scripts/UI/Bookshop/Controllers/NPCModalController.cs
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

// Placeholder type matching existing BookHunter system.
// Adjust the field names if your real BookHunterData differs.
[System.Serializable]
public class BookHunterData
{
    public string id;
    public string name;
    public string role;
    public string portraitIcon;
    public string dialogue;
    public List<DialogueOption> options;
    public int level;
    public int reputation;
    public bool isClubMember;
    public int dealsMade;
    public BookGenre preferredGenre;
    public int budget;
}

[System.Serializable]
public class DialogueOption
{
    public string icon;
    public string text;
    public int price;
    public string actionId; // e.g. "sell-collection", "offer-membership"
}

public class NPCModalController : MonoBehaviour
{
    private VisualElement _root;
    private BookshopUIController _master;

    private Label _portrait;
    private Label _nameLabel;
    private Label _roleLabel;
    private Label _dialogue;
    private VisualElement _optionsContainer;
    private ScrollView _statsScroll;
    private Label _tagTop;
    private Label _pageNum;

    private BookHunterData _currentNPC;

    public void Initialize(VisualElement root, BookshopUIController master)
    {
        _root = root;
        _master = master;

        _portrait    = root.Q<Label>("NPCPortrait");
        _nameLabel   = root.Q<Label>("NPCName");
        _roleLabel   = root.Q<Label>("NPCRole");
        _dialogue    = root.Q<Label>("NPCDialogue");
        _optionsContainer = root.Q<VisualElement>("NPCOptions");
        _statsScroll = root.Q<ScrollView>("NPCStats");
        _tagTop      = root.Q<Label>("NPCTagTop");
        _pageNum     = root.Q<Label>("NPCPageNum");
    }

    public void LoadNPC(BookHunterData npc)
    {
        if (npc == null) return;
        _currentNPC = npc;

        if (_portrait  != null) _portrait.text  = npc.portraitIcon ?? "🐆";
        if (_nameLabel != null) _nameLabel.text = npc.name ?? "Unknown";
        if (_roleLabel != null) _roleLabel.text = $"— {npc.role ?? "Visitor"} —";
        if (_dialogue  != null) _dialogue.text  = npc.dialogue ?? "...";
        if (_tagTop    != null) _tagTop.text    = $"NPC·{npc.id}";
        if (_pageNum   != null) _pageNum.text   = $"form NPC-{npc.id}";

        BuildStats(npc);
        BuildOptions(npc);
    }

    private void BuildStats(BookHunterData npc)
    {
        if (_statsScroll == null) return;
        _statsScroll.Clear();

        AddStat("Level", ToRoman(npc.level));
        AddStat("Reputation", new string('★', Mathf.Clamp(npc.reputation, 0, 5))
                            + new string('☆', 5 - Mathf.Clamp(npc.reputation, 0, 5)));
        AddStat("Club Member", npc.isClubMember ? "Yes" : "No");
        AddStat("Deals", npc.dealsMade.ToString());
        AddStat("Preferred", npc.preferredGenre.ToString());
        AddStat("Budget", $"$ {npc.budget}");
    }

    private void AddStat(string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("npc-cover-stat");

        row.Add(new Label(label));
        var valLabel = new Label(value);
        valLabel.AddToClassList("npc-cover-stat__val");
        row.Add(valLabel);

        _statsScroll.Add(row);
    }

    private void BuildOptions(BookHunterData npc)
    {
        if (_optionsContainer == null) return;
        _optionsContainer.Clear();

        if (npc.options == null) return;

        foreach (var opt in npc.options)
        {
            var btn = new Button(() => OnOptionClick(opt));
            btn.AddToClassList("npc-option");

            string text = opt.icon + " " + opt.text;
            if (opt.price > 0) text += $" · $ {opt.price}";
            btn.text = text;

            _optionsContainer.Add(btn);
        }

        // Always-available decline option
        var declineBtn = new Button(() => _master?.CloseModal("npc-modal"));
        declineBtn.AddToClassList("npc-option");
        declineBtn.text = "✕ Politely decline";
        _optionsContainer.Add(declineBtn);
    }

    private void OnOptionClick(DialogueOption opt)
    {
        Debug.Log($"[NPC] Action: {opt.actionId}");
        // Hook into actual NPC logic here
        // BookHunterBrain.Instance?.ExecuteAction(_currentNPC, opt.actionId);

        if (opt.price > 0)
        {
            EconomyManager.Instance?.AddMoney(opt.price);
            _master?.Toast?.Show("💰", $"Deal $ {opt.price}", ToastType.Good);
        }
        else
        {
            _master?.Toast?.Show("⭐", "Agreement reached", ToastType.Good);
        }
        _master?.CloseModal("npc-modal");
    }

    private string ToRoman(int n)
    {
        if (n < 1) return "—";
        var values = new[] { 10, 9, 5, 4, 1 };
        var symbols = new[] { "X", "IX", "V", "IV", "I" };
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < values.Length; i++)
            while (n >= values[i]) { sb.Append(symbols[i]); n -= values[i]; }
        return sb.ToString();
    }
}
