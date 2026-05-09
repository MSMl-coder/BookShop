// Assets/Scripts/World/EditMode/EditModeInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// Підсвічування при наведенні + ПКМ для повернення в інвентар.
/// Окремий компонент — не захаращує PlacementController.
///
/// UNITY SETUP:
/// Додати на той самий GO що EditModeManager.
/// Furniture Layer → шар "Furniture" (той що на root шафи).
/// Тільки цей шар — щоб не підсвічувати shelf collider-и або підлогу.
public class EditModeInputHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;

    [Header("Layers")]
    [Tooltip("Тільки шар Furniture — той що на root GO шафи. НЕ Shelf, НЕ Default.")]
    [SerializeField] private LayerMask furnitureLayer;

    [Header("Settings")]
    [SerializeField] private float maxDistance    = 30f;
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.2f, 0.8f);

    private PlacedObject          _hoveredObject;
    private Renderer[]            _hoveredRenderers;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        _mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        bool inEditMode = EditModeManager.Instance != null && EditModeManager.Instance.IsEditMode;
        bool isPlacing  = PlacementController.Instance != null && PlacementController.Instance.IsPlacing;

        if (!inEditMode || isPlacing)
        {
            ClearHover();
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        UpdateHover(mouse);

        // ПКМ → повернути в інвентар
        if (mouse.rightButton.wasPressedThisFrame && _hoveredObject != null)
        {
            var obj = _hoveredObject;
            ClearHover();
            PlacementController.Instance?.ReturnToInventory(obj);
        }
    }

    private void UpdateHover(Mouse mouse)
    {
        Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

        // Raycast тільки по шару Furniture — точно потрапляємо на root шафи
        // Shelf collider-и на іншому шарі — вони не потраплять сюди
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, furnitureLayer))
        {
            // Перевіряємо що це розміщений об'єкт (є PlacedObject)
            var placedObj = hit.collider.GetComponentInParent<PlacedObject>();
            if (placedObj == null)
                placedObj = hit.collider.transform.root.GetComponentInChildren<PlacedObject>();

            if (placedObj != _hoveredObject)
            {
                ClearHover();
                if (placedObj != null)
                {
                    _hoveredObject    = placedObj;
                    // Підсвічуємо тільки renderer-и на root GO — не shelf візуали
                    _hoveredRenderers = placedObj.GetComponents<Renderer>();
                    if (_hoveredRenderers.Length == 0)
                        _hoveredRenderers = placedObj.GetComponentsInChildren<Renderer>();
                    ApplyHighlight();
                }
            }
        }
        else
        {
            ClearHover();
        }
    }

    private void ApplyHighlight()
    {
        if (_hoveredRenderers == null) return;
        _mpb.SetColor("_BaseColor", highlightColor);
        foreach (var r in _hoveredRenderers)
            if (r != null) r.SetPropertyBlock(_mpb);
    }

    private void ClearHover()
    {
        if (_hoveredRenderers != null)
            foreach (var r in _hoveredRenderers)
                if (r != null) r.SetPropertyBlock(null);

        _hoveredObject    = null;
        _hoveredRenderers = null;
    }
}