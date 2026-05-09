// Assets/Scripts/World/EditMode/PlacementRegistry.cs
using UnityEngine;
using System.Collections.Generic;

public class PlacementRegistry : MonoBehaviour
{
    public static PlacementRegistry Instance { get; private set; }

    private readonly List<(GameObject obj, FurnitureTemplate data)> _placed = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Register(GameObject obj, FurnitureTemplate data)
    {
        _placed.Add((obj, data));
    }

    public void Unregister(GameObject obj) =>
        _placed.RemoveAll(e => e.obj == obj);

    public IReadOnlyList<(GameObject obj, FurnitureTemplate data)> GetAll() => _placed;
}