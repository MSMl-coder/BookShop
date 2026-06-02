// Assets/Scripts/World/NPC/NPCStatConfig.cs
// ScriptableObject — всі константи швидкостей зміни показників NPC.
// Один екземпляр на весь проєкт.
// Create: Assets → Bookstore → NPC Stat Config

using UnityEngine;

[CreateAssetMenu(fileName = "NPCStatConfig", menuName = "Bookstore/NPC Stat Config")]
public class NPCStatConfig : ScriptableObject
{
    // ─────────────────────────────────────────────
    [Header("Mood — одиниць/сек")]
    // ─────────────────────────────────────────────

    [Tooltip("Базова швидкість спаду настрою — завжди активна незалежно від декору.")]
    [Range(0f, 10f)]
    public float moodDecayRate = 2f;

    // ─────────────────────────────────────────────
    [Header("Comfort — одиниць/сек")]
    // ─────────────────────────────────────────────

    [Tooltip("Швидкість спаду комфорту коли NPC НЕ сидить.")]
    [Range(0f, 5f)]
    public float comfortDecayRate = 1.5f;

    // ─────────────────────────────────────────────
    [Header("Patience — одиниць/сек")]
    // ─────────────────────────────────────────────

    [Tooltip("Базова швидкість спаду терпіння. Сповільнюється Comfort та Mood.")]
    [Range(0f, 5f)]
    public float patienceDecayRate = 1f;

    [Tooltip("Швидкість відновлення Patience поки NPC сидить на меблі " +
             "(додається до patienceRestoreBonus пропа).")]
    [Range(0f, 3f)]
    public float patienceRestoreWhileResting = 0.8f;

    [Tooltip("Максимальний дільник для Patience decay від повного Comfort. " +
             "2.0 = вдвічі повільніше при Comfort=+100.")]
    [Range(1f, 4f)]
    public float comfortPatienceSlowdown = 2f;

    [Tooltip("Максимальний дільник для Patience decay від повного Mood.")]
    [Range(1f, 3f)]
    public float moodPatienceSlowdown = 1.5f;

    // ─────────────────────────────────────────────
    [Header("Пороги прийняття рішень")]
    // ─────────────────────────────────────────────

    [Tooltip("Patience нижче якого NPC шукає місце для сидіння (якщо є).")]
    [Range(-50f, 80f)]
    public float restingPatienceThreshold = 30f;

    [Tooltip("Comfort вище якого NPC отримує +1 зайву полицю для огляду.")]
    [Range(0f, 100f)]
    public float extraShelfComfortThreshold = 50f;

    [Tooltip("Mood вище якого NPC може зробити імпульсну покупку " +
             "(поза жанром або вище бюджету).")]
    [Range(30f, 100f)]
    public float impulseBuyMoodThreshold = 70f;

    [Tooltip("Множник MaxBudget при імпульсній покупці.")]
    [Range(1f, 2f)]
    public float impulseBudgetMultiplier = 1.3f;

    // ─────────────────────────────────────────────
    [Header("ShopAtmosphereService — декор")]
    // ─────────────────────────────────────────────

    [Tooltip("Максимальний сумарний moodRiseRate незалежно від к-ті декорацій. " +
             "Невидимий кап — захист від стекінгу.")]
    [Range(4f, 20f)]
    public float moodRiseCap = 8f;

    [Tooltip("Множник на сумарний moodForce якщо розміщено 2+ різних DecorCategory.")]
    [Range(1f, 2f)]
    public float diversityBonusMultiplier = 1.2f;

    [Tooltip("Мінімальна кількість різних DecorCategory для diversity bonus.")]
    [Range(2, 5)]
    public int diversityMinCategories = 2;
}