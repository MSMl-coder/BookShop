// Assets/Scripts/World/EditMode/PlacedObject.cs
using UnityEngine;

/// Компонент-мітка на розміщеному обʼєкті в сцені.
/// Звʼязує GameObject з FurnitureInstance.
public class PlacedObject : MonoBehaviour
{
    public FurnitureInstance Instance { get; private set; }

    public void Init(FurnitureInstance instance)
    {
        Instance = instance;
    }
}