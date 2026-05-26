// Assets/Scripts/Core/NotificationSystem.cs
// НОВИЙ (Фаза 2):
//   Підписується на ключові події і показує toast через ToastController.
//   Додати компонент на BookshopUI GameObject.
//
//   Підписки:
//     EconomyManager.OnRentApplied     → оренда / борг
//     EconomyManager.OnBankruptcy      → банкрутство
//     EconomyManager.OnMoneyChanged    → продаж книги (тільки під час WorkDay)
//     GameLoopManager.OnStateChanged   → зміна фази
//     GameLoopManager.OnNewDayStarted  → новий день
//
//   Статичні методи для виклику ззовні:
//     NotificationSystem.Notify(icon, msg, type)   → простий toast
//     NotificationSystem.NotifyBookPlaced(title)   → розміщення книги
//     NotificationSystem.NotifyError(msg)          → помилка (warn)
//     NotificationSystem.NotifyBookSold(title, price) → продаж

using UnityEngine;

public class NotificationSystem : MonoBehaviour
{
    public static NotificationSystem Instance { get; private set; }

    // Щоб не спамити тостами при кожній зміні грошей —
    // трекаємо чи зміна від продажу (тільки під час WorkDay і тільки ріст)
    private int _lastMoneySnapshot = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    private void OnEnable()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnRentApplied  += OnRentApplied;
            EconomyManager.Instance.OnBankruptcy   += OnBankruptcy;
        }

        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged  += OnStateChanged;
            GameLoopManager.Instance.OnNewDayStarted += OnNewDay;
        }
    }

    private void OnDisable()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnRentApplied  -= OnRentApplied;
            EconomyManager.Instance.OnBankruptcy   -= OnBankruptcy;
        }

        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged  -= OnStateChanged;
            GameLoopManager.Instance.OnNewDayStarted -= OnNewDay;
        }
    }

    // ── Handlers ─────────────────────────────────────────────────

    private void OnRentApplied(DailyRentResult result)
    {
        if (result.IsBankrupt) return; // OnBankruptcy сам покаже

        if (result.IsInDebt)
        {
            // Борг
            int daysLeft = result.DebtAllowed - result.DebtDay;
            Toast("⚠️",
                $"Оренда -{result.RentAmount}₴  |  БОРГ {result.DebtDay}/{result.DebtAllowed}" +
                (daysLeft > 0 ? $"  |  Залишилось {daysLeft} дн." : ""),
                ToastType.Warn);
        }
        else
        {
            // Нормальне списання
            Toast("💸", $"Оренда -{result.RentAmount}₴  |  Баланс: {result.BalanceAfter}₴", ToastType.Info);
        }
    }

    private void OnBankruptcy()
    {
        Toast("💀", "БАНКРУТСТВО! Гра завершена.", ToastType.Warn);
    }

    private void OnStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.WorkDay:
                Toast("🏪", "Магазин відкрито!", ToastType.Good);
                _lastMoneySnapshot = EconomyManager.Instance?.Money ?? 0;
                break;
            case GameState.DayStats:
                Toast("📋", "День завершено. Підраховуємо результати...", ToastType.Info);
                break;
            case GameState.LootPhase:
                Toast("🎁", "Час обирати нагороди!", ToastType.Good);
                break;
            case GameState.Preparation:
                Toast("🌅", "Новий день починається. Готуйте магазин!", ToastType.Info);
                break;
        }
    }

    private void OnNewDay(int day)
    {
        // OnStateChanged вже показує "Новий день" — тут не дублюємо
    }

    // ── Статичні хелпери (викликати з будь-якого місця) ──────────

    /// Книга розміщена на полиці
    public static void NotifyBookPlaced(string title)
    {
        if (Instance == null) return;
        Instance.Toast("📚", $"«{title}» розміщено", ToastType.Info);
    }

    /// Книга продана
    public static void NotifyBookSold(string title, int price)
    {
        if (Instance == null) return;
        Instance.Toast("📖", $"«{title}» продано  +{price}₴", ToastType.Good);
    }

    /// Помилка (не вистачає коштів, неможливо розмістити тощо)
    public static void NotifyError(string message)
    {
        if (Instance == null) return;
        Instance.Toast("⛔", message, ToastType.Warn);
    }

    /// Загальне повідомлення
    public static void Notify(string icon, string message, ToastType type = ToastType.Info)
    {
        if (Instance == null) return;
        Instance.Toast(icon, message, type);
    }

    // ── Internal ─────────────────────────────────────────────────

    private void Toast(string icon, string message, ToastType type)
    {
        if (ToastController.Instance == null)
        {
            Debug.LogWarning($"[Notifications] ToastController відсутній: {message}");
            return;
        }
        ToastController.Instance.Show(icon, message, type);
    }
}