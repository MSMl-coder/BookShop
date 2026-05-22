// NPCWorldUI.cs
// Повний рефакторинг — замінює стару версію і NPCInteractionUI.
//
// ЛОГІКА ХМАРИНКИ:
//   Entering         → вітання (іконка + текст) → автозгортання через greetingDuration
//   Inspecting       → 4 анімовані крапки поки стоїть біля полиці
//   WaitingForPlayer → жанровий чіп + текст бажання + бюджет + drop-zone для книги
//   Buying           → текст подяки (acceptMessageDuration) → collapse до mini-badge
//   Leaving          → миттєве приховання всього
//
// SETUP У PREFAB (World Space Canvas):
//   NPC_UI_Canvas (Canvas — World Space)
//   └── UI_Root  ← призначити у поле uiRoot (billboard ціль)
//       ├── TimerRing       ← timerFillImage  (Image, Filled, Radial360)
//       ├── BubbleFull      ← bubbleFull
//       │   ├── BubbleBg        ← bubbleBackground (Image, 9-sliced)
//       │   ├── Tail            ← bubbleTail (Image)
//       │   ├── ContentGreeting ← contentGreeting
//       │   │   ├── GreetIcon   ← greetIcon (Image)
//       │   │   └── GreetText   ← greetText (TMP)
//       │   ├── ContentDots     ← contentDots
//       │   │   └── Dot1..Dot4  ← searchDots[0..3] (Image × 4)
//       │   ├── ContentWant     ← contentWant
//       │   │   ├── GenreChipBg ← genreChipImage (Image)
//       │   │   ├── GenreIcon   ← genreIconImage (Image)
//       │   │   ├── GenreText   ← genreNameText (TMP)
//       │   │   ├── WantText    ← wantText (TMP)
//       │   │   ├── BudgetText  ← budgetText (TMP)
//       │   │   └── BookDropZone← bookDropZone (Image + BookDropZoneUI компонент)
//       │   └── ContentAccept   ← contentAccept
//       │       ├── AcceptIcon  ← acceptIcon (Image)
//       │       └── AcceptText  ← acceptText (TMP)
//       └── BubbleMini      ← bubbleMini
//           ├── MiniBg          (Image, pill)
//           └── MiniText        ← miniText (TMP)

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NPCBrain))]
public class NPCWorldUI : MonoBehaviour
{
    // ── Inspector: структура ────────────────────────────────────

    [Header("Root")]
    [SerializeField] private GameObject uiRoot;          // весь UI — billboard ціль

    [Header("Timer Ring")]
    [SerializeField] private Image  timerFillImage;      // Image (Filled, Radial360)
    [SerializeField] private Color  timeFullColor  = new Color(0.30f, 0.69f, 0.31f);
    [SerializeField] private Color  timeHalfColor  = new Color(1.00f, 0.76f, 0.03f);
    [SerializeField] private Color  timeEmptyColor = new Color(0.90f, 0.23f, 0.21f);

    [Header("Bubble — контейнер")]
    [SerializeField] private GameObject bubbleFull;      // вся розгорнута хмаринка
    [SerializeField] private GameObject bubbleMini;      // мінімальний badge

    [Header("Bubble — контент-панелі (дочірні від BubbleFull)")]
    [SerializeField] private GameObject contentGreeting; // вітання
    [SerializeField] private GameObject contentDots;     // крапки пошуку
    [SerializeField] private GameObject contentWant;     // запит + drop-zone
    [SerializeField] private GameObject contentAccept;   // подяка

    [Header("Greeting panel")]
    [SerializeField] private Image            greetIcon;  // іконка вітання
    [SerializeField] private TextMeshProUGUI  greetText;  // текст вітання
    [SerializeField] private Sprite           greetSprite;// спрайт іконки (assign in Inspector)

    [Header("Dots panel (4 крапки)")]
    [SerializeField] private Image[] searchDots = new Image[4]; // Dot1..Dot4

    [Header("Want panel")]
    [SerializeField] private Image            genreChipImage;  // фон чіпа
    [SerializeField] private Image            genreIconImage;  // іконка жанру
    [SerializeField] private TextMeshProUGUI  genreNameText;   // назва жанру
    [SerializeField] private TextMeshProUGUI  wantText;        // текст бажання
    [SerializeField] private TextMeshProUGUI  budgetText;      // "до X грн"
    [SerializeField] private RectTransform    bookDropZone;    // drop-зона для книги

