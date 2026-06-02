// Assets/Scripts/UI/v2/UIScreenManager.cs
//
// ВИПРАВЛЕННЯ блокування кліків:
// UI_v2 документи додаються до списку "transparent" документів
// в UIPointerChecker щоб їх ігнорувати при IsOverUI() перевірці.
//
// Логіка: v2 HUD є "overlay-only" — він не має solid background,
// кліки крізь нього мають проходити до 3D або старого UI.
// Тільки конкретні інтерактивні елементи (Button, Genre cards) мають реагувати.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public enum UIScreen { None, Game, EditMode }

[DefaultExecutionOrder(-30)]
public class UIScreenManager : MonoBehaviour
{
    public static UIScreenManager Instance { get; private set; }

    [Header("UIDocuments v2")]
    [SerializeField] private UIDocument screen1Document; // GameHUD
    [SerializeField] private UIDocument screen2Document; // EditHUD

    [Header("Controllers")]
    [SerializeField] private EditHUDController editHUDController;

    private UIScreen _current = UIScreen.None;
    public  UIScreen Current  => _current;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Реєструємо v2 документи як "transparent" — не блокують 3D кліки
        RegisterTransparentDocuments();

        if (editHUDController == null)
            editHUDController = FindAnyObjectByType<EditHUDController>();

        SwitchTo(UIScreen.Game);
    }

    // ── Public API ────────────────────────────────────────────────

    public void SwitchTo(UIScreen screen)
    {
        if (_current == screen) return;
        _current = screen;

        SetScreenActive(screen1Document, screen == UIScreen.Game);
        SetScreenActive(screen2Document, screen == UIScreen.EditMode);

        switch (screen)
        {
            case UIScreen.Game:
                editHUDController?.OnPanelHidden();
                break;
            case UIScreen.EditMode:
                NPCInspectorMount.Instance?.Hide();
                editHUDController?.OnPanelShown();
                break;
        }
    }

    public void ToggleEditMode()
        => SwitchTo(_current == UIScreen.EditMode ? UIScreen.Game : UIScreen.EditMode);

    // ── Private ───────────────────────────────────────────────────

    private static void SetScreenActive(UIDocument doc, bool active)
    {
        if (doc == null) return;
        var root = doc.rootVisualElement;
        if (root == null) return;
        root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
        root.pickingMode   = PickingMode.Ignore; // завжди Ignore на root
    }

    /// Додаємо v2 UIDocument до списку виключень UIPointerChecker.
    /// Це дозволяє кліки проходити крізь v2 HUD до 3D і старого UI.
    private void RegisterTransparentDocuments()
    {
        var docs = new List<UIDocument>();
        if (screen1Document != null) docs.Add(screen1Document);
        if (screen2Document != null) docs.Add(screen2Document);

        // Також реєструємо NPCInspectorMount document якщо є
        var npcMount = FindAnyObjectByType<NPCInspectorMount>();
        if (npcMount != null)
        {
            var npcDoc = npcMount.GetComponent<UIDocument>();
            if (npcDoc != null) docs.Add(npcDoc);
        }

        UIPointerChecker.RegisterTransparentDocuments(docs);
    }
}