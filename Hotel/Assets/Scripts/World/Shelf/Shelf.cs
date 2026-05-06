// Assets/Scripts/World/Shelf/Shelf.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class Shelf : MonoBehaviour
{
    [Header("Components")]
    public Transform startPoint;

    [Header("Placement Settings")]
    [Tooltip("Кут повороту книги для цієї полиці")]
    public Vector3 bookRotation = Vector3.zero;

    [Range(0f, 15f)]
    [Tooltip("Випадковий нахил вздовж полиці для реалістичності")]
    public float maxRandomTilt = 3f;

    [Tooltip("Відступ між книгами")]
    public float spacingOffset = 0.002f;

    [Tooltip("Швидкість анімації появи")]
    public float animationSpeed = 5f;

    [Header("Fallback")]
    [Tooltip("Товщина книги за замовчуванням якщо MeshFilter не знайдено у prefab")]
    [SerializeField] private float defaultBookThickness = 0.03f;

    // Список розміщених візуальних об'єктів книг
    private List<GameObject> _placedBookVisuals = new List<GameObject>();

    // ──────────────────────────────────────────────────────────
    #region Unity Lifecycle
    // ──────────────────────────────────────────────────────────

    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Shelves");

        if (startPoint == null)
        {
            Debug.LogError($"[Shelf] StartPoint не призначено на {gameObject.name}!");
            enabled = false;
        }
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Публічний API
    // ──────────────────────────────────────────────────────────

    /// Перевіряє чи поміститься книга на полицю
    public bool CanFitBook(GameObject bookPrefab)
    {
        if (bookPrefab == null) return false;

        float bookThickness   = GetPrefabThickness(bookPrefab);
        float worldShelfWidth = GetShelfWorldWidth();
        float usedWidth       = GetTotalUsedWidth();

        return (usedWidth + bookThickness + spacingOffset) <= worldShelfWidth;
    }

    /// Розміщує книгу на полиці
    public void PlaceBook(BookInstance instance, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError($"[Shelf] PlaceBook: prefab = null на {gameObject.name}!");
            return;
        }
        if (instance == null)
        {
            Debug.LogError($"[Shelf] PlaceBook: instance = null на {gameObject.name}!");
            return;
        }
        if (startPoint == null)
        {
            Debug.LogError($"[Shelf] PlaceBook: startPoint = null на {gameObject.name}!");
            return;
        }

        GameObject newBookObj = Instantiate(prefab, startPoint);
        ResetToWorldScale(newBookObj.transform, prefab.transform.localScale);

        var worldItem = newBookObj.AddComponent<BookWorldItem>();
        worldItem.instance    = instance;
        worldItem.parentShelf = this;
        // Генеруємо tilt ОДИН РАЗ при розміщенні — не перераховуємо щоразу
        worldItem.savedTilt = Random.Range(-maxRandomTilt, maxRandomTilt);

        _placedBookVisuals.Add(newBookObj);
        RefreshPositions();

        if (gameObject.activeInHierarchy)
            StartCoroutine(AnimateBookEntry(newBookObj));
    }

    /// Забирає останню книгу з полиці та повертає її дані
    public BookInstance TakeLastBook()
    {
        if (_placedBookVisuals.Count == 0) return null;

        // Прибираємо null-entries що могли залишитись
        _placedBookVisuals.RemoveAll(b => b == null);
        if (_placedBookVisuals.Count == 0) return null;

        int lastIndex = _placedBookVisuals.Count - 1;
        GameObject lastBook = _placedBookVisuals[lastIndex];

        BookWorldItem item = lastBook.GetComponent<BookWorldItem>();
        if (item == null)
        {
            Debug.LogWarning($"[Shelf] TakeLastBook: BookWorldItem не знайдено на {lastBook.name}");
            return null;
        }

        BookInstance data = item.instance;
        _placedBookVisuals.RemoveAt(lastIndex);
        Destroy(lastBook);
        RefreshPositions();

        return data;
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Розміри та ширина полиці
    // ──────────────────────────────────────────────────────────

    /// Повертає повну світову ширину полиці (з урахуванням масштабу)
    /// Використовується в CanFitBook та Debug
    public float GetShelfWorldWidth()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
        {
            Debug.LogWarning($"[Shelf] GetShelfWorldWidth: BoxCollider не знайдено на {gameObject.name}");
            return 1f; // безпечне значення за замовчуванням
        }
        return col.size.x * transform.lossyScale.x;
    }

    /// Повертає кількість книг на полиці
    public int GetBookCount() => _placedBookVisuals.Count;

    /// Повертає тип зони (Storefront / BookClub / Storage)
    public ShopZoneType ZoneType
    {
        get
        {
            var zone = GetComponentInParent<ShopZone>();
            return zone != null ? zone.zoneType : ShopZoneType.Storefront;
        }
    }

    /// Скільки місця зайнято (у світових одиницях)
    public float GetTotalUsedWidth()
    {
        if (startPoint == null) return 0f;

        float total = 0f;
        Vector3 parentScale = startPoint.lossyScale;

        foreach (var bookObj in _placedBookVisuals)
        {
            if (bookObj == null) continue;
            total += GetRuntimeThickness(bookObj, parentScale) + spacingOffset;
        }
        return total;
    }

    /// Скільки вільного місця залишилось
    public float GetFreeWidth() => GetShelfWorldWidth() - GetTotalUsedWidth();

    /// Заповненість полиці від 0 до 1
    public float GetFillRatio()
    {
        float shelfWidth = GetShelfWorldWidth();
        if (shelfWidth <= 0f) return 0f;
        return Mathf.Clamp01(GetTotalUsedWidth() / shelfWidth);
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Збереження / Завантаження
    // ──────────────────────────────────────────────────────────

    /// Збирає дані полиці для серіалізації
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

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Розташування (Layout)
    // ──────────────────────────────────────────────────────────

    /// Перераховує позиції всіх книг на полиці
    public void RefreshPositions()
    {
        if (startPoint == null) return;

        float currentX      = 0f;
        Vector3 parentScale = startPoint.lossyScale;

        foreach (var bookObj in _placedBookVisuals)
        {
            if (bookObj == null) continue;

            float worldThickness = GetRuntimeThickness(bookObj, parentScale);
            float localX = (currentX + (worldThickness / 2f))
                           / Mathf.Max(parentScale.x, 0.001f);
            float localY = GetBottomAlignedY(bookObj);

            bookObj.transform.localPosition = new Vector3(localX, localY, 0f);

            // Tilt береться зі збереженого значення — не генеруємо щоразу
            BookWorldItem item = bookObj.GetComponent<BookWorldItem>();
            float tilt = item != null ? item.savedTilt : 0f;
            bookObj.transform.localRotation =
                Quaternion.Euler(bookRotation.x + tilt, bookRotation.y, bookRotation.z);

            currentX += worldThickness + spacingOffset;
        }
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Приватні допоміжні методи
    // ──────────────────────────────────────────────────────────

    /// Безпечне отримання товщини з PREFAB-об'єкта.
    /// Ніколи не кидає NullReferenceException.
    /// Порядок пошуку: MeshFilter(self) → MeshFilter(children) → Renderer → default
    private float GetPrefabThickness(GameObject prefab)
    {
        if (prefab == null) return defaultBookThickness;

        // 1. MeshFilter безпосередньо на об'єкті
        MeshFilter mf = prefab.GetComponent<MeshFilter>();

        // 2. MeshFilter у дочірніх (includeInactive = true для вимкнених)
        if (mf == null)
            mf = prefab.GetComponentInChildren<MeshFilter>(includeInactive: true);

        if (mf != null && mf.sharedMesh != null)
            return mf.sharedMesh.bounds.size.z * prefab.transform.localScale.z;

        // 3. Renderer як запасний варіант
        Renderer rend = prefab.GetComponentInChildren<Renderer>(includeInactive: true);
        if (rend != null && rend.bounds.size.z > 0.001f)
            return rend.bounds.size.z;

        // 4. Дефолтне значення — не крашимо
        Debug.LogWarning($"[Shelf] GetPrefabThickness: MeshFilter/Renderer не знайдено у '{prefab.name}'. " +
                         $"Використовую defaultBookThickness={defaultBookThickness}m.");
        return defaultBookThickness;
    }

    /// Товщина вже ІНСТАНЦІЙОВАНОГО об'єкта (для RefreshPositions)
    private float GetRuntimeThickness(GameObject bookObj, Vector3 parentScale)
    {
        if (bookObj == null) return defaultBookThickness;

        MeshFilter mf = bookObj.GetComponentInChildren<MeshFilter>(includeInactive: true);
        if (mf != null && mf.sharedMesh != null)
        {
            return mf.sharedMesh.bounds.size.z
                   * bookObj.transform.localScale.z
                   * Mathf.Max(parentScale.z, 0.001f);
        }
        return defaultBookThickness;
    }

    /// Вирівнює книгу по нижньому краю полиці
    private float GetBottomAlignedY(GameObject bookObj)
    {
        if (bookObj == null) return 0f;

        MeshFilter mf = bookObj.GetComponentInChildren<MeshFilter>(includeInactive: true);
        if (mf != null && mf.sharedMesh != null)
        {
            float bottomY = mf.sharedMesh.bounds.min.y;
            float scaleY  = bookObj.transform.localScale.y;
            return -(bottomY * scaleY);
        }
        return 0f;
    }

    /// Встановлює масштаб об'єкта компенсуючи lossy scale батька
    private void ResetToWorldScale(Transform target, Vector3 targetWorldScale)
    {
        if (target == null || target.parent == null) return;

        Vector3 parentScale = target.parent.lossyScale;
        target.localScale = new Vector3(
            targetWorldScale.x / Mathf.Max(parentScale.x, 0.001f),
            targetWorldScale.y / Mathf.Max(parentScale.y, 0.001f),
            targetWorldScale.z / Mathf.Max(parentScale.z, 0.001f)
        );
    }

    /// Анімація появи книги (росте з нуля до повного розміру)
    private IEnumerator AnimateBookEntry(GameObject book)
    {
        if (book == null) yield break;

        Vector3 targetScale = book.transform.localScale;
        Vector3 startScale  = new Vector3(targetScale.x, 0f, targetScale.z);
        book.transform.localScale = startScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * animationSpeed;
            if (book == null) yield break;
            book.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        if (book != null)
            book.transform.localScale = targetScale;
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Gizmos (Editor debug)
    // ──────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (startPoint == null) return;

        float shelfWidth = GetShelfWorldWidth();
        float usedWidth  = GetTotalUsedWidth();

        // Зелена лінія — вільне місце
        Vector3 freeStart = startPoint.position + (-startPoint.right * usedWidth);
        Vector3 freeEnd   = startPoint.position + (-startPoint.right * shelfWidth);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(freeStart, freeEnd);

        // Червона лінія — зайняте місце
        Gizmos.color = Color.red;
        Gizmos.DrawLine(startPoint.position, freeStart);

        // Мітка заповненості
        float pct = shelfWidth > 0 ? (usedWidth / shelfWidth * 100f) : 0f;
        UnityEditor.Handles.Label(
            startPoint.position + Vector3.up * 0.15f,
            $"{GetBookCount()} books | {pct:F0}%"
        );
    }

    #endregion
}
