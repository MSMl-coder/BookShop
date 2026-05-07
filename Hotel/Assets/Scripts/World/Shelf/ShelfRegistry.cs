// Assets/Scripts/World/Shelf/ShelfRegistry.cs — NEW FILE
// A lightweight scene registry for all active Shelf instances.
// Avoids repeated FindObjectsByType<Shelf> calls in Update loops,
// NPCBrain initialization, and GameStateSerializer save collection.
//
// SETUP: Add a ShelfRegistry component to the GameManagers GameObject.
//        Shelves auto-register/unregister via OnEnable/OnDisable.
using UnityEngine;
using System.Collections.Generic;

public class ShelfRegistry : MonoBehaviour
{
    public static ShelfRegistry Instance { get; private set; }

    private readonly List<Shelf> _shelves = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Register(Shelf shelf)
    {
        if (shelf != null && !_shelves.Contains(shelf))
            _shelves.Add(shelf);
    }

    public void Unregister(Shelf shelf)
    {
        _shelves.Remove(shelf);
    }

    public IReadOnlyList<Shelf> GetAll() => _shelves;

    public int Count => _shelves.Count;
}
