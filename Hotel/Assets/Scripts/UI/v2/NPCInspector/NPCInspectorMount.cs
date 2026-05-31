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

    [ContextMenu("Show Test Preview")]
    public void ShowTestPreview() => _controller?.ShowForPreview();

    [ContextMenu("Hide")]
    public void HideFromMenu() => _controller?.Hide();
}