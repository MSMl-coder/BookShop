// Assets/Scripts/World/Shelf/BookInstancedRenderer.cs
// Колір книги передається через _BaseColor (Color, Per Render Data) — палітра в коді.
// Не залежить від UV розгортки меша і UV атласу.
//
// В Shader Graph (SG_BookSpine):
//   - Видали Color Atlas, UV Rect, Split, Multiply, Add, Vector2 ноди
//   - Залиш тільки: _BaseColor (Color, Per Render Data, Scope=PerRenderData) → Base Color
//   - Або: _BaseColor × Tint → Base Color (якщо хочеш глобальне тонування)
using UnityEngine;
using System.Collections.Generic;

// UNITY SETUP: додавати на кожну Shelf окремо (не на Cabinet).
// Кожна Shelf має свій BookInstancedRenderer.
// Shelf.cs знаходить його через GetComponent<BookInstancedRenderer>() (не InParent).
[AddComponentMenu("Bookstore/Book Instanced Renderer")]
public class BookInstancedRenderer : MonoBehaviour
{
    // ── Палітра 16 кольорів — відповідає colorIndex 0–15 ────────────────────
    public static readonly Color[] Palette =
    {
        new Color(0.27f, 0.13f, 0.07f), // 0  темно-коричневий
        new Color(0.55f, 0.18f, 0.11f), // 1  червоно-коричневий
        new Color(0.72f, 0.42f, 0.18f), // 2  охра
        new Color(0.76f, 0.69f, 0.54f), // 3  бежевий
        new Color(0.45f, 0.47f, 0.34f), // 4  оливковий
        new Color(0.25f, 0.35f, 0.22f), // 5  темно-зелений
        new Color(0.18f, 0.25f, 0.35f), // 6  темно-синій
        new Color(0.35f, 0.42f, 0.52f), // 7  сталевий
        new Color(0.52f, 0.28f, 0.18f), // 8  теракота
        new Color(0.68f, 0.55f, 0.38f), // 9  пісочний
        new Color(0.30f, 0.22f, 0.42f), // 10 фіолетовий
        new Color(0.42f, 0.18f, 0.22f), // 11 бордо
        new Color(0.18f, 0.38f, 0.42f), // 12 бірюзовий
        new Color(0.55f, 0.50f, 0.42f), // 13 сірий теплий
        new Color(0.62f, 0.35f, 0.15f), // 14 рудий
        new Color(0.22f, 0.22f, 0.25f), // 15 антрацит
    };

    public static Color GetColor(int colorIndex) =>
        Palette[Mathf.Clamp(colorIndex, 0, Palette.Length - 1)];

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Mesh & Material")]
    [SerializeField] private Mesh     bookMesh;
    [SerializeField] private Material coverMaterial;  // обкладинка — SG_BookSpine_base
    [SerializeField] private Material pagesMaterial;  // сторінки — білий/кремовий URP Lit

    [Header("Mesh Correction")]
    [Tooltip("Компенсація повороту меша. Book000 має X=-90° в prefab → виправляємо тут." +
             "Якщо книги лежать — спробуй (90,0,0). Якщо стоять але розгорнуті — (90,0,90).")]
    [Header("Debug")]
    [SerializeField] private bool showGizmos = false;

    // Зворотна сумісність
    public Material BookMaterial => coverMaterial;

    // ── Дані рендерингу ───────────────────────────────────────────────────────
    private readonly List<Matrix4x4> _matrices = new(256);
    private readonly List<Vector4>   _colors   = new(256);

    private Matrix4x4[] _matrixBatch = new Matrix4x4[1023];
    private Vector4[]   _colorBatch  = new Vector4[1023];

    private MaterialPropertyBlock _mpb;


    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake() => _mpb = new MaterialPropertyBlock();

    private void Update()
    {
        if (_matrices.Count == 0 || bookMesh == null || coverMaterial == null) return;

        int total = _matrices.Count;
        for (int start = 0; start < total; start += 1023)
        {
            int count = Mathf.Min(1023, total - start);

            for (int i = 0; i < count; i++) _matrixBatch[i] = _matrices[start + i];
            for (int i = 0; i < count; i++) _colorBatch[i]  = _colors[start + i];

            // submesh 0 — обкладинка з кольором з палітри
            _mpb.SetVectorArray("_BaseColor", _colorBatch);
            Graphics.DrawMeshInstanced(
                bookMesh, 0, coverMaterial, _matrixBatch, count, _mpb,
                UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows: true, layer: gameObject.layer);

            // submesh 1 — сторінки (якщо є другий submesh і матеріал)
            if (pagesMaterial != null && bookMesh.subMeshCount > 1)
                Graphics.DrawMeshInstanced(
                    bookMesh, 1, pagesMaterial, _matrixBatch, count, null,
                    UnityEngine.Rendering.ShadowCastingMode.On,
                    receiveShadows: true, layer: gameObject.layer);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void RebuildFromEntries(List<ShelfBookEntry> entries, Transform startPoint, Vector3 bookRotation)
    {
        _matrices.Clear();
        _colors.Clear();

        if (entries == null || startPoint == null) return;

        // Нормуємо розмір меша один раз — щоб scale в матриці = реальні метри
        // Якщо bookMesh.bounds.size = (0.1, 0.3, 0.05), то нормалізуючий коефіцієнт
        // компенсує це і scale стає в реальних одиницях
        Vector3 meshBounds = bookMesh != null && bookMesh.bounds.size != Vector3.zero
            ? bookMesh.bounds.size
            : Vector3.one;

        foreach (var entry in entries)
        {
            // Колір з палітри по colorIndex
            Color c = GetColor(entry.colorIndex);
            _colors.Add(new Vector4(c.r, c.g, c.b, c.a));

            // Якщо є renderMatrix з реального GO — використовуємо її напряму.
            // Вона вже містить правильний transform включно з ротацією меша (-90° X)
            // і масштабом батьківського prefab (Book001 scale 0.8 тощо).
            if (entry.renderMatrix != Matrix4x4.zero)
            {
                _matrices.Add(entry.renderMatrix);
                continue;
            }

            // Fallback — будуємо матрицю вручну (при завантаженні збереження
            // поки GO ще не заспавнений)
            Vector3    worldPos = startPoint.TransformPoint(entry.localPosition);
            Quaternion worldRot = startPoint.rotation
                                  * Quaternion.Euler(bookRotation.x + entry.tilt,
                                                     bookRotation.y, bookRotation.z);
            Vector3 scale = new Vector3(
                entry.thickness          / meshBounds.x,
                (entry.thickness * 2.5f) / meshBounds.y,
                entry.height             / meshBounds.z
            );
            _matrices.Add(Matrix4x4.TRS(worldPos, worldRot, scale));
        }

        // Розширити батч якщо треба
        if (_matrices.Count > _matrixBatch.Length)
        {
            int n = Mathf.NextPowerOfTwo(_matrices.Count);
            _matrixBatch = new Matrix4x4[n];
            _colorBatch  = new Vector4[n];
        }
    }

    public void SetEnabled(bool active) => enabled = active;
    public int RenderedCount => _matrices.Count;

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || _matrices.Count == 0) return;
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        foreach (var m in _matrices)
            // Просто точка де центр книги
            Gizmos.DrawWireSphere(m.GetColumn(3), 0.02f);
    }
}