// Assets/Scripts/World/Shelf/Shelf.cs
//
// DATA-DRIVEN SHELF — v4.2
//
// v4.2 ЗМІНИ vs v4.1:
//   • Прибрано всю висування-логіку (zExtra, hoverExtrudeZ, hoverAnimDuration,
//     SetHoveredBookIndex, UpdateHoverAnimation)
//   • Підсвітка через ghost: MaterializeBookForInteraction створює ПОВНОЦІННИЙ
//     візуальний ghost (з MeshRenderer + 2 матеріали + BookWorldItem + Collider).
//     HoverHighlighter автоматично підхопить його через raycast.
//   • OnValidate hook: зміни spacingOffset/maxRandomTilt у Inspector
//     одразу перебудовують layout під час Play
//   • Ghost з offset +0.001м по Z щоб уникнути z-fighting з реально-рендереною книгою
//
// АРХІТЕКТУРА:
//   • 0 GameObject для звичайних книг (тільки ShelfBookEntry struct)
//   • Рендеринг через BookInstancedRenderer
//   • Hit-test через ShelfRayMath OBB raycast
//   • Ghost-on-Demand: повноцінний візуальний GO для context menu + підсвітка

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class Shelf : MonoBehaviour
{
    // ───────────────────────────────────────────────────────────────────
    // INSPECTOR
    // ───────────────────────────────────────────────────────────────────

    [Header("Components")]
    public Transform startPoint;
    public BookGeometryProfile geometry;

    [Header("Placement Settings")]
    [Range(0f, 15f)]
    [Tooltip("Максимальний випадковий нахил книги по X-axis у градусах.")]
    public float maxRandomTilt = 3f;

    [Range(0f, 0.02f)]
    [Tooltip("Відстань між сусідніми книгами на полиці в метрах.")]
    public float spacingOffset = 0.002f;

    [Header("Book Size Restriction")]
    public BookSize maxBookSize = BookSize.Large;

    [Header("Fallback")]
    [SerializeField] private float defaultBookThickness = 0.03f;
    [SerializeField] private float defaultBookHeight    = 0.24f;
    [SerializeField] private float defaultBookDepth     = 0.15f;

    [Header("Interaction")]
    [Tooltip("Layer для ghost-GO. Має бути в interactionLayer InteractionRouter і HoverHighlighter.")]
    [SerializeField] private int bookLayer = 0;

    [Tooltip("Зсув ghost вперед по Z щоб уникнути z-fighting з рендереною книгою.")]
    [Range(0f, 0.005f)]
    [SerializeField] private float ghostZOffset = 0.001f;

    // ───────────────────────────────────────────────────────────────────
    // RUNTIME STATE
    // ───────────────────────────────────────────────────────────────────

    private List<ShelfBookEntry> _books = new List<ShelfBookEntry>(32);

    private BookInstancedRenderer _renderer;

    private BookWorldItem _materializedBook;
    [System.NonSerialized] public BookWorldItem _materializedBookRef;


    // ───────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ───────────────────────────────────────────────────────────────────

    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Shelves");
 
    // НЕ вимикаємо компонент якщо startPoint не призначено — 
    // це вимикає і BookInstancedRenderer на тому ж GO
    if (startPoint == null)
        Debug.LogError($"[Shelf] StartPoint not assigned on {gameObject.name}! Assign it in Inspector.");
    // enabled = false; // ← ВИДАЛИТИ ЦЕЙ РЯДОК


        _renderer = GetComponent<BookInstancedRenderer>();
        if (_renderer == null)
            Debug.LogWarning($"[Shelf] '{gameObject.name}': немає BookInstancedRenderer. Книги невидимі.");

        if (geometry == null)
            Debug.LogError($"[Shelf] BookGeometryProfile не призначено на '{gameObject.name}'!");
    }
    private void Start()
    {
        if (_books != null && _books.Count > 0)
        {
            RebuildLayout();
            PushToRenderer();
        }
    }
    private void OnEnable()  => ShelfRegistry.Instance?.Register(this);
    private void OnDisable()
    {
        ShelfRegistry.Instance?.Unregister(this);
        HideHoverCube();
    }

    private void OnDestroy()
    {
        HideHoverCube();
        if (_materializedBook != null) DematerializeBook(_materializedBook);
    }

    // ── OnValidate: real-time зміна spacing/tilt в Inspector ─────────────
