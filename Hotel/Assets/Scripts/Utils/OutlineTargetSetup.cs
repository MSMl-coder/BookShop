// Assets/Scripts/Editor/OutlineTargetSetup.cs
// Editor-утиліта: автоматично додає OutlineTarget на всі GO
// що мають Renderer і знаходяться в потрібних layers.
//
// Запуск: Tools → Outline → Setup Outline Targets in Scene

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class OutlineTargetSetup
{
    // Layers на яких шукаємо об'єкти
    private static readonly string[] TargetLayerNames =
    {
        "Furniture",      // Layer 8 — шафи
     //   "Shelves",        // Layer 9 — полиці
        "PlacedBooks",    // Layer 10 — книги на полицях
        "Interactive",    // Layer 20 — коробки та інші інтерактивні
    };

    [MenuItem("Tools/Outline/Setup Outline Targets in Scene")]
    public static void SetupInScene()
    {
        Material outlineMat = FindOutlineMaterial();
        if (outlineMat == null)
        {
            EditorUtility.DisplayDialog(
                "OutlineTargetSetup",
                "Матеріал 'HoverOutlineMat' не знайдено в проекті.\n\n" +
                "Створи матеріал:\n" +
                "Assets → Create → Material → назви HoverOutlineMat\n" +
                "Shader: Custom/HoverOutline",
                "OK"
            );
            return;
        }

        int layerMask = BuildLayerMask();
        var allRenderers = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        int added   = 0;
        int skipped = 0;

        Undo.SetCurrentGroupName("Setup Outline Targets");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var renderer in allRenderers)
        {
            if (!IsInLayerMask(renderer.gameObject.layer, layerMask))
                continue;

            // Шукаємо OutlineTarget на цьому GO або батьківському
            var existing = renderer.GetComponentInParent<OutlineTarget>();
            if (existing != null) { skipped++; continue; }

            // Додаємо на кореневий інтерактивний GO
            GameObject target = FindInteractiveRoot(renderer.gameObject);
            if (target.GetComponent<OutlineTarget>() != null) { skipped++; continue; }

            Undo.RecordObject(target, "Add OutlineTarget");
            var ot = Undo.AddComponent<OutlineTarget>(target);

            // Призначаємо матеріал через SerializedObject
            var so = new SerializedObject(ot);
            so.FindProperty("outlineMaterial").objectReferenceValue = outlineMat;
            so.ApplyModifiedProperties();

            added++;
        }

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog(
            "OutlineTargetSetup",
            $"Готово!\n\nДодано OutlineTarget: {added}\nПропущено (вже є): {skipped}",
            "OK"
        );

        Debug.Log($"[OutlineSetup] Додано: {added}, пропущено: {skipped}");
    }

    [MenuItem("Tools/Outline/Remove All Outline Targets")]
    public static void RemoveAll()
    {
        if (!EditorUtility.DisplayDialog(
            "OutlineTargetSetup",
            "Видалити всі компоненти OutlineTarget зі сцени?",
            "Так", "Скасувати")) return;

        var all = Object.FindObjectsByType<OutlineTarget>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Undo.SetCurrentGroupName("Remove All Outline Targets");
        int group = Undo.GetCurrentGroup();

        foreach (var ot in all)
            Undo.DestroyObjectImmediate(ot);

        Undo.CollapseUndoOperations(group);
        Debug.Log($"[OutlineSetup] Видалено {all.Length} OutlineTarget компонентів.");
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static Material FindOutlineMaterial()
    {
        // Шукаємо за назвою в проекті
        var guids = AssetDatabase.FindAssets("HoverOutlineMat t:Material");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath(guids[0])
        );
    }

    private static int BuildLayerMask()
    {
        int mask = 0;
        foreach (var name in TargetLayerNames)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0) mask |= (1 << layer);
            else Debug.LogWarning($"[OutlineSetup] Layer '{name}' не знайдено.");
        }
        return mask;
    }

    private static bool IsInLayerMask(int layer, int mask) =>
        (mask & (1 << layer)) != 0;

    /// Знаходить кореневий GO що має інтерактивний компонент
    /// (Cabinet, BookWorldItem, LootBox) — туди і чіпляємо OutlineTarget.
    /// Якщо нічого не знайдено — повертає сам GO.
    private static GameObject FindInteractiveRoot(GameObject go)
    {
        // Піднімаємось вгору по ієрархії
        var t = go.transform;
        while (t != null)
        {
            if (t.GetComponent<Cabinet>()      != null) return t.gameObject;
            if (t.GetComponent<BookWorldItem>() != null) return t.gameObject;
            if (t.GetComponent<LootBox>()       != null) return t.gameObject;
            if (t.GetComponent<Shelf>()         != null) return t.gameObject;
            t = t.parent;
        }
        return go;
    }
}