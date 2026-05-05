using TMPro;
using UnityEngine;
using UnityEngine.UI;
[RequireComponent(typeof(NPCBrain))]
public class NPCWorldUI : MonoBehaviour
{
    [Header("References — призначити в Inspector")]
    [SerializeField] private GameObject uiRoot;          // NPCUI Canvas
    [SerializeField] private Image timerFillImage;       // TimerRing → Image
    [SerializeField] private GameObject thoughtBubble;   // ThoughtBubble panel
    [SerializeField] private TextMeshProUGUI thoughtText; // ThoughtBubble → Text
    [SerializeField] private GameObject genreChip;       // GenreChip panel
    [SerializeField] private TextMeshProUGUI genreText;  // GenreChip → Text
 
    [Header("Timer Colors")]
    [SerializeField] private Color timeFullColor   = new Color(0.30f, 0.69f, 0.31f); // зелений
    [SerializeField] private Color timeHalfColor   = new Color(1.00f, 0.76f, 0.03f); // жовтий
    [SerializeField] private Color timeEmptyColor  = new Color(0.90f, 0.23f, 0.21f); // червоний
 
    [Header("Genre Colors")]
    [SerializeField] private Color genreChipColor = new Color(0.08f, 0.40f, 0.75f); // синій
 
    private NPCBrain _brain;
    private Camera _mainCamera;
 
    private static readonly System.Collections.Generic.Dictionary<BookEnums.BookGenre, string>
        GenreUkrainianNames = new System.Collections.Generic.Dictionary<BookEnums.BookGenre, string>
    {
        { BookEnums.BookGenre.Fantasy,   "⚔️ Фентезі"       },
        { BookEnums.BookGenre.Horror,    "💀 Жахи"           },
        { BookEnums.BookGenre.Mystery,   "🔍 Детектив"       },
        { BookEnums.BookGenre.Classic,   "📜 Класика"        },
        { BookEnums.BookGenre.SciFi,     "🚀 Наукова фантастика" },
        { BookEnums.BookGenre.Biography, "👤 Біографія"      },
        { BookEnums.BookGenre.Academic,  "🎓 Академічна"     },
    };
 
    private static readonly System.Collections.Generic.Dictionary<BookEnums.BookGenre, string>
        GenreThoughts = new System.Collections.Generic.Dictionary<BookEnums.BookGenre, string>
    {
        { BookEnums.BookGenre.Fantasy,   "Шукаю щось чарівне..." },
        { BookEnums.BookGenre.Horror,    "Маєш щось моторошне?"  },
        { BookEnums.BookGenre.Mystery,   "Хочу загадку..."       },
        { BookEnums.BookGenre.Classic,   "Класика потрібна."     },
        { BookEnums.BookGenre.SciFi,     "Майбутнє цікавить..."  },
        { BookEnums.BookGenre.Biography, "Шукаю чиюсь історію." },
        { BookEnums.BookGenre.Academic,  "Є наукова праця?"      },
    };
 
    // --- Unity Lifecycle ---
 
    private void Awake()
    {
        _brain = GetComponent<NPCBrain>();
        _mainCamera = Camera.main;
    }
 
    private void OnEnable()
    {
        if (_brain != null)
            _brain.OnStateChanged += HandleStateChanged;
    }
 
    private void OnDisable()
    {
        if (_brain != null)
            _brain.OnStateChanged -= HandleStateChanged;
    }
 
    private void LateUpdate()
    {
        UpdateTimer();
        BillboardUI();
    }
 
    // --- State Handling ---
 
    private void HandleStateChanged(NPCState state)
    {
        switch (state)
        {
            case NPCState.Entering:
                HideAll();
                break;
 
            case NPCState.Browsing:
            case NPCState.Inspecting:
                ShowTimer();
                ShowThought("Шукаю щось цікаве...", Color.white);
                HideGenreChip();
                break;
 
            case NPCState.ShowingHint:
            case NPCState.WaitingForPlayer:
                ShowTimer();
                ShowGenreChip(_brain.DesiredGenre);
                // Думка показує що саме хоче NPC
                if (GenreThoughts.TryGetValue(_brain.DesiredGenre, out string thought))
                    ShowThought(thought, new Color(0.9f, 0.95f, 1f)); // трохи синюватий
                break;
 
            case NPCState.Buying:
                HideAll();
                ShowThought("✓ Беру!", new Color(0.3f, 1f, 0.3f));
                break;
 
            case NPCState.Leaving:
                HideAll();
                break;
        }
    }
 
    // --- Timer ---
 
    private void UpdateTimer()
    {
        if (_brain == null || timerFillImage == null) return;
 
        float t = _brain.GetRemainingTimeNormalized();
        timerFillImage.fillAmount = t;
 
        // Колір: зелений → жовтий → червоний
        Color color;
        if (t > 0.5f)
            color = Color.Lerp(timeHalfColor, timeFullColor, (t - 0.5f) * 2f);
        else
            color = Color.Lerp(timeEmptyColor, timeHalfColor, t * 2f);
 
        timerFillImage.color = color;
    }
 
    private void ShowTimer()
    {
        if (timerFillImage != null)
            timerFillImage.gameObject.SetActive(true);
    }
 
    // --- Thought Bubble ---
 
    private void ShowThought(string text, Color textColor)
    {
        if (thoughtBubble != null) thoughtBubble.SetActive(true);
        if (thoughtText != null)
        {
            thoughtText.text = text;
            thoughtText.color = textColor;
        }
    }
 
    // --- Genre Chip ---
 
    private void ShowGenreChip(BookEnums.BookGenre genre)
    {
        if (genreChip != null) genreChip.SetActive(true);
 
        if (genreText != null)
        {
            genreText.text = GenreUkrainianNames.TryGetValue(genre, out string name)
                ? name : genre.ToString();
        }
 
        // Підсвічуємо фон синім
        var bg = genreChip?.GetComponent<Image>();
        if (bg != null) bg.color = genreChipColor;
    }
 
    private void HideGenreChip()
    {
        if (genreChip != null) genreChip.SetActive(false);
    }
 
    private void HideAll()
    {
        if (thoughtBubble != null) thoughtBubble.SetActive(false);
        if (genreChip != null) genreChip.SetActive(false);
        // Таймер не ховаємо — він завжди видимий поки NPC живий
    }
 
    // --- Billboard ---
 
    /// UI повертається обличчям до камери (billboard effect)
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
 
    // --- Public (for PlayerInteraction) ---
 
    public void ShowRejection()
    {
        ShowThought("✗ Ні, дякую.", new Color(1f, 0.4f, 0.4f));
    }
 
    public void ShowAcceptance()
    {
        ShowThought("✓ Беру!", new Color(0.3f, 1f, 0.3f));
    }
}