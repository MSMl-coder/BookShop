// Assets/Editor/WallPrefabSetup.cs
// ═══════════════════════════════════════════════════════════════════════════
//  Editor Tool — збирає правильний Wall Prefab з двох існуючих GO
//  -----------------------------------------------------------------------
//  Відкрити: Tools → Hotel → Setup Wall Prefab
//
//  Що робить:
//    1. Бере два виділених GameObject (Static + Dynamic частини)
//    2. Створює порожній батько Wall_[назва]
//    3. Вкладає обидва GO як дочірні
//    4. Додає WallUVSync з автоматичним призначенням рендерерів
//    5. Додає WallFacingTag
//    6. Зберігає як Prefab у вказану папку
// ═══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class WallPrefabSetup : EditorWindow
{
    private GameObject _staticGO;
    private GameObject _dynamicGO;
    private WallDirection _facing = WallDirection.North;
    private string _prefabFolder  = "Assets/Prefabs/Walls";
    private string _wallName      = "Wall_01";
    private WallUVSync.WorldAxis _axis = WallUVSync.WorldAxis.X;
    private Vector2 _tiling = new Vector2(1f, 1f);

    [MenuItem("Tools/Hotel/Setup Wall Prefab")]
    public static void Open() => GetWindow<WallPrefabSetup>("Wall Prefab Setup");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Wall Prefab Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        // ── Назва та папка ───────────────────────────────────────────
        _wallName     = EditorGUILayout.TextField("Wall Name", _wallName);
        _prefabFolder = EditorGUILayout.TextField("Prefab Folder", _prefabFolder);
        EditorGUILayout.Space(4);

        // ── Частини стіни ────────────────────────────────────────────
        EditorGUILayout.LabelField("Wall Parts", EditorStyles.boldLabel);
        _staticGO  = (GameObject)EditorGUILayout.ObjectField(
            "Static Part (кути)", _staticGO, typeof(GameObject), true);
        _dynamicGO = (GameObject)EditorGUILayout.ObjectField(
            "Dynamic Part (панель)", _dynamicGO, typeof(GameObject), true);

        EditorGUILayout.Space(4);

        // ── Налаштування ─────────────────────────────────────────────
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        _facing = (WallDirection)EditorGUILayout.EnumPopup("Facing Direction", _facing);
        _axis   = (WallUVSync.WorldAxis)EditorGUILayout.EnumPopup("Pattern Axis", _axis);
        _tiling = EditorGUILayout.Vector2Field("Tiling (має = матеріалу)", _tiling);

        EditorGUILayout.Space(12);

        // ── Кнопки ───────────────────────────────────────────────────
        GUI.enabled = _staticGO != null && _dynamicGO != null;

        if (GUILayout.Button("▶  Зібрати Prefab", GUILayout.Height(36)))
            BuildPrefab();

        GUI.enabled = true;

        // ── Підказка ─────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Виділи Static та Dynamic GO у сцені, призначь їх вище.\n" +
            "Facing: напрямок в який дивиться стіна (нормаль зовнішньої поверхні).\n" +
            "Pattern Axis: X для вертикальних смуг, Z для горизонтальних.",
            MessageType.Info);
    }

    private void BuildPrefab()
    {
        // Перевірка рендерерів
        Renderer staticRend  = _staticGO.GetComponentInChildren<Renderer>();
        Renderer dynamicRend = _dynamicGO.GetComponentInChildren<Renderer>();

        if (staticRend == null || dynamicRend == null)
        {
            EditorUtility.DisplayDialog("Помилка",
                "Один або обидва GO не мають Renderer компонента!", "OK");
            return;
        }

        // Undo group
        Undo.SetCurrentGroupName("Create Wall Prefab");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. Батько
        GameObject parent = new GameObject(_wallName);
        Undo.RegisterCreatedObjectUndo(parent, "Create Wall Parent");

        // Позиція батька = центр між двома частинами
        parent.transform.position =
            (_staticGO.transform.position + _dynamicGO.transform.position) * 0.5f;

        // 2. Вкладаємо дочірні
        Undo.SetTransformParent(_staticGO.transform,  parent.transform, "Parent Static");
        Undo.SetTransformParent(_dynamicGO.transform, parent.transform, "Parent Dynamic");

        // 3. Перейменовуємо для ясності
        _staticGO.name  = "Wall_Static";
        _dynamicGO.name = "Wall_Dynamic";

        // 4. WallFacingTag
        WallFacingTag facingTag = parent.AddComponent<WallFacingTag>();
        facingTag.Facing = _facing;

        // 5. WallUVSync
        WallUVSync uvSync    = parent.AddComponent<WallUVSync>();
        // Через SerializedObject щоб правильно серіалізувалось
        SerializedObject so  = new SerializedObject(uvSync);
        so.FindProperty("staticPart") .objectReferenceValue = staticRend;
        so.FindProperty("dynamicPart").objectReferenceValue = dynamicRend;
        so.FindProperty("patternAxis").enumValueIndex       = (int)_axis;
        so.FindProperty("tiling")    .vector2Value         = _tiling;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 6. WallFacingTag на Dynamic (для WallVisibilityController)
        WallFacingTag dynamicTag = _dynamicGO.AddComponent<WallFacingTag>();
        dynamicTag.Facing = _facing;

        // 7. Зберігаємо як Prefab
        if (!Directory.Exists(_prefabFolder))
            Directory.CreateDirectory(_prefabFolder);

        string path    = $"{_prefabFolder}/{_wallName}.prefab";
        string unique  = AssetDatabase.GenerateUniqueAssetPath(path);
        PrefabUtility.SaveAsPrefabAssetAndConnect(parent, unique, InteractionMode.UserAction);

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog("Готово!",
            $"Prefab збережено:\n{unique}\n\n" +
            $"WallFacingTag: {_facing}\n" +
            $"UV Sync: axis={_axis}, tiling={_tiling}", "OK");

        Debug.Log($"[WallPrefabSetup] Created: {unique}");
    }
}
#endif