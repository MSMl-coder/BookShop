// Assets/Scripts/World/Shelf/BookInstancedRenderer.cs
//
// v3.2 ЗМІНИ:
//   • Прибрано zExtra з матриці — висування видалено
//   • Scale напряму з thickness/height/depth ÷ baseSize
//
// Колір — окремий матеріал на кожен з 16 варіантів.
// Групування по colorIndex → до 32 draw calls (16 × 2 submesh).

using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Bookstore/Book Instanced Renderer")]
public class BookInstancedRenderer : MonoBehaviour
{
    private const int BATCH_LIMIT = 1023;

    private readonly List<Matrix4x4>[] _matricesByColor =
        new List<Matrix4x4>[BookGeometryProfile.PaletteSize];

    private Matrix4x4[] _batchBuffer = new Matrix4x4[BATCH_LIMIT];

    private BookGeometryProfile _profile;

    [SerializeField] private bool showGizmos = false;

    public int RenderedCount { get; private set; }
    public int DrawCallsLastFrame { get; private set; }

    private void Awake()
    {
        for (int i = 0; i < _matricesByColor.Length; i++)
            _matricesByColor[i] = new List<Matrix4x4>(16);
    }

    private void LateUpdate()
    {
        if (_profile == null || _profile.baseMesh == null) return;

        DrawCallsLastFrame = 0;

        for (int colorIdx = 0; colorIdx < _matricesByColor.Length; colorIdx++)
        {
            List<Matrix4x4> matrices = _matricesByColor[colorIdx];
            if (matrices == null || matrices.Count == 0) continue;

            Material coverMat = _profile.GetCoverMaterial(colorIdx);
            if (coverMat == null) continue;

            for (int start = 0; start < matrices.Count; start += BATCH_LIMIT)
            {
                int count = Mathf.Min(BATCH_LIMIT, matrices.Count - start);
                for (int i = 0; i < count; i++)
                    _batchBuffer[i] = matrices[start + i];

                Graphics.DrawMeshInstanced(
                    _profile.baseMesh, 0, coverMat,
                    _batchBuffer, count, null,
                    _profile.shadowMode, _profile.receiveShadows,
                    gameObject.layer);
                DrawCallsLastFrame++;

                if (_profile.pagesMaterial != null && _profile.baseMesh.subMeshCount > 1)
                {
                    Graphics.DrawMeshInstanced(
                        _profile.baseMesh, 1, _profile.pagesMaterial,
                        _batchBuffer, count, null,
                        _profile.shadowMode, _profile.receiveShadows,
                        gameObject.layer);
                    DrawCallsLastFrame++;
                }
            }
        }
    }

    public void RebuildFromEntries(
        List<ShelfBookEntry> entries,
        Transform startPoint,
        BookGeometryProfile profile)
    {
        _profile = profile;

        for (int i = 0; i < _matricesByColor.Length; i++)
            _matricesByColor[i].Clear();

        RenderedCount = 0;
        if (entries == null || entries.Count == 0 || startPoint == null || profile == null)
            return;

        Matrix4x4  startLocalToWorld = startPoint.localToWorldMatrix;
        Quaternion meshFix           = Quaternion.Euler(profile.meshRotationFix);

        Vector3 baseSize = profile.baseSize;
        float   bsX = baseSize.x > 0f ? baseSize.x : 1f;
        float   bsY = baseSize.y > 0f ? baseSize.y : 1f;
        float   bsZ = baseSize.z > 0f ? baseSize.z : 1f;

        for (int i = 0; i < entries.Count; i++)
        {
            ShelfBookEntry e = entries[i];

            int colorIdx = Mathf.Clamp(e.colorIndex, 0, _matricesByColor.Length - 1);

            // Tilt — поворот навколо Z startPoint (бокове коливання книги).
            // Z startPoint вказує в полицю; обертання навколо нього = верхівка ходить ліво/право.
            // Tilt застосовується ПЕРЕД meshFix щоб працював на вже-обертовому меші.
            Quaternion localRot = Quaternion.Euler(0f, 0f, e.tilt) * meshFix;

            // Scale напряму з реальних розмірів
            Vector3 scale = new Vector3(
                e.thickness / bsX,
                e.height    / bsY,
                e.depth     / bsZ);

            Matrix4x4 slotLocal = Matrix4x4.TRS(e.localPosition, localRot, scale);
            Matrix4x4 world     = startLocalToWorld * slotLocal;

            _matricesByColor[colorIdx].Add(world);
            RenderedCount++;
        }
    }

    public void SetEnabled(bool active) => enabled = active;

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        for (int c = 0; c < _matricesByColor.Length; c++)
        {
            if (_matricesByColor[c] == null) continue;
            foreach (var m in _matricesByColor[c])
                Gizmos.DrawWireSphere(m.GetColumn(3), 0.02f);
        }
    }
}