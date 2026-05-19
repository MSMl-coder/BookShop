// ═══════════════════════════════════════════════════════════
// BonusSystemUIAdapter.cs — Adapter for BuffsController
// Path: Assets/Scripts/UI/Bookshop/Adapters/BonusSystemUIAdapter.cs
//
// IMPORTANT: If your BonusSystem class doesn't have the OnBuffsChanged
// event or GetActiveBonuses() method, you need to either:
//   A) Add them to your BonusSystem class, OR
//   B) Use this adapter to provide them
//
// This file shows what BonusSystem needs to expose for BuffsController:
// ═══════════════════════════════════════════════════════════

/*
ADD TO YOUR EXISTING BonusSystem.cs:

using System;
using System.Collections.Generic;

public partial class BonusSystem : MonoBehaviour
{
    // ── Required by BuffsController ──
    public event Action OnBuffsChanged;

    /// Returns currently active bonuses for UI display.
    /// Adjust the BonusInfo struct to match your internal data.
    public List<BonusInfo> GetActiveBonuses()
    {
        // Map your internal active bonuses to BonusInfo
        var list = new List<BonusInfo>();
        // foreach (var b in _activeBonuses) list.Add(new BonusInfo {...});
        return list;
    }

    // Helper struct for UI
    [System.Serializable]
    public struct BonusInfo
    {
        public string bonusID;       // unique id
        public string bonusName;     // "Book Zeal"
        public string description;   // "Sales increased by 20%"
        public string iconChar;      // "📚"
        public string effectText;    // "+20%"
    }

    // Trigger UI refresh whenever bonuses change:
    private void NotifyBuffsChanged() => OnBuffsChanged?.Invoke();
}
*/

// If you don't want to modify BonusSystem, attach this adapter to a GameObject
// and it will poll for bonuses every 0.5s.

using UnityEngine;
using System.Collections.Generic;
using System;

public class BonusSystemUIAdapter : MonoBehaviour
{
    public static BonusSystemUIAdapter Instance { get; private set; }

    public event Action OnBuffsChanged;

    [System.Serializable]
    public struct BonusInfo
    {
        public string bonusID;
        public string bonusName;
        public string description;
        public string iconChar;
        public string effectText;
    }

    private List<BonusInfo> _currentBuffs = new();
    private float _lastPoll;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        // Stub buffs for testing — replace with real data
        _currentBuffs.Add(new BonusInfo { bonusID="zeal", bonusName="Book Zeal",       description="Sales boost",     iconChar="📚", effectText="+20%" });
        _currentBuffs.Add(new BonusInfo { bonusID="cozy", bonusName="Cozy Atmosphere", description="Mood +8%",        iconChar="☕", effectText="+8%"  });
        _currentBuffs.Add(new BonusInfo { bonusID="star", bonusName="Star Reputation", description="Clients +15%",    iconChar="⭐", effectText="+15%" });
        _currentBuffs.Add(new BonusInfo { bonusID="green",bonusName="Green Corner",    description="Health +5%",      iconChar="🌿", effectText="+5%"  });
        _currentBuffs.Add(new BonusInfo { bonusID="music",bonusName="Music",           description="Happiness +10%",  iconChar="🎵", effectText="+10%" });
        _currentBuffs.Add(new BonusInfo { bonusID="spring",bonusName="Spring Mood",    description="All buffs ×1.1",  iconChar="🦋", effectText="×1.1" });
    }

    private void Start()
    {
        OnBuffsChanged?.Invoke();
    }

    public List<BonusInfo> GetActiveBonuses() => new(_currentBuffs);

    public void SetBuffs(List<BonusInfo> buffs)
    {
        _currentBuffs = buffs ?? new();
        OnBuffsChanged?.Invoke();
    }
}
