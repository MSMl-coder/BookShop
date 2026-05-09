// Assets/Scripts/World/WallUVSync.cs
// ═══════════════════════════════════════════════════════════════════════════
//  Wall UV Sync — синхронізує тайлінг між Static і Dynamic частинами стіни
//  -----------------------------------------------------------------------
//  ПРОБЛЕМА:
//    Два окремих GameObject мають різні локальні координати.
//    Шейдер SG_Wallpapers рахує UV від локального origin кожного меша →
//    патерн смуг зсувається на стику.
//
//  РІШЕННЯ:
//    Використовуємо world-space позицію меша як базу для UV offset.
//    Обидві частини стіни отримують однаковий Offset в матеріалі,
//    прораховуючи world position відносно спільної точки (батьківський GO).
//
//  СТРУКТУРА PREFAB (рекомендована):
//    Wall_North_01  (порожній GameObject — батько, WallUVSync тут)
//      ├── Wall_Static   (кути, завжди видимі)
//      └── Wall_Dynamic  (центральна панель, ховається)
//
//  АЛЬТЕРНАТИВА якщо Prefab ще не створено:
//    Просто постав WallUVSync на будь-який спільний батько,
//    або прямо на Wall_Static і вручну призначи обидва рендерери.
//
//  ВИМОГА ДО SG_Wallpapers:
//    Property "Offset" (Vector2, Reference: _Offset) вже є в шейдері ✓
//    Цей скрипт пише в _Offset через MaterialPropertyBlock (не псує шейдер)
// ═══════════════════════════════════════════════════════════════════════════

using UnityEngine;

[ExecuteAlways] // Працює і в Editor Mode — зразу видно результат
public class WallUVSync : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────

    [Header("Wall Parts")]
    [Tooltip("Статична частина стіни (кути). Її позиція = база для UV.")]
    [SerializeField] private Renderer staticPart;

    [Tooltip("Динамічна частина стіни (центральна панель що ховається).")]
    [SerializeField] private Renderer dynamicPart;

    [Header("UV Settings")]
    [Tooltip("Вісь вздовж якої рахується offset. " +
             "Для вертикальних смуг: WorldAxis.X. " +
             "Для горизонтальних: WorldAxis.Z.")]
    [SerializeField] private WorldAxis patternAxis = WorldAxis.X;

    [Tooltip("Tiling — має збігатись з Tiling у матеріалі стіни.")]
    [SerializeField] private Vector2 tiling = new Vector2(1f, 1f);

    [Tooltip("Ручний додатковий зсув якщо патерн треба підрівняти.")]
    [SerializeField] private float manualOffset = 0f;

    // ── Private ───────────────────────────────────────────────────────────

    private static readonly int OffsetID = Shader.PropertyToID("_Offset");
    private static readonly int TilingID = Shader.PropertyToID("_Tiling");

    private MaterialPropertyBlock _staticBlock;
    private MaterialPropertyBlock _dynamicBlock;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()  => Init();
    private void OnEnable() => Init();

    private void Init()
    {
        _staticBlock  = new MaterialPropertyBlock();
        _dynamicBlock = new MaterialPropertyBlock();
    }

    private void LateUpdate() => Sync();

    // Для Editor Mode (ExecuteAlways)
    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) Sync();
#endif
    }

    // ── Core ──────────────────────────────────────────────────────────────

    private void Sync()
    {
        if (staticPart == null || dynamicPart == null) return;

        // World-position статичної частини як єдина точка відліку
        Vector3 worldOrigin = staticPart.bounds.min;

        // Offset для кожного рендерера в UV-просторі
        Vector2 staticOffset  = WorldToUVOffset(staticPart,  worldOrigin);
        Vector2 dynamicOffset = WorldToUVOffset(dynamicPart, worldOrigin);

        ApplyToRenderer(staticPart,  staticOffset,  _staticBlock);
        ApplyToRenderer(dynamicPart, dynamicOffset, _dynamicBlock);
    }

    private Vector2 WorldToUVOffset(Renderer rend, Vector3 origin)
    {
        // Різниця між позицією цього меша і спільним origin
        Vector3 delta = rend.bounds.min - origin;

        // Проектуємо на потрібну вісь
        float worldDelta = patternAxis == WorldAxis.X ? delta.x : delta.z;

        // Переводимо в UV space з урахуванням tiling
        float uvOffset = (worldDelta * tiling.x) + manualOffset;

        // Повертаємо fractional частину щоб offset був у межах [0,1]
        uvOffset = uvOffset - Mathf.Floor(uvOffset);

        return new Vector2(uvOffset, 0f);
    }

    private static void ApplyToRenderer(Renderer rend, Vector2 offset,
                                         MaterialPropertyBlock block)
    {
        rend.GetPropertyBlock(block);
        block.SetVector(OffsetID, offset);
        rend.SetPropertyBlock(block);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (staticPart == null || dynamicPart == null) return;

        // Показуємо спільний origin
        Vector3 origin = staticPart.bounds.min;
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(origin, 0.08f);

        // Лінія між частинами
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawLine(staticPart.bounds.center, dynamicPart.bounds.center);

        // Підпис
        UnityEditor.Handles.Label(
            (staticPart.bounds.center + dynamicPart.bounds.center) * 0.5f + Vector3.up * 0.3f,
            "UV Sync", new GUIStyle
            {
                normal    = { textColor = Color.cyan },
                fontStyle = FontStyle.Bold,
                fontSize  = 11
            });
    }
#endif

    // ── Public API ────────────────────────────────────────────────────────

    /// Примусово пересинхронізувати (наприклад після переміщення стіни)
    public void ForceSync() => Sync();

    public enum WorldAxis { X, Z }
}