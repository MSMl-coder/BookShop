// Assets/Scripts/UI/Components/NPCInspector/NPCInspectorMount.cs
//
// MonoBehaviour-обгортка для NPCInspectorController.
// NPCBrain.OnNPCClicked() викликає NPCInspectorMount.Instance.Show(npc)
// (після патчу NPCBrain — див. NPCBrain_patch.cs)

using UnityEngine;
using UnityEngine.UIElements;

public class NPCInspectorMount : MonoBehaviour
{
    public static NPCInspectorMount Instance { get; private set; }

    [Header("UXML asset")]
    [SerializeField] private VisualTreeAsset inspectorAsset;

    [Header("Speech Bubble Sprite")]
    [Tooltip("Спрайт для хмаринки. Якщо null — використовується CSS стиль.")]
    [SerializeField] private Sprite speechBubbleSprite;

    [Header("Speech Bubble Size & Position")]
    [SerializeField] private float bubbleWidth    = 180f;
    [SerializeField] private float bubbleHeight   = 80f;
    [SerializeField] private float bubbleOffsetX  = 0f;
    [SerializeField] private float bubbleOffsetY  = 12f;  // відступ над mood

    [Header("Testing")]
    [SerializeField] private bool showTestPreviewOnStart = false;

    private NPCInspectorController _controller;

    public NPCInspectorController Controller => _controller;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || inspectorAsset == null)
        {
            Debug.LogWarning("[NPCInspectorMount] UIDocument або inspectorAsset не призначені");
            return;
        }

        // Монтуємо UXML
        var el = inspectorAsset.Instantiate();

        // TemplateContainer — absolute fullscreen wrapper не блокує кліки
        el.style.position = Position.Absolute;
        el.style.left  = 0; el.style.top    = 0;
        el.style.right = 0; el.style.bottom = 0;
        el.pickingMode = PickingMode.Ignore;

        // Root документу теж Ignore
        var root = doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.Add(el);

        _controller = new NPCInspectorController(el);

        // Застосовуємо налаштування хмаринки
        ApplySpeechBubbleSettings(el);

        if (showTestPreviewOnStart)
            _controller.ShowForPreview();

        Debug.Log("[NPCInspectorMount] Mounted. Waiting for NPCBrain.OnNPCClicked()");
    }

    private void Update() => _controller?.Tick();

    // ── Public API (викликається з NPCBrain.OnNPCClicked) ─────────

    /// Показати панель для NPC.
    public void Show(NPCBrain npc)
    {
        if (_controller == null)
        {
            Debug.LogError("[NPCInspectorMount] Controller не ініціалізований");
            return;
        }
        _controller.Show(npc);
    }

    /// Сховати панель.
    public void Hide() => _controller?.Hide();

    /// Сховати тільки якщо показує цього NPC.
    public void HideIfShowing(NPCBrain npc) => _controller?.HideIfShowing(npc);

    /// Викликається V2HUDInjector замість OnEnable монтування.
    /// Передає вже створений контролер (UXML змонтований зовні).
    public void InjectController(NPCInspectorController controller)
    {
        _controller = controller;
        // Instance вже встановлений в Awake
        Debug.Log("[NPCInspectorMount] Controller injected by V2HUDInjector");
    }

    [ContextMenu("Show Test Preview")]
    public void ShowTestPreview() => _controller?.ShowForPreview();

    [ContextMenu("Hide")]
    public void HideFromMenu() => _controller?.Hide();

    // ── Speech Bubble Settings ────────────────────────────────────

    [ContextMenu("Apply Speech Bubble Settings")]
    public void ApplySpeechBubbleSettings() => ApplySpeechBubbleSettings(null);

    private void ApplySpeechBubbleSettings(VisualElement container)
    {
        var root = container ?? GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null) return;

        var bubble = root.Q<VisualElement>("SpeechBubble");
        if (bubble == null) return;

        // Розмір
        bubble.style.width  = bubbleWidth;
        bubble.style.height = bubbleHeight;

        // Позиція (відносно mood stat-row)
        bubble.style.left        = bubbleOffsetX;
        bubble.style.marginBottom = bubbleOffsetY;

        // Спрайт якщо призначений
        if (speechBubbleSprite != null)
        {
            bubble.style.backgroundImage   = new StyleBackground(speechBubbleSprite);
            bubble.style.backgroundColor   = StyleKeyword.None;
            bubble.style.borderTopWidth    = 0;
            bubble.style.borderBottomWidth = 0;
            bubble.style.borderLeftWidth   = 0;
            bubble.style.borderRightWidth  = 0;
            bubble.style.borderTopLeftRadius     = 0;
            bubble.style.borderTopRightRadius    = 0;
            bubble.style.borderBottomLeftRadius  = 0;
            bubble.style.borderBottomRightRadius = 0;

            // Текст поверх спрайту — padding для відступу від країв
            var text = bubble.Q<Label>("SpeechBubbleText");
            if (text != null)
            {
                text.style.position   = Position.Absolute;
                text.style.top        = 10f;
                text.style.left       = 14f;
                text.style.right      = 14f;
                text.style.bottom     = 16f;
                text.style.whiteSpace = WhiteSpace.Normal;
            }

            // Хвостик ховаємо — він вже намальований на спрайті
            var tail = bubble.Q<VisualElement>("speech-bubble__tail");
            if (tail != null) tail.style.display = DisplayStyle.None;
        }

        Debug.Log($"[NPCInspectorMount] Bubble applied: {bubbleWidth}x{bubbleHeight} " +
                  $"offset=({bubbleOffsetX},{bubbleOffsetY}) sprite={speechBubbleSprite?.name ?? "none"}");
    }

}