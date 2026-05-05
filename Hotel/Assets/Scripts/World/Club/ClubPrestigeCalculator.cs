using UnityEngine;
using System.Collections.Generic;

// Розраховує престиж клубу — поки порожній, але готовий до наповнення
public class ClubPrestigeCalculator : MonoBehaviour
{
    public static ClubPrestigeCalculator Instance { get; private set; }

    [Header("Multipliers (налаштуй в інспекторі)")]
    [SerializeField] private float completeSeriesBonus = 1.5f;
    [SerializeField] private float firstEditionMultiplier = 3f;

    // Буде розраховуватись в Етапі 3
    public int CurrentPrestige { get; private set; } = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Заглушка — викликати після кожної зміни полиці ClubZone
    public void RecalculatePrestige(ShopZone clubZone)
    {
        if (clubZone.zoneType != ShopZoneType.BookClub) return;

        // TODO в Етапі 3:
        // 1. Зібрати всі BookInstance з clubZone
        // 2. Перевірити повні серії (completeSeriesBonus)
        // 3. Перевірити першодруки (firstEditionMultiplier)
        // 4. Підрахувати жанровий рейтинг

        CurrentPrestige = clubZone.CountBooks() * 10; // Тимчасова формула
        Debug.Log($"[Club] Prestige recalculated: {CurrentPrestige}");
    }

    // Пасивний дохід per day — викликати з GameLoopManager
    public int CalculateDailyIncome() => CurrentPrestige / 10;
}