// Assets/Scripts/World/Shelf/BookInstancedRenderer.cs
// FIX: компонент вимикався бо _initialized = false після OnDisable,
//      і pending rebuild не виконувався коректно.
//      Рішення: прибрати _initialized логіку повністю —
//      замість цього EnsureInit() перевіряє лише null масив.
//      Окремий Start() з відкладеним PushToRenderer для першої полиці.

using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Bookstore/Book Instanced Renderer")]
public class BookInstancedRenderer : MonoBehaviour
{
    private const int BATCH_LIMIT = 1023;

    private List<Matrix4x4>[] _matricesByColor;
    private Matrix4x4[]       _batchBuffer;
    private BookGeometryProfile _profile;

    // Зберігаємо останній виклик RebuildFromEntries для повтору в Start()
    private bool                 _hasData;
    private List<ShelfBookEntry> _lastEntries;
    private Transform            _lastStart;
    private BookGeometryProfile  _lastProfile;

    [SerializeField] private bool showGizmos = false;

    public int RenderedCount      { get; private set; }
    public int DrawCallsLastFrame { get; private set; }

    // ── Init ─────────────────────────────────────────────────────
    private void EnsureArrays()
    {
        if (_matricesByColor != null) return;
        _matricesByColor = new List<Matrix4x4>[BookGeometryProfile.PaletteSize];
        for (int i = 0; i < _matricesByColor.Length; i++)
            _matricesByColor[i] = new List<Matrix4x4>(16);
        _batchBuffer = new Matrix4x4[BATCH_LIMIT];
    }

    private void Awake()  => EnsureArrays();
    

        private void OnDisable() => Debug.LogError($"[BIR] DISABLED on {gameObject.name}", this);
     
    
    /// Start: якщо RebuildFromEntries вже викликали — перемальовуємо.
    /// Це вирішує баг з першою полицею де Awake ще не завершився.
    private void Start()
    {
        if (_hasData)
            RebuildFromEntries(_lastEntries, _lastStart, _lastProfile);
    }

    // ── Render Loop ──────────────────────────────────────────────
    private void LateUpdate()
    {
        if (_profile == null || _profile.baseMesh == null) return;
        EnsureArrays(); // захист на випадок якщо OnEnable раніше Awake

        DrawCallsLastFrame = 0;

        for (int c = 0; c < _matricesByColor.Length; c++)
        {
            var mats = _matricesByColor[c];
            if (mats == null || mats.Count == 0) continue;

            Material cover = _profile.GetCoverMaterial(c);
            if (cover == null) continue;

            for (int s = 0; s < mats.Count; s += BATCH_LIMIT)
            {
                int count = Mathf.Min(BATCH_LIMIT, mats.Count - s);
                for (int i = 0; i < count; i++)
                    _batchBuffer[i] = mats[s + i];

                Graphics.DrawMeshInstanced(
                    _profile.baseMesh, 0, cover, _batchBuffer, count, null,
                    _profile.shadowMode, _profile.receiveShadows, gameObject.layer);
                DrawCallsLastFrame++;

                if (_profile.pagesMaterial != null && _profile.baseMesh.subMeshCount > 1)
                {
                    Graphics.DrawMeshInstanced(
                        _profile.baseMesh, 1, _profile.pagesMaterial, _batchBuffer, count, null,
                        _profile.shadowMode, _profile.receiveShadows, gameObject.layer);
                    DrawCallsLastFrame++;
                }
            }
        }
    }

    // ── Public API ───────────────────────────────────────────────
    public void RebuildFromEntries(
        List<ShelfBookEntry> entries,
        Transform            startPoint,
        BookGeometryProfile  profile)
    {
        EnsureArrays(); // безпечно викликати до Awake

        // Завжди зберігаємо останні дані для відновлення після OnEnable/Start
        _lastEntries = entries != null ? new List<ShelfBookEntry>(entries) : null;
        _lastStart   = startPoint;
        _lastProfile = profile;
        _hasData     = true;

        _profile = profile;

        for (int i = 0; i < _matricesByColor.Length; i++)
            _matricesByColor[i].Clear();

        RenderedCount = 0;
        if (entries == null || entries.Count == 0 || startPoint == null || profile == null)
            return;

        Matrix4x4  startLocal = startPoint.localToWorldMatrix;
        Quaternion meshFix    = Quaternion.Euler(profile.meshRotationFix);

        Vector3 bs = profile.baseSize;
        float   bX = bs.x > 0f ? bs.x : 1f;
        float   bY = bs.y > 0f ? bs.y : 1f;
        float   bZ = bs.z > 0f ? bs.z : 1f;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            int c = Mathf.Clamp(e.colorIndex, 0, _matricesByColor.Length - 1);

            Quaternion rot   = Quaternion.Euler(0f, 0f, e.tilt) * meshFix;
            Vector3    scale = new Vector3(e.thickness / bX, e.height / bY, e.depth / bZ);

            _matricesByColor[c].Add(startLocal * Matrix4x4.TRS(e.localPosition, rot, scale));
            RenderedCount++;
        }
    }

    /// Повторно рендеримо з останніх даних (наприклад після OnEnable)
    public void Refresh()
    {
        if (_hasData)
            RebuildFromEntries(_lastEntries, _lastStart, _lastProfile);
    }

    public void SetEnabled(bool active) => enabled = active;

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || _matricesByColor == null) return;
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        foreach (var list in _matricesByColor)
        {
            if (list == null) continue;
            foreach (var m in list)
                Gizmos.DrawWireSphere(m.GetColumn(3), 0.02f);
        }
    }
}