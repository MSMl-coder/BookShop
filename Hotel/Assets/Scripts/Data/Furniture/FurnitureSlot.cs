// Assets/Scripts/Data/Furniture/FurnitureSlot.cs
// PATCH: додано публічний геттер CurrentTemplate
using UnityEngine;
using System.Collections.Generic;

public class FurnitureSlot : MonoBehaviour
{
    [Header("Settings")]
    public FurnitureClass allowedClass;

    [SerializeField] private FurnitureTemplate currentTemplate;

    // ← ЄДИНА ЗМІНА: публічний геттер для UpgradeSlotUI
    public FurnitureTemplate CurrentTemplate => currentTemplate;

    private Cabinet _currentCabinetInstance;

    private void Start()
    {
        if (currentTemplate != null)
            SpawnCabinet(currentTemplate);
    }

    public void UpgradeFurniture(FurnitureTemplate newTemplate)
    {
        if (newTemplate.furnitureClass != allowedClass)
        {
            Debug.LogWarning("[Slot] Wrong furniture class for this slot!");
            return;
        }

        List<BookInstance> savedBooks = ExtractAllBooks();

        if (_currentCabinetInstance != null)
            Destroy(_currentCabinetInstance.gameObject);

        SpawnCabinet(newTemplate);
       // RestoreBooks(savedBooks);

        Debug.Log($"[Slot] Upgraded to {newTemplate.furnitureName}");
    }

    private void SpawnCabinet(FurnitureTemplate template)
    {
        currentTemplate = template;
        GameObject go = Instantiate(
            template.prefab, transform.position, transform.rotation, transform);
        _currentCabinetInstance = go.GetComponent<Cabinet>();

        if (_currentCabinetInstance == null)
            Debug.LogError($"[Slot] Prefab {template.prefab.name} is missing a Cabinet component!");
    }

    private List<BookInstance> ExtractAllBooks()
    {
        var books = new List<BookInstance>();
        if (_currentCabinetInstance == null) return books;

        foreach (var shelf in _currentCabinetInstance.shelves)
        {
            if (shelf == null) continue;
            BookInstance b;
            while ((b = shelf.TakeLastBook()) != null)
                books.Add(b);
        }
        return books;
    }

   /* private void RestoreBooks(List<BookInstance> books)
    {
        if (_currentCabinetInstance == null || books.Count == 0) return;

        foreach (var shelf in _currentCabinetInstance.shelves)
        {
            if (shelf == null) continue;
            foreach (var book in books)
                shelf.TryAddBook(book);
            break;
        }
    }
    */
}
