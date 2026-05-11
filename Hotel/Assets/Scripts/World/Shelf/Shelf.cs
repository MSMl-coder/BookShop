// Assets/Scripts/World/Shelf/Shelf.cs
// ВИПРАВЛЕНО всі помилки компілятора:
//   CS1061 _materializedBookRef — додано як [NonSerialized] public поле
//   CS1061 BookTemplate.height / .thickness — замінено на GetPrefabHeight()/GetPrefabThickness()
//   CS0618 GetInstanceID() — замінено на name + sibling index
//   CS0618 FindObjectsByType obsolete — не використовується тут (в CabinetCullingSystem виправлено окремо)
//   + maxBookSize (BookSize enum) — обмеження розміру книги на полицю (з попереднього чату)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class Shelf : MonoBehaviour
{
    [Header("Components")]
    public Transform startPoint;

    [Header("Placement Settings")]
    public Vector3 bookRotation  = Vector3.zero;
    [Range(0f, 15f)] public float maxRandomTilt = 3f;
    public float spacingOffset  = 0.002f;
    public float animationSpeed = 5f;

    [Header("Book Size Restriction")]
    [Tooltip("Максимальний розмір книги що вміщується на цю полицю.\n" +
             "Small — низька полиця, Medium — стандарт, Large — висока")]
    public BookSize maxBookSize = BookSize.Large;

    [Header("Fallback")]
    [SerializeField] private float defaultBookThickness = 0.03f;
    [SerializeField] private float defaultBookHeight    = 0.24f;

    // Data layer — дані книг (завжди актуальні)
    private List<ShelfBookEntry> _books   = new List<ShelfBookEntry>();
    // GO список — книги як реальні об'єкти (доки Instanced Renderer не підключений)
    private List<GameObject>     _bookGOs = new List<GameObject>();

    // Renderer — опціональний
    private BookInstancedRenderer _renderer;

    [Header("Optimization (вмикати тільки після налаштування BookInstancedRenderer)")]
    [Tooltip("false = книги як GameObjects (стабільно)\ntrue = GPU Instancing (потребує налаштування)")]
    [SerializeField] private bool _enableInstancing = false;

    // Instancing активний тільки якщо явно увімкнений І renderer знайдений
    private bool UseInstancing => _enableInstancing && _renderer != null;

    // Активно матеріалізована книга (тільки при взаємодії)
    private BookWorldItem _materializedBook;

    // ВИПРАВЛЕНО CS1061: public ref потрібен BookWorldItem.OnDestroy і CabinetCullingSystem
    [System.NonSerialized] public BookWorldItem _materializedBookRef;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Shelves");

        if (startPoint == null)
        {
            Debug.LogError($"[Shelf] StartPoint не призначено на {gameObject.name}!");
            enabled = false;
            return;
        }
        _renderer = GetComponentInParent<BookInstancedRenderer>()
                 ?? GetComponent<BookInstancedRenderer>();
    }

    // ── CanFitBook ────────────────────────────────────────────────────────────

    /// Зворотна сумісність з InventoryManager.PushOneToShelf(prefab)
    public bool CanFitBook(GameObject bookPrefab)
    {
        if (bookPrefab == null) return false;

        float bookThickness   = GetPrefabThickness(bookPrefab);
        float worldShelfWidth = GetShelfWorldWidth();
        float usedWidth       = GetTotalUsedWidth();
        if ((usedWidth + bookThickness + spacingOffset) > worldShelfWidth) return false;

        // Перевірка bookSize якщо знайдемо template по prefab
        if (BookDatabase.Instance != null)
            foreach (var bt in BookDatabase.Instance.allBooks)
                if (bt?.containerPrefab == bookPrefab)
                    return bt.bookSize <= maxBookSize;

        return true;
    }

    /// Новий API — швидший, перевіряє bookSize + ширину
    public bool CanFitBook(BookTemplate template)
    {
        if (template == null) return false;
        if (template.bookSize > maxBookSize) return false;
        if (template.containerPrefab == null) return true;

        float bookThickness   = GetPrefabThickness(template.containerPrefab);
        float worldShelfWidth = GetShelfWorldWidth();
        float usedWidth       = GetTotalUsedWidth();
        return (usedWidth + bookThickness + spacingOffset) <= worldShelfWidth;
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

    // ── PlaceBook / TakeBook ──────────────────────────────────────────────────

    public void PlaceBook(BookInstance instance, GameObject prefab)
    {
        if (instance == null) { Debug.LogError($"[Shelf] PlaceBook: instance=null на {name}!"); return; }
        if (prefab    == null) { Debug.LogError($"[Shelf] PlaceBook: prefab=null на {name}!");    return; }
        if (startPoint == null) { Debug.LogError($"[Shelf] PlaceBook: startPoint=null на {name}!"); return; }

        BookTemplate template = BookDatabase.Instance?.GetBook(instance.templateID);

        var entry = new ShelfBookEntry
        {
            instanceID    = instance.instanceID,
            templateID    = instance.templateID,
            thickness     = GetPrefabThickness(prefab),
            height        = GetPrefabHeight(prefab, template),
            tilt          = Random.Range(-maxRandomTilt, maxRandomTilt),
            coverColor    = GetCoverColor(template),
            prefabVariant = 0,
            isReserved    = false,
            reservedByID  = string.Empty,
        };

        _books.Add(entry);

        // Spawning real GO — works with and without Instanced Renderer
        if (gameObject.activeInHierarchy)
            StartCoroutine(SpawnBookGO(entry, _books.Count - 1, prefab, instance));
        else
            SpawnBookGOImmediate(entry, _books.Count - 1, prefab, instance);
    }

    public BookInstance TakeLastBook() =>
        _books.Count == 0 ? null : TakeBookAt(_books.Count - 1);

    public BookInstance TakeBookAt(int index)
    {
        if (index < 0 || index >= _books.Count) return null;

        ShelfBookEntry entry = _books[index];

        if (_materializedBook != null && _materializedBook.bookIndex == index)
            DematerializeBook(_materializedBook);

        _books.RemoveAt(index);

        // Destroy the GO for this book
        if (index < _bookGOs.Count)
        {
            if (_bookGOs[index] != null) Destroy(_bookGOs[index]);
            _bookGOs.RemoveAt(index);
        }

        if (_materializedBook != null && _materializedBook.bookIndex > index)
            _materializedBook.bookIndex--;

        RebuildLayout();
        PushToRenderer();
        RefreshGOPositions();

        var inst = new BookInstance(entry.templateID);
        inst.instanceID = entry.instanceID;
        return inst;
    }

    // ── Ghost-on-Demand ───────────────────────────────────────────────────────

    public int GetBookIndexAtPoint(Vector3 worldPoint)
    {
        if (startPoint == null || _books.Count == 0) return -1;

        Vector3 localPoint = startPoint.InverseTransformPoint(worldPoint);
        float   clickX     = localPoint.x * startPoint.lossyScale.x;

        float cursor = 0f;
        for (int i = 0; i < _books.Count; i++)
        {
            float right = cursor + _books[i].thickness;
            if (clickX >= cursor && clickX <= right) return i;
            cursor = right + spacingOffset;
        }
        return -1;
    }

    public void MaterializeBookForInteraction(int index, GameObject prefab)
    {
        if (index < 0 || index >= _books.Count || prefab == null) return;

        if (_materializedBook != null) DematerializeBook(_materializedBook);

        ShelfBookEntry entry  = _books[index];
        GameObject     bookGO = Instantiate(prefab, startPoint);
        ApplyBookTransform(bookGO.transform, entry);

        var worldItem = bookGO.AddComponent<BookWorldItem>();
        worldItem.instance          = BuildInstance(entry);
        worldItem.parentShelf       = this;
        worldItem.bookIndex         = index;
        worldItem.savedTilt         = entry.tilt;
        worldItem.isBeingInteracted = true;

        _materializedBook    = worldItem;
        _materializedBookRef = worldItem;  // public ref для CabinetCullingSystem

        StartCoroutine(AnimateHoverEntry(bookGO));
    }

    public void DematerializeBook(BookWorldItem item)
    {
        if (item == null) return;
        if (_materializedBook    == item) _materializedBook    = null;
        if (_materializedBookRef == item) _materializedBookRef = null;
        if (item.gameObject != null) Destroy(item.gameObject);
    }

    public ShelfBookEntry           GetBookData(int index) =>
        (index >= 0 && index < _books.Count) ? _books[index] : default;

    public IReadOnlyList<ShelfBookEntry> GetAllBookData() => _books;

    // ── Розміри ───────────────────────────────────────────────────────────────

    public float GetShelfWorldWidth()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) { Debug.LogWarning($"[Shelf] BoxCollider не знайдено на {name}"); return 1f; }
        return col.size.x * transform.lossyScale.x;
    }

    public int   GetBookCount()    => _books.Count;
    public float GetTotalUsedWidth()
    {
        float total = 0f;
        foreach (var b in _books) total += b.thickness + spacingOffset;
        return total;
    }
    public float GetFreeWidth()  => GetShelfWorldWidth() - GetTotalUsedWidth();
    public float GetFillRatio()
    {
        float w = GetShelfWorldWidth();
        return w > 0f ? Mathf.Clamp01(GetTotalUsedWidth() / w) : 0f;
    }

    public ShopZoneType ZoneType
    {
        get { var z = GetComponentInParent<ShopZone>(); return z != null ? z.zoneType : ShopZoneType.Storefront; }
    }

    // ── Резервування ──────────────────────────────────────────────────────────

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

    // ── Збереження / Завантаження ─────────────────────────────────────────────

    public ShelfSaveEntry CollectSaveData()
    {
        var entry = new ShelfSaveEntry();
        // ВИПРАВЛЕНО CS0618: GetInstanceID() deprecated → стабільний ID з імені та позиції
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
        ClearAllGOs();
        _books.Clear();
        for (int i = 0; i < saveEntry.templateIDs.Count; i++)
        {
            string tid = saveEntry.templateIDs[i];
            string iid = i < saveEntry.instanceIDs.Count
                         ? saveEntry.instanceIDs[i]
                         : System.Guid.NewGuid().ToString();

            BookTemplate t = BookDatabase.Instance?.GetBook(tid);
            if (t == null) { Debug.LogWarning($"[Shelf] Template not found: '{tid}'"); continue; }

            float thick = t.containerPrefab != null ? GetPrefabThickness(t.containerPrefab) : defaultBookThickness;
            float h     = t.containerPrefab != null ? GetPrefabHeight(t.containerPrefab, t) : defaultBookHeight;

            _books.Add(new ShelfBookEntry
            {
                instanceID = iid, templateID = tid,
                thickness  = thick, height   = h,
                tilt       = Random.Range(-maxRandomTilt, maxRandomTilt),
                coverColor = GetCoverColor(t),
            });
        }
        RebuildLayout();
        PushToRenderer();
    }

    // ── Layout / Renderer ─────────────────────────────────────────────────────

    private void RebuildLayout()
    {
        if (startPoint == null) return;
        float currentX = 0f;
        for (int i = 0; i < _books.Count; i++)
        {
            var e = _books[i];
            e.localPosition = new Vector3(
                (currentX + e.thickness * 0.5f) / Mathf.Max(startPoint.lossyScale.x, 0.001f),
                -(e.height * 0.5f)              / Mathf.Max(startPoint.lossyScale.y, 0.001f),
                0f);
            _books[i] = e;
            currentX += e.thickness + spacingOffset;
        }
    }

    private void PushToRenderer()
    {
        if (UseInstancing)
            _renderer.RebuildFromEntries(_books, startPoint, bookRotation);
        // Без Instanced Renderer — GO список є основним джерелом рендерингу
    }

    private void ApplyBookTransform(Transform t, in ShelfBookEntry e)
    {
        if (t == null || startPoint == null) return;
        t.localPosition = e.localPosition;
        t.localRotation = Quaternion.Euler(bookRotation.x + e.tilt, bookRotation.y, bookRotation.z);
        // НЕ чіпаємо localScale — префаб вже має правильний масштаб (Book001 scale 0.8 тощо).
        // ResetToWorldScale викликається окремо якщо потрібна компенсація lossy scale батька.
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// Товщина книги — враховує масштаб кореневого GO префаба (Book001.scale.z)
    /// і масштаб дочірнього меша (Book000.scale.z).
    private float GetPrefabThickness(GameObject prefab)
    {
        if (prefab == null) return defaultBookThickness;

        // Масштаб кореня префаба (Book001 може мати scale 0.8, 1.0, 0.6...)
        float rootScaleZ = prefab.transform.localScale.z;

        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null) continue;
            Mesh mesh = mf.sharedMesh;
            if (mesh == null) continue;

            // Масштаб дочірнього об'єкта відносно кореня
            float childScaleZ = mf.transform.localScale.z;

            // Товщина = meshBounds.z * childScale * rootScale
            float thickness = mesh.bounds.size.z * childScaleZ * rootScaleZ;
            if (thickness > 0.001f) return thickness;
        }

        Debug.LogWarning($"[Shelf] GetPrefabThickness: sharedMesh=null у '{prefab.name}'. " +
                         $"Використовую default={defaultBookThickness}m");
        return defaultBookThickness;
    }

    /// Висота книги — аналогічно враховує кореневий і дочірній масштаб.
    private float GetPrefabHeight(GameObject prefab, BookTemplate template)
    {
        if (prefab != null)
        {
            float rootScaleY = prefab.transform.localScale.y;

            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null) continue;
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;

                float childScaleY = mf.transform.localScale.y;
                float h = mesh.bounds.size.y * childScaleY * rootScaleY;
                if (h > 0.001f) return h;
            }
        }

        if (template != null)
            return template.bookSize switch
            {
                BookSize.Small  => 0.17f,
                BookSize.Medium => 0.24f,
                BookSize.Large  => 0.30f,
                _               => defaultBookHeight,
            };
        return defaultBookHeight;
    }

    private static Color GetCoverColor(BookTemplate t)
    {
        if (t == null) return new Color(0.5f, 0.35f, 0.2f);
        return t.genre switch
        {
            BookGenre.Fantasy   => new Color(0.31f, 0.33f, 0.75f),
            BookGenre.Horror    => new Color(0.55f, 0.10f, 0.10f),
            BookGenre.Mystery   => new Color(0.25f, 0.25f, 0.35f),
            BookGenre.Classic   => new Color(0.47f, 0.28f, 0.10f),
            BookGenre.SciFi     => new Color(0.10f, 0.40f, 0.55f),
            BookGenre.Biography => new Color(0.30f, 0.50f, 0.25f),
            BookGenre.Academic  => new Color(0.55f, 0.45f, 0.15f),
            _                   => new Color(0.5f, 0.35f, 0.2f),
        };
    }

    private static BookInstance BuildInstance(in ShelfBookEntry e)
    {
        var inst = new BookInstance(e.templateID);
        inst.instanceID = e.instanceID;
        return inst;
    }

    // ── GO управління ─────────────────────────────────────────────────────────

    /// Миттєво створює GO без анімації (при завантаженні або коли GO неактивний)
    private void SpawnBookGOImmediate(ShelfBookEntry entry, int index, GameObject prefab, BookInstance instance)
    {
        GameObject bookGO = Instantiate(prefab, startPoint);
        ApplyBookTransform(bookGO.transform, entry);
        SetupBookWorldItem(bookGO, entry, instance, index);

        // Синхронізуємо список GO з _books
        while (_bookGOs.Count <= index) _bookGOs.Add(null);
        if (_bookGOs[index] != null) Destroy(_bookGOs[index]);
        _bookGOs[index] = bookGO;
    }

    /// Створює GO з анімацією появи (виростає знизу вгору)
    private IEnumerator SpawnBookGO(ShelfBookEntry entry, int index, GameObject prefab, BookInstance instance)
    {
        GameObject bookGO = Instantiate(prefab, startPoint);
        ApplyBookTransform(bookGO.transform, entry);
        SetupBookWorldItem(bookGO, entry, instance, index);

        // Синхронізуємо список GO
        while (_bookGOs.Count <= index) _bookGOs.Add(null);
        if (_bookGOs[index] != null) Destroy(_bookGOs[index]);
        _bookGOs[index] = bookGO;

        // Якщо є Instanced Renderer — знищуємо GO після анімації
        // Якщо ні — GO залишається як основний рендер
        // Зберігаємо оригінальний масштаб префаба (не чіпаємо після ApplyBookTransform)
        Vector3 targetScale = bookGO.transform.localScale;
        Vector3 startScale  = new Vector3(targetScale.x, 0f, targetScale.z);
        bookGO.transform.localScale = startScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * animationSpeed;
            if (bookGO == null) yield break;
            bookGO.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        if (bookGO != null)
            bookGO.transform.localScale = targetScale;

        // Тільки якщо Instanced Renderer активний — переходимо на GPU рендеринг
        if (UseInstancing && bookGO != null)
        {
            _bookGOs[index] = null;
            Destroy(bookGO);
            PushToRenderer();
        }
    }

    private void SetupBookWorldItem(GameObject go, in ShelfBookEntry entry, BookInstance instance, int index)
    {
        if (!go.TryGetComponent<BookWorldItem>(out var wi))
            wi = go.AddComponent<BookWorldItem>();
        wi.instance    = instance;
        wi.parentShelf = this;
        wi.bookIndex   = index;
        wi.savedTilt   = entry.tilt;
    }

    /// Перераховує позиції існуючих GO після видалення книги
    private void RefreshGOPositions()
    {
        if (_bookGOs.Count == 0) return;
        for (int i = 0; i < Mathf.Min(_bookGOs.Count, _books.Count); i++)
        {
            if (_bookGOs[i] == null) continue;
            ApplyBookTransform(_bookGOs[i].transform, _books[i]);
        }
    }

    /// Очищає всі GO (при LoadFromSaveEntry)
    private void ClearAllGOs()
    {
        foreach (var go in _bookGOs)
            if (go != null) Destroy(go);
        _bookGOs.Clear();
    }

    private IEnumerator AnimateHoverEntry(GameObject bookGO)
    {
        if (bookGO == null) yield break;
        Vector3 start = bookGO.transform.localPosition;
        Vector3 end   = start + new Vector3(0f, 0f, -0.05f);
        float t = 0f;
        while (t < 1f && bookGO != null)
        {
            t += Time.deltaTime * 8f;
            bookGO.transform.localPosition = Vector3.Lerp(start, end, t);
            yield return null;
        }
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────
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