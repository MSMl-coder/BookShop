using UnityEngine;
using System.Collections.Generic;

// Компонент-мітка на кожній групі шаф
// Зараз: просто тег. В Етапі 3: додасться логіка ClubZone
public class ShopZone : MonoBehaviour
{
    [Header("Zone Settings")]
    public ShopZoneType zoneType = ShopZoneType.Storefront;
    public string zoneName = "Main Floor";

    // Всі шафи цієї зони
    [SerializeField] private List<Cabinet> cabinets = new List<Cabinet>();

    // Флаг розблокування (для Book Club)
    public bool IsUnlocked { get; private set; } = true;

    public void Unlock()
    {
        IsUnlocked = true;
        gameObject.SetActive(true);
        Debug.Log($"[Zone] {zoneName} unlocked!");
    }

    public void Lock()
    {
        IsUnlocked = false;
        // Можна прикрити простирадлом (через окремий компонент)
    }

    public List<Cabinet> GetCabinets() => cabinets;

    // Підраховує кількість книг у зоні
    public int CountBooks()
    {
        int total = 0;
        foreach (var cab in cabinets)
            foreach (var shelf in cab.shelves)
                total += shelf.GetBookCount(); // додати метод до Shelf
        return total;
    }

    [ContextMenu("Auto-find Cabinets")]
    private void AutoFindCabinets()
    {
        cabinets = new List<Cabinet>(GetComponentsInChildren<Cabinet>());
    }
}