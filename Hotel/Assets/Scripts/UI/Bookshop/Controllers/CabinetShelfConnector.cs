// ═══════════════════════════════════════════════════════════════════
// CabinetShelfConnector.cs — Connects 3D shelf click → Inventory UI
// Path: Assets/Scripts/UI/Bookshop/CabinetShelfConnector.cs
//
// SETUP:
//  1. Attach this script to any shelf/cabinet GameObject in the scene
//  2. Assign the shelfId in Inspector (e.g. "ShelfA", "CabinetMain")
//  3. The shelf needs a Collider component (any type)
//  4. Player must have a script that does raycasting and calls OnInteract()
//     OR use the existing CabinetClickHandler/PlayerInteraction system
//
// INTEGRATION with existing InteractionRouter:
//  In your InteractionRouter.HandleClick(GameObject hit):
//    var connector = hit.GetComponent<CabinetShelfConnector>();
//    if (connector != null) { connector.OnInteract(); return; }
// ═══════════════════════════════════════════════════════════════════

using UnityEngine;

public class CabinetShelfConnector : MonoBehaviour
{
    [Header("Shelf Configuration")]
    [Tooltip("Unique identifier for this shelf. Used to filter inventory items.")]
    [SerializeField] private string shelfId = "Shelf_A";

    [Tooltip("Display name shown in toast notification")]
    [SerializeField] private string displayName = "Bookshelf A";

    [Header("Interaction")]
    [Tooltip("Layer mask for player raycast. Leave default for all layers.")]
    [SerializeField] private bool useMouseRaycast = true;   // for standalone testing

    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        if (!useMouseRaycast) return;

        // Simple mouse click raycast for testing
        if (Input.GetMouseButtonDown(0))
        {
            if (_cam == null) return;
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                if (hit.collider.gameObject == gameObject)
                    OnInteract();
            }
        }
    }

    /// Call this from your existing interaction system (CabinetClickHandler, PlayerInteraction, etc.)
    public void OnInteract()
    {
        if (BookshopUIController.Instance == null)
        {
            Debug.LogWarning("[ShelfConnector] BookshopUIController.Instance is null");
            return;
        }

        Debug.Log($"[ShelfConnector] Opening inventory for shelf: {shelfId}");
        BookshopUIController.Instance.OpenInventoryFromShelf(shelfId);
    }

    // ── Static helper: call from any interactor without GetComponent ──
    public static bool TryOpenFromHit(GameObject hitObject)
    {
        var connector = hitObject.GetComponent<CabinetShelfConnector>();
        if (connector == null) return false;
        connector.OnInteract();
        return true;
    }
}
