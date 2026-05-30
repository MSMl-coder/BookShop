// Assets/Scripts/Utils/UIPointerChecker.cs
// Єдине місце для перевірки "чи курсор над UI Toolkit елементом".
// Замінює EventSystem.current.IsPointerOverGameObject() що не бачить UIToolkit.
//
// Використовується:
//   - CameraController.IsPointerOverUI()
//   - InteractionRouter.IsPointerOverUIToolkit() → замінити на цей клас
//   - WorldHoverInfoTrigger.IsPointerOverUI()
//   - ShelfInteractionHandler.IsPointerOverUI()
//
// ВАЖЛИВО: RuntimePanelUtils.ScreenToPanel конвертує координати тільки якщо
//           панель вже розрахована (після першого рендеру). На першому кадрі
//           може повернути false — це нормально і не викликає баги.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public static class UIPointerChecker
{
    /// Повертає true якщо курсор миші зараз над будь-яким активним UI Toolkit елементом
    /// що має picking-mode != Ignore.
    public static bool IsOverUI()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;

        Vector2 screenPos = mouse.position.ReadValue();

        // Перевіряємо всі активні UIDocument на сцені
        // FindObjectsByType кешується на рівні кадру якщо викликається часто —
        // але для camera/input перевірки (LateUpdate) це прийнятно.
        foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            if (doc == null || doc.rootVisualElement == null) continue;

            var panel = doc.rootVisualElement.panel;
            if (panel == null) continue;

            // Конвертуємо screen → panel координати (Y інвертований)
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