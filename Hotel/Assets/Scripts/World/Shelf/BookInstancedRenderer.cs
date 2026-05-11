// Assets/Scripts/World/Shelf/BookInstancedRenderer.cs
// НОВИЙ ФАЙЛ: єдиний компонент що рендерить усі книги полиці через GPU Instancing.
// Один DrawCall на 1023 книжки замість окремого MeshRenderer на кожну.
//
// UNITY SETUP:
//   1. Додати на root-об'єкт Cabinet (або Shelf якщо шафи немає).
//   2. Призначити bookMesh та bookMaterial у Inspector.
//   3. У bookMaterial увімкнути "Enable GPU Instancing".
//   4. Shelf.cs викликає RebuildFromEntries() після будь-якої зміни книг.
using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Bookstore/Book Instanced Renderer")]
public class BookInstancedRenderer : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────
    [Header("Mesh & Material")]
    [Tooltip("Спільний mesh для всіх книг (повинен мати GPU Instancing у матеріалі)")]
    [SerializeField] private Mesh bookMesh;

    [Tooltip("Матеріал з увімкненим Enable GPU Instancing")]
    [SerializeField] private Material bookMaterial;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = false;

    // ── Private state ─────────────────────────────────────────────────────────
    private readonly List<Matrix4x4> _matrices = new(256);
    private readonly List<Vector4>   _colors   = new(256);
    private MaterialPropertyBlock    _mpb;

    // Кеш масивів для DrawMeshInstanced (уникаємо алокації щокадру)
    private Matrix4x4[] _matrixBatch = new Matrix4x4[1023];
    private Vector4[]   _colorBatch  = new Vector4[1023];

    private bool _isDirty = false;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (_matrices.Count == 0 || bookMesh == null || bookMaterial == null)
            return;

        // Рендеримо батчами по 1023 (ліміт DrawMeshInstanced)
        int total = _matrices.Count;
        for (int start = 0; start < total; start += 1023)
        {
            int count = Mathf.Min(1023, total - start);

            // Копіюємо в pre-allocated масиви (без GC alloc)
            for (int i = 0; i < count; i++)
            {
                _matrixBatch[i] = _matrices[start + i];
                _colorBatch[i]  = _colors[start + i];
            }

            _mpb.SetVectorArray("_BaseColor", _colorBatch);

            Graphics.DrawMeshInstanced(
                bookMesh,
                submeshIndex: 0,
                bookMaterial,
                _matrixBatch,
                count,
                _mpb,
                UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows: true,
                layer: gameObject.layer
            );
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Повністю перебудовує дані рендерингу зі списку ShelfBookEntry.
    /// Викликається Shelf після кожної зміни (PlaceBook, TakeBookAt, завантаження).
    public void RebuildFromEntries(List<ShelfBookEntry> entries, Transform startPoint, Vector3 bookRotation)
    {
        _matrices.Clear();
        _colors.Clear();

        if (entries == null || startPoint == null)
            return;

        foreach (var entry in entries)
        {
            // Перетворюємо локальну позицію відносно startPoint у світову матрицю
            Vector3    worldPos = startPoint.TransformPoint(entry.localPosition);
            Quaternion worldRot = startPoint.rotation
                                  * Quaternion.Euler(bookRotation.x + entry.tilt,
                                                     bookRotation.y,
                                                     bookRotation.z);
            // Масштаб: товщина по X (startPoint.right), висота по Y фіксована з prefab
            Vector3 scale = new Vector3(entry.thickness, entry.height, entry.thickness * 3f);

            _matrices.Add(Matrix4x4.TRS(worldPos, worldRot, scale));
            _colors.Add(entry.coverColor);
        }

        // Розширити кеш якщо книг стало більше
        if (_matrices.Count > _matrixBatch.Length)
        {
            int newSize = Mathf.NextPowerOfTwo(_matrices.Count);
            _matrixBatch = new Matrix4x4[newSize];
            _colorBatch  = new Vector4[newSize];
        }

        _isDirty = false;
    }

    /// Вмикає або вимикає рендеринг (використовується CabinetCullingSystem)
    public void SetEnabled(bool active)
    {
        enabled = active;
    }

    /// Кількість книг що рендеряться зараз
    public int RenderedCount => _matrices.Count;

    // ── Gizmos ───────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || _matrices.Count == 0) return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        foreach (var m in _matrices)
            Gizmos.DrawWireCube(m.GetColumn(3), new Vector3(0.03f, 0.24f, 0.03f));
    }
}