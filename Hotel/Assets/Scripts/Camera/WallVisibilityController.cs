// Assets/Scripts/Camera/WallVisibilityController.cs
// ═══════════════════════════════════════════════════════════════════════════
//  Wall Visibility — по куту повороту камери
//  -----------------------------------------------------------------------
//  Ідея: стіни заздалегідь розбиті на 4 групи за напрямком "обличчям":
//    North (Z+), South (Z-), East (X+), West (X-)
//
//  Камера крутиться → визначаємо в якому квадранті її Y-кут →
//  ховаємо стіни що "дивляться на камеру" (тобто стоять між нею і кімнатою).
//
//  Жодних Raycast, жодної прозорості. Просто enabled on/off.
//  Стіна ховається тільки якщо камера дивиться з її боку.
//
//  UNITY SETUP:
//    1. Компонент — на тому ж GameObject що й Camera.
//    2. Стінам призначити компонент WallFacingTag з відповідним напрямком.
//       АБО: заповнити масиви вручну в Inspector цього компонента.
//    3. Більше нічого не потрібно.
//
//  Як визначити напрямок стіни:
//    North (Z+) — стіна що "дивиться" на північ, тобто її нормаль = (0,0,+1)
//                  стоїть на південному краю кімнати, перекриває вид з півдня
//    Правило: стіна ховається коли камера знаходиться з ТОГО ж боку
//             що і її нормаль. Наприклад North стіна ховається коли
//             камера дивиться з North (кут ~0° або ~360°).
// ═══════════════════════════════════════════════════════════════════════════

using UnityEngine;
using System.Collections.Generic;

