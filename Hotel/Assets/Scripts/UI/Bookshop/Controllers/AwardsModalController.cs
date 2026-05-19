// ═══════════════════════════════════════════════════════════
// AwardsModalController.cs — Awards/Achievements modal
// Path: Assets/Scripts/UI/Bookshop/Controllers/AwardsModalController.cs
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

[System.Serializable]
public class AchievementData
{
    public string id;
    public string name;
    public string description;
    public string icon;
    public int current;
    public int target;
    public string unit;
    public bool isLocked;
    public int points;
}

public class AwardsModalController : MonoBehaviour
{
    private VisualElement _root;
    private BookshopUIController _master;

    private VisualElement _grid;
    private Label _earned;
    private Label _inProgress;
    private Label _locked;
    private Label _points;
    private Label _total;

    [Header("Achievements (assign in Inspector or feed via SetAchievements)")]
    [SerializeField] private List<AchievementData> achievements = new()
    {
        new() { id="bookworm",    name="Bookworm",         description="Sell 100 books",        icon="📚", current=67,  target=100, unit="ITEMS",     points=50 },
        new() { id="starclub",    name="Star Club",        description="5 Club Stars",          icon="⭐", current=2,   target=5,   unit="STARS",     points=80 },
        new() { id="wallet",      name="Golden Wallet",    description="Accumulate $5,000",     icon="💰", current=1240,target=5000,unit="$",         points=60 },
        new() { id="collector",   name="Collector",        description="Gather full author series",icon="🔒",current=0,target=1,   unit="LOCKED",   points=100, isLocked=true },
        new() { id="panthers",    name="Friend of Panthers",description="5 visits from Panthers",icon="🐾",current=2,   target=5,   unit="VISITS",    points=40 },
        new() { id="legend",      name="Book Legend",      description="Max shop level",        icon="🔒", current=0,   target=1,   unit="LOCKED",    points=200, isLocked=true }
    };

    public void Initialize(VisualElement root, BookshopUIController master)
    {
        _root = root;
        _master = master;

        _grid       = root.Q<VisualElement>("AchGridContainer");
        _earned     = root.Q<Label>("AchEarned");
        _inProgress = root.Q<Label>("AchInProgress");
        _locked     = root.Q<Label>("AchLocked");
        _points     = root.Q<Label>("AchPoints");
        _total      = root.Q<Label>("AchTotal");

        Refresh();
    }

    public void SetAchievements(List<AchievementData> data)
    {
        achievements = data;
        Refresh();
    }

    public void Refresh()
    {
        if (_grid == null) return;
        _grid.Clear();

        int earned = 0, inProg = 0, lockedCount = 0, totalPoints = 0;

        foreach (var a in achievements)
        {
            _grid.Add(MakeItem(a));

            bool isEarned = !a.isLocked && a.current >= a.target;
            if (a.isLocked) lockedCount++;
            else if (isEarned) earned++;
            else inProg++;

            if (isEarned) totalPoints += a.points;
        }

        if (_earned     != null) _earned.text     = $"{earned}/{achievements.Count}";
        if (_inProgress != null) _inProgress.text = inProg.ToString();
        if (_locked     != null) _locked.text     = lockedCount.ToString();
        if (_points     != null) _points.text     = totalPoints.ToString();
        if (_total      != null) _total.text      = $"total: {achievements.Count}";
    }

    private VisualElement MakeItem(AchievementData a)
    {
        var item = new VisualElement();
        item.AddToClassList("ach-item");

        // Plate
        var plate = new VisualElement();
        plate.AddToClassList("ach-plate");
        if (a.isLocked) plate.AddToClassList("locked");
        plate.Add(new Label(a.icon));
        item.Add(plate);

        // Info
        var info = new VisualElement();
        info.AddToClassList("ach-info");

        var nameLabel = new Label(a.name);
        nameLabel.AddToClassList("ach-name");
        if (a.isLocked) nameLabel.style.opacity = 0.5f;
        info.Add(nameLabel);

        var descLabel = new Label(a.description);
        descLabel.AddToClassList("ach-desc");
        if (a.isLocked) descLabel.style.opacity = 0.5f;
        info.Add(descLabel);

        // Progress track
        var track = new VisualElement();
        track.AddToClassList("ach-track");
        var fill = new VisualElement();
        fill.AddToClassList("ach-fill");
        float pct = a.target > 0 ? Mathf.Clamp01((float)a.current / a.target) : 0f;
        fill.style.width = Length.Percent(pct * 100f);
        track.Add(fill);
        info.Add(track);

        var countLabel = new Label(a.isLocked
            ? "LOCKED"
            : $"{a.current} / {a.target} {a.unit}");
        countLabel.AddToClassList("ach-count");
        info.Add(countLabel);

        item.Add(info);
        return item;
    }
}
