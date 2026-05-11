// Assets/Scripts/World/Cabinet/CabinetCullingSystem.cs
// ВИПРАВЛЕНО CS0618: FindObjectsByType<T>(FindObjectsSortMode) → FindObjectsByType<T>()
using UnityEngine;

[AddComponentMenu("Bookstore/Cabinet Culling System")]
public class CabinetCullingSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;

    [Header("Culling Settings")]
    [SerializeField] private float cullDistance  = 20f;
    [SerializeField] private float updateInterval = 0.1f;

    private Cabinet[] _allCabinets;
    private Plane[]   _frustumPlanes = new Plane[6];
    private float     _nextUpdateTime;

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // ВИПРАВЛЕНО CS0618: прибрано FindObjectsSortMode параметр
        _allCabinets = FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude);
        Debug.Log($"[CabinetCulling] Registered {_allCabinets.Length} cabinets");
    }

    private void Update()
    {
        if (Time.time < _nextUpdateTime) return;
        _nextUpdateTime = Time.time + updateInterval;
        UpdateCulling();
    }

    private void UpdateCulling()
    {
        if (mainCamera == null || _allCabinets == null) return;

        GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustumPlanes);
        Vector3 camPos = mainCamera.transform.position;

        foreach (var cabinet in _allCabinets)
        {
            if (cabinet == null) continue;

            if (HasActiveInteraction(cabinet))
            {
                cabinet.SetRenderingEnabled(true);
                continue;
            }

            cabinet.SetRenderingEnabled(IsCabinetVisible(cabinet, camPos));
        }
    }

    private bool IsCabinetVisible(Cabinet cabinet, Vector3 camPos)
    {
        if (Vector3.Distance(camPos, cabinet.transform.position) > cullDistance) return false;
        return GeometryUtility.TestPlanesAABB(_frustumPlanes, cabinet.GetBounds());
    }

    private bool HasActiveInteraction(Cabinet cabinet)
    {
        foreach (var shelf in cabinet.shelves)
        {
            if (shelf != null && shelf._materializedBookRef != null)
                return true;
        }
        return false;
    }

    public void ForceUpdate()     => _nextUpdateTime = 0f;

    public void RefreshCabinetList()
    {
        // ВИПРАВЛЕНО CS0618
        _allCabinets = FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (mainCamera == null) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(mainCamera.transform.position, cullDistance);
    }
#endif
}