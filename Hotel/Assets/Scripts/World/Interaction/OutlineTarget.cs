// Assets/Scripts/World/Interaction/OutlineTarget.cs
// Керує hover-підсвіткою одного об'єкта.
// Автоматично додається HoverHighlighter через GetOrAdd — не треба
// вручну додавати на кожен prefab.
//
// SetHighlight(true)  → всі Renderer-и переходять в Layer "Outline"
//                        → URP RenderObjects feature малює їх через OutlineHoverMat
// SetHighlight(false) → Renderer-и повертаються в оригінальний layer
//
// Якщо Layer "Outline" не існує — виводить попередження один раз і
// нічого не робить (не крашиться).

using UnityEngine;

public class OutlineTarget : MonoBehaviour
{
    [Tooltip("Залиш порожнім — знайде автоматично всі дочірні Renderer-и")]
    [SerializeField] private Renderer[] targetRenderers;

    private int[]  _originalLayers;
    private bool   _highlighted;
    private bool   _initialized;

    private static int  _outlineLayer   = -2; // -2 = ще не шукали
    private static bool _warnedMissing;

    // ── Unity ──────────────────────────────────────────────────

    private void Awake() => EnsureInit();

    // ── Public API ─────────────────────────────────────────────

    public void SetHighlight(bool on)
    {
        EnsureInit();
        if (_highlighted == on) return;

        // Layer не знайдений — пропускаємо але не крашимось
        if (_outlineLayer < 0)
        {
            if (!_warnedMissing)
            {
                Debug.LogWarning("[OutlineTarget] Layer 'Outline' не знайдено.\n" +
                                 "Створи його: Edit → Project Settings → Tags and Layers");
                _warnedMissing = true;
            }
            return;
        }

        _highlighted = on;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null) continue;
            targetRenderers[i].gameObject.layer = on ? _outlineLayer : _originalLayers[i];
        }
    }

    private void OnDisable()
    {
        if (_highlighted) SetHighlight(false);
    }

    // ── Private ────────────────────────────────────────────────

    private void EnsureInit()
    {
        if (_initialized) return;
        _initialized = true;

        // Шукаємо layer один раз для всіх екземплярів
        if (_outlineLayer == -2)
            _outlineLayer = LayerMask.NameToLayer("Outline"); // -1 якщо не знайдено

        // Автоматично знаходимо Renderer-и якщо не призначені
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(includeInactive: false);

        // Зберігаємо оригінальні layers
        _originalLayers = new int[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
            _originalLayers[i] = targetRenderers[i] != null
                                ? targetRenderers[i].gameObject.layer
                                : 0;
    }
}