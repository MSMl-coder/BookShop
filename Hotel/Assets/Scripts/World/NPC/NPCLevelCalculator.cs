// Assets/Scripts/World/NPC/NPCLevelCalculator.cs
// Комбінує показники NPCPersonality в рівень бота від 1 до 10.
// Кольори рівнів задаються через NPCLevelBadge в Inspector (не тут).
// Цей клас тільки рахує рівень і надає назву.

using UnityEngine;

public static class NPCLevelCalculator
{
    private const float MaxBudgetReference = 200f;

    /// Повертає рівень від 1 до 10 на основі особистості NPC.
    public static int Calculate(NPCPersonality personality)
    {
        if (personality == null) return 1;

        float score = 0f;

        // Бюджет (0–3 бали)
        float budgetNorm = Mathf.Clamp01(personality.MaxBudget / MaxBudgetReference);
        score += budgetNorm * 3f;

        // К-ть книг (0–2 бали): 1 книга = 0, 5+ = 2
        float booksNorm = Mathf.Clamp01((personality.WantsToBuy - 1f) / 4f);
        score += booksNorm * 2f;

        // Рарність (0–2.5 бали)
        int   rarityCount = System.Enum.GetValues(typeof(BookRarity)).Length;
        float rarityMid   = ((int)personality.MinRarity + (int)personality.MaxRarity) * 0.5f;
        float rarityNorm  = Mathf.Clamp01(rarityMid / (rarityCount - 1));
        score += rarityNorm * 2.5f;

        // Buy Chance (0–1.5 бали)
        score += personality.BuyChance * 1.5f;

        // Настрій (0–1 бал)
        score += personality.CurrentMood switch
        {
            NPCPersonality.Mood.Impulsive => 1.0f,
            NPCPersonality.Mood.Relaxed   => 0.7f,
            NPCPersonality.Mood.Rushed    => 0.4f,
            NPCPersonality.Mood.Picky     => 0.1f,
            _                             => 0.5f
        };

        // score [0..10] → рівень [1..10]
        return Mathf.Clamp(Mathf.RoundToInt(score), 1, 10);
    }

    /// Назва рівня для debug або tooltip.
    public static string GetLevelTitle(int level) => level switch
    {
        1  => "Випадковий перехожий",
        2  => "Допитливий",
        3  => "Читач-початківець",
        4  => "Постійний читач",
        5  => "Книжковий фанат",
        6  => "Ерудит",
        7  => "Колекціонер",
        8  => "Меценат",
        9  => "Знавець",
        10 => "VIP-покупець",
        _  => "Невідомо"
    };
}