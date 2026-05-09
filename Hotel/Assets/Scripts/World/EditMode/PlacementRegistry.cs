// Assets/Scripts/World/EditMode/PlacementRegistry.cs
using UnityEngine;
using System.Collections.Generic;

public class PlacementRegistry : MonoBehaviour
{
    public static PlacementRegistry Instance { get; private set; }

    // GO → Instance (щоб швидко знайти по кліку)
    private readonly Dictionary<GameObject, FurnitureInstance> _goToInstance = new();
    // instanceID → GO (для збереження/відновлення)
    private readonly Dictionary<string, GameObject>            _idToGo       = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Register(GameObject go, FurnitureInstance instance)
    {
        _goToInstance[go]               = instance;
        _idToGo[instance.instanceID]    = go;
        instance.isPlaced               = true;
        instance.placedPosition         = go.transform.position;
        instance.placedRotation         = go.transform.rotation;
        Debug.Log($"[Registry] Зареєстровано: {instance.instanceID} @ {go.transform.position}");
    }

    public void Unregister(GameObject go)
    {
        if (!_goToInstance.TryGetValue(go, out var instance)) return;
        instance.isPlaced = false;
        _idToGo.Remove(instance.instanceID);
        _goToInstance.Remove(go);
    }

    public FurnitureInstance GetInstance(GameObject go) =>
        _goToInstance.TryGetValue(go, out var inst) ? inst : null;

    public IEnumerable<(GameObject go, FurnitureInstance instance)> GetAll()
    {
        foreach (var kv in _goToInstance)
            yield return (kv.Key, kv.Value);
    }

    /// Синхронізує збережені позиції (викликати перед Save)
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