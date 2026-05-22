// ═══════════════════════════════════════════════════════════════════
// BookshopUIBridge.cs
// Path: Assets/Scripts/UI/Bookshop/BookshopUIBridge.cs
//
// ПРОБЛЕМА:
//   ContextMenuUI.ShowForCabinet() викликає ShopUIManager.OpenCabinetUI(cabinet)
//   але новий BookshopUIController.OpenInventoryFromShelf() не отримує виклик.
//
// РІШЕННЯ:
//   Цей міст перехоплює OpenCabinetUI і перенаправляє в BookshopUIController.
//   Замість зміни ContextMenuUI — просто додай цей компонент в сцену.
//
// SETUP: 
//   Додай BookshopUIBridge на той самий GameObject що BookshopUIController.
//   Нічого більше не потрібно.
// ═══════════════════════════════════════════════════════════════════

using UnityEngine;

public class BookshopUIBridge : MonoBehaviour
{
    private void Awake()
    {
        // Підписуємось на ShopUIManager якщо він є
        // (ShopUIManager продовжує існувати — ми лише додаємо новий UI поверх)
    }

    private void Start()
    {
        // Перевіряємо наявність обох систем
        if (BookshopUIController.Instance == null)
            Debug.LogError("[UIBridge] BookshopUIController.Instance is null! Додай BookshopUIController в сцену.");

        if (ShopUIManager.Instance == null)
            Debug.LogWarning("[UIBridge] ShopUIManager.Instance is null — legacy UI вимкнений, це нормально.");
    }

    // ─────────────────────────────────────────────────────────────
    // Викликай цей метод замість ShopUIManager.OpenCabinetUI(cabinet)
    // АБО переключи ContextMenuUI на цей виклик.
    // ─────────────────────────────────────────────────────────────

    /// Замінює ShopUIManager.OpenCabinetUI(cabinet) — відкриває новий BookshopUI
    public static void OpenCabinet(Cabinet cabinet)
    {
        if (BookshopUIController.Instance != null)
        {
            BookshopUIController.Instance.OpenInventoryFromShelf(cabinet?.cabinetName);
        }
        else
        {
            // Fallback до старого ShopUIManager
            ShopUIManager.Instance?.OpenCabinetUI(cabinet);
            Debug.LogWarning("[UIBridge] BookshopUIController не знайдено, використовуємо ShopUIManager");
        }
    }

    /// Відкрити інвентар без прив'язки до шафи
    public static void OpenInventory()
    {
        if (BookshopUIController.Instance != null)
            BookshopUIController.Instance.OpenInventoryFromShelf();
        else
            ShopUIManager.Instance?.OpenInventoryPanel();
    }
}
