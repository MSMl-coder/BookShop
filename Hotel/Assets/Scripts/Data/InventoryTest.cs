using UnityEngine;
using UnityEngine.InputSystem; // Для Keyboard
using System.Collections.Generic;

public class InventoryTest : MonoBehaviour
{
    [Header("Settings")]
    public Shelf activeShelf;             // Призначте полицю в інспекторі
    public bool addAllOnStart = true;     // Чи додавати все автоматично при запуску?
    [Range(1, 10)] public int countPerBook = 1; // Скільки екземплярів кожної книги додавати

    private void Start()
    {
        // Чекаємо один кадр або викликаємо після ініціалізації бази
        if (addAllOnStart)
        {
        //    AddEveryBookToInventory();
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Натисніть 'A', щоб додати ВСІ книги з бази в інвентар
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
        //    AddEveryBookToInventory();
        }

        // Клавіша 'P' — виставити ОДНУ книгу на полицю (Кнопка [>])
        if (Keyboard.current.pKey.wasPressedThisFrame && activeShelf != null)
        {
            InventoryManager.Instance.PushOneToShelf(activeShelf);
        }

        // Клавіша 'L' — заповнити полицю ПОВНІСТЮ (Кнопка [>>])
        if (Keyboard.current.lKey.wasPressedThisFrame && activeShelf != null)
        {
            InventoryManager.Instance.PushAllToShelf(activeShelf);
        }
    }

    /// <summary>
    /// Проходить по всій базі даних і додає кожну книгу в інвентар.
    /// </summary>
 /*   public void AddEveryBookToInventory()
    {
        if (BookDatabase.Instance == null || BookDatabase.Instance.allBooks == null)
        {
            Debug.LogError("[Test] База даних не знайдена або порожня!");
            return;
        }

        Debug.Log("[Test] Починаємо масове завантаження книг...");

        foreach (var bookTemplate in BookDatabase.Instance.allBooks)
        {
            if (bookTemplate == null) continue;

            for (int i = 0; i < countPerBook; i++)
            {
                InventoryManager.Instance.AddBook(bookTemplate.bookID);
            }
        }

        Debug.Log($"[Test] Додано по {countPerBook} примірників кожної книги.");
    
    }
    */
}