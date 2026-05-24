// Assets/Scripts/World/Cabinet/Cabinet.cs
// FIX:
//   1. SetRenderingEnabled — вмикає/вимикає ВСІ BookInstancedRenderer у шафі (не тільки перший)
//   2. GetBounds — враховує позиції всіх полиць як fallback якщо Renderer не знайдено
//   3. _allRenderers — кешований масив всіх рендерерів щоб не робити GetComponentsInChildren кожен кадр

using UnityEngine;
using System.Collections.Generic;

public class Cabinet : MonoBehaviour
{
    [Header("Identity")]
    public string cabinetName = "Cabinet";

    [Header("Shelves")]
    public List<Shelf> shelves = new List<Shelf>();

    // ── Culling cache ─────────────────────────────────────────────
    private BookInstancedRenderer[] _allRenderers;
    private Bounds _cachedBounds;
    private bool   _boundsValid = false;

    // ── Unity ─────────────────────────────────────────────────────

    private void Awake()
    {
        if (shelves == null || shelves.Count == 0)
            shelves = new List<Shelf>(GetComponentsInChildren<Shelf>());

        if (GetComponent<Collider>() == null)
            Debug.LogWarning($"[Cabinet] {cabinetName} не має Collider!");

        // Кешуємо ВСІ рендерери одразу
        _allRenderers = GetComponentsInChildren<BookInstancedRenderer>(includeInactive: true);
        Debug.Log($"[Cabinet] {cabinetName}: {shelves.Count} полиць, {_allRenderers.Length} рендерерів");
    }

    private void OnEnable()
    {
        _boundsValid  = false;
        // Перекешовуємо рендерери якщо шафа реактивується
        _allRenderers = GetComponentsInChildren<BookInstancedRenderer>(includeInactive: true);
    }

    // ── Culling API ───────────────────────────────────────────────

    /// Вмикає або вимикає ВСІ BookInstancedRenderer у шафі.
    /// FIX: GetComponentInChildren знаходив тільки ПЕРШИЙ — Shelf_01 завжди вимикався.
    public void SetRenderingEnabled(bool active)
    {
        if (_allRenderers == null || _allRenderers.Length == 0)
            _allRenderers = GetComponentsInChildren<BookInstancedRenderer>(includeInactive: true);

        foreach (var r in _allRenderers)
            if (r != null) r.SetEnabled(active);
    }

    /// Bounds для frustum culling.
    /// FIX: використовує позиції полиць як fallback бо BookInstancedRenderer
    /// не має Renderer компонента → GetComponentsInChildren<Renderer> його не бачить.
    public Bounds GetBounds()
    {
        if (_boundsValid) return _cachedBounds;

        // Спочатку пробуємо mesh renderers шафи (меблі)
        var meshRenderers = GetComponentsInChildren<Renderer>();
        if (meshRenderers.Length > 0)
        {
            _cachedBounds = meshRenderers[0].bounds;
            for (int i = 1; i < meshRenderers.Length; i++)
                _cachedBounds.Encapsulate(meshRenderers[i].bounds);
        }
        else
        {
            // Fallback: bounds по трансформу полиць
            _cachedBounds = new Bounds(transform.position, Vector3.one * 0.1f);
            foreach (var shelf in shelves)
            {
                if (shelf == null) continue;
                _cachedBounds.Encapsulate(shelf.transform.position);
            }
            // Розширюємо bounds до мінімум 1м щоб frustum culling не давав false negative
            _cachedBounds.Expand(Mathf.Max(1f, _cachedBounds.size.magnitude * 0.3f));
        }

        _boundsValid = true;
        return _cachedBounds;
    }

    // ── Utils ─────────────────────────────────────────────────────

    [ContextMenu("Auto-find Shelves")]
    public void AutoFindShelves()
    {
        shelves = new List<Shelf>(GetComponentsInChildren<Shelf>());
        Debug.Log($"[Cabinet] {cabinetName}: знайдено {shelves.Count} полиць");
    }

    public int GetTotalBookCount()
    {
        int total = 0;
        foreach (var shelf in shelves)
            if (shelf != null) total += shelf.GetBookCount();
        return total;
    }
}