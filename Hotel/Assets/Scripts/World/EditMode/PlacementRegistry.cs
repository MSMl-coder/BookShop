// Assets/Scripts/World/EditMode/PlacementRegistry.cs
// ЗМІНИ v2:
//   - FurnitureInstance → PropInstance
//   - Додано подію OnRegistryChanged — слухають ShopAtmosphereService та SeatRegistry
//
// Всі інші методи незмінні (сумісність).

using UnityEngine;
using System;
using System.Collections.Generic;

public class PlacementRegistry : MonoBehaviour
{
    public static PlacementRegistry Instance { get; private set; }

    // ── NEW: подія для реактивних сервісів ────────────────────────
    /// Спрацьовує при Register() та Unregister().
    /// ShopAtmosphereService і SeatRegistry підписуються сюди.
    public event Action OnRegistryChanged;

    // ── Storage ───────────────────────────────────────────────────
    // GO → Instance (швидкий пошук по кліку)
    private readonly Dictionary<GameObject, PropInstance> _goToInstance = new();
    // instanceID → GO (для збереження/відновлення)
    private readonly Dictionary<string, GameObject>       _idToGo       = new();

    // ─────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ─────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────

    public void Register(GameObject go, PropInstance instance)
    {
        _goToInstance[go]            = instance;
        _idToGo[instance.instanceID] = go;
        instance.isPlaced            = true;
        instance.placedPosition      = go.transform.position;
        instance.placedRotation      = go.transform.rotation;

        Debug.Log($"[Registry] Registered: {instance.instanceID} @ {go.transform.position}");
        OnRegistryChanged?.Invoke();
    }

    public void Unregister(GameObject go)
    {
        if (!_goToInstance.TryGetValue(go, out var instance)) return;

        instance.isPlaced = false;
        _idToGo.Remove(instance.instanceID);
        _goToInstance.Remove(go);

        Debug.Log($"[Registry] Unregistered: {instance.instanceID}");
        OnRegistryChanged?.Invoke();
    }

    public PropInstance GetInstance(GameObject go) =>
        _goToInstance.TryGetValue(go, out var inst) ? inst : null;

    public IEnumerable<(GameObject go, PropInstance instance)> GetAll()
    {
        foreach (var kv in _goToInstance)
            yield return (kv.Key, kv.Value);
    }

    /// Синхронізує збережені позиції (викликати перед Save).
    public void SyncPositions()
    {
        foreach (var kv in _goToInstance)
        {
            if (kv.Key == null) continue;
            kv.Value.placedPosition = kv.Key.transform.position;
            kv.Value.placedRotation = kv.Key.transform.rotation;
        }
    }
}