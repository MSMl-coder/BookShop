// Assets/Scripts/Utils/UIPointerChecker.cs
//
// ОНОВЛЕНО: додано RegisterTransparentDocuments() —
// v2 HUD документи реєструються як "transparent" і не блокують 3D кліки.
//
// Використовується:
//   - InteractionRouter.IsPointerOverUIToolkit()
//   - CameraController.IsPointerOverUI()
//   - WorldHoverInfoTrigger.IsPointerOverUI()

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public static class UIPointerChecker
{
    // UIDocument-и що є "прозорими" — їх елементи не блокують 3D кліки.
    // V2 HUD реєструється тут через UIScreenManager.
    private static readonly HashSet<UIDocument> _transparentDocs = new();

    /// Зареєструвати документи як прозорі (не блокують 3D).
    public static void RegisterTransparentDocuments(IEnumerable<UIDocument> docs)
    {
        foreach (var doc in docs)
            if (doc != null) _transparentDocs.Add(doc);
    }

    /// Скасувати реєстрацію.
    public static void UnregisterTransparentDocument(UIDocument doc)
        => _transparentDocs.Remove(doc);

    /// Повертає true якщо курсор над НЕПРОЗОРИМ UI Toolkit елементом
    /// що має picking-mode != Ignore.
    public static bool IsOverUI()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;

        Vector2 screenPos = mouse.position.ReadValue();

        foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            if (doc == null || doc.rootVisualElement == null) continue;

            // Пропускаємо v2 HUD документи — вони "прозорі"
            if (_transparentDocs.Contains(doc)) continue;

            var panel = doc.rootVisualElement.panel;
            if (panel == null) continue;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
                panel,
                new Vector2(screenPos.x, Screen.height - screenPos.y));

            var picked = panel.Pick(panelPos);
            if (picked != null && picked.pickingMode != PickingMode.Ignore)
                return true;
        }

        return false;
    }
}