// Assets/Scripts/World/Cabinet/Cabinet.cs
// ДОПОВНЕННЯ: додано SetRenderingEnabled() та GetBounds() для CabinetCullingSystem.
// Решта коду залишається без змін — лише дописати ці методи в існуючий клас.
//
// ⚠️  НЕ замінювати весь файл — додати тільки методи нижче до існуючого Cabinet.cs
//
// Що додати до існуючого Cabinet.cs:
// ─────────────────────────────────────────────────────────────
//
//    [Header("Optimization")]
//    [SerializeField] private BookInstancedRenderer _instancedRenderer;
//
//    // Кешований Bounds — перераховується при потребі
//    private Bounds _cachedBounds;
//    private bool   _boundsValid = false;
//
//    // Викликається CabinetCullingSystem
//    public void SetRenderingEnabled(bool active)
//    {
//        if (_instancedRenderer != null)
//            _instancedRenderer.SetEnabled(active);
//
//        // Якщо renderer не призначений — шукаємо у дітях
//        if (_instancedRenderer == null)
//        {
//            _instancedRenderer = GetComponentInChildren<BookInstancedRenderer>();
//            if (_instancedRenderer != null)
//                _instancedRenderer.SetEnabled(active);
//        }
//    }
//
//    public Bounds GetBounds()
//    {
//        if (_boundsValid) return _cachedBounds;
//
//        // Збираємо bounds з усіх Renderer у шафі
//        var renderers = GetComponentsInChildren<Renderer>();
//        if (renderers.Length == 0)
//        {
//            _cachedBounds = new Bounds(transform.position, Vector3.one * 2f);
//        }
//        else
//        {
//            _cachedBounds = renderers[0].bounds;
//            for (int i = 1; i < renderers.Length; i++)
//                _cachedBounds.Encapsulate(renderers[i].bounds);
//        }
//
//        _boundsValid = true;
//        return _cachedBounds;
//    }
//
//    // Скидаємо кеш якщо шафа переміщується (у OnTransformChildrenChanged або OnEnable)
//    private void OnEnable() => _boundsValid = false;
//
// ─────────────────────────────────────────────────────────────
// Також: _materializedBookRef — публічне поле додане в Shelf.cs (не тут).

// Нижче — повна версія доповненого Cabinet.cs якщо потрібно замінити файл повністю:
using UnityEngine;
using System.Collections.Generic;

public class Cabinet : MonoBehaviour
{
    [Header("Identity")]
    public string cabinetName = "Cabinet";

    [Header("Shelves")]
    public List<Shelf> shelves = new List<Shelf>();

    [Header("Optimization")]
    [SerializeField] private BookInstancedRenderer _instancedRenderer;

    // ── Culling bounds cache ──────────────────────────────────────────────────
    private Bounds _cachedBounds;
    private bool   _boundsValid = false;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        // Авто-знаходження полиць у дочірніх якщо не призначені (оригінальна логіка)
        if (shelves == null || shelves.Count == 0)
            shelves = new List<Shelf>(GetComponentsInChildren<Shelf>());

        // Перевірка collider
        if (GetComponent<Collider>() == null)
            Debug.LogWarning($"[Cabinet] {cabinetName} не має Collider!");

        // Автоматично знаходимо renderer якщо не призначено
        if (_instancedRenderer == null)
            _instancedRenderer = GetComponentInChildren<BookInstancedRenderer>();
    }

    private void OnEnable()
    {
        _boundsValid = false; // скидаємо кеш при активації
    }

    // ── Public API (нові методи для CabinetCullingSystem) ────────────────────

    /// Вмикає або вимикає BookInstancedRenderer.
    /// Викликається CabinetCullingSystem кожні updateInterval секунд.
    public void SetRenderingEnabled(bool active)
    {
        if (_instancedRenderer != null)
        {
            _instancedRenderer.SetEnabled(active);
            return;
        }

        // Ліниво знаходимо renderer
        _instancedRenderer = GetComponentInChildren<BookInstancedRenderer>();
        _instancedRenderer?.SetEnabled(active);
    }

    /// Повертає Bounds шафи для frustum culling (з кешуванням).
    public Bounds GetBounds()
    {
        if (_boundsValid) return _cachedBounds;

        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            _cachedBounds = new Bounds(transform.position, Vector3.one * 2f);
        }
        else
        {
            _cachedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                _cachedBounds.Encapsulate(renderers[i].bounds);
        }

        _boundsValid = true;
        return _cachedBounds;
    }

    // ── Existing methods (без змін) ───────────────────────────────────────────

    [ContextMenu("Auto-find Shelves")]
    public void AutoFindShelves()
    {
        shelves = new List<Shelf>(GetComponentsInChildren<Shelf>());
        Debug.Log($"[Cabinet] Found {shelves.Count} shelves in {cabinetName}");
    }

    public int GetTotalBookCount()
    {
        int total = 0;
        foreach (var shelf in shelves)
            if (shelf != null) total += shelf.GetBookCount();
        return total;
    }
}