// Assets/Scripts/World/Cabinet/CabinetClickHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// Відстежує кліки мишки в 3D-сцені та відправляє їх до Cabinet.
///
/// КРИТИЧНО ВАЖЛИВО: цей скрипт ігнорує кліки що були зроблені над UI.
/// Без цього кліки на UI також пробивали б у 3D-сцену.
///
/// UNITY SETUP:
/// 1. Створи GameObject [CabinetClickHandler]
/// 2. Add Component → CabinetClickHandler
/// 3. Призначи Camera (Main Camera) та UIDocument (той самий що в ShopUIManager)
/// 4. Cabinet Layer → шар на якому знаходяться шафи
public class CabinetClickHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private UIDocument uiDocument;

    [Header("Settings")]
    [SerializeField] private LayerMask cabinetLayer = -1;
    [SerializeField] private float maxClickDistance = 30f;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null || mainCamera == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // КЛЮЧОВА ПЕРЕВІРКА: чи клік був над UI?
        if (IsPointerOverUI()) return;

        // Raycast у 3D-сцену
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, cabinetLayer))
        {
            // Шукаємо Cabinet на об'єкті або його батьках
            Cabinet cabinet = hit.collider.GetComponentInParent<Cabinet>();
            if (cabinet != null)
            {
                cabinet.OnClicked();
            }
        }
    }

    /// Перевіряє чи курсор знаходиться над UI Toolkit елементом
    private bool IsPointerOverUI()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return false;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // UI Toolkit: координати з нижнього лівого кута екрана,
        // але picking використовує верхній лівий кут.
        // Конвертуємо: Y треба інвертувати.
        Vector2 panelPos = new Vector2(mousePos.x, Screen.height - mousePos.y);

        // Перевіряємо чи є елемент під курсором (крім Root з picking-mode=Ignore)
        VisualElement picked = uiDocument.rootVisualElement.panel.Pick(panelPos);

        // Якщо нічого не знайдено АБО знайдено сам Root — клік пройшов крізь UI
        if (picked == null) return false;
        if (picked == uiDocument.rootVisualElement) return false;
        if (picked.name == "Root") return false;

        return true;
    }
}
