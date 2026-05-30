// Assets/Scripts/UI/v2/UIScreenManager.cs
//
// Singleton — єдина точка перемикання між HUD екранами v2.
// Screen 1 (GameHUD): час + ліва навбар + NPC панель
// Screen 2 (EditHUD): панель декорацій/меблів
//
// ПІДКЛЮЧЕННЯ:
//   1. Create Empty GO "UIScreenManager"
//   2. Add UIScreenManager.cs
//   3. Призначте screen1Document і screen2Document в Inspector
//
// ВИКОРИСТАННЯ:
//   UIScreenManager.Instance.SwitchTo(UIScreen.Game);    // нормальна гра
//   UIScreenManager.Instance.SwitchTo(UIScreen.EditMode); // EditMode ON
//
// Сумісність: паралельно з існуючим BookshopUIController.
// UIScreenManager керує ТІЛЬКИ v2 документами, не чіпаючи старий UI.

using UnityEngine;
using UnityEngine.UIElements;

public enum UIScreen
{
    None,
    Game,      // Screen 1: GameHUD + NPCPanel
    EditMode,  // Screen 2: EditHUD (декорації/меблі)
}

[DefaultExecutionOrder(-30)]
public class UIScreenManager : MonoBehaviour
{
    public static UIScreenManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────
    [Header("UIDocuments v2")]
    [SerializeField] private UIDocument screen1Document; // GameHUD
    [SerializeField] private UIDocument screen2Document; // EditHUD

    [Header("Controllers (auto-found if not set)")]
    [SerializeField] private NPCPanelController  npcPanelController;
    [SerializeField] private EditHUDController   editHUDController;

    // ── State ─────────────────────────────────────────────────────
    private UIScreen _current = UIScreen.None;

    public UIScreen Current => _current;

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Auto-find controllers якщо не призначені
        if (npcPanelController == null)
            npcPanelController = FindAnyObjectByType<NPCPanelController>();
        if (editHUDController == null)
            editHUDController = FindAnyObjectByType<EditHUDController>();
    }

    private void Start()
    {
        // За замовчуванням: GameHUD видний, EditHUD прихований
        SwitchTo(UIScreen.Game);
    }

    // ── Public API ────────────────────────────────────────────────

    public void SwitchTo(UIScreen screen)
    {
        if (_current == screen) return;
        _current = screen;

        SetScreenActive(screen1Document, screen == UIScreen.Game);
        SetScreenActive(screen2Document, screen == UIScreen.EditMode);

        // Підказати sub-controllers
        switch (screen)
        {
            case UIScreen.Game:
                editHUDController?.OnPanelHidden();
                break;
            case UIScreen.EditMode:
                npcPanelController?.Hide(); // ховаємо NPC панель при вході в EditMode
                editHUDController?.OnPanelShown();
                break;
        }

        Debug.Log($"[UIScreenManager] Switched to: {screen}");
    }

    /// Перемикання між екранами (toggle)
    public void ToggleEditMode()
    {
        SwitchTo(_current == UIScreen.EditMode ? UIScreen.Game : UIScreen.EditMode);
    }

    // ── Private ───────────────────────────────────────────────────

    private static void SetScreenActive(UIDocument doc, bool active)
    {
        if (doc == null) return;
        var root = doc.rootVisualElement;
        if (root == null) return;
        root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
