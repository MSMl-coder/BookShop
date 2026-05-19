// ═══════════════════════════════════════════════════════════
// BuffsController.cs — Dynamic buffs display
// Path: Assets/Scripts/UI/Bookshop/Controllers/BuffsController.cs
//
// Populates BuffsArea with small overlapping cards.
// Hover expands the card and shows full name + tooltip.
//
// INTEGRATION:
//   Bind to BonusSystem.Instance (if exists) for live updates.
//   Falls back to manual SetBuffs(List<BuffData>) calls.
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class BuffsController : MonoBehaviour
{
    [System.Serializable]
    public struct BuffData
    {
        public string id;
        public string shortName;     // e.g. "ZEAL"
        public string fullName;      // e.g. "Book Zeal — Sales +20%"
        public string icon;          // emoji or sprite-style char
        public string value;         // e.g. "+20%"
    }

    private VisualElement _buffsArea;
    private BookshopUIController _master;
    private readonly List<BuffData> _currentBuffs = new();

    public void Initialize(VisualElement root, BookshopUIController master)
    {
        _buffsArea = root.Q<VisualElement>("BuffsArea");
        _master = master;

        // Subscribe to BonusManager if available
        // (assumes BonusManager.Instance.OnBuffsChanged event with List<BuffData> arg)
        if (BonusManager.Instance != null)
        {
       //     BonusManager.Instance.OnBonusesChanged += RefreshFromBonusSystem;
            RefreshFromBonusSystem();
        }
    }

    private void OnDisable()
    {
        //if (BonusManager.Instance != null)
         //   BonusManager.Instance.OnBonusChanged -= RefreshFromBonusSystem;
    }

    private void RefreshFromBonusSystem()
    {
        // Adapter — pull from BonusManager and convert to BuffData
        // Adjust this based on actual BonusManager API
        var buffs = new List<BuffData>();
      /*  var active = BonusManager.Instance?.GetActiveBonuses();
        if (active != null)
        {
            foreach (var b in active)
            {
                buffs.Add(new BuffData
                {
                    id = b.bonusID,
                    shortName = (b.bonusName ?? "").ToUpper().Substring(0, System.Math.Min(7, (b.bonusName ?? "").Length)),
                    fullName = b.bonusName + " · " + b.description,
                    icon = b.iconChar,
                    value = b.effectText
                });
            }
        }
       */ SetBuffs(buffs);
    }

    public void SetBuffs(List<BuffData> buffs)
    {
        if (_buffsArea == null) return;
        _buffsArea.Clear();
        _currentBuffs.Clear();
        _currentBuffs.AddRange(buffs);

        if (_master?.BuffCardTemplate == null)
        {
            Debug.LogWarning("[BuffsController] BuffCardTemplate not assigned");
            return;
        }

        for (int i = 0; i < buffs.Count; i++)
        {
            var b = buffs[i];
            var card = _master.BuffCardTemplate.Instantiate().ElementAt(0);
            if (i == 0) card.AddToClassList("first");

            // alternate tilt
            var cord = card.Q<VisualElement>(className: "tag-cord");
            if (cord != null)
            {
                cord.AddToClassList(i % 2 == 0 ? "tilt-left" : "tilt-right");
            }

            var iconLabel  = card.Q<Label>("BuffIcon");
            var nameLabel  = card.Q<Label>("BuffName");
            var valueLabel = card.Q<Label>("BuffValue");
            var tipLabel   = card.Q<Label>("BuffTip");

            if (iconLabel  != null) iconLabel.text  = b.icon;
            if (nameLabel  != null) nameLabel.text  = b.shortName;
            if (valueLabel != null) valueLabel.text = b.value;
            if (tipLabel   != null) tipLabel.text   = b.fullName;

            _buffsArea.Add(card);
        }
    }
}