public class WallVisibilityController : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────

    [Header("Wall Groups (заповни вручну або через WallFacingTag)")]
    [Tooltip("Стіни що дивляться на North (нормаль Z+). Ховаються коли камера на North.")]
    [SerializeField] private Renderer[] northWalls;

    [Tooltip("Стіни що дивляться на South (нормаль Z-). Ховаються коли камера на South.")]
    [SerializeField] private Renderer[] southWalls;

    [Tooltip("Стіни що дивляться на East (нормаль X+). Ховаються коли камера на East.")]
    [SerializeField] private Renderer[] eastWalls;

    [Tooltip("Стіни що дивляться на West (нормаль X-). Ховаються коли камера на West.")]
    [SerializeField] private Renderer[] westWalls;

    [Header("Settings")]
    [Tooltip("Зона перекриття між секторами (градусів). " +
             "При переході між секторами ховаються дві сусідні групи разом. " +
             "Рекомендовано: 10-20°")]
    [SerializeField] private float blendZone = 15f;

    // ── Private ───────────────────────────────────────────────────────────

    // Поточний активний стан кожного Renderer (false = прихований)
    private readonly Dictionary<Renderer, bool> _state = new();

    private WallDirection _lastDirection = (WallDirection)(-1); // форс першого апдейту

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Start()
    {
        // Авто-збір через WallFacingTag якщо масиви порожні
        AutoCollectFromTags();
        // Реєструємо всі стіни як видимі
        RegisterAll();
    }

    private void LateUpdate()
    {
        float yAngle = NormalizeAngle(transform.eulerAngles.y);
        UpdateVisibility(yAngle);
    }

    private void OnDisable()  => ShowAll();
    private void OnDestroy()  => ShowAll();

    // ── Visibility Logic ──────────────────────────────────────────────────

    private void UpdateVisibility(float yAngle)
    {
        // Визначаємо які групи треба сховати залежно від кута камери.
        // Камера дивиться в напрямку свого forward (Z-).
        // Якщо yAngle ≈ 0°   → камера дивиться на South → ховаємо South стіни
        // Якщо yAngle ≈ 90°  → камера дивиться на West  → ховаємо West стіни
        // Якщо yAngle ≈ 180° → камера дивиться на North → ховаємо North стіни
        // Якщо yAngle ≈ 270° → камера дивиться на East  → ховаємо East стіни

        bool hideNorth = InSector(yAngle, 180f, blendZone);
        bool hideSouth = InSector(yAngle,   0f, blendZone);
        bool hideEast  = InSector(yAngle, 270f, blendZone);
        bool hideWest  = InSector(yAngle,  90f, blendZone);

        SetGroupVisible(northWalls, !hideNorth);
        SetGroupVisible(southWalls, !hideSouth);
        SetGroupVisible(eastWalls,  !hideEast);
        SetGroupVisible(westWalls,  !hideWest);
    }

    /// Чи знаходиться кут у межах сектора (center ± halfWidth)
    private static bool InSector(float angle, float center, float halfWidth)
    {
        float diff = Mathf.Abs(Mathf.DeltaAngle(angle, center));
        return diff <= halfWidth + 45f; // 45° = половина квадранта
    }

    private void SetGroupVisible(Renderer[] group, bool visible)
    {
        if (group == null) return;
        foreach (Renderer r in group)
        {
            if (r == null) continue;
            if (!_state.TryGetValue(r, out bool current) || current != visible)
            {
                r.enabled    = visible;
                _state[r]    = visible;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ShowAll()
    {
        SetGroupVisible(northWalls, true);
        SetGroupVisible(southWalls, true);
        SetGroupVisible(eastWalls,  true);
        SetGroupVisible(westWalls,  true);
    }

    private void RegisterAll()
    {
        Register(northWalls);
        Register(southWalls);
        Register(eastWalls);
        Register(westWalls);
    }

    private void Register(Renderer[] group)
    {
        if (group == null) return;
        foreach (Renderer r in group)
            if (r != null) _state[r] = true;
    }

    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a < 0) a += 360f;
        return a;
    }

    // ── Auto-collect via WallFacingTag ────────────────────────────────────

    private void AutoCollectFromTags()
    {
        // Якщо хоча б один масив вже заповнений — не перезаписуємо
        if ((northWalls != null && northWalls.Length > 0) ||
            (southWalls != null && southWalls.Length > 0) ||
            (eastWalls  != null && eastWalls.Length  > 0) ||
            (westWalls  != null && westWalls.Length  > 0))
            return;

        var north = new List<Renderer>();
        var south = new List<Renderer>();
        var east  = new List<Renderer>();
        var west  = new List<Renderer>();

        foreach (WallFacingTag tag in FindObjectsByType<WallFacingTag>(
                     FindObjectsSortMode.None))
        {
            Renderer[] renderers = tag.GetComponentsInChildren<Renderer>(true);
            switch (tag.Facing)
            {
                case WallDirection.North: north.AddRange(renderers); break;
                case WallDirection.South: south.AddRange(renderers); break;
                case WallDirection.East:  east.AddRange(renderers);  break;
                case WallDirection.West:  west.AddRange(renderers);  break;
            }
        }

        if (north.Count > 0) northWalls = north.ToArray();
        if (south.Count > 0) southWalls = south.ToArray();
        if (east.Count  > 0) eastWalls  = east.ToArray();
        if (west.Count  > 0) westWalls  = west.ToArray();

        Debug.Log($"[WallVisibility] Auto-collected: " +
                  $"N={northWalls?.Length ?? 0} S={southWalls?.Length ?? 0} " +
                  $"E={eastWalls?.Length  ?? 0} W={westWalls?.Length  ?? 0}");
    }

#if UNITY_EDITOR
    // Gizmos — показує активний сектор у Scene View
    private void OnDrawGizmosSelected()
    {
        float yAngle = NormalizeAngle(transform.eulerAngles.y);
        Vector3 pos  = transform.position;

        void DrawSector(float center, Color color, string label)
        {
            bool active = InSector(yAngle, center, blendZone);
            Gizmos.color = active ? color : new Color(color.r, color.g, color.b, 0.15f);
            float rad = center * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
            Gizmos.DrawRay(pos, dir * 5f);
        }

        DrawSector(180f, Color.blue,   "N");
        DrawSector(0f,   Color.yellow, "S");
        DrawSector(270f, Color.red,    "E");
        DrawSector(90f,  Color.green,  "W");
    }
#endif
}

public enum WallDirection { North, South, East, West }