    [Header("Accept panel")]
    [SerializeField] private Image            acceptIcon;      // іконка книги
    [SerializeField] private TextMeshProUGUI  acceptText;

    [Header("Mini badge")]
    [SerializeField] private TextMeshProUGUI  miniText;

    [Header("Genre Icons — перетягнути 7 спрайтів")]
    [SerializeField] private GenreIconEntry[] genreIcons;

    [Header("Animation Timings")]
    [SerializeField] private float bubbleScaleInDuration  = 0.25f;
    [SerializeField] private float bubbleScaleOutDuration = 0.20f;
    [SerializeField] private float dotsPulsePeriod        = 0.35f; // затримка між крапками
    [SerializeField] private float shakeIntensity         = 8f;
    [SerializeField] private float shakeDuration          = 0.45f;

    [Header("Want Panel Colors")]
    [SerializeField] private Color chipNormalColor = new Color(0.08f, 0.40f, 0.75f, 1f);
    [SerializeField] private Color chipRejectColor = new Color(0.76f, 0.18f, 0.18f, 1f);

    // ── Серіалізований запис для жанрових іконок ───────────────

    [System.Serializable]
    public struct GenreIconEntry
    {
        public BookGenre genre;
        public Sprite    icon;
    }

    // ── Статичні дані ───────────────────────────────────────────

    private static readonly Dictionary<BookGenre, string> GenreUkrainianNames =
        new Dictionary<BookGenre, string>
    {
        { BookGenre.Fantasy,   "Фентезі"           },
        { BookGenre.Horror,    "Жахи"               },
        { BookGenre.Mystery,   "Детектив"           },
        { BookGenre.Classic,   "Класика"            },
        { BookGenre.SciFi,     "Фантастика"         },
        { BookGenre.Biography, "Біографія"          },
        { BookGenre.Academic,  "Наукова"            },
    };

    private static readonly Dictionary<BookGenre, string> GenreWantTexts =
        new Dictionary<BookGenre, string>
    {
        { BookGenre.Fantasy,   "Шукаю щось чарівне..."   },
        { BookGenre.Horror,    "Маєш щось моторошне?"    },
        { BookGenre.Mystery,   "Хочу загадку..."         },
        { BookGenre.Classic,   "Потрібна класика."       },
        { BookGenre.SciFi,     "Цікавить майбутнє..."    },
        { BookGenre.Biography, "Шукаю чиюсь історію."   },
        { BookGenre.Academic,  "Є наукова праця?"        },
    };

    // ── Private state ───────────────────────────────────────────

    private NPCBrain  _brain;
    private Camera    _mainCamera;
    private bool      _isCollapsed;
    private Coroutine _activeCoroutine;
    private Coroutine _dotsCoroutine;
    private int       _offerAttemptsLeft;

    // ── Unity Lifecycle ─────────────────────────────────────────

    private void Awake()
    {
        _brain      = GetComponent<NPCBrain>();
        _mainCamera = Camera.main;

        // Ховаємо все в Awake (до OnEnable/Start) щоб уникнути
        // конфлікту: Start() не перезаписує стан який вже поставив HandleStateChanged
        SetPanelActive(bubbleFull, false);
        SetPanelActive(bubbleMini, false);
        SetAllContentPanels(false);
        if (timerFillImage != null) timerFillImage.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (_brain != null) _brain.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (_brain != null) _brain.OnStateChanged -= HandleStateChanged;
    }

    private void LateUpdate()
    {
        UpdateTimer();
        BillboardUI();
    }

    // ── Public API (викликається з InteractionRouter при кліку на NPC) ──

    /// Перемикає хмаринку між розгорнутою і collapsed версією.
    public void ToggleBubble()
    {
        if (!bubbleFull && !bubbleMini) return;

        _isCollapsed = !_isCollapsed;
        if (_isCollapsed) CollapseToMiniBadge();
        else              ExpandFromBadge();
    }

    /// Викликається BookDropZoneUI коли гравець кидає книгу на drop-zone або на NPC.
    /// Передає книгу до NPCBrain для перевірки.
    public void OnBookDropped(BookTemplate offeredBook)
    {
        _brain?.ReceiveBookOffer(offeredBook);
    }

