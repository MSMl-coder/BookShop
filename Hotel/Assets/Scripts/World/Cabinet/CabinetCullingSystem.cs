// Assets/Scripts/World/Cabinet/CabinetCullingSystem.cs
// РІШЕННЯ: прибрати culling для BookInstancedRenderer.
// Graphics.DrawMeshInstanced має вбудований GPU frustum culling — ручний не потрібен.
// SetRenderingEnabled(false) вимикало рендерери і розривало ланцюжок PushToRenderer.
//
// Culling меблевих MeshRenderer залишається (через LODGroup або стандартний Unity culling).

using UnityEngine;

[AddComponentMenu("Bookstore/Cabinet Culling System")]
public class CabinetCullingSystem : MonoBehaviour
{
    // Culling для BookInstancedRenderer більше не потрібен —
    // Graphics.DrawMeshInstanced має власний GPU frustum culling.
    // Цей компонент тепер тільки логує кількість шаф для дебагу.

    private void Start()
    {
        var cabinets = FindObjectsByType<Cabinet>(FindObjectsInactive.Exclude);
        Debug.Log($"[CabinetCulling] {cabinets.Length} шаф у сцені. " +
                  $"BookInstancedRenderer culling вимкнено — GPU handles it.");

        // Гарантуємо що всі рендерери увімкнені при старті
        foreach (var cab in cabinets)
            cab?.SetRenderingEnabled(true);
    }
}