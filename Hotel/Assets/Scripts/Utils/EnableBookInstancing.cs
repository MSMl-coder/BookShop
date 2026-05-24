// Запустити один раз через Tools → Enable Book GPU Instancing
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class EnableBookInstancing : EditorWindow
{
    [MenuItem("Tools/Enable GPU Instancing on Book Materials")]
    static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials/Books" });
        int count = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !mat.enableInstancing)
            {
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                count++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Enabled GPU Instancing on {count} materials.");
    }
}
#endif