// Assets/Scripts/UI/v2/V2HUDInjector.cs
//
// Монтує ВСІ v2 компоненти (GameHUD, NPCInspector, InventoryPanel)
// в ІСНУЮЧИЙ UIDocument BookshopUIController — без нових UIDocument.
//
// Один UIDocument = нема конфліктів з wheel events, zoom, picking.
//
// SETUP:
//   1. Видали GO: UI_v2_Screen1, UI_v2_Screen2, NPCInspector_Test,
//                 InventoryPanel_UI, UIScreenManager (якщо окремий GO)
//   2. На GO де є BookshopUIController — Add Component → V2HUDInjector
//   3. Призначте VisualTreeAsset поля в Inspector
//
// ПОРЯДОК монтування (sortingOrder не потрібен — все в одному дереві):
//   BookshopMainUI root
//   └── V2HUDLayer (position:absolute, top/left/right/bottom=0, Ignore)
//       ├── GameHUD.uxml instance    (nav, clock, speed)
//       ├── NPCInspector.uxml instance
//       └── InventoryPanel.uxml instance

using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-5)]
public class V2HUDInjector : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────
    [Header("UXML Assets (призначте в Inspector)")]
    [SerializeField] private VisualTreeAsset gameHUDAsset;
    [SerializeField] private VisualTreeAsset npcInspectorAsset;
    [SerializeField] private VisualTreeAsset inventoryPanelAsset;

    [Header("Controllers (auto-created)")]
    // Публічні для доступу з інших скриптів
    public GameHUDController    HUDController     { get; private set; }
    public NPCInspectorMount    NPCMount          { get; private set; }
    public InventoryPanelMount  InventoryMount    { get; private set; }

    // ── Runtime ───────────────────────────────────────────────────
    private VisualElement _v2Layer;

    private void Start()
    {
        // Чекаємо BookshopUIController
        var bookshopDoc = BookshopUIController.Instance?.GetUIDocument();
        if (bookshopDoc == null)
        {
            Debug.LogError("[V2HUDInjector] BookshopUIController.UIDocument не знайдено. " +
                           "Переконайся що BookshopUIController ініціалізований.");
            return;
        }

        Mount(bookshopDoc.rootVisualElement);
    }

    private void Mount(VisualElement bookshopRoot)
    {
        // Прозорий шар поверх всього — не блокує старий UI
        _v2Layer = new VisualElement();
        _v2Layer.name = "V2HUDLayer";
        _v2Layer.style.position = Position.Absolute;
        _v2Layer.style.top    = 0; _v2Layer.style.left   = 0;
        _v2Layer.style.right  = 0; _v2Layer.style.bottom = 0;
        _v2Layer.pickingMode  = PickingMode.Ignore;
        _v2Layer.style.overflow = Overflow.Visible;
        bookshopRoot.Add(_v2Layer);

        // ── 1. GameHUD ──────────────────────────────────────────
        if (gameHUDAsset != null)
        {
            var hudEl = gameHUDAsset.Instantiate();
            SetupWrapper(hudEl);
            _v2Layer.Add(hudEl);

            // GameHUDController як компонент на цьому GO
            HUDController = gameObject.GetComponent<GameHUDController>()
                         ?? gameObject.AddComponent<GameHUDController>();
            HUDController.InjectRoot(hudEl);
        }

        // ── 2. NPC Inspector ────────────────────────────────────
        if (npcInspectorAsset != null)
        {
            var npcEl = npcInspectorAsset.Instantiate();
            SetupWrapper(npcEl);
            _v2Layer.Add(npcEl);

            var controller = new NPCInspectorController(npcEl);
            // Реєструємо як NPCInspectorMount singleton
            NPCMount = gameObject.GetComponent<NPCInspectorMount>()
                    ?? gameObject.AddComponent<NPCInspectorMount>();
            NPCMount.InjectController(controller);
        }

        // ── 3. Inventory Panel ──────────────────────────────────
        if (inventoryPanelAsset != null)
        {
            var invEl = inventoryPanelAsset.Instantiate();
            SetupWrapper(invEl);
            _v2Layer.Add(invEl);

            var controller = new InventoryPanelController(invEl);
            InventoryMount = gameObject.GetComponent<InventoryPanelMount>()
                          ?? gameObject.AddComponent<InventoryPanelMount>();
            InventoryMount.InjectController(controller);
        }

        Debug.Log("[V2HUDInjector] All v2 components mounted into BookshopUI document.");
    }

    // Wrapper: абсолютний fullscreen контейнер що не блокує кліки
    private static void SetupWrapper(VisualElement el)
    {
        el.style.position = Position.Absolute;
        el.style.top    = 0; el.style.left   = 0;
        el.style.right  = 0; el.style.bottom = 0;
        el.pickingMode  = PickingMode.Ignore;
    }
}