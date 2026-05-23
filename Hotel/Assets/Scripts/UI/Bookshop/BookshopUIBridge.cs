// Assets/Scripts/UI/Bookshop/BookshopUIBridge.cs
// FIX: OpenCabinet тепер викликає ShelfManagerPanelController напряму,
//      а не BookshopUIController.OpenInventoryFromShelf (старий шлях).

using UnityEngine;

public class BookshopUIBridge : MonoBehaviour
{
    private void Start()
    {
        if (ShelfManagerPanelController.Instance == null)
            Debug.LogWarning("[UIBridge] ShelfManagerPanelController не знайдено — додай його в сцену.");
    }

    /// Викликається з ContextMenuUI.ShowForCabinet → ShopUIManager.OpenCabinetUI
    public static void OpenCabinet(Cabinet cabinet)
    {
        // ВИПРАВЛЕНО: використовуємо ShelfManagerPanelController, не BookshopUIController
        if (ShelfManagerPanelController.Instance != null)
        {
            Debug.Log($"[UIBridge] OpenCabinet → ShelfManagerPanelController: {cabinet?.cabinetName}");
            ShelfManagerPanelController.Instance.Open(cabinet);
        }
        else
        {
            Debug.LogError("[UIBridge] ShelfManagerPanelController.Instance == null!");
        }
    }

    public static void OpenInventory()
    {
        ShopUIManager.Instance?.OpenInventory();
    }
}