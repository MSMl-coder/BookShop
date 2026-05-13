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
        // Renderer на самій Shelf (не на Cabinet — кожна Shelf рендерить свої книги)
        _renderer = GetComponent<BookInstancedRenderer>();
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
            colorIndex    = GetColorIndex(template),
            prefabVariant = 0,
            isReserved    = false,
            reservedByID  = string.Empty,
        };

        _books.Add(entry);

        // КРИТИЧНО: RebuildLayout ДО spawn — щоб entry.localPosition було розраховано
        RebuildLayout();
        PushToRenderer();

        // Беремо оновлений entry з _books (після RebuildLayout localPosition вже правильний)
        ShelfBookEntry placedEntry = _books[_books.Count - 1];
        int            placedIndex = _books.Count - 1;

        if (gameObject.activeInHierarchy)
            StartCoroutine(SpawnBookGO(placedEntry, placedIndex, prefab, instance));
        else
            SpawnBookGOImmediate(placedEntry, placedIndex, prefab, instance);
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

        // Проектуємо hitPoint на вісь полиці (world right напрямок startPoint).
        // Точніше ніж InverseTransformPoint при non-uniform scale батька.
        Vector3 toPoint = worldPoint - startPoint.position;
        float   clickX  = Vector3.Dot(toPoint, startPoint.right); // world units вздовж полиці

        float cursor = 0f;
        for (int i = 0; i < _books.Count; i++)
        {
            float right = cursor + _books[i].thickness;
            // +/- 5мм допуск для зручності вибору
            if (clickX >= cursor - 0.005f && clickX <= right + 0.005f) return i;
            cursor = right + spacingOffset;
        }
        return -1;
    }
     

    public void MaterializeBookForInteraction(int index, GameObject prefab)
    {
        if (index < 0 || index >= _books.Count || prefab == null) return;

        if (_materializedBook != null) DematerializeBook(_materializedBook);

        ShelfBookEntry entry  = _books[index];

        // Спавнимо як дочірній Shelf (не startPoint)
        GameObject bookGO = Instantiate(prefab, transform);
        bookGO.transform.position = startPoint.TransformPoint(entry.localPosition);
        bookGO.transform.rotation = startPoint.rotation
                                    * Quaternion.Euler(bookRotation.x + entry.tilt,
                                                       bookRotation.y, bookRotation.z);

        // Застосовуємо матеріал і колір — той самий що SetupBookWorldItem
        var wi = bookGO.AddComponent<BookWorldItem>();
        wi.instance          = BuildInstance(entry);
        wi.parentShelf       = this;
        wi.bookIndex         = index;
        wi.savedTilt         = entry.tilt;
        wi.isBeingInteracted = true;

        // ── Матеріал і колір (як у SetupBookWorldItem) ───────────────────────
        Material mat = bookMaterial;
        if (mat == null && _renderer != null) mat = _renderer.BookMaterial;

        Color bookColor = BookInstancedRenderer.GetColor(entry.colorIndex);
        var   mpb       = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", bookColor);

        foreach (var r in bookGO.GetComponentsInChildren<Renderer>(true))
        {
            if (mat != null) r.material = mat;
            r.SetPropertyBlock(mpb);
        }

        _materializedBook    = wi;
        _materializedBookRef = wi;

        StartCoroutine(AnimateHoverEntry(bookGO));
    }

    /// Повертає BookWorldItem для книги за індексом.
    /// Якщо GO вже є в _bookGOs — повертає компонент з нього.
    /// Якщо ні — матеріалізує через MaterializeBookForInteraction.
    public BookWorldItem GetOrMaterializeBookForInteraction(int index, GameObject prefab)
    {
        if (index < 0 || index >= _books.Count) return null;

        // Перевіряємо чи вже є GO для цієї книги
        if (index < _bookGOs.Count && _bookGOs[index] != null)
        {
            var existing = _bookGOs[index].GetComponent<BookWorldItem>()
                        ?? _bookGOs[index].GetComponentInChildren<BookWorldItem>();
            if (existing != null) return existing;
        }

        // GO немає (Instancing режим або ще не спавнився) — матеріалізуємо
        MaterializeBookForInteraction(index, prefab);
        return _materializedBookRef;
    }

    public void DematerializeBook(BookWorldItem item)
    {
        if (item == null) return;
        if (_materializedBook    == item) _materializedBook    = null;
        if (_materializedBookRef == item) _materializedBookRef = null;
        if (item.gameObject != null) Destroy(item.gameObject);
    }

    public ShelfBookEntry GetBookData(int index) =>
        (index >= 0 && index < _books.Count) ? _books[index] : default;

    public IReadOnlyList<ShelfBookEntry> GetAllBookData() => _books;

    /// Повертає GO книги за індексом (для hover анімації в ShelfInteractionHandler).
    /// null якщо Instancing ON і GO не існує.
    public GameObject GetBookGO(int index)
    {
        if (index < 0 || index >= _bookGOs.Count) return null;
        return _bookGOs[index];
    }

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
                colorIndex = GetColorIndex(t),
            });
        }
        // RebuildLayout розраховує localPosition для кожної книги
        RebuildLayout();
        PushToRenderer();

        // Спавнимо GO після того як localPosition вже правильний
        for (int i = 0; i < _books.Count; i++)
        {
            var tmpl = BookDatabase.Instance?.GetBook(_books[i].templateID);
            if (tmpl?.containerPrefab == null) { _bookGOs.Add(null); continue; }
            SpawnBookGOImmediate(_books[i], i, tmpl.containerPrefab, BuildInstance(_books[i]));

            // При Instancing вимикаємо Renderer на GO
            if (UseInstancing && i < _bookGOs.Count && _bookGOs[i] != null)
                foreach (var r in _bookGOs[i].GetComponentsInChildren<Renderer>())
                    r.enabled = false;
        }
    }

    // ── Layout / Renderer ─────────────────────────────────────────────────────

    private void RebuildLayout()
    {
        if (startPoint == null) return;

        // currentX накопичується у world units (метри)
        // localPosition = позиція у LOCAL просторі startPoint
        // startPoint.TransformPoint(localPosition) конвертує в world — без додаткового ділення
        float currentX = 0f;

        for (int i = 0; i < _books.Count; i++)
        {
            var   e      = _books[i];
            float scaleX = Mathf.Max(startPoint.lossyScale.x, 0.001f);
            float scaleY = Mathf.Max(startPoint.lossyScale.y, 0.001f);

            // localPosition в просторі startPoint:
            // X — вздовж полиці: currentX + пів-товщини, нормовано під localScale
            // Y — вирівнювання по нижньому краю полиці
            // Z — нуль (книга стоїть на площині startPoint)
            e.localPosition = new Vector3(
                (currentX + e.thickness * 0.5f) / scaleX,
                0f,  // Y=0: startPoint вже на поверхні полиці де має стояти книга
                0f
            );
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

    /// Повертає colorIndex з BookTemplate.
    /// Якщо template null — fallback по жанру (0-7).
    private static int GetColorIndex(BookTemplate t)
    {
        if (t != null) return t.colorIndex;
        return 0;
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
        // Спавнимо як дочірній об'єкт самої Shelf (не startPoint щоб уникнути
        // спотворення масштабу від non-uniform lossyScale startPoint).
        // localPosition вже розрахований відносно startPoint у RebuildLayout.
        GameObject bookGO = Instantiate(prefab, transform);

        // Виставляємо world-позицію через startPoint
        bookGO.transform.position = startPoint.TransformPoint(entry.localPosition);
        bookGO.transform.rotation = startPoint.rotation
                                    * Quaternion.Euler(bookRotation.x + entry.tilt,
                                                       bookRotation.y, bookRotation.z);
        // localScale — власний масштаб префаба (не чіпаємо)

        SetupBookWorldItem(bookGO, entry, instance, index);

        while (_bookGOs.Count <= index) _bookGOs.Add(null);
        if (_bookGOs[index] != null) Destroy(_bookGOs[index]);
        _bookGOs[index] = bookGO;
    }

    /// Створює GO з анімацією появи (виростає знизу вгору)
    private IEnumerator SpawnBookGO(ShelfBookEntry entry, int index, GameObject prefab, BookInstance instance)
    {
        // Спавнимо як дочірній Shelf (не startPoint — уникаємо non-uniform scale)
        GameObject bookGO = Instantiate(prefab, transform);
        bookGO.transform.position = startPoint.TransformPoint(entry.localPosition);
        bookGO.transform.rotation = startPoint.rotation
                                    * Quaternion.Euler(bookRotation.x + entry.tilt,
                                                       bookRotation.y, bookRotation.z);
        SetupBookWorldItem(bookGO, entry, instance, index);

        // Синхронізуємо список GO
        while (_bookGOs.Count <= index) _bookGOs.Add(null);
        if (_bookGOs[index] != null) Destroy(_bookGOs[index]);
        _bookGOs[index] = bookGO;

        // Ховаємо GO під час анімації — він може стояти боком через внутрішню
        // ротацію меша (Book000 X=-90°). Показуємо тільки після завершення анімації.
        var renderers = bookGO.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = false;

        // Анімація масштабу (невидима — просто затримка перед показом)
        yield return new WaitForSeconds(1f / Mathf.Max(animationSpeed, 0.1f));

        if (bookGO == null) yield break;

        if (UseInstancing)
        {
            // Instancing — знищуємо GO, GPU рендерить
            _bookGOs[index] = null;
            Destroy(bookGO);
            PushToRenderer();
        }
        else
        {
            // GO режим — показуємо після анімації (вже стоїть правильно)
            foreach (var r in renderers)
                if (r != null) r.enabled = true;
        }
    }

    [Header("Book Material (for GO mode)")]
    [Tooltip("Матеріал з SG_BookSpine шейдером — застосовується до GO в режимі без Instancing." +
             "Якщо null — використовується матеріал з BookInstancedRenderer (якщо є).")]
    [SerializeField] private Material bookMaterial;

    [Header("Interaction")]
    [Tooltip("Layer для книг — має бути в interactionLayer маску InteractionRouter")]
    [SerializeField] private int bookLayer = 0; // виставити в Inspector = той самий layer що interactionLayer

    private void SetupBookWorldItem(GameObject go, in ShelfBookEntry entry, BookInstance instance, int index)
    {
        if (!go.TryGetComponent<BookWorldItem>(out var wi))
            wi = go.AddComponent<BookWorldItem>();
        wi.instance    = instance;
        wi.parentShelf = this;
        wi.bookIndex   = index;
        wi.savedTilt   = entry.tilt;

        // Зберігаємо world matrix з реального GO — для Instanced Renderer
        // Читаємо з MeshRenderer або MeshFilter дочірнього об'єкта (Book000)
        // щоб отримати правильну матрицю з урахуванням X=-90° rotation меша
        var meshRenderer = go.GetComponentInChildren<MeshRenderer>(true);
        if (meshRenderer != null && index < _books.Count)
        {
            var e = _books[index];
            e.renderMatrix = meshRenderer.localToWorldMatrix;
            _books[index]  = e;
        }

        SetLayerRecursive(go, bookLayer);

        // ── Матеріал і колір ─────────────────────────────────────────────────
        Material mat = bookMaterial;
        if (mat == null && _renderer != null)
            mat = _renderer.BookMaterial;

        if (mat == null)
        {
            Debug.LogWarning($"[Shelf] {name}: Book Material не призначений! " +
                             "Вистав Mat_BookSpine у поле 'Book Material' на Shelf або Book Instanced Renderer.");
            // Застосовуємо колір навіть без кастомного матеріалу через MPB
        }

        // Колір з палітри — той самий що використовує BookInstancedRenderer
        Color bookColor = BookInstancedRenderer.GetColor(entry.colorIndex);
        var   mpb       = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", bookColor);

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (mat != null) r.material = mat;
            r.SetPropertyBlock(mpb);
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer == 0) return; // 0 = Default — не перевизначаємо якщо не налаштовано
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    /// Перераховує позиції існуючих GO після видалення книги.
    /// GO в world space — оновлюємо position/rotation безпосередньо.
    private void RefreshGOPositions()
    {
        if (_bookGOs.Count == 0 || startPoint == null) return;
        int count = Mathf.Min(_bookGOs.Count, _books.Count);
        for (int i = 0; i < count; i++)
        {
            if (_bookGOs[i] == null) continue;
            var e = _books[i];
            _bookGOs[i].transform.position = startPoint.TransformPoint(e.localPosition);
            _bookGOs[i].transform.rotation = startPoint.rotation
                * Quaternion.Euler(bookRotation.x + e.tilt, bookRotation.y, bookRotation.z);
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