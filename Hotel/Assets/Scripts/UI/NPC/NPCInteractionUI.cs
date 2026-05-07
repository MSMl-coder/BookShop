using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

// World-space UI над головою NPC (Billboard)
// Використовує простий World Space Canvas або окремий UIDocument
public class NPCInteractionUI : MonoBehaviour
{
    [Header("World UI References")]
    [SerializeField] private GameObject timerRingPrefab;    // Prefab: кільце-таймер
    [SerializeField] private GameObject thoughtBubblePrefab; // Prefab: хмарка думок
    [SerializeField] private GameObject genreIconPrefab;     // Prefab: іконка жанру

    [Header("Settings")]
    [SerializeField] private float heightOffset = 2.2f;

    private GameObject _timerRing;
    private GameObject _thoughtBubble;
    private GameObject _genreIcon;
    private NPCBrain _brain;

    // Sprite references per genre (assign in Inspector via array or dict)
    [Header("Genre Icons")]
    [SerializeField] private GenreIconEntry[] genreIcons;

    private void Awake()
    {
        _brain = GetComponent<NPCBrain>();
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

    private void Update()
    {
        // Keep UI above NPC head
        Vector3 headPos = transform.position + Vector3.up * heightOffset;

        if (_timerRing != null)
        {
            _timerRing.transform.position = headPos;
            // Update timer fill (requires a FillImage component on prefab)
            var fill = _timerRing.GetComponent<TimerRingVisual>();
            fill?.SetFill(_brain.GetRemainingTimeNormalized());
        }

        if (_thoughtBubble != null)
            _thoughtBubble.transform.position = headPos + Vector3.right * 0.3f;

        if (_genreIcon != null)
            _genreIcon.transform.position = headPos;

        // Billboard: face camera
        BillboardToCamera(_timerRing);
        BillboardToCamera(_thoughtBubble);
        BillboardToCamera(_genreIcon);
    }

    private void HandleStateChanged(NPCState state)
    {
        ClearAll();

        switch (state)
        {
            case NPCState.Browsing:
            case NPCState.Inspecting:
                SpawnTimerRing();
                SpawnThoughtBubble("...");
                break;

            case NPCState.ShowingHint:
            case NPCState.WaitingForPlayer:
                SpawnTimerRing();
                SpawnGenreIcon(_brain.DesiredGenre);
                SpawnThoughtBubble(GetGenreThought(_brain.DesiredGenre));
                break;

            case NPCState.Buying:
                SpawnThoughtBubble("💰");
                break;

            case NPCState.Leaving:
                ClearAll();
                break;
        }
    }

    private void SpawnTimerRing()
    {
        if (timerRingPrefab == null) return;
        _timerRing = Instantiate(timerRingPrefab);
    }

    private void SpawnThoughtBubble(string text)
    {
        if (thoughtBubblePrefab == null) return;
        _thoughtBubble = Instantiate(thoughtBubblePrefab);
        _thoughtBubble.GetComponent<ThoughtBubbleVisual>()?.SetText(text);
    }

    private void SpawnGenreIcon(BookGenre genre)
    {
        if (genreIconPrefab == null) return;
        _genreIcon = Instantiate(genreIconPrefab);

        Sprite icon = GetIconForGenre(genre);
        _genreIcon.GetComponent<GenreIconVisual>()?.SetIcon(icon);
    }

    public void ShowRejection()
    {
        StartCoroutine(ShowTemporaryThought("❌", 2f));
    }

    private IEnumerator ShowTemporaryThought(string text, float duration)
    {
        SpawnThoughtBubble(text);
        yield return new WaitForSeconds(duration);
        if (_thoughtBubble != null) Destroy(_thoughtBubble);
    }

    private void ClearAll()
    {
        if (_timerRing != null) Destroy(_timerRing);
        if (_thoughtBubble != null) Destroy(_thoughtBubble);
        if (_genreIcon != null) Destroy(_genreIcon);
    }

    private void BillboardToCamera(GameObject obj)
    {
        if (obj == null || Camera.main == null) return;
        obj.transform.LookAt(Camera.main.transform);
        obj.transform.Rotate(0, 180f, 0);
    }

    private string GetGenreThought(BookGenre genre)
    {
        return genre switch
        {
            BookGenre.Fantasy  => "Шукаю фентезі...",
            BookGenre.Horror   => "Маєш щось моторошне?",
            BookGenre.Mystery  => "Детективи є?",
            BookGenre.Classic  => "Класика потрібна.",
            BookGenre.SciFi    => "Наукова фантастика?",
            BookGenre.Biography => "Біографії шукаю.",
            BookGenre.Academic => "Наукова праця.",
            _ => "Щось шукаю..."
        };
    }

    private Sprite GetIconForGenre(BookGenre genre)
    {
        foreach (var entry in genreIcons)
            if (entry.genre == genre) return entry.icon;
        return null;
    }
}

// Helper data struct
[System.Serializable]
public class GenreIconEntry
{
    public BookGenre genre;
    public Sprite icon;
}