#if UNITY_EDITOR
    private void OnValidate()
    {
        // OnValidate може викликатись до Awake — захищаємось
        if (!Application.isPlaying) return;
        if (_books == null || _books.Count == 0) return;

        // Затримуємо виклик на 1 кадр щоб уникнути проблем зі змінами Inspector
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return; // об'єкт міг бути знищений
            RebuildLayout();
            PushToRenderer();
        };
    }
#endif

    // ───────────────────────────────────────────────────────────────────
    // CAN FIT BOOK
    // ───────────────────────────────────────────────────────────────────

    public bool CanFitBook(BookTemplate template)
    {
        if (template == null || geometry == null) return false;
        if (template.bookSize > maxBookSize) return false;

        Vector3 realSize = GetRealSizeFromTemplate(template);
        return CanFitByThickness(realSize.x);
    }

    public bool CanFitBook(GameObject bookPrefab)
    {
        if (bookPrefab == null || geometry == null) return false;

        BookTemplate found = null;
        if (BookDatabase.Instance != null)
            foreach (var bt in BookDatabase.Instance.allBooks)
                if (bt?.containerPrefab == bookPrefab) { found = bt; break; }

        if (found != null) return CanFitBook(found);
        return CanFitByThickness(geometry.baseSize.x);
    }

    private bool CanFitByThickness(float thickness)
    {
        return (GetTotalUsedWidth() + thickness + spacingOffset) <= GetShelfWorldWidth() + 1e-5f;
    }

    public string GetSizeRejectReason(BookTemplate template)
    {
        if (template == null || template.bookSize <= maxBookSize) return "";
        string bs = template.bookSize switch
        {
            BookSize.Small  => "Маленька",
            BookSize.Medium => "Середня",
            BookSize.Large  => "Велика",
            _               => template.bookSize.ToString()
        };
        return $"Книга ({bs}) не вміщується на цю полицю.";
    }

    // ───────────────────────────────────────────────────────────────────
    // PLACE / TAKE
    // ───────────────────────────────────────────────────────────────────

    public void PlaceBook(BookInstance instance)
    {
        if (instance == null) { Debug.LogError($"[Shelf] PlaceBook: instance=null на {name}!"); return; }
        if (geometry == null) { Debug.LogError($"[Shelf] PlaceBook: geometry=null на {name}!"); return; }

        BookTemplate template = BookDatabase.Instance?.GetBook(instance.templateID);
        if (template == null)
        {
            Debug.LogWarning($"[Shelf] PlaceBook: невідомий templateID '{instance.templateID}'");
            return;
        }
        if (!CanFitBook(template))
        {
            Debug.Log($"[Shelf] '{template.title}' не вмістилась у {gameObject.name}.");
            return;
        }

        Vector3 realSize = GetRealSizeFromTemplate(template);

        var entry = new ShelfBookEntry
        {
            instanceID = instance.instanceID,
            templateID = instance.templateID,
            thickness  = realSize.x,
            height     = realSize.y,
            depth      = realSize.z,
            tilt       = Random.Range(-maxRandomTilt, maxRandomTilt),
            colorIndex = template.colorIndex,
        };

        _books.Add(entry);
        RebuildLayout();
        PushToRenderer();
    }

    /// <summary>LEGACY: prefab ігнорується.</summary>
    public void PlaceBook(BookInstance instance, GameObject prefab) => PlaceBook(instance);

    public BookInstance TakeLastBook() => _books.Count == 0 ? null : TakeBookAt(_books.Count - 1);

    public BookInstance TakeBookAt(int index)
    {
        if (index < 0 || index >= _books.Count) return null;

        ShelfBookEntry entry = _books[index];

        if (_materializedBook != null && _materializedBook.bookIndex == index)
            DematerializeBook(_materializedBook);

        _books.RemoveAt(index);

        if (_materializedBook != null && _materializedBook.bookIndex > index)
            _materializedBook.bookIndex--;

        RebuildLayout();
        PushToRenderer();

        var inst = new BookInstance(entry.templateID);
        inst.instanceID = entry.instanceID;
        return inst;
    }

    public BookInstance RemoveBook(BookWorldItem worldItem)
    {
        if (worldItem == null) return null;
        if (worldItem.parentShelf != this)
        {
            Debug.LogWarning($"[Shelf] RemoveBook: BookWorldItem не належить цій полиці.");
            return null;
        }
        return TakeBookAt(worldItem.bookIndex);
    }

    public BookInstance RemoveBook(GameObject bookObj)
    {
        if (bookObj == null) return null;
        var wi = bookObj.GetComponent<BookWorldItem>()
              ?? bookObj.GetComponentInParent<BookWorldItem>();
        if (wi == null) return null;
        return RemoveBook(wi);
    }

    // ───────────────────────────────────────────────────────────────────
    // HIT-TEST
    // ───────────────────────────────────────────────────────────────────