    /// Публічний метод для показу реакції відмови (викликається NPCBrain).
    public void ShowRejectionFeedback(string reason = "")
    {
        StopActive();
        _activeCoroutine = StartCoroutine(PlayRejectAnimation(reason));
    }

    // ── State Handler ───────────────────────────────────────────

    private void HandleStateChanged(NPCState state)
    {
        StopActive();

        switch (state)
        {
            case NPCState.Entering:
                // Таймер ховаємо — він ще не має сенсу при вході
                SetTimerVisible(false);
                _activeCoroutine = StartCoroutine(ShowGreeting());
                break;

            case NPCState.Browsing:
                // Між полицями — хмаринка прихована, таймер видимий
                HideAllBubbles();
                SetTimerVisible(true);
                break;

            case NPCState.Inspecting:
                // Підійшов до полиці — крапки пошуку
                SetTimerVisible(true);
                _activeCoroutine = StartCoroutine(ShowDotsPanel());
                break;

            case NPCState.WaitingForPlayer:
                // Не знайшов — показуємо запит гравцю
                SetTimerVisible(true);
                _offerAttemptsLeft = _brain.Data != null ? _brain.Data.maxPlayerOfferAttempts : 3;
                _activeCoroutine = StartCoroutine(ShowWantPanel());
                break;

            case NPCState.Buying:
                // Книга прийнята — текст подяки → collapse
                SetTimerVisible(false);
                _activeCoroutine = StartCoroutine(ShowAcceptAndCollapse());
                break;

            case NPCState.Leaving:
                // Виходить — всі UI елементи зникають
                SetTimerVisible(false);
                _activeCoroutine = StartCoroutine(HideBubbleAnimated());
                break;
        }
    }

    // ── Coroutines — сцени ─────────────────────────────────────

    // 1. ВІТАННЯ
    private IEnumerator ShowGreeting()
    {
        // Вибираємо рандомний текст вітання
        string message = PickRandom(_brain.Data?.greetingMessages) ?? "Привіт!";

        // Наповнюємо панель
        if (greetText  != null) greetText.text   = message;
        if (greetIcon  != null && greetSprite != null) greetIcon.sprite = greetSprite;

        SetAllContentPanels(false);
        SetPanelActive(contentGreeting, true);

        yield return StartCoroutine(ShowBubbleAnimated());

        // Чекаємо greetingDuration секунд
        float duration = _brain.Data != null ? _brain.Data.greetingDuration : 2.5f;
        yield return new WaitForSeconds(duration);

        // Тільки якщо стан ще Entering/Browsing — ховаємо (міг змінитись)
        if (_brain.CurrentState == NPCState.Entering || _brain.CurrentState == NPCState.Browsing)
            yield return StartCoroutine(HideBubbleAnimated());
    }

    // 2. КРАПКИ ПОШУКУ
    private IEnumerator ShowDotsPanel()
    {
        SetAllContentPanels(false);
        SetPanelActive(contentDots, true);

        // Запускаємо анімацію крапок
        _dotsCoroutine = StartCoroutine(AnimateDots());

        yield return StartCoroutine(ShowBubbleAnimated());
        // Хмаринка лишається активною до зміни стану — не ховаємо тут
    }

    // 3. ЗАПИТ + DROP-ZONE
    private IEnumerator ShowWantPanel()
    {
        BookGenre genre  = _brain.DesiredGenre;
        float     budget = _brain.Data?.maxBudget ?? 0f;

        // Заповнюємо жанровий чіп
        if (genreChipImage != null) genreChipImage.color = chipNormalColor;

        if (genreNameText != null)
            genreNameText.text = GenreUkrainianNames.TryGetValue(genre, out string n) ? n : genre.ToString();

        if (genreIconImage != null)
        {
            genreIconImage.sprite  = FindGenreSprite(genre);
            genreIconImage.enabled = genreIconImage.sprite != null;
        }

        if (wantText != null)
            wantText.text = GenreWantTexts.TryGetValue(genre, out string w) ? w : "Шукаю книгу...";

        if (budgetText != null)
            budgetText.text = $"до {budget:F0} грн";

        SetAllContentPanels(false);
        SetPanelActive(contentWant, true);

        // Якщо хмаринка ще не відкрита — відкриваємо
        if (!IsBubbleVisible())
            yield return StartCoroutine(ShowBubbleAnimated());
        // Якщо вже відкрита (переходить з Inspecting) — просто міняємо контент без анімації
    }

