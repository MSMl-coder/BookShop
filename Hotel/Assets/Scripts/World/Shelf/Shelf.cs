// Assets/Scripts/World/Shelf/Shelf.cs — FIXED
// Fixes:
//   1. OnDrawGizmosSelected wrapped in #if UNITY_EDITOR (build compile error)
//   2. Added TakeBookByInstance() so NPCBrain can remove specific purchased book
//   3. Added PlaceBookSilent() + CommitLayout() for O(n) batch fill instead of O(n²)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class Shelf : MonoBehaviour
{
    [Header("Components")]
    public Transform startPoint;

    [Header("Placement Settings")]
    public Vector3 bookRotation = Vector3.zero;

    [Range(0f, 15f)]
    public float maxRandomTilt = 3f;

    public float spacingOffset = 0.002f;
    public float animationSpeed = 5f;

    [Header("Fallback")]
    [SerializeField] private float defaultBookThickness = 0.03f;

    private List<GameObject> _placedBookVisuals = new List<GameObject>();

    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Shelves");
        if (startPoint == null)
        {
            Debug.LogError($"[Shelf] StartPoint not assigned on {gameObject.name}!");
            enabled = false;
        }
    }

    // ── Public API ──────────────────────────────────────────────

    public bool CanFitBook(GameObject bookPrefab)
    {
        if (bookPrefab == null) return false;
        float bookThickness = GetPrefabThickness(bookPrefab);
        return (GetTotalUsedWidth() + bookThickness + spacingOffset) <= GetShelfWorldWidth();
    }

    public void PlaceBook(BookInstance instance, GameObject prefab)
    {
        PlaceBookSilent(instance, prefab);
        RefreshPositions();
        if (gameObject.activeInHierarchy)
            StartCoroutine(AnimateBookEntry(_placedBookVisuals[_placedBookVisuals.Count - 1]));
    }

    /// <summary>
    /// Places a book without triggering layout refresh or animation.
    /// Call CommitLayout() after batch placement.
    /// </summary>
    public void PlaceBookSilent(BookInstance instance, GameObject prefab)
    {
        if (prefab == null || instance == null || startPoint == null)
        {
            Debug.LogError($"[Shelf] PlaceBookSilent: null argument on {gameObject.name}");
            return;
        }

        GameObject obj = Instantiate(prefab, startPoint);
        ResetToWorldScale(obj.transform, prefab.transform.localScale);

        var worldItem = obj.AddComponent<BookWorldItem>();
        worldItem.instance    = instance;
        worldItem.parentShelf = this;
        worldItem.savedTilt   = Random.Range(-maxRandomTilt, maxRandomTilt);

        _placedBookVisuals.Add(obj);
    }

    /// <summary>Call after a batch of PlaceBookSilent calls.</summary>
    public void CommitLayout() => RefreshPositions();

    public BookInstance TakeLastBook()
    {
        _placedBookVisuals.RemoveAll(b => b == null);
        if (_placedBookVisuals.Count == 0) return null;

        int last = _placedBookVisuals.Count - 1;
        GameObject obj = _placedBookVisuals[last];
        var item = obj.GetComponent<BookWorldItem>();
        if (item == null)
        {
            Debug.LogWarning($"[Shelf] TakeLastBook: no BookWorldItem on {obj.name}");
            return null;
        }
        BookInstance data = item.instance;
        _placedBookVisuals.RemoveAt(last);
        Destroy(obj);
        RefreshPositions();
        return data;
    }

        public BookInstance RemoveBook(GameObject bookObj)
    {
        if (bookObj == null) return null;
 
        int idx = _placedBookVisuals.IndexOf(bookObj);
        if (idx < 0)
        {
            Debug.LogWarning($"[Shelf] RemoveBook: {bookObj.name} не знайдено на {gameObject.name}");
            return null;
        }
 
        BookWorldItem item = bookObj.GetComponent<BookWorldItem>();
        BookInstance  data = item?.instance;
 
        _placedBookVisuals.RemoveAt(idx);
        Destroy(bookObj);
        RefreshPositions();
 
        return data;
    }
 

    /// <summary>
    /// Removes a specific book by its BookInstance (used when NPC purchases it).
    /// Returns true if the book was found and removed.
    /// </summary>
    public bool TakeBookByInstance(BookInstance target)
    {
        if (target == null) return false;

        for (int i = 0; i < _placedBookVisuals.Count; i++)
        {
            if (_placedBookVisuals[i] == null) continue;
            var item = _placedBookVisuals[i].GetComponent<BookWorldItem>();
            if (item?.instance?.instanceID == target.instanceID)
            {
                Destroy(_placedBookVisuals[i]);
                _placedBookVisuals.RemoveAt(i);
                RefreshPositions();
                return true;
            }
        }

        Debug.LogWarning($"[Shelf] TakeBookByInstance: instanceID {target.instanceID} not found.");
        return false;
    }

    // ── Dimensions ──────────────────────────────────────────────

    public float GetShelfWorldWidth()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
        {
            Debug.LogWarning($"[Shelf] No BoxCollider on {gameObject.name}");
            return 1f;
        }
        return col.size.x * transform.lossyScale.x;
    }

    public int GetBookCount() => _placedBookVisuals.Count;

    public ShopZoneType ZoneType
    {
        get
        {
            var zone = GetComponentInParent<ShopZone>();
            return zone != null ? zone.zoneType : ShopZoneType.Storefront;
        }
    }

    public float GetTotalUsedWidth()
    {
        if (startPoint == null) return 0f;
        float total = 0f;
        Vector3 parentScale = startPoint.lossyScale;
        foreach (var obj in _placedBookVisuals)
        {
            if (obj == null) continue;
            total += GetRuntimeThickness(obj, parentScale) + spacingOffset;
        }
        return total;
    }

    public float GetFreeWidth()   => GetShelfWorldWidth() - GetTotalUsedWidth();
    public float GetFillRatio()
    {
        float w = GetShelfWorldWidth();
        return w <= 0f ? 0f : Mathf.Clamp01(GetTotalUsedWidth() / w);
    }

    // ── Save / Load ─────────────────────────────────────────────

    public ShelfSaveEntry CollectSaveData()
    {
        var entry = new ShelfSaveEntry();
        entry.shelfID = GetInstanceID().ToString();
        foreach (var obj in _placedBookVisuals)
        {
            if (obj == null) continue;
            var item = obj.GetComponent<BookWorldItem>();
            if (item?.instance == null) continue;
            entry.templateIDs.Add(item.instance.templateID);
            entry.instanceIDs.Add(item.instance.instanceID);
        }
        return entry;
    }

    // ── Layout ──────────────────────────────────────────────────

    public void RefreshPositions()
    {
        if (startPoint == null) return;
        float currentX = 0f;
        Vector3 parentScale = startPoint.lossyScale;

        foreach (var obj in _placedBookVisuals)
        {
            if (obj == null) continue;
            float worldThickness = GetRuntimeThickness(obj, parentScale);
            float localX = (currentX + (worldThickness / 2f)) / Mathf.Max(parentScale.x, 0.001f);
            float localY = GetBottomAlignedY(obj);
            obj.transform.localPosition = new Vector3(localX, localY, 0f);

            var wi = obj.GetComponent<BookWorldItem>();
            float tilt = wi != null ? wi.savedTilt : 0f;
            obj.transform.localRotation = Quaternion.Euler(bookRotation.x + tilt, bookRotation.y, bookRotation.z);
            currentX += worldThickness + spacingOffset;
        }
    }

    // ── Private Helpers ─────────────────────────────────────────

    private float GetPrefabThickness(GameObject prefab)
    {
        if (prefab == null) return defaultBookThickness;
        MeshFilter mf = prefab.GetComponent<MeshFilter>()
                     ?? prefab.GetComponentInChildren<MeshFilter>(includeInactive: true);
        if (mf != null && mf.sharedMesh != null)
            return mf.sharedMesh.bounds.size.z * prefab.transform.localScale.z;
        Renderer rend = prefab.GetComponentInChildren<Renderer>(includeInactive: true);
        if (rend != null && rend.bounds.size.z > 0.001f)
            return rend.bounds.size.z;
        return defaultBookThickness;
    }

    private float GetRuntimeThickness(GameObject obj, Vector3 parentScale)
    {
        if (obj == null) return defaultBookThickness;
        MeshFilter mf = obj.GetComponentInChildren<MeshFilter>(includeInactive: true);
        if (mf != null && mf.sharedMesh != null)
            return mf.sharedMesh.bounds.size.z * obj.transform.localScale.z * Mathf.Max(parentScale.z, 0.001f);
        return defaultBookThickness;
    }

    private float GetBottomAlignedY(GameObject obj)
    {
        if (obj == null) return 0f;
        MeshFilter mf = obj.GetComponentInChildren<MeshFilter>(includeInactive: true);
        if (mf != null && mf.sharedMesh != null)
            return -(mf.sharedMesh.bounds.min.y * obj.transform.localScale.y);
        return 0f;
    }

    private void ResetToWorldScale(Transform target, Vector3 targetWorldScale)
    {
        if (target == null || target.parent == null) return;
        Vector3 ps = target.parent.lossyScale;
        target.localScale = new Vector3(
            targetWorldScale.x / Mathf.Max(ps.x, 0.001f),
            targetWorldScale.y / Mathf.Max(ps.y, 0.001f),
            targetWorldScale.z / Mathf.Max(ps.z, 0.001f));
    }

    private IEnumerator AnimateBookEntry(GameObject book)
    {
        if (book == null) yield break;
        Vector3 target = book.transform.localScale;
        Vector3 start  = new Vector3(target.x, 0f, target.z);
        book.transform.localScale = start;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * animationSpeed;
            if (book == null) yield break;
            book.transform.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }
        if (book != null) book.transform.localScale = target;
    }

    // FIX: wrapped in #if UNITY_EDITOR — Handles.Label is editor-only API
    // Without this, the build fails with CS0234
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (startPoint == null) return;
        float shelfWidth = GetShelfWorldWidth();
        float usedWidth  = GetTotalUsedWidth();

        Vector3 freeStart = startPoint.position + (-startPoint.right * usedWidth);
        Vector3 freeEnd   = startPoint.position + (-startPoint.right * shelfWidth);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(freeStart, freeEnd);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(startPoint.position, freeStart);

        float pct = shelfWidth > 0 ? (usedWidth / shelfWidth * 100f) : 0f;
        UnityEditor.Handles.Label(
            startPoint.position + Vector3.up * 0.15f,
            $"{GetBookCount()} books | {pct:F0}%");
    }
#endif
}
