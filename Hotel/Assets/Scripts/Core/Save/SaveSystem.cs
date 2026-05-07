// Assets/Scripts/Core/Save/SaveSystem.cs
using UnityEngine;
using System;

public static class SaveSystem
{
    private const string KeyPrefix  = "save_slot_";
    private const string QuickKey   = "save_quick";
    private const string DateFormat = "yyyy-MM-dd HH:mm";
    public  const int    SlotCount  = 3;

    // ── Автозбереження ──────────────────────────────────────────────────────

    public static void Save(SaveData data)
    {
        if (data == null) return;
        StampMeta(data);

        string json = JsonUtility.ToJson(data, prettyPrint: false);
        PlayerPrefs.SetString(QuickKey, json);

        if (data.slotIndex >= 0 && data.slotIndex < SlotCount)
            PlayerPrefs.SetString(KeyPrefix + data.slotIndex, json);

        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] Збережено: {data.saveName} (слот {data.slotIndex})");
    }

    // ── Ручне збереження в конкретний слот ──────────────────────────────────

    public static void SaveToSlot(int slot)
    {
        var data = GameStateSerializer.Instance?.CollectSaveData() ?? new SaveData();
        data.slotIndex = slot;
        StampMeta(data);

        string json = JsonUtility.ToJson(data, prettyPrint: false);
        PlayerPrefs.SetString(KeyPrefix + slot, json);
        PlayerPrefs.SetString(QuickKey, json);
        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] Збережено в слот {slot}: {data.saveName}");
    }

    // ── Завантаження ─────────────────────────────────────────────────────────

    public static SaveData Load(int slot)
    {
        string key = KeyPrefix + slot;
        if (!PlayerPrefs.HasKey(key))
        {
            Debug.LogWarning($"[SaveSystem] Слот {slot} порожній.");
            return null;
        }
        var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(key));
        Debug.Log($"[SaveSystem] Завантажено слот {slot}: {data?.saveName}");
        return data;
    }

    public static SaveData Peek(int slot) => Load(slot);

    public static SaveData LoadQuick()
    {
        if (!PlayerPrefs.HasKey(QuickKey)) return null;
        return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(QuickKey));
    }

    // ── Видалення ────────────────────────────────────────────────────────────

    public static void Delete(int slot)
    {
        PlayerPrefs.DeleteKey(KeyPrefix + slot);
        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] Слот {slot} видалено.");
    }

    // ── Перевірка ────────────────────────────────────────────────────────────

    public static bool SaveExists(int slot) =>
        PlayerPrefs.HasKey(KeyPrefix + slot);

    public static bool QuickSaveExists() =>
        PlayerPrefs.HasKey(QuickKey);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void StampMeta(SaveData data)
    {
        data.saveDateTime = DateTime.Now.ToString(DateFormat);
        if (string.IsNullOrEmpty(data.saveName))
            data.saveName = $"День {data.currentDay} · {data.money:N0} грн";
    }

    // ВИПРАВЛЕНО: видалено метод Save(int v) що кидав NotImplementedException
    // Він ніде не використовувався і міг спричинити краш при випадковому виклику
}