    // 4. ПРИЙНЯТО → COLLAPSE
    private IEnumerator ShowAcceptAndCollapse()
    {
        string message = PickRandom(_brain.Data?.acceptMessages) ?? "Дякую!";

        // Показуємо іконку книги яку купили (BookTemplate.icon)
        if (acceptIcon != null && _brain.FoundBook?.icon != null)
            acceptIcon.sprite = _brain.FoundBook.icon;

        if (acceptText != null) acceptText.text = message;

        SetAllContentPanels(false);
        SetPanelActive(contentAccept, true);

        // Якщо хмаринка не відкрита — анімовано відкриваємо
        if (!IsBubbleVisible())
            yield return StartCoroutine(ShowBubbleAnimated());

        // Чекаємо поки гравець прочитає
        float duration = _brain.Data != null ? _brain.Data.acceptMessageDuration : 2f;
        yield return new WaitForSeconds(duration);

        // Collapse до mini-badge
        yield return StartCoroutine(HideBubbleAnimated());
        CollapseToMiniBadge();
    }

    // 5. АНІМАЦІЯ ВІДМОВИ (не змінює стан — тільки UI)
    private IEnumerator PlayRejectAnimation(string reason)
    {
        _offerAttemptsLeft--;

        // Підсвічуємо чіп червоним
        if (genreChipImage != null) genreChipImage.color = chipRejectColor;

        // Показуємо причину відмови
        if (wantText != null)
        {
            string rejectMsg = PickRandom(_brain.Data?.rejectMessages) ?? "Не те...";
            if (!string.IsNullOrEmpty(reason)) rejectMsg = reason;
            wantText.text = rejectMsg;
        }

        // Shake анімація хмаринки
        if (bubbleFull != null)
            yield return StartCoroutine(ShakeBubble(bubbleFull.transform));

        // Чекаємо секунду → повертаємо нормальний стан панелі
        yield return new WaitForSeconds(1.2f);

        if (_brain.CurrentState == NPCState.WaitingForPlayer)
        {
            if (genreChipImage != null) genreChipImage.color = chipNormalColor;

            // Повертаємо оригінальний want-текст
            if (wantText != null)
            {
                wantText.text = GenreWantTexts.TryGetValue(_brain.DesiredGenre, out string w)
                    ? w : "Шукаю книгу...";
            }

            // Якщо спроби вичерпані — підказка
            if (_offerAttemptsLeft <= 0 && budgetText != null)
                budgetText.text = "більше немає спроб...";
        }
    }

    // ── Базові анімаційні корутини ──────────────────────────────

    // Відкриває BubbleFull з scale 0→1
    private IEnumerator ShowBubbleAnimated()
    {
        SetPanelActive(bubbleFull, true);
        SetPanelActive(bubbleMini, false);
        _isCollapsed = false;

        if (bubbleFull == null) yield break;

        var rt = bubbleFull.GetComponent<RectTransform>();
        if (rt == null) yield break;

        rt.localScale = Vector3.zero;
        float elapsed = 0f;

        while (elapsed < bubbleScaleInDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / bubbleScaleInDuration);
            // EaseOutBack
            float s  = EaseOutBack(t);
            rt.localScale = Vector3.one * s;
            yield return null;
        }

