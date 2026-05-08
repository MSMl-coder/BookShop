// Assets/Scripts/UI/HUD/ShopTagData.cs
// ScriptableObject — статичні дані для бірок HUD
// Create → Bookstore → HUD → Shop Tag Data

using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ShopTagData", menuName = "Bookstore/HUD/Shop Tag Data")]
public class ShopTagData : ScriptableObject
{
    [Header("Крамниця")]
    public string shopName       = "Книгарня";
    public string shopSubtitle   = "«Старий Архів»";
    public string shopTicketNo   = "UA-1987";

    [Header("Рівні крамниці")]
    public ShopLevelEntry[] levels = new[]
    {
        new ShopLevelEntry { level = 1, rankName = "Новачок",   prestigeRequired = 0    },
        new ShopLevelEntry { level = 2, rankName = "Продавець", prestigeRequired = 200  },
        new ShopLevelEntry { level = 3, rankName = "Букініст",  prestigeRequired = 600  },
        new ShopLevelEntry { level = 4, rankName = "Антиквар",  prestigeRequired = 1400 },
        new ShopLevelEntry { level = 5, rankName = "Майстер",   prestigeRequired = 3000 },
    };

    [Header("Клуб")]
    public string clubName         = "Книжковий Клуб";
    public int    clubMaxStars     = 5;

    public ShopLevelEntry GetLevelForPrestige(int prestige)
    {
        ShopLevelEntry result = levels[0];
        foreach (var l in levels)
            if (prestige >= l.prestigeRequired) result = l;
        return result;
    }

    public ShopLevelEntry GetNextLevel(int currentLevel)
    {
        foreach (var l in levels)
            if (l.level == currentLevel + 1) return l;
        return null;
    }
}

[System.Serializable]
public class ShopLevelEntry
{
    public int    level;
    public string rankName;
    public int    prestigeRequired;
}
