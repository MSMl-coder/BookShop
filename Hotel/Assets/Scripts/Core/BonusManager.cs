// Assets/Scripts/Core/BonusManager.cs
// Singleton — реєструє/видаляє бонуси і повідомляє слухачів
//
// FIX: ActiveBonus, BonusShape, BonusColor визначені тут же,
//      щоб уникнути CS0246 при компіляції між папками.
//      Якщо ActiveBonus.cs вже є в проекті — видаліть його.

using UnityEngine;
using System;
using System.Collections.Generic;

// ── Data types ───────────────────────────────────────────────────

public enum BonusShape { Rect, Round, Hex, Ticket }
public enum BonusColor { Gold, Teal, Purple, Orange, Red, Blue }

[Serializable]
public class ActiveBonus
{
    public string     label;           // "ПРОДАЖІ"
    public string     value;           // "+15%"
    public string     tooltipTitle;    // "БОНУС"
    public string     tooltipBody;     // "Тип: Продажі\nЗначення: +15%"
    public BonusShape shape     = BonusShape.Rect;
    public BonusColor color     = BonusColor.Gold;
    public int        sortOrder = 0;
}

// ── Manager ──────────────────────────────────────────────────────

public class BonusManager : MonoBehaviour
{
    public static BonusManager Instance { get; private set; }

    /// Список бонусів змінився
    public event Action<IReadOnlyList<ActiveBonus>> OnBonusesChanged;

    private readonly List<ActiveBonus> _bonuses = new List<ActiveBonus>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ── Public API ───────────────────────────────────────────────

    public IReadOnlyList<ActiveBonus> GetBonuses() => _bonuses;

    public void AddBonus(ActiveBonus bonus)
    {
        _bonuses.Add(bonus);
        _bonuses.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        OnBonusesChanged?.Invoke(_bonuses);
    }

    public void RemoveBonus(string label)
    {
        int removed = _bonuses.RemoveAll(b => b.label == label);
        if (removed > 0) OnBonusesChanged?.Invoke(_bonuses);
    }

    public void ClearBonuses()
    {
        _bonuses.Clear();
        OnBonusesChanged?.Invoke(_bonuses);
    }

    // ── Shortcut helpers ─────────────────────────────────────────

    public void AddSalesBonus(float percent, int sort = 0)
    {
        AddBonus(new ActiveBonus
        {
            label        = "ПРОДАЖІ",
            value        = $"+{percent:0}%",
            tooltipTitle = "БОНУС ПРОДАЖІВ",
            tooltipBody  = $"Тип: Продажі\nЗначення: +{percent:0}%",
            shape        = BonusShape.Rect,
            color        = BonusColor.Gold,
            sortOrder    = sort
        });
    }

    public void AddClubBonus(float percent, int sort = 1)
    {
        AddBonus(new ActiveBonus
        {
            label        = "КЛУБ",
            value        = $"+{percent:0}%",
            tooltipTitle = "БОНУС КЛУБУ",
            tooltipBody  = $"Тип: Клуб\nЗначення: +{percent:0}%",
            shape        = BonusShape.Round,
            color        = BonusColor.Teal,
            sortOrder    = sort
        });
    }

    public void AddSeriesBonus(float multiplier, int sort = 2)
    {
        AddBonus(new ActiveBonus
        {
            label        = "СЕРІЯ",
            value        = $"×{multiplier:0.0}",
            tooltipTitle = "СЕРІЙНИЙ БОНУС",
            tooltipBody  = $"Тип: Повна серія\nМножник: ×{multiplier:0.0}",
            shape        = BonusShape.Hex,
            color        = BonusColor.Orange,
            sortOrder    = sort
        });
    }

    public void AddRarityBonus(float multiplier, int sort = 3)
    {
        AddBonus(new ActiveBonus
        {
            label        = "РАРІТЕТ",
            value        = $"×{multiplier:0}",
            tooltipTitle = "БОНУС РІДКОСТІ",
            tooltipBody  = $"Тип: Рідкісна книга\nМножник: ×{multiplier:0}",
            shape        = BonusShape.Ticket,
            color        = BonusColor.Purple,
            sortOrder    = sort
        });
    }
}
