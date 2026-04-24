using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class Shelf : MonoBehaviour
{
    [Header("Components")]
    public Transform startPoint; 

    [Header("Placement Settings")]
    [Tooltip("Кут повороту книги для цієї полиці (напр. 90, 0, 0 щоб книги лежали)")]
    public Vector3 bookRotation = Vector3.zero; 
    
    [Range(0f, 15f)] 
    [Tooltip("Випадковий нахил вздовж полиці для реалістичності")]
    public float maxRandomTilt = 3f; 
    
    [Tooltip("Відступ між книгами")]
    public float spacingOffset = 0.002f;
    
    [Tooltip("Швидкість анімації появи")]
    public float animationSpeed = 5f;

    private List<GameObject> _placedBookVisuals = new List<GameObject>();

    private void Awake()
    {
        // Встановлюємо шар для взаємодії
        gameObject.layer = LayerMask.NameToLayer("Shelves");
        
        if (startPoint == null)
        {
            Debug.LogError($"[Shelf] StartPoint не призначено на {gameObject.name}!");
            enabled = false;
        }
    }

    #region Публічний API

   
    /// Перевіряє, чи влізе книга, ігноруючи викривлення масштабу батьків.
   
    public bool CanFitBook(GameObject bookPrefab)
    {
        if (bookPrefab == null) return false;
        
        // 1. Світова товщина префаба
        float bookThickness = GetProjectedThickness(bookPrefab);
        
        // 2. Світова ширина полиці
        BoxCollider collider = GetComponent<BoxCollider>();
        // Ми беремо розмір колайдера і множимо на масштаб по осі X
        float worldShelfWidth = collider.size.x * transform.lossyScale.x;
        
        // 3. Скільки ВЖЕ зайнято (у світових одиницях)
        float usedWidth = GetTotalUsedWidth();
        
        // Debug для тебе, щоб бачити цифри в консолі
        // Debug.Log($"Used: {usedWidth} + New: {bookThickness} <= Max: {worldShelfWidth}");

        return (usedWidth + bookThickness + spacingOffset) <= worldShelfWidth;
    }

    // Залиш ТІЛЬКИ ЦЮ ВЕРСІЮ методу GetTotalUsedWidth:

    public void PlaceBook(BookInstance instance, GameObject prefab)
    {
        GameObject newBookObj = Instantiate(prefab, startPoint);
        
        // 1. НІВЕЛЮВАННЯ МАШТАБУ: Книга ігнорує розтягування шафи
        ResetToWorldScale(newBookObj.transform, prefab.transform.localScale);

        var worldItem = newBookObj.AddComponent<BookWorldItem>();
        worldItem.instance = instance;
        worldItem.parentShelf = this;

        _placedBookVisuals.Add(newBookObj);
        
        // 2. РОЗРАХУНОК ПОЗИЦІЙ
        RefreshPositions();

        // 3. АНІМАЦІЯ
        if (gameObject.activeInHierarchy)
            StartCoroutine(AnimateBookEntry(newBookObj));
    }

    #endregion

    #region Ядро логіки (Масштаб та Вирівнювання)

   public void RefreshPositions()
{
    float currentX = 0;
    Vector3 pScale = startPoint.lossyScale;

    foreach (var bookObj in _placedBookVisuals)
    {
        if (bookObj == null) continue;
        
        MeshFilter mf = bookObj.GetComponentInChildren<MeshFilter>();
        if (mf == null) continue;

        // 1. Отримуємо локальні межі меша
        Bounds b = mf.sharedMesh.bounds;
        
        // 2. Враховуємо масштаб самої книги
        Vector3 s = bookObj.transform.localScale;

        // 3. РОЗРАХУНОК ТОЧНОЇ ВИСОТИ (Y)
        // b.min.y - це найнижча точка моделі відносно її півота.
        // Ми множимо її на масштаб і віднімаємо від позиції, щоб "підтягнути" низ до 0.
        float bottomYOffset = b.min.y * s.y;
        float localY = -bottomYOffset; 

        // 4. РОЗРАХУНОК ШИРИНИ (X)
        // Товщина у світових одиницях (з урахуванням масштабу полиці)
        float worldThickness = b.size.z * s.z * pScale.z;
        float localX = (currentX + (worldThickness / 2f)) / pScale.x;

        // 5. ЗАСТОСУВАННЯ
        bookObj.transform.localPosition = new Vector3(localX, localY, 0);

        // 6. ПОВОРОТ ТА НАХИЛ
        bookObj.transform.localRotation = Quaternion.Euler(bookRotation);
        float randomTilt = Random.Range(-maxRandomTilt, maxRandomTilt);
        bookObj.transform.localRotation = Quaternion.Euler(bookRotation.x + randomTilt, bookRotation.y, bookRotation.z);
        
        // Крок для наступної книги
        currentX += worldThickness + spacingOffset;
    }
}

    
    /// Встановлює локальний масштаб так, щоб об'єкт виглядав як префаб, 
    /// навіть якщо батько розтягнутий (Lossy Scale Compensation).
  
    private void ResetToWorldScale(Transform target, Vector3 targetWorldScale)
    {
        Vector3 parentScale = target.parent.lossyScale;
        target.localScale = new Vector3(
            targetWorldScale.x / parentScale.x,
            targetWorldScale.y / parentScale.y,
            targetWorldScale.z / parentScale.z
        );
    }

    public float GetTotalUsedWidth()
    {
        float total = 0;
        foreach (var b in _placedBookVisuals)
        {
            if (b == null) continue;
            MeshFilter mf = b.GetComponentInChildren<MeshFilter>();
            if (mf != null)
            {
                // Рахуємо товщину кожної книги з урахуванням її масштабу та масштабу полиці
                float thickness = mf.sharedMesh.bounds.size.z * b.transform.localScale.z * startPoint.lossyScale.z;
                total += thickness + spacingOffset;
            }
        }
        return total;
    }

    private float GetProjectedThickness(GameObject prefab)
        {
            MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf == null) return 0.03f;
            // Використовуємо розмір меша, а не Bounds рендерера
            return mf.sharedMesh.bounds.size.z * prefab.transform.localScale.z;
        }

    private IEnumerator AnimateBookEntry(GameObject book)
    {
        Vector3 targetScale = book.transform.localScale;
        Vector3 initialScale = new Vector3(targetScale.x, 0, targetScale.z);
        book.transform.localScale = initialScale;
        
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * animationSpeed;
            if (book == null) yield break;
            book.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
            yield return null;
        }
    }

    public BookInstance TakeLastBook()
    {
        if (_placedBookVisuals.Count == 0) return null;

        // Беремо останню додану книгу
        GameObject lastBook = _placedBookVisuals[_placedBookVisuals.Count - 1];
        BookWorldItem item = lastBook.GetComponent<BookWorldItem>();
        
        if (item == null) return null;

        BookInstance data = item.instance;

        _placedBookVisuals.RemoveAt(_placedBookVisuals.Count - 1);
        Destroy(lastBook);

        // Оновлюємо позиції тих, що залишилися
        RefreshPositions();

        return data;
    }
    #endregion
}