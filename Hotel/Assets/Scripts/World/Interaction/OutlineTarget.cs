// Assets/Scripts/World/Interaction/OutlineTarget.cs
// Hover outline через додавання матеріалу в sharedMaterials[].
// Не потребує шейдерів, stencil, або RenderObjects feature.
//
// UNITY SETUP:
//   1. Створи матеріал OutlineHoverMat:
//      Shader: Universal Render Pipeline/Unlit
//      Color: білий (1,1,1,1)
//      Surface: Opaque
//   2. Assign в Inspector → Outline Material
//   3. Компонент додається автоматично через HoverHighlighter.GetOrAdd()

using UnityEngine;

public class OutlineTarget : MonoBehaviour
{
    [Tooltip("Матеріал що додається як overlay для outline ефекту.\n" +
             "Shader: URP/Unlit, Color: білий")]
    [SerializeField] private Material outlineMaterial;

    private Renderer[] _renderers;
    private Material[][] _originalMaterials; // оригінальні масиви матеріалів
    private bool _highlighted;
    private bool _initialized;

    // ── Public API ──────────────────────────────────────────────

    public void SetHighlight(bool on)
    {
        EnsureInit();
        if (_highlighted == on) return;
        if (outlineMaterial == null)
        {
            if (on) Debug.LogWarning($"[OutlineTarget] outlineMaterial не призначено на {gameObject.name}!");
            return;
        }

        _highlighted = on;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            if (on)
            {
                // Додаємо outline матеріал як останній в масиві
                var original = _originalMaterials[i];
                var newMats  = new Material[original.Length + 1];
                original.CopyTo(newMats, 0);
                newMats[original.Length] = outlineMaterial;
                _renderers[i].materials = newMats;
            }
            else
            {
                // Відновлюємо оригінальний масив
                _renderers[i].materials = _originalMaterials[i];
            }
        }
    }

    private void OnDisable()
    {
        if (_highlighted) SetHighlight(false);
    }

    // ── Private ─────────────────────────────────────────────────

    private void EnsureInit()
    {
        if (_initialized) return;
        _initialized = true;

        _renderers = GetComponentsInChildren<Renderer>(includeInactive: false);
        _originalMaterials = new Material[_renderers.Length][];

        for (int i = 0; i < _renderers.Length; i++)
            _originalMaterials[i] = _renderers[i] != null
                ? _renderers[i].materials
                : new Material[0];
    }
}