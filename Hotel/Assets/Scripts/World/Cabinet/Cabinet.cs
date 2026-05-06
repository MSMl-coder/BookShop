// Assets/Scripts/World/Cabinet/Cabinet.cs
using UnityEngine;
using System.Collections.Generic;

/// Шафа в 3D-сцені. Клік відкриває UI крамниці з вибраною шафою.
///
/// UNITY SETUP:
/// 1. На Cabinet GameObject ОБОВ'ЯЗКОВО має бути Collider
///    (Box Collider або Mesh Collider — не Trigger)
/// 2. Шар (Layer) має бути в маску що зчитує камера
/// 3. У сцені має бути Camera з PhysicsRaycaster + EventSystem
///    АБО окремий CabinetClickHandler (нижче)
public class Cabinet : MonoBehaviour
{
    [Header("Identity")]
    public string cabinetName = "Cabinet";

    [Header("Shelves")]
    public List<Shelf> shelves = new List<Shelf>();

    private void Awake()
    {
        // Авто-знаходження полиць у дочірніх якщо не призначені
        if (shelves == null || shelves.Count == 0)
            shelves = new List<Shelf>(GetComponentsInChildren<Shelf>());

        // Перевірка collider
        if (GetComponent<Collider>() == null)
            Debug.LogWarning($"[Cabinet] {cabinetName} не має Collider! Кліки не будуть працювати.");
    }

    /// Викликається з CabinetClickHandler коли гравець клікає на шафу
    public void OnClicked()
    {
        Debug.Log($"[Cabinet] Клік: {cabinetName}");
        if (ShopUIManager.Instance != null)
            ShopUIManager.Instance.OpenCabinetUI(this);
        else
            Debug.LogError("[Cabinet] ShopUIManager.Instance не знайдено!");
    }
}
