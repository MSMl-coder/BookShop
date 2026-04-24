using UnityEngine;
using System.Collections.Generic;

public class FurnitureSlot : MonoBehaviour
{
    [Header("Settings")]
    public FurnitureClass allowedClass;
    [SerializeField] private FurnitureTemplate currentTemplate;
    
    private Cabinet _currentCabinetInstance;

    private void Start()
    {
        if (currentTemplate != null)
        {
            SpawnCabinet(currentTemplate);
        }
    }

    public void UpgradeFurniture(FurnitureTemplate newTemplate)
    {
        if (newTemplate.furnitureClass != allowedClass) 
        {
            Debug.LogWarning("[Slot] Wrong furniture class for this slot!");
            return;
        }

        // 1. Save all existing books from the old cabinet
        List<BookInstance> savedBooks = ExtractAllBooks();

        // 2. Destroy old cabinet
        if (_currentCabinetInstance != null)
        {
            Destroy(_currentCabinetInstance.gameObject);
        }

        // 3. Spawn new cabinet
        SpawnCabinet(newTemplate);

        // 4. Put books back
        RestoreBooks(savedBooks);
        
        Debug.Log($"[Slot] Upgraded to {newTemplate.furnitureName}");
    }

    private void SpawnCabinet(FurnitureTemplate template)
    {
        currentTemplate = template;
        GameObject go = Instantiate(template.prefab, transform.position, transform.rotation, transform);
        _currentCabinetInstance = go.GetComponent<Cabinet>();
        
        if (_currentCabinetInstance == null)
            Debug.LogError($"[Slot] Prefab {template.prefab.name} is missing a Cabinet component!");
    }

    private List<BookInstance> ExtractAllBooks()
    {
        List<BookInstance> extracted = new List<BookInstance>();
        if (_currentCabinetInstance == null) return extracted;

        foreach (Shelf shelf in _currentCabinetInstance.shelves)
        {
            BookInstance book = shelf.TakeLastBook();
            while (book != null)
            {
                extracted.Add(book);
                book = shelf.TakeLastBook();
            }
        }
        return extracted;
    }

    private void RestoreBooks(List<BookInstance> booksToRestore)
    {
        if (booksToRestore.Count == 0 || _currentCabinetInstance == null) return;

        int bookIndex = 0;
        foreach (Shelf shelf in _currentCabinetInstance.shelves)
        {
            // Simple logic: push books back via InventoryManager logic or directly to shelf
            // Assuming we return them to Inventory and push one by one to use existing logic
            while (bookIndex < booksToRestore.Count)
            {
                BookInstance b = booksToRestore[bookIndex];
                BookTemplate bt = BookDatabase.Instance.GetBook(b.templateID);
                
                if (shelf.CanFitBook(bt.containerPrefab))
                {
                    shelf.PlaceBook(b, bt.containerPrefab);
                    bookIndex++;
                }
                else
                {
                    break; // Shelf is full, move to next shelf
                }
            }
        }

        // If any books left over (new cabinet is smaller), return them to inventory
        while (bookIndex < booksToRestore.Count)
        {
            InventoryManager.Instance.AddExistingBook(booksToRestore[bookIndex]);
            Debug.Log("[Slot] Not enough space in new cabinet. Book returned to inventory.");
            bookIndex++;
        }
    }
}