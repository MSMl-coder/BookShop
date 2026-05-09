// Assets/Scripts/World/EditMode/PlacedObject.cs
using UnityEngine;

public class PlacedObject : MonoBehaviour
{
    [HideInInspector] public GameObject originalPrefab;
    [HideInInspector] public FurnitureTemplate sourceTemplate; // лишаємо для Registry
}