/*
    public int GetBookIndexAtPoint(Vector3 worldPoint)
    {
        if (startPoint == null || _books.Count == 0) return -1;

        Vector3 toPoint = worldPoint - startPoint.position;
        float   clickX  = Vector3.Dot(toPoint, startPoint.right);

        float cursor = 0f;
        for (int i = 0; i < _books.Count; i++)
        {
            float right = cursor + _books[i].thickness;
            if (clickX >= cursor - 0.005f && clickX <= right + 0.005f) return i;
            cursor = right + spacingOffset;
        }
        return -1;
    }
*/
    /// <summary>Точний OBB hit-test з поверненням відстані входу tMin.
    /// Використовується ShelfInteractionHandler для вибору найближчої книги по всіх Shelf.</summary>
    public bool HitTestRayWithDistance(Ray worldRay, out int bookIndex, out float tMin)
    {
        bookIndex = -1;
        tMin      = float.PositiveInfinity;

        if (startPoint == null || _books.Count == 0) return false;

        Vector3 originLocal = startPoint.InverseTransformPoint(worldRay.origin);
        Vector3 dirLocal    = startPoint.InverseTransformDirection(worldRay.direction);

#if UNITY_EDITOR
        if (_debugHitTest && _books.Count > 0)
        {
            var e0 = _books[0];
            Debug.Log($"[Shelf '{name}'] HitTestRay debug:\n" +
                      $"  worldRay.origin={worldRay.origin}\n" +
                      $"  worldRay.dir={worldRay.direction}\n" +
                      $"  startPoint.position={startPoint.position}\n" +
                      $"  startPoint.right={startPoint.right}\n" +
                      $"  startPoint.up={startPoint.up}\n" +
                      $"  startPoint.forward={startPoint.forward}\n" +
                      $"  originLocal={originLocal}\n" +
                      $"  dirLocal={dirLocal}\n" +
                      $"  book[0]: localPos={e0.localPosition}, thick={e0.thickness:F4}, h={e0.height:F4}, d={e0.depth:F4}");
        }
#endif

        for (int i = 0; i < _books.Count; i++)
        {
            var e = _books[i];

            // ТЕСТ: спочатку просто перевіряємо X і Y slab (плоский фронт книги)
            // щоб зрозуміти чи X/Y локального простору правильні.
            // Якщо це працює — проблема у Z slab (глибина) і в орієнтації startPoint.forward.
            Vector3 size = new Vector3(e.thickness, e.height, e.depth);

            if (ShelfRayMath.RayOBBIntersect(
                    originLocal, dirLocal,
                    e.localPosition.x, 0f, size, e.tilt,
                    out float t)
                && t < tMin)
            {
                tMin      = t;
                bookIndex = i;
            }
        }
        return bookIndex >= 0;
    }

    [Header("Debug")]
    [Tooltip("Виводить детальний лог HitTestRay для першої книги. Вмикай тільки для діагностики.")]
    [SerializeField] private bool _debugHitTest = false;

    public int HitTestRay(Ray worldRay)
    {
        if (startPoint == null || _books.Count == 0) return -1;

        Vector3 originLocal = startPoint.InverseTransformPoint(worldRay.origin);
        Vector3 dirLocal    = startPoint.InverseTransformDirection(worldRay.direction);

        int   bestIdx = -1;
        float bestT   = float.PositiveInfinity;

        for (int i = 0; i < _books.Count; i++)
        {
            var e = _books[i];
            // Геометрія книги на полиці у локальному просторі startPoint:
            //   X = thickness (вздовж довжини полиці)
            //   Y = height
            //   Z = depth (в полицю)
            Vector3 size = new Vector3(e.thickness, e.height, e.depth);

            if (ShelfRayMath.RayOBBIntersect(
                    originLocal, dirLocal,
                    e.localPosition.x, 0f, size, e.tilt,
                    out float t)
                && t < bestT)
            {
                bestT   = t;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    // ───────────────────────────────────────────────────────────────────
    // GHOST-ON-DEMAND (повноцінний візуальний ghost для підсвітки + context menu)
    // ───────────────────────────────────────────────────────────────────

    // ───────────────────────────────────────────────────────────────────
    // HOVER VISUAL — простий прозорий cube +5% (без collider, без BookWorldItem)
    // Клік проходить через нього і потрапляє в shelf BoxCollider як зазвичай.
    // ───────────────────────────────────────────────────────────────────

    [Header("Hover Visual")]
    [Header("Hover Visual")]
    [Tooltip("Layer для hover-ghost. Стандартно: PlacedBooks.\n" +
             "Має бути в HoverHighlighter.interactionLayer,\n" +
             "АЛЕ НЕ в InteractionRouter.interactionLayer (бо клік ловиться окремо).")]
    [SerializeField] private string hoverGhostLayer = "PlacedBooks";

    [Tooltip("Наскільки збільшити collider відносно реального розміру книги (1.05 = +5%).")]
    [Range(1.0f, 1.5f)]
    [SerializeField] private float hoverColliderScale = 1.05f;

    private GameObject _hoverGhost;
    private int        _hoverGhostIndex = -1;

    /// <summary>
    /// Показати hover-ghost на книзі. -1 = сховати.
    ///
    /// Ghost = повноцінний GO з:
    ///   • MeshRenderer + 2 матеріали (як справжня книга) — для OutlineTarget overlay
    ///   • BoxCollider (IsTrigger=true, +5% розмір) — для raycast HoverHighlighter
    ///   • BookWorldItem — HoverHighlighter знаходить його через пріоритет 1
    ///   • OutlineTarget — overlay shader для підсвітки (та сама система що меблі)
    ///
    /// HoverHighlighter автоматично знайде BookWorldItem на ghost через raycast
    /// і підсвітить його через OutlineTarget overlay.
    ///
    /// Layer PlacedBooks НЕ повинен бути в InteractionRouter.interactionLayer —
    /// інакше клік буде ловити ghost замість Shelf.
    /// </summary>
    public void ShowHoverCube(int index)
    {
        if (index < 0 || index >= _books.Count)
        {
            HideHoverCube();
            return;
        }
        if (_hoverGhostIndex == index && _hoverGhost != null) return;

        HideHoverCube();

        if (geometry == null || geometry.baseMesh == null) return;

        ShelfBookEntry entry = _books[index];

        _hoverGhost = new GameObject($"HoverGhost_{entry.templateID}");
        int layer = LayerMask.NameToLayer(hoverGhostLayer);
        if (layer >= 0) _hoverGhost.layer = layer;

        _hoverGhost.transform.SetParent(startPoint, false);
        _hoverGhost.transform.localPosition = entry.localPosition + new Vector3(0f, 0f, ghostZOffset);
        _hoverGhost.transform.localRotation = Quaternion.Euler(0f, 0f, entry.tilt)
                                              * Quaternion.Euler(geometry.meshRotationFix);

        Vector3 baseSize = geometry.baseSize;
        _hoverGhost.transform.localScale = new Vector3(
            entry.thickness / Mathf.Max(baseSize.x, 1e-6f),
            entry.height    / Mathf.Max(baseSize.y, 1e-6f),
            entry.depth     / Mathf.Max(baseSize.z, 1e-6f));

        // MeshRenderer (для OutlineTarget overlay)
        var mf = _hoverGhost.AddComponent<MeshFilter>();
        mf.sharedMesh = geometry.baseMesh;

        var mr = _hoverGhost.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        Material coverMat = geometry.GetCoverMaterial(entry.colorIndex);
        Material pagesMat = geometry.pagesMaterial;
        if (geometry.baseMesh.subMeshCount > 1 && pagesMat != null)
            mr.sharedMaterials = new[] { coverMat, pagesMat };
        else
            mr.sharedMaterial = coverMat;

        // Collider — IsTrigger щоб не блокувати фізику; +5% для легшого hover
        var box = _hoverGhost.AddComponent<BoxCollider>();
        box.size      = new Vector3(baseSize.x * hoverColliderScale,
                                    baseSize.y * hoverColliderScale,
                                    baseSize.z * hoverColliderScale);
        box.center    = new Vector3(0f, baseSize.y * 0.5f, 0f);
        box.isTrigger = true;

        // BookWorldItem — HoverHighlighter знаходить його через пріоритет 1
        var wi = _hoverGhost.AddComponent<BookWorldItem>();
        wi.instance          = BuildInstance(entry);
        wi.parentShelf       = this;
        wi.bookIndex         = index;
        wi.savedTilt         = entry.tilt;
        wi.isBeingInteracted = false; // це hover, не click

        _hoverGhostIndex = index;
    }

    public void HideHoverCube()
    {
        if (_hoverGhost != null) Destroy(_hoverGhost);
        _hoverGhost      = null;
        _hoverGhostIndex = -1;
    }

    // ───────────────────────────────────────────────────────────────────
    // GHOST-ON-DEMAND для контекстного меню
    // (повноцінний BookWorldItem для ContextMenuUI / NPC reservation)
    // ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Створює invisible-ghost з BookWorldItem на місці книги.
    /// Використовується ТІЛЬКИ для context menu — не для hover.
    /// </summary>
    public void MaterializeBookForInteraction(int index, GameObject _ignored = null)
    {
        if (index < 0 || index >= _books.Count || geometry == null) return;

        if (_materializedBook != null) DematerializeBook(_materializedBook);

        ShelfBookEntry entry = _books[index];

        // Порожній GO без MeshRenderer — реальна книга вже рендериться через Renderer
        GameObject ghost = new GameObject($"GhostBook_{entry.templateID}");
        ghost.transform.SetParent(startPoint, false);
        ghost.transform.localPosition = entry.localPosition;
        ghost.transform.localRotation = Quaternion.Euler(0f, 0f, entry.tilt);
        ghost.transform.localScale    = Vector3.one;

        SetLayerRecursive(ghost, bookLayer);

        var wi = ghost.AddComponent<BookWorldItem>();
        wi.instance          = BuildInstance(entry);
        wi.parentShelf       = this;
        wi.bookIndex         = index;
        wi.savedTilt         = entry.tilt;
        wi.isBeingInteracted = true;

        _materializedBook    = wi;
        _materializedBookRef = wi;
    }

    public BookWorldItem GetOrMaterializeBookForInteraction(int index, GameObject _ignored = null)
    {
        if (index < 0 || index >= _books.Count) return null;
        if (_materializedBook != null && _materializedBook.bookIndex == index)
            return _materializedBook;
        MaterializeBookForInteraction(index);
        return _materializedBookRef;
    }

    public void DematerializeBook(BookWorldItem item)
    {
        if (item == null) return;
        if (_materializedBook    == item) _materializedBook    = null;
        if (_materializedBookRef == item) _materializedBookRef = null;
        if (item.gameObject != null) Destroy(item.gameObject);
    }

    // ───────────────────────────────────────────────────────────────────
    // PUBLIC GETTERS
    // ───────────────────────────────────────────────────────────────────

    public ShelfBookEntry GetBookData(int index) =>
        (index >= 0 && index < _books.Count) ? _books[index] : default;

    public IReadOnlyList<ShelfBookEntry> GetAllBookData() => _books;

    /// <summary>LEGACY — повертає null. GO книг не існує в data-driven архітектурі.</summary>
    public GameObject GetBookGO(int index) => null;

    public int GetBookCount() => _books.Count;

    public float GetShelfWorldWidth()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) { Debug.LogWarning($"[Shelf] BoxCollider не знайдено на {name}"); return 1f; }
        return col.size.x * transform.lossyScale.x;
    }

    public float GetTotalUsedWidth()
    {
        float total = 0f;
        for (int i = 0; i < _books.Count; i++)
            total += _books[i].thickness + spacingOffset;
        return total;
    }

    public float GetFreeWidth() => GetShelfWorldWidth() - GetTotalUsedWidth();

    public float GetFillRatio()
    {
        float w = GetShelfWorldWidth();
        return w > 0f ? Mathf.Clamp01(GetTotalUsedWidth() / w) : 0f;
    }

    public int GetBookIndexAtPoint(Vector3 worldPoint)
    {
        if (startPoint == null || _books == null || _books.Count == 0) return -1;
    
        // Переводимо world point у локальний простір startPoint
        Vector3 localPoint = startPoint.InverseTransformPoint(worldPoint);
    
        // Шукаємо книгу чий X-діапазон містить localPoint.x
        float cursor = 0f;
        for (int i = 0; i < _books.Count; i++)
        {
            float left  = cursor;
            float right = cursor + _books[i].thickness;
    
            if (localPoint.x >= left && localPoint.x <= right)
                return i;
    
            cursor = right + spacingOffset;
        }
        return -1;
    }


    public ShopZoneType ZoneType
    {
        get { var z = GetComponentInParent<ShopZone>(); return z != null ? z.zoneType : ShopZoneType.Storefront; }
    }

    // ───────────────────────────────────────────────────────────────────
    // RESERVATION
    // ───────────────────────────────────────────────────────────────────

    public bool ReserveBook(int index, string npcID)
    {
        if (index < 0 || index >= _books.Count || _books[index].isReserved) return false;
        var e = _books[index]; e.isReserved = true; e.reservedByID = npcID; _books[index] = e;
        return true;
    }

    public void UnreserveBook(int index)
    {
        if (index < 0 || index >= _books.Count) return;
        var e = _books[index]; e.isReserved = false; e.reservedByID = string.Empty; _books[index] = e;
    }

    public int FindAvailableBookIndex(string templateID)
    {
        for (int i = 0; i < _books.Count; i++)
            if (_books[i].templateID == templateID && !_books[i].isReserved) return i;
        return -1;
    }

    // ───────────────────────────────────────────────────────────────────
    // SAVE / LOAD
    // ───────────────────────────────────────────────────────────────────

    public ShelfSaveEntry CollectSaveData()
    {
        var entry = new ShelfSaveEntry();
        entry.shelfID = $"{name}_{transform.GetSiblingIndex()}";
        foreach (var book in _books)
        {
            entry.templateIDs.Add(book.templateID);
            entry.instanceIDs.Add(book.instanceID);
        }
        return entry;
    }

    public void LoadFromSaveEntry(ShelfSaveEntry saveEntry)
    {
        _books.Clear();
        if (_materializedBook != null) DematerializeBook(_materializedBook);

        for (int i = 0; i < saveEntry.templateIDs.Count; i++)
        {
            string tid = saveEntry.templateIDs[i];
            string iid = i < saveEntry.instanceIDs.Count
                         ? saveEntry.instanceIDs[i]
                         : System.Guid.NewGuid().ToString();

            BookTemplate t = BookDatabase.Instance?.GetBook(tid);
            if (t == null) { Debug.LogWarning($"[Shelf] Template not found: '{tid}'"); continue; }

            Vector3 realSize = GetRealSizeFromTemplate(t);

            _books.Add(new ShelfBookEntry
            {
                instanceID = iid,
                templateID = tid,
                thickness  = realSize.x,
                height     = realSize.y,
                depth      = realSize.z,
                tilt       = Random.Range(-maxRandomTilt, maxRandomTilt),
                colorIndex = t.colorIndex,
            });
        }

        RebuildLayout();
        PushToRenderer();
    }

    // ───────────────────────────────────────────────────────────────────
    // LAYOUT
    // ───────────────────────────────────────────────────────────────────

    private void RebuildLayout()
    {
        if (startPoint == null) return;

        // localPosition у локальному просторі startPoint.
        // Unity сам застосує startPoint.localToWorldMatrix (включно з lossyScale)
        // при рендерингу. Тому ділити на scale НЕ ПОТРІБНО — це б дало подвійну компенсацію.
        //
        // Формула: книга центрована на (cursor + thickness/2), наступна — після proxy + spacing.
        float currentX = 0f;
        for (int i = 0; i < _books.Count; i++)
        {
            var e = _books[i];
            e.localPosition = new Vector3(currentX + e.thickness * 0.5f, 0f, 0f);
            _books[i] = e;
            currentX += e.thickness + spacingOffset;
        }
    }

    public void PushToRenderer()
    {
        if (_renderer != null && geometry != null)
            _renderer.RebuildFromEntries(_books, startPoint, geometry);
    }

    // ───────────────────────────────────────────────────────────────────
    // HELPERS
    // ───────────────────────────────────────────────────────────────────

    private Vector3 GetRealSizeFromTemplate(BookTemplate template)
    {
        if (template == null || geometry == null)
            return new Vector3(defaultBookThickness, defaultBookHeight, defaultBookDepth);

        if (template.containerPrefab == null)
            return geometry.baseSize;

        return geometry.GetRealSizeFromPrefab(template.containerPrefab);
    }

    private static BookInstance BuildInstance(in ShelfBookEntry e)
    {
        var inst = new BookInstance(e.templateID);
        inst.instanceID = e.instanceID;
        return inst;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer == 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // ───────────────────────────────────────────────────────────────────
    // GIZMOS
    // ───────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (startPoint == null) return;
        float sw = GetShelfWorldWidth(), uw = GetTotalUsedWidth();
        Gizmos.color = Color.green;
        Gizmos.DrawLine(startPoint.position + startPoint.right * uw,
                        startPoint.position + startPoint.right * sw);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(startPoint.position, startPoint.position + startPoint.right * uw);
        float pct = sw > 0 ? uw / sw * 100f : 0f;
        UnityEditor.Handles.Label(startPoint.position + Vector3.up * 0.15f,
            $"{GetBookCount()} книг | {pct:F0}% | max:{maxBookSize}");
    }
#endif
}