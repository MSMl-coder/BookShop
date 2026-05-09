// Assets/Scripts/World/WallFacingTag.cs
// ═══════════════════════════════════════════════════════════════════════════
//  Маркер напрямку стіни — додається на кожну стіну або групу стін
//  -----------------------------------------------------------------------
//  UNITY SETUP:
//    1. Виділи GameObject стіни (або їх спільний батько)
//    2. Add Component → WallFacingTag
//    3. Вибери Facing: North / South / East / West
//
//  Як визначити напрямок:
//    Стань в центрі кімнати, дивись на стіну.
//    Якщо стіна попереду і ти дивишся на північ → це North стіна.
//
//    Або по нормалі меша:
//      Нормаль (0, 0, +1) → North
//      Нормаль (0, 0, -1) → South
//      Нормаль (+1, 0, 0) → East
//      Нормаль (-1, 0, 0) → West
//
//  WallVisibilityController автоматично знайде всі WallFacingTag у сцені
//  при старті (якщо його масиви порожні).
// ═══════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class WallFacingTag : MonoBehaviour
{
    [Tooltip("В який бік «дивиться» ця стіна (напрямок нормалі в world space)")]
    public WallDirection Facing = WallDirection.North;

#if UNITY_EDITOR
    private static readonly Color[] GizmoColors =
    {
        new Color(0.2f, 0.4f, 1f, 0.8f),   // North — синій
        new Color(1f, 0.8f, 0.1f, 0.8f),   // South — жовтий
        new Color(1f, 0.3f, 0.2f, 0.8f),   // East  — червоний
        new Color(0.2f, 0.9f, 0.3f, 0.8f), // West  — зелений
    };

    private static readonly string[] GizmoLabels = { "N", "S", "E", "W" };

    private void OnDrawGizmosSelected()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        int idx = (int)Facing;
        Gizmos.color = GizmoColors[idx];

        // Стрілка в напрямку нормалі стіни
        Vector3 center = rend.bounds.center;
        Vector3 dir    = FacingToVector(Facing);
        Gizmos.DrawRay(center, dir * 1.5f);
        Gizmos.DrawSphere(center + dir * 1.5f, 0.1f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = GizmoColors[idx];
        UnityEditor.Handles.Label(center + dir * 1.7f,
            $"  {GizmoLabels[idx]}  ({Facing})",
            new GUIStyle { normal = { textColor = GizmoColors[idx] },
                           fontStyle = FontStyle.Bold, fontSize = 12 });
#endif
    }

    private static Vector3 FacingToVector(WallDirection d) => d switch
    {
        WallDirection.North => Vector3.forward,
        WallDirection.South => Vector3.back,
        WallDirection.East  => Vector3.right,
        WallDirection.West  => Vector3.left,
        _                   => Vector3.forward
    };
#endif
}