using UnityEngine;

public class PlacementPhantom
{
    public GameObject Visual { get; private set; }

    public void Spawn(GameObject prefab)
    {
        Visual = Object.Instantiate(prefab);
        foreach (var c in Visual.GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    public void Update(Vector3 pos, Quaternion rot, float speed)
    {
        if (Visual == null) return;
        Visual.transform.position = Vector3.Lerp(Visual.transform.position, pos, Time.deltaTime * speed);
        Visual.transform.rotation = Quaternion.Lerp(Visual.transform.rotation, rot, Time.deltaTime * speed);
    }

    public void Clear() { if (Visual != null) Object.Destroy(Visual); }
}