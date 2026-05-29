// Assets/Scripts/World/EditMode/SnapSystem.cs
using UnityEngine;

public struct SnapResult
{
    public Vector3 position;
    public Quaternion rotation;
    public bool isValid;
}

public class SnapSystem : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private LayerMask surfaceMask = -1; // всі шари що вже є (Floor, Wall тощо)
    [SerializeField] private float maxDistance = 30f;

    [Header("Normal Thresholds")]
    [Tooltip("Якщо нормаль хіту направлена вгору більше цього — це підлога/горизонталь")]
    [SerializeField] private float floorDotThreshold = 0.7f;
    [Tooltip("Якщо нормаль майже горизонтальна — це стіна")]
    [SerializeField] private float wallDotThreshold = 0.5f;

    public bool TryGetSnapPoint(Ray ray, PropTemplate item, out SnapResult result)
    {
        result = default;
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, surfaceMask))
            return false;

        float upDot = Vector3.Dot(hit.normal, Vector3.up);

        if (upDot >= floorDotThreshold)
        {
            // Горизонтальна поверхня: підлога або полиця
            bool isShelf = hit.collider.GetComponentInParent<Shelf>() != null;

            if (isShelf && item.canSnapToShelf)
                result = BuildShelfSnap(hit, item);
            else
                result = BuildFloorSnap(hit, item);
        }
        else if (Mathf.Abs(upDot) < wallDotThreshold && item.canSnapToWall)
        {
            // Вертикальна поверхня — стіна
            result = BuildWallSnap(hit, item);
        }
        else
        {
            // Поверхня є але item не підходить для неї — підлога як fallback
            result = BuildFloorSnap(hit, item);
        }

        result.isValid = true;
        return true;
    }

    private SnapResult BuildFloorSnap(RaycastHit hit, PropTemplate item) => new SnapResult
    {
        position = hit.point + Vector3.up * item.pivotOffset,
        rotation = Quaternion.identity
    };

    private SnapResult BuildWallSnap(RaycastHit hit, PropTemplate item)
    {
        var rot = Quaternion.LookRotation(-hit.normal, Vector3.up);
        return new SnapResult
        {
            position = hit.point + hit.normal * item.wallOffset,
            rotation = rot
        };
    }

    private SnapResult BuildShelfSnap(RaycastHit hit, PropTemplate item) => new SnapResult
    {
        position = hit.point + Vector3.up * item.shelfOffset,
        rotation = Quaternion.identity
    };
}