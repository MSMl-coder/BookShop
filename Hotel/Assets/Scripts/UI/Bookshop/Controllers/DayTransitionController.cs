// Assets/Scripts/UI/Bookshop/Controllers/DayTransitionController.cs
// НОВИЙ (Фаза 2):
//   Перехідний екран між днями — затемнення + "ДЕНЬ N" на 2 секунди.
//   Спрацьовує при LootPhase → Preparation (OnNewDayStarted).
//
//   UXML: потрібен елемент "DayTransitionOverlay" в BookshopMainUI.uxml
//   (дивись патч нижче).
//
//   UNITY SETUP:
//   1. Додати компонент DayTransitionController на BookshopUI GameObject
//   2. Підключити в BookshopUIController (дивись патч)

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class DayTransitionController : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Тривалість fade-in (секунди)")]
    [SerializeField] private float fadeInDuration  = 0.4f;

    [Tooltip("Скільки секунд тримати текст на екрані")]
    [SerializeField] private float holdDuration    = 1.6f;

    [Tooltip("Тривалість fade-out (секунди)")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    // ── UI ───────────────────────────────────────────────────────
    private VisualElement _overlay;
    private Label         _dayLabel;

    private Coroutine _transitionCoroutine;

    // ── Initialize ───────────────────────────────────────────────

    public void Initialize(VisualElement root)
    {
        _overlay  = root.Q<VisualElement>("DayTransitionOverlay");
        _dayLabel = root.Q<Label>("DayTransitionLabel");

        if (_overlay == null)
        {
            Debug.LogWarning("[DayTransition] DayTransitionOverlay не знайдено в UXML.");
            return;
        }

        // Приховуємо на старті
        _overlay.style.display = DisplayStyle.None;
        _overlay.style.opacity = 0f;

        // Підписуємось на початок нового дня
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnNewDayStarted += OnNewDayStarted;
    }

    private void OnDisable()
    {
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.OnNewDayStarted -= OnNewDayStarted;
    }

    // ── Handler ──────────────────────────────────────────────────

    private void OnNewDayStarted(int day)
    {
        if (_overlay == null) return;

        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);

        _transitionCoroutine = StartCoroutine(PlayTransition(day));
    }

    // ── Transition ───────────────────────────────────────────────

    private IEnumerator PlayTransition(int day)
    {
        // Оновлюємо текст
        if (_dayLabel != null)
            _dayLabel.text = $"ДЕНЬ {day}";

        // Показуємо overlay
        _overlay.style.display = DisplayStyle.Flex;

        // Fade IN
        yield return StartCoroutine(Fade(0f, 1f, fadeInDuration));

        // Hold
        yield return new WaitForSeconds(holdDuration);

        // Fade OUT
        yield return StartCoroutine(Fade(1f, 0f, fadeOutDuration));

        // Ховаємо
        _overlay.style.display = DisplayStyle.None;
        _transitionCoroutine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _overlay.style.opacity = Mathf.Lerp(from, to, t);
            yield return null;
        }
        _overlay.style.opacity = to;
    }

    // ── Public API (для тестування) ──────────────────────────────

    public void TestTransition(int day = 1)
    {
        if (_overlay == null) { Debug.LogWarning("[DayTransition] Overlay не ініціалізовано."); return; }
        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(PlayTransition(day));
    }
}