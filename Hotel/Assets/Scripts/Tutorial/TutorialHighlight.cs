// Assets/Scripts/Tutorial/TutorialHighlight.cs
// ФАЗА 1 — підсвітка/пульсація цільового елемента туторіалу
//
// Підтримує:
//   1. UI елементи (UIToolkit) — додає CSS клас "tutorial-highlight"
//   2. 3D GameObject-и — обводка через Outline або пульсація матеріалу
//
// UNITY SETUP:
//   1. У USS файлі додай:
//      .tutorial-highlight { border-color: #FFD700; border-width: 2px; transition: all 0.5s; }
//   2. Для 3D-об'єктів встанови шар або тег для пошуку.
 
using UnityEngine;
using UnityEngine.UIElements;
 
public static class TutorialHighlight
{
    private static VisualElement _highlightedElement;
    private static GameObject    _highlightedObject;
 
    private const string CSS_CLASS = "tutorial-highlight";
 
    /// Підсвітити елемент за іменем.
    /// Спочатку шукає у UIToolkit, потім у 3D-сцені.
    public static void Highlight(string targetName)
    {
        ClearHighlight();
 
        if (string.IsNullOrEmpty(targetName)) return;
 
        // 1. Шукаємо у UIDocument (UIToolkit)
        var uiDoc = Object.FindAnyObjectByType<UIDocument>();
        if (uiDoc != null)
        {
            var el = uiDoc.rootVisualElement.Q(targetName);
            if (el != null)
            {
                el.AddToClassList(CSS_CLASS);
                _highlightedElement = el;
                Debug.Log($"[TutorialHighlight] UI підсвітка: '{targetName}'");
                return;
            }
        }
 
        // 2. Шукаємо у 3D-сцені за назвою
        var go = GameObject.Find(targetName);
        if (go != null)
        {
            HighlightGameObject(go);
            _highlightedObject = go;
            Debug.Log($"[TutorialHighlight] 3D підсвітка: '{targetName}'");
            return;
        }
 
        Debug.LogWarning($"[TutorialHighlight] Елемент '{targetName}' не знайдено!");
    }
 
    /// Зняти всі підсвітки
    public static void ClearHighlight()
    {
        if (_highlightedElement != null)
        {
            _highlightedElement.RemoveFromClassList(CSS_CLASS);
            _highlightedElement = null;
        }
 
        if (_highlightedObject != null)
        {
            RemoveHighlightFromGameObject(_highlightedObject);
            _highlightedObject = null;
        }
    }
 
    // ── 3D підсвітка ───────────────────────────────────────────
 
    private static void HighlightGameObject(GameObject go)
    {
        // Додаємо компонент підсвітки якщо є
        var outline = go.GetComponent<TutorialOutline>();
        if (outline == null) outline = go.AddComponent<TutorialOutline>();
        outline.enabled = true;
    }
 
    private static void RemoveHighlightFromGameObject(GameObject go)
    {
        var outline = go.GetComponent<TutorialOutline>();
        if (outline != null) outline.enabled = false;
    }
}
 
 
// ─────────────────────────────────────────────────────────────────────────────
