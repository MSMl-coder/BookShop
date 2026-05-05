using UnityEngine;
using System.Collections.Generic;

public class Cabinet : MonoBehaviour
{
    public string cabinetName = "Стелаж";
    public List<Shelf> shelves = new List<Shelf>();

    [ContextMenu("Auto-find Shelves")]
    void OnValidate()
    {
        // Автоматично знаходить усі скрипти Shelf у дочірніх об'єктах
        shelves = new List<Shelf>(GetComponentsInChildren<Shelf>());
    }
}