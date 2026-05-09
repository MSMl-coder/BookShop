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

    private void RemoveFurniture(FurnitureTemplate item)
    {
        // Знаходимо реальний обʼєкт в сцені через Registry
        if (PlacementRegistry.Instance != null)
        {
            var all = PlacementRegistry.Instance.GetAll();
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (all[i].data == item)
                {
                    var go = all[i].obj;
                    PlacementRegistry.Instance.Unregister(go);
                    if (go != null) Destroy(go);
                    break;
                }
            }
        }

        item.IsPlaced = false;
        Debug.Log($"[DecoUI] Прибрано з сцени: {item.furnitureName}");
    }
}