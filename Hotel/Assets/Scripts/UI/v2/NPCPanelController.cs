// Assets/Scripts/UI/v2/GameHUD/NPCPanelController.cs
//
// ВИПРАВЛЕНО:
//   - Stats знаходяться в brain.Ticker.Stats (тип NPCStats)
//   - NPCStats.Mood/Comfort/Patience: діапазон -100..+100
//     → нормалізація: InverseLerp(NPCStats.MIN, NPCStats.MAX, value)
//   - NPCStats.Wallet: 0..100% → ділимо на 100f
//   - NPCBrain.DesiredGenre — ОДИН BookGenre (не список)
//     → робимо список з одного жанру
//   - NPCState.Left не існує → використовуємо тільки NPCState.Leaving
//   - Немає IsGenreFulfilled на NPCBrain → перевіряємо через Personality.BooksBought

using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-10)]
public class NPCPanelController : MonoBehaviour
{
    public static NPCPanelController Instance { get; private set; }

    [Header("UIDocument — призначте Screen 1 UIDocument")]
    [SerializeField] private UIDocument uiDocument;

    // ── UI Elements ───────────────────────────────────────────────
    private VisualElement _panel;
    private VisualElement _moodFill;
    private VisualElement _comfortFill;
    private VisualElement _patienceFill;
    private VisualElement _walletFill;
    private Label         _moodTrend;
    private Label         _comfortTrend;
    private Label         _patienceTrend;

    // ── Sub-controllers ───────────────────────────────────────────
    private GenreCardListController _genreCards;

    // ── State ─────────────────────────────────────────────────────
    private NPCBrain _currentNPC;
    private bool     _isVisible;

    // Попередні значення для trend arrows (нормалізовані 0..1)
    private float _prevMood;
    private float _prevComfort;
    private float _prevPatience;

    // ── Public accessor ───────────────────────────────────────────
    public GenreCardListController GenreCards => _genreCards;

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        var root = uiDocument?.rootVisualElement;
        if (root == null) return;

        _panel         = root.Q<VisualElement>("NPCInspectorPanel");
        _moodFill      = root.Q<VisualElement>("MoodBarFill");
        _comfortFill   = root.Q<VisualElement>("ComfortBarFill");
        _patienceFill  = root.Q<VisualElement>("PatienceBarFill");
        _walletFill    = root.Q<VisualElement>("WalletBarFill");
        _moodTrend     = root.Q<Label>("MoodTrend");
        _comfortTrend  = root.Q<Label>("ComfortTrend");
        _patienceTrend = root.Q<Label>("PatienceTrend");

        _genreCards = new GenreCardListController(root);

        if (_panel != null)
        {
            _panel.AddToClassList("hidden");
            _panel.pickingMode = PickingMode.Ignore;
        }
    }

    private void Update()
    {
        if (!_isVisible || _currentNPC == null) return;
        RefreshBars();
    }

    // ── Public API ────────────────────────────────────────────────

    public void Show(NPCBrain npc)
    {
        if (npc == null) return;

        if (_currentNPC != null)
            _currentNPC.OnStateChanged -= OnNPCStateChanged;

        _currentNPC = npc;
        _currentNPC.OnStateChanged += OnNPCStateChanged;

        // Зберігаємо стартові нормалізовані значення
        if (npc.Ticker?.Stats != null)
        {
            _prevMood     = Normalize(npc.Ticker.Stats.Mood);
            _prevComfort  = Normalize(npc.Ticker.Stats.Comfort);
            _prevPatience = Normalize(npc.Ticker.Stats.Patience);
        }

        BuildGenreCards(npc);

        if (_panel != null)
        {
            _panel.RemoveFromClassList("hidden");
            _panel.pickingMode = PickingMode.Position;
        }

        RefreshBars();
        _isVisible = true;

        Debug.Log($"[NPCPanel] Show: {npc.Data?.npcName}");
    }

    public void Hide()
    {
        if (_currentNPC != null)
        {
            _currentNPC.OnStateChanged -= OnNPCStateChanged;
            _currentNPC = null;
        }

        if (_panel != null)
        {
            _panel.AddToClassList("hidden");
            _panel.pickingMode = PickingMode.Ignore;
        }

        _isVisible = false;
    }

    public void HideIfShowing(NPCBrain npc)
    {
        if (_currentNPC == npc) Hide();
    }

    // ── Private ───────────────────────────────────────────────────

    private void RefreshBars()
    {
        if (_currentNPC?.Ticker?.Stats == null) return;

        var stats = _currentNPC.Ticker.Stats;

        SetBar(_moodFill,     Normalize(stats.Mood),     ref _prevMood,     _moodTrend);
        SetBar(_comfortFill,  Normalize(stats.Comfort),  ref _prevComfort,  _comfortTrend);
        SetBar(_patienceFill, Normalize(stats.Patience), ref _prevPatience, _patienceTrend);

        // Wallet: 0..100 → нормалізуємо /100
        if (_walletFill != null)
        {
            float w = Mathf.Clamp01(stats.Wallet / NPCStats.WALLET_MAX);
            _walletFill.style.width = new StyleLength(new Length(w * 100f, LengthUnit.Percent));
        }
    }

    /// NPCStats.Mood/Comfort/Patience: -100..+100 → 0..1
    private static float Normalize(float value)
        => Mathf.InverseLerp(NPCStats.MIN, NPCStats.MAX, value);

    private void SetBar(VisualElement fill, float value01, ref float prev, Label trendLabel)
    {
        if (fill == null) return;
        fill.style.width = new StyleLength(new Length(value01 * 100f, LengthUnit.Percent));

        // Рівень кольору
        fill.EnableInClassList("low", value01 < 0.3f);
        fill.EnableInClassList("mid", value01 >= 0.3f && value01 < 0.6f);

        // Trend arrow
        if (trendLabel != null)
        {
            float delta = value01 - prev;
            if (Mathf.Abs(delta) > 0.01f)
            {
                trendLabel.text = delta > 0 ? "↑" : "↓";
                trendLabel.EnableInClassList("trend--up",   delta > 0);
                trendLabel.EnableInClassList("trend--down", delta < 0);
            }
        }
        prev = value01;
    }

    private void BuildGenreCards(NPCBrain npc)
    {
        var cards = new System.Collections.Generic.List<GenreCardData>();
 
        int total  = npc.Personality?.WantsToBuy ?? 1;
        int bought = npc.Personality?.BooksBought ?? 0;
 
        for (int i = 0; i < total; i++)
        {
            // ✅ ЗМІНА: кожен слот отримує СВІЙ жанр з ShoppingList
            var genre = (npc.Personality?.ShoppingList != null && i < npc.Personality.ShoppingList.Length)
                ? npc.Personality.ShoppingList[i]
                : npc.DesiredGenre; // fallback на поточний жанр brain
 
            var card = new GenreCardData(genre, genre.ToString())
            {
                IsFulfilled = i < bought
            };
            cards.Add(card);
        }
 
        _genreCards?.BuildCards(cards);
    }

    private void OnNPCStateChanged(NPCState newState)
    {
        // NPCState.Left не існує в enum — тільки Leaving
        if (newState == NPCState.Leaving)
            Hide();
    }
}