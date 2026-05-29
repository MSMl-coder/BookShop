// Assets/Scripts/World/EditMode/PlacedObject.cs
using UnityEngine;

/// Компонент-мітка на розміщеному обʼєкті в сцені.
/// Звʼязує GameObject з FurnitureInstance.
public class PlacedObject : MonoBehaviour
{
    public PropInstance Instance { get; private set; }

    public void Init(PropInstance instance)
    {
        Instance = instance;
    }
}