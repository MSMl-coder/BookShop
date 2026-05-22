// Assets/Scripts/Core/ShopLevelProvider.cs
// Singleton — надає поточний рівень крамниці всім системам.
// Читає престиж з ClubPrestigeCalculator і конвертує в рівень через ShopTagData.
//
// SETUP: Додай на GameManagers GameObject разом з ClubPrestigeCalculator.

using UnityEngine;

public class ShopLevelProvider : MonoBehaviour
{
    public static ShopLevelProvider Instance { get; private set; }

    [SerializeField] private ShopTagData shopTagData;

    public int CurrentShopLevel { get; private set; } = 1;
    public int MaxShopLevel => shopTagData != null ? shopTagData.levels.Length : 5;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start() => Refresh();

    /// Перерахувати рівень (виклик після зміни престижу).
    public void Refresh()
    {
        if (shopTagData == null) return;
        int prestige = ClubPrestigeCalculator.Instance?.CurrentPrestige ?? 0;
        CurrentShopLevel = shopTagData.GetLevelForPrestige(prestige).level;
    }

    /// Нормалізований рівень [0..1] для інтерполяції.
    public float LevelNormalized => Mathf.Clamp01((CurrentShopLevel - 1f) / Mathf.Max(MaxShopLevel - 1f, 1f));
}