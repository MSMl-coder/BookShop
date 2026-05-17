// Assets/Scripts/Diagnostics/BookInstancingDiagnostic.cs
//
// ─────────────────────────────────────────────────────────────────────
// ДІАГНОСТИЧНИЙ СКРИПТ — НЕ для продакшну
// ─────────────────────────────────────────────────────────────────────
//
// ЩО РОБИТЬ:
//   Спавнить 48 книг (16 кольорів × 3 розміри) на сцені через
//   Graphics.DrawMeshInstanced з групуванням по кольорах.
//   Це СИМУЛЯЦІЯ того, як працюватиме фінальний BookInstancedRenderer.
//
// ЯК ВИКОРИСТАТИ:
//   1. Додай цей файл у Assets/Scripts/Diagnostics/.
//   2. На сцені створи порожній GameObject "BookDiagnostic".
//   3. Додай цей компонент.
//   4. У Inspector:
//      - Book Mesh = твій базовий меш книги (Book000_base mesh asset)
//      - Cover Material = матеріал з SG_BookSpine_base (Scope=Per Material)
//      - Pages Material = можна BookPaper.mat (або просто URP Lit білий)
//   5. Натисни Play.
//   6. Перевір:
//      a) В сцені бачиш 48 книг різних кольорів?
//      b) В Game window → Stats → Batches. Запам'ятай число.
//      c) В Console читай повідомлення цього скрипта.
//
// ОЧІКУВАНИЙ РЕЗУЛЬТАТ:
//   - 48 книг видимі.
//   - КОЖНА книга має СВІЙ колір з палітри (не всі однакові!).
//   - Stats → Batches: ~32 (16 colors × 2 submesh) + scene overhead.
//   - Console: "[DIAG] Книги різного кольору видимі — OK".
//
// ЯКЩО НЕ ПРАЦЮЄ:
//   - "Всі книги одного кольору" → Scope матеріала неправильний (треба Per Material).
//   - "Невидимі" → меш не задано / pivot меша зміщений.
//   - "Crash на InvalidOperationException" → Enable GPU Instancing в матеріалі вимкнено.

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[AddComponentMenu("Diagnostics/Book Instancing Diagnostic")]
public class BookInstancingDiagnostic : MonoBehaviour
{
    [Header("Rendering Assets")]
    [Tooltip("Базовий меш книги (Book000 mesh, 2 submesh).")]
    public Mesh bookMesh;

    [Tooltip("Cover матеріал. Enable GPU Instancing ✓. _BaseColor scope = Per Material.")]
    public Material coverMaterial;

    [Tooltip("Pages матеріал. Enable GPU Instancing ✓. Optional.")]
    public Material pagesMaterial;

    [Header("Layout")]
    [Tooltip("Початкова позиція першої книги.")]
    public Vector3 origin = new Vector3(0f, 1f, 0f);

    [Tooltip("Інтервал між книгами по X (м).")]
    public float spacingX = 0.05f;

    [Tooltip("Інтервал між рядами по Z (м).")]
    public float spacingZ = 0.3f;

    [Tooltip("Меш unity X-rotation корекція (як у твоєму prefab Book000).")]
    public Vector3 meshRotation = new Vector3(-90f, 0f, 0f);

    [Header("Diagnostic Settings")]
    [Tooltip("Друкувати детальну інфу про матриці і кольори.")]
    public bool verboseLogging = false;

    // ── Палітра — ТА САМА що в твоєму BookInstancedRenderer ──────────────
    private static readonly Color[] Palette =
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

    // ── Дані рендерингу — групи по кольору ──────────────────────────────
    private List<Matrix4x4>[] _matricesByColor;
    private Matrix4x4[]       _batchBuffer; // reused
    private MaterialPropertyBlock _mpb;

    private int _drawCallsThisFrame;
    private bool _validated;

    // ── Lifecycle ───────────────────────────────────────────────────────
    private void Start()
    {
        if (!Validate()) { enabled = false; return; }

        _mpb         = new MaterialPropertyBlock();
        _batchBuffer = new Matrix4x4[1023];

        // 16 списків — один на колір
        _matricesByColor = new List<Matrix4x4>[16];
        for (int i = 0; i < 16; i++) _matricesByColor[i] = new List<Matrix4x4>(8);

        BuildLayout();

        Debug.Log($"[DIAG] Layout built: {Sum()} books across 16 color groups.\n" +
                  "Now check Scene/Game view: books should be visible in different colors.\n" +
                  "Check Stats → Batches.");
        _validated = true;
    }

