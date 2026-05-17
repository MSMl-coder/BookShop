// Assets/Scripts/Data/Books/BookGeometryProfile.cs
//
// ScriptableObject з геометрією і рендер-ресурсами книг.
// ОДИН asset для всього проєкту — підвʼязується до кожного Shelf.
//
// v2.0 ЗМІНИ:
//   • ВИДАЛЕНО smallScale/mediumScale/largeScale поля.
//     Розміри тепер беруться з BookTemplate.containerPrefab.localScale × baseSize.
//   • BookSize в template залишається тільки як категорія для maxBookSize обмеження полиці.
//
// WORKFLOW для DESIGNER:
//   1. Assets → Create → Bookstore → Book Geometry Profile
//   2. Признач baseMesh = Book000_base mesh (2 submesh: [0]=cover, [1]=pages)
//   3. coverMaterials[0..15] = 16 матеріалів BookCover_00..15 (різний UV Rect.X)
//   4. pagesMaterial = матеріал сторінок
//   5. baseSize = реальні розміри baseMesh у Blender (метри)
//
// ПРИКЛАД scale-варіювання:
//   baseSize = (0.030, 0.215, 0.145)
//   BookTemplate.containerPrefab.localScale = (1.24, 0.94, 1.06)
//   → реальні розміри книги: (0.0372, 0.2021, 0.1537)

using UnityEngine;

[CreateAssetMenu(fileName = "BookGeometryProfile", menuName = "Bookstore/Book Geometry Profile", order = 0)]
public class BookGeometryProfile : ScriptableObject
{
    public const int PaletteSize = 16;

    [Header("Mesh")]
    [Tooltip("Базовий меш книги. 2 submesh: [0]=cover, [1]=pages.")]
    public Mesh baseMesh;

    [Header("Materials — 16 варіантів обкладинки")]
    [Tooltip("16 матеріалів BookCover_00..15 з різним UV Rect.X (0/16, 1/16, ... 15/16).\n" +
             "Enable GPU Instancing ✓ на кожному.")]
    public Material[] coverMaterials = new Material[PaletteSize];

    [Tooltip("Матеріал сторінок (BookPaper). Enable GPU Instancing ✓.")]
    public Material pagesMaterial;

    [Header("Base Real-World Size (meters, Unity coords)")]
    [Tooltip("Розміри baseMesh на scale (1,1,1). Множаться на BookTemplate.containerPrefab.localScale.\n" +
             "X = thickness (вздовж полиці), Y = height (вгору), Z = depth (в полицю).")]
    public Vector3 baseSize = new Vector3(0.030f, 0.215f, 0.145f);

    [Header("Mesh Rotation Correction")]
    [Tooltip("Якщо у Blender меш зорієнтований не як хочеш у Unity — компенсуй.\n" +
             "Наприклад, (90, 0, 0) якщо книги лежать на сцені.")]
    public Vector3 meshRotationFix = Vector3.zero;

    [Header("Rendering")]
    public UnityEngine.Rendering.ShadowCastingMode shadowMode =
        UnityEngine.Rendering.ShadowCastingMode.On;

    public bool receiveShadows = true;

    // ── Public API: Materials ───────────────────────────────────────────────

    /// <summary>Безпечний accessor — повертає матеріал по colorIndex з clamping.</summary>
    public Material GetCoverMaterial(int colorIndex)
    {
        if (coverMaterials == null || coverMaterials.Length == 0) return null;
        int idx = Mathf.Clamp(colorIndex, 0, coverMaterials.Length - 1);
        return coverMaterials[idx];
    }

    // ── Public API: Size from prefab ────────────────────────────────────────

    /// <summary>
    /// Обчислює реальні розміри книги: baseSize × prefab.localScale.
    /// Якщо prefab null — повертає baseSize (як scale 1,1,1).
    /// </summary>
    public Vector3 GetRealSizeFromPrefab(GameObject prefab)
    {
        if (prefab == null) return baseSize;
        Vector3 s = prefab.transform.localScale;
        return new Vector3(baseSize.x * s.x, baseSize.y * s.y, baseSize.z * s.z);
    }

    // ── Validation (Editor only) ────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (coverMaterials == null || coverMaterials.Length != PaletteSize)
            System.Array.Resize(ref coverMaterials, PaletteSize);

        if (baseSize.x <= 0f || baseSize.y <= 0f || baseSize.z <= 0f)
            Debug.LogWarning($"[BookGeometryProfile] '{name}': baseSize має бути > 0.");

        if (baseMesh != null && baseMesh.subMeshCount < 2)
            Debug.LogWarning($"[BookGeometryProfile] '{name}': baseMesh '{baseMesh.name}' " +
                             $"має лише {baseMesh.subMeshCount} submesh — потрібно 2 (cover + pages).");

        if (coverMaterials != null)
        {
            for (int i = 0; i < coverMaterials.Length; i++)
            {
                var m = coverMaterials[i];
                if (m != null && !m.enableInstancing)
                    Debug.LogWarning($"[BookGeometryProfile] '{name}': coverMaterials[{i}] " +
                                     $"'{m.name}' → Enable GPU Instancing ✓");
            }
        }

        if (pagesMaterial != null && !pagesMaterial.enableInstancing)
            Debug.LogWarning($"[BookGeometryProfile] '{name}': pagesMaterial " +
                             $"'{pagesMaterial.name}' → Enable GPU Instancing ✓");
    }
#endif
}