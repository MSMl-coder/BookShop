using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

// Статична утиліта збереження — не MonoBehaviour навмисно
public static class SaveSystem
{
    private static readonly string SAVE_PATH =
        Path.Combine(Application.persistentDataPath, "save_slot_{0}.dat");

    private const string SAVE_VERSION = "1.0";

    // --- Save ---
    public static bool Save(SaveData data, int slot = 0)
    {
        try
        {
            data.saveDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            data.saveVersion = SAVE_VERSION;

            string path = string.Format(SAVE_PATH, slot);
            BinaryFormatter formatter = new BinaryFormatter();

            using (FileStream stream = new FileStream(path, FileMode.Create))
                formatter.Serialize(stream, data);

            Debug.Log($"[SaveSystem] Saved to slot {slot}: {path}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
            return false;
        }
    }

    // --- Load ---
    public static SaveData Load(int slot = 0)
    {
        string path = string.Format(SAVE_PATH, slot);

        if (!File.Exists(path))
        {
            Debug.Log($"[SaveSystem] No save found at slot {slot}. Returning new game.");
            return null;
        }

        try
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream stream = new FileStream(path, FileMode.Open))
            {
                SaveData data = (SaveData)formatter.Deserialize(stream);

                // Version check
                if (data.saveVersion != SAVE_VERSION)
                    Debug.LogWarning($"[SaveSystem] Save version mismatch: {data.saveVersion} vs {SAVE_VERSION}");

                Debug.Log($"[SaveSystem] Loaded slot {slot}. Day: {data.currentDay}");
                return data;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Load failed: {e.Message}");
            return null;
        }
    }

    // --- Delete ---
    public static void Delete(int slot = 0)
    {
        string path = string.Format(SAVE_PATH, slot);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[SaveSystem] Deleted save slot {slot}");
        }
    }

    public static bool SaveExists(int slot = 0) =>
        File.Exists(string.Format(SAVE_PATH, slot));
}