    private void Update()
    {
        if (!_validated) return;
        _drawCallsThisFrame = 0;

        Quaternion meshRot = Quaternion.Euler(meshRotation);
        // (не використовуємо тут — матриці вже built в Start)

        for (int colorIdx = 0; colorIdx < 16; colorIdx++)
        {
            var matrices = _matricesByColor[colorIdx];
            if (matrices.Count == 0) continue;

            // Виставляємо колір цієї групи через MPB
            _mpb.Clear();
            _mpb.SetColor("_BaseColor", Palette[colorIdx]);

            // Розбиваємо на batches до 1023 (у нашому випадку 3 на групу, але про всяк)
            for (int start = 0; start < matrices.Count; start += 1023)
            {
                int count = Mathf.Min(1023, matrices.Count - start);
                for (int i = 0; i < count; i++)
                    _batchBuffer[i] = matrices[start + i];

                // submesh 0 — cover
                Graphics.DrawMeshInstanced(
                    bookMesh, 0, coverMaterial, _batchBuffer, count, _mpb,
                    ShadowCastingMode.On, true, gameObject.layer);
                _drawCallsThisFrame++;

                // submesh 1 — pages (без per-instance даних, тому MPB не передаємо)
                if (pagesMaterial != null && bookMesh.subMeshCount > 1)
                {
                    Graphics.DrawMeshInstanced(
                        bookMesh, 1, pagesMaterial, _batchBuffer, count, null,
                        ShadowCastingMode.On, true, gameObject.layer);
                    _drawCallsThisFrame++;
                }
            }
        }
    }

    // ── Layout ──────────────────────────────────────────────────────────
    private void BuildLayout()
    {
        Quaternion meshRot = Quaternion.Euler(meshRotation);

        // 16 кольорів × 3 розміри. Кожен колір — окремий рядок по Z.
        Vector3[] scales =
        {
            new Vector3(1.0f, 1.0f, 0.7f),  // Small (товщина 70%)
            new Vector3(1.0f, 1.0f, 1.0f),  // Medium
            new Vector3(1.0f, 1.0f, 1.3f),  // Large
        };

        for (int colorIdx = 0; colorIdx < 16; colorIdx++)
        {
            for (int sizeIdx = 0; sizeIdx < 3; sizeIdx++)
            {
                Vector3 pos = origin +
                              new Vector3(sizeIdx * spacingX, 0f, colorIdx * spacingZ);
                Matrix4x4 m = Matrix4x4.TRS(pos, meshRot, scales[sizeIdx]);
                _matricesByColor[colorIdx].Add(m);

                if (verboseLogging)
                    Debug.Log($"[DIAG] color={colorIdx} size={sizeIdx} pos={pos} " +
                              $"colorRGB=({Palette[colorIdx].r:F2},{Palette[colorIdx].g:F2},{Palette[colorIdx].b:F2})");
            }
        }
    }

    // ── Validation ──────────────────────────────────────────────────────
    private bool Validate()
    {
        bool ok = true;

        if (bookMesh == null)
        { Debug.LogError("[DIAG] bookMesh не призначено!"); ok = false; }

        if (coverMaterial == null)
        { Debug.LogError("[DIAG] coverMaterial не призначено!"); ok = false; }

        if (coverMaterial != null && !coverMaterial.enableInstancing)
        {
            Debug.LogError($"[DIAG] '{coverMaterial.name}': Enable GPU Instancing ВИМКНЕНО! " +
                           "У Material Inspector → Enable GPU Instancing ✓");
            ok = false;
        }

        if (coverMaterial != null && !coverMaterial.HasProperty("_BaseColor"))
        {
            Debug.LogError($"[DIAG] '{coverMaterial.name}': немає property _BaseColor. " +
                           "Перевір shader.");
            ok = false;
        }

        if (!SystemInfo.supportsInstancing)
        { Debug.LogError("[DIAG] GPU Instancing не підтримується на цій платформі!"); ok = false; }

        return ok;
    }

    private int Sum()
    {
        int t = 0;
        if (_matricesByColor != null)
            for (int i = 0; i < _matricesByColor.Length; i++)
                t += _matricesByColor[i].Count;
        return t;
    }

    // ── On-screen debug ─────────────────────────────────────────────────
    private void OnGUI()
    {
        if (!_validated) return;
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 600, 20),
                  $"[DIAG] Books: {Sum()}  |  DrawCalls last frame: {_drawCallsThisFrame}  |  " +
                  $"Press F1 to log palette breakdown");
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F1)
            LogBreakdown();
    }

    private void LogBreakdown()
    {
        for (int i = 0; i < 16; i++)
        {
            Color c = Palette[i];
            Debug.Log($"[DIAG] Color {i}: RGB=({c.r:F2},{c.g:F2},{c.b:F2}) " +
                      $"→ {_matricesByColor[i].Count} books");
        }
    }
}