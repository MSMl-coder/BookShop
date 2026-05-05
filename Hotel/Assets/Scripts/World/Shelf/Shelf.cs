using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class Shelf : MonoBehaviour
{
    [Header("Components")]
    public Transform startPoint;

    [Header("Placement Settings")]
    [Tooltip("Book rotation for this shelf")]
    public Vector3 bookRotation = Vector3.zero;

    [Range(0f, 15f)]
    [Tooltip("Random tilt along shelf for realism")]
    public float maxRandomTilt = 3f;

    [Tooltip("Spacing between books in world units")]
    public float spacingOffset = 0.002f;

    [Tooltip("Book entry animation speed")]
    public float animationSpeed = 5f;

    [Header("Visual Indicator")]
    [SerializeField] private GameObject fillIndicatorPrefab; // assign simple quad prefab
    [SerializeField] private Color freeColor  = new Color(0f, 1f, 0f, 0.3f);
    [SerializeField] private Color fullColor  = new Color(1f, 0f, 0f, 0.3f);

    // ── internal state ──────────────────────────────────────────────
    private List<BookData>     _bookData   = new List<BookData>();   // parallel to visuals
    private List<GameObject>   _placedBookVisuals = new List<GameObject>();
    private List<Vector3>      _finalScales       = new List<Vector3>(); // target scales
    private GameObject         _indicator;
    private MeshRenderer       _indicatorRenderer;

    // small helper: book entry is animating → skip RefreshPositions scale overwrite
    private HashSet<GameObject> _animating = new HashSet<GameObject>();

    // ── data struct kept locally so Shelf owns its books ─────────────
    private struct BookData
    {
        public BookInstance instance;
    }

    // ═══════════════════════════════════════════════════════════════
    #region Unity lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Shelves");

        if (startPoint == null)
        {
            Debug.LogError($"[Shelf] StartPoint not assigned on {gameObject.name}!");
            enabled = false;
        }
    }

    private void Start()
    {
        SpawnIndicator();
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Public API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Returns true if the book prefab fits on remaining shelf space.</summary>
    public bool CanFitBook(GameObject bookPrefab)
    {
        if (bookPrefab == null) return false;

        float bookThickness  = GetPrefabThickness(bookPrefab);
        float worldShelfWidth = GetShelfWorldWidth();
        float usedWidth       = GetTotalUsedWidth();

        return (usedWidth + bookThickness + spacingOffset) <= worldShelfWidth;
    }

    /// <summary>Place a book instance onto this shelf.</summary>
    public void PlaceBook(BookInstance instance, GameObject prefab)
    {
        GameObject bookObj = Instantiate(prefab, startPoint);

        // Compensate parent lossy-scale so book keeps its original world size
        ApplyLossyScaleCompensation(bookObj.transform, prefab.transform.localScale);

        // Store final scale BEFORE animation mutates it
        Vector3 finalScale = bookObj.transform.localScale;
        _finalScales.Add(finalScale);

        // Book component
        var worldItem = bookObj.AddComponent<BookWorldItem>();
        worldItem.instance    = instance;
        worldItem.parentShelf = this;

        _placedBookVisuals.Add(bookObj);
        _bookData.Add(new BookData { instance = instance });

        // Position everything (skips animating books' scale)
        RefreshPositions();
        UpdateIndicator();

        // Now animate only this new book
        if (gameObject.activeInHierarchy)
            StartCoroutine(AnimateBookEntry(bookObj, finalScale));

        Debug.Log($"[Shelf] PlaceBook → total books: {_placedBookVisuals.Count}");
    }

    /// <summary>Remove and return the last book's instance data.</summary>
    public BookInstance TakeLastBook()
    {
        if (_placedBookVisuals.Count == 0) return null;

        int last    = _placedBookVisuals.Count - 1;
        var bookObj = _placedBookVisuals[last];
        var item    = bookObj?.GetComponent<BookWorldItem>();
        if (item == null) return null;

        BookInstance data = item.instance;

        _animating.Remove(bookObj);
        _placedBookVisuals.RemoveAt(last);
        _bookData.RemoveAt(last);
        _finalScales.RemoveAt(last);
        Destroy(bookObj);

        RefreshPositions();
        UpdateIndicator();
        return data;
    }

    /// <summary>Show / hide the free-space indicator (called by placement manager).</summary>
    public void SetIndicatorActive(bool active)
    {
        if (_indicator != null)
            _indicator.SetActive(active);
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Core positioning
    // ═══════════════════════════════════════════════════════════════

    public void RefreshPositions()
    {
        float  currentX = 0f;
        Vector3 pLossy  = startPoint.lossyScale;

        for (int i = 0; i < _placedBookVisuals.Count; i++)
        {
            GameObject bookObj = _placedBookVisuals[i];
            if (bookObj == null) continue;

            MeshFilter mf = bookObj.GetComponentInChildren<MeshFilter>();
            if (mf == null) continue;

            Bounds  b = mf.sharedMesh.bounds;

            // ── Use FINAL (target) scale, not current animated scale ──
            Vector3 s = (i < _finalScales.Count) ? _finalScales[i] : bookObj.transform.localScale;

            // ── Y: lift book so its mesh bottom sits exactly on the shelf surface ──
            // In world space:  bookWorldPivotY + b.min.y * s.y * pLossy.y = 0
            // → localY = -(b.min.y * s.y * pLossy.y) / pLossy.y
            //           = -b.min.y * s.y          (pLossy.y cancels for localPos calc)
            float localY = -(b.min.y * s.y);

            // ── X: lay books side by side along startPoint's -right axis ──
            float worldThickness = b.size.z * s.z * pLossy.z;
            float worldCentre    = currentX + worldThickness * 0.5f;
            float localX         = worldCentre / pLossy.x;

            bookObj.transform.localPosition = new Vector3(localX, localY, 0f);

            // ── Rotation + random tilt (seed per book index for stability) ──
            Random.State prevState = Random.state;
            Random.InitState(i * 1337);                       // deterministic per slot
            float tilt = Random.Range(-maxRandomTilt, maxRandomTilt);
            Random.state = prevState;

            bookObj.transform.localRotation = Quaternion.Euler(
                bookRotation.x + tilt,
                bookRotation.y,
                bookRotation.z);

            // ── Only overwrite scale if NOT currently animating ──
            if (!_animating.Contains(bookObj) && i < _finalScales.Count)
                bookObj.transform.localScale = _finalScales[i];

            currentX += worldThickness + spacingOffset;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Helpers
    // ═══════════════════════════════════════════════════════════════

    public float GetTotalUsedWidth()
    {
        float total = 0f;
        Vector3 pLossy = startPoint.lossyScale;

        for (int i = 0; i < _placedBookVisuals.Count; i++)
        {
            var b = _placedBookVisuals[i];
            if (b == null) continue;

            MeshFilter mf = b.GetComponentInChildren<MeshFilter>();
            if (mf == null) continue;

            Vector3 s = (i < _finalScales.Count) ? _finalScales[i] : b.transform.localScale;
            float thickness = mf.sharedMesh.bounds.size.z * s.z * pLossy.z;
            total += thickness + spacingOffset;
        }
        return total;
    }

    public float GetShelfWorldWidth()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        return col.size.x * transform.lossyScale.x;
    }

    private float GetPrefabThickness(GameObject prefab)
    {
        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        if (mf == null) return 0.03f;
        return mf.sharedMesh.bounds.size.z * prefab.transform.localScale.z;
    }

    private void ApplyLossyScaleCompensation(Transform target, Vector3 targetWorldScale)
    {
        Vector3 p = target.parent.lossyScale;
        target.localScale = new Vector3(
            targetWorldScale.x / p.x,
            targetWorldScale.y / p.y,
            targetWorldScale.z / p.z);
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Animation
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator AnimateBookEntry(GameObject book, Vector3 finalScale)
    {
        _animating.Add(book);

        Vector3 startScale = new Vector3(finalScale.x, 0f, finalScale.z);
        book.transform.localScale = startScale;

        float t = 0f;
        while (t < 1f)
        {
            if (book == null) { _animating.Remove(book); yield break; }
            t += Time.deltaTime * animationSpeed;
            book.transform.localScale = Vector3.Lerp(startScale, finalScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        if (book != null)
            book.transform.localScale = finalScale;

        _animating.Remove(book);
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Indicator
    // ═══════════════════════════════════════════════════════════════

    private void SpawnIndicator()
    {
        if (fillIndicatorPrefab == null) return;

        _indicator = Instantiate(fillIndicatorPrefab, startPoint);
        _indicatorRenderer = _indicator.GetComponent<MeshRenderer>();
        _indicator.SetActive(false);
        UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        if (_indicator == null || _indicatorRenderer == null) return;

        float used  = GetTotalUsedWidth();
        float total = GetShelfWorldWidth();
        float fill  = Mathf.Clamp01(used / total);

        // Scale indicator quad to show free space
        Vector3 pLossy = startPoint.lossyScale;
        float freeWorld = (total - used);
        _indicator.transform.localScale = new Vector3(
            freeWorld / pLossy.x,
            _indicator.transform.localScale.y,
            _indicator.transform.localScale.z);

        // Position at the free zone start
        _indicator.transform.localPosition = new Vector3(
            used / pLossy.x,
            _indicator.transform.localPosition.y,
            0f);

        // Color: green→red based on fill
        if (_indicatorRenderer != null)
            _indicatorRenderer.material.color = Color.Lerp(freeColor, fullColor, fill);
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Debug Gizmos
    // ═══════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (startPoint == null) return;

        float used  = GetTotalUsedWidth();
        float total = GetShelfWorldWidth();

        // Used space — red
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Vector3 usedCenter = startPoint.position
                           - startPoint.right * (used * 0.5f)
                           + Vector3.up * 0.1f;
        Gizmos.DrawCube(usedCenter, new Vector3(used, 0.05f, 0.05f));

        // Free space — green
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.4f);
        float free = total - used;
        Vector3 freeCenter = startPoint.position
                           - startPoint.right * (used + free * 0.5f)
                           + Vector3.up * 0.1f;
        Gizmos.DrawCube(freeCenter, new Vector3(free, 0.05f, 0.05f));

        // Shelf total boundary — white
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(
            startPoint.position - startPoint.right * (total * 0.5f),
            new Vector3(total, 0.02f, 0.02f));
    }
#endif

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Editor helpers
    // ═══════════════════════════════════════════════════════════════

    [ContextMenu("Clear Shelf")]
    private void EditorClearShelf()
    {
        foreach (var b in _placedBookVisuals)
            if (b != null) DestroyImmediate(b);

        _placedBookVisuals.Clear();
        _bookData.Clear();
        _finalScales.Clear();
        _animating.Clear();
        UpdateIndicator();
        Debug.Log("[Shelf] Cleared.");
    }

    [ContextMenu("Refresh Positions (Debug)")]
    private void EditorRefresh() => RefreshPositions();

    public int GetBookCount() => _placedBookVisuals.Count;

    // Та публічна властивість зони (для NPC Scanner)
    public ShopZoneType ZoneType
    {
        get
        {
            var zone = GetComponentInParent<ShopZone>();
            return zone != null ? zone.zoneType : ShopZoneType.Storefront;
        }
    }


    #endregion

    // Зберігає стан полиці
    public ShelfSaveEntry CollectSaveData()
    {
        var entry = new ShelfSaveEntry();
        entry.shelfID = GetInstanceID().ToString();

        foreach (var bookObj in _placedBookVisuals)
        {
            if (bookObj == null) continue;
            var item = bookObj.GetComponent<BookWorldItem>();
            if (item?.instance == null) continue;

            entry.templateIDs.Add(item.instance.templateID);
            entry.instanceIDs.Add(item.instance.instanceID);
        }
        return entry;
    }
}