        rt.localScale = Vector3.one;
    }

    // Закриває BubbleFull з scale 1→0
    private IEnumerator HideBubbleAnimated()
    {
        if (bubbleFull == null || !bubbleFull.activeSelf) yield break;

        var rt = bubbleFull.GetComponent<RectTransform>();
        if (rt == null) { SetPanelActive(bubbleFull, false); yield break; }

        float elapsed = 0f;

        while (elapsed < bubbleScaleOutDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / bubbleScaleOutDuration);
            rt.localScale = Vector3.one * (1f - t);
            yield return null;
        }

        rt.localScale = Vector3.one;
        SetPanelActive(bubbleFull, false);
    }

    // Анімація 4 крапок — безперервна поки не зупинена
    private IEnumerator AnimateDots()
    {
        if (searchDots == null || searchDots.Length == 0) yield break;

        Color dimColor    = new Color(0.65f, 0.60f, 0.55f, 1f);
        Color brightColor = new Color(0.25f, 0.20f, 0.15f, 1f);

        // Скидаємо всі крапки
        foreach (var d in searchDots)
            if (d != null) d.color = dimColor;

        int active = 0;
        while (true)
        {
            // Підсвічуємо поточну крапку
            if (active < searchDots.Length && searchDots[active] != null)
            {
                // Гасимо попередню
                int prev = (active - 1 + searchDots.Length) % searchDots.Length;
                if (searchDots[prev] != null) searchDots[prev].color = dimColor;

                searchDots[active].color = brightColor;
            }

            active = (active + 1) % searchDots.Length;
            yield return new WaitForSeconds(dotsPulsePeriod);
        }
    }

    // Shake хмаринки
    private IEnumerator ShakeBubble(Transform target)
    {
        if (target == null) yield break;

        Vector3 original = target.localPosition;
        float   elapsed  = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / shakeDuration;
            float strength = shakeIntensity * (1f - progress); // затухаючий shake
            target.localPosition = original + (Vector3)(Random.insideUnitCircle * strength);
            yield return null;
        }

        target.localPosition = original;
    }

    // ── Collapse / Expand ───────────────────────────────────────

    private void CollapseToMiniBadge()
    {
        _isCollapsed = true;
        SetPanelActive(bubbleFull, false);

        if (miniText != null) miniText.text = "🛒 Іде на касу";
        SetPanelActive(bubbleMini, true);
    }

    private void ExpandFromBadge()
    {
        _isCollapsed = false;
        SetPanelActive(bubbleMini, false);
        StopActive();
        _activeCoroutine = StartCoroutine(ShowBubbleAnimated());
    }

    private void HideAllBubbles()
    {
        StopDotsAnim();
        SetPanelActive(bubbleFull, false);
        SetPanelActive(bubbleMini, false);
        _isCollapsed = false;
    }

    // ── Timer ───────────────────────────────────────────────────

    private void UpdateTimer()
    {
        if (_brain == null || timerFillImage == null) return;

        float t = _brain.GetRemainingTimeNormalized();
        timerFillImage.fillAmount = t;

        Color color;
        if (t > 0.5f)
            color = Color.Lerp(timeHalfColor,  timeFullColor,  (t - 0.5f) * 2f);
        else
            color = Color.Lerp(timeEmptyColor, timeHalfColor,  t * 2f);

        timerFillImage.color = color;
    }

    private void SetTimerVisible(bool visible)
    {
        if (timerFillImage != null)
            timerFillImage.gameObject.SetActive(visible);
    }

    // ── Billboard ───────────────────────────────────────────────

    private void BillboardUI()
    {
        if (uiRoot == null) return;
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        uiRoot.transform.LookAt(
            uiRoot.transform.position + _mainCamera.transform.rotation * Vector3.forward,
            _mainCamera.transform.rotation * Vector3.up
        );
    }

    // ── Helpers ─────────────────────────────────────────────────

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private void SetAllContentPanels(bool active)
    {
        SetPanelActive(contentGreeting, active);
        SetPanelActive(contentDots,     active);
        SetPanelActive(contentWant,     active);
        SetPanelActive(contentAccept,   active);
    }

    private bool IsBubbleVisible()
        => bubbleFull != null && bubbleFull.activeSelf;

    private void StopActive()
    {
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }
        StopDotsAnim();
    }

    private void StopDotsAnim()
    {
        if (_dotsCoroutine != null)
        {
            StopCoroutine(_dotsCoroutine);
            _dotsCoroutine = null;
        }
    }

    private Sprite FindGenreSprite(BookGenre genre)
    {
        if (genreIcons == null) return null;
        foreach (var entry in genreIcons)
            if (entry.genre == genre) return entry.icon;
        return null;
    }

    private static string PickRandom(string[] arr)
    {
        if (arr == null || arr.Length == 0) return null;
        return arr[Random.Range(0, arr.Length)];
    }

    // EaseOutBack — пружній scale-in ефект
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}