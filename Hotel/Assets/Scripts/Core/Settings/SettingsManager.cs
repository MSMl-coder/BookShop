using UnityEngine;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer;

    public SettingsData Current { get; private set; } = new SettingsData();

    private const string PREFS_KEY = "GameSettings";

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        Load();
        Apply();
    }

    public void SetMasterVolume(float value)
    {
        Current.masterVolume = value;
        audioMixer?.SetFloat("MasterVolume", Mathf.Log10(Mathf.Max(value, 0.001f)) * 20);
    }

    public void SetMusicVolume(float value)
    {
        Current.musicVolume = value;
        audioMixer?.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(value, 0.001f)) * 20);
    }

    public void SetQuality(int level)
    {
        Current.qualityLevel = level;
        QualitySettings.SetQualityLevel(level);
    }

    public void SetFullscreen(bool value)
    {
        Current.fullscreen = value;
        Screen.fullScreen = value;
    }

    public void Save()
    {
        string json = JsonUtility.ToJson(Current);
        PlayerPrefs.SetString(PREFS_KEY, json);
        PlayerPrefs.Save();
        Debug.Log("[Settings] Saved.");
    }

    public void Load()
    {
        if (PlayerPrefs.HasKey(PREFS_KEY))
        {
            string json = PlayerPrefs.GetString(PREFS_KEY);
            Current = JsonUtility.FromJson<SettingsData>(json);
            Debug.Log("[Settings] Loaded.");
        }
    }

    private void Apply()
    {
        SetMasterVolume(Current.masterVolume);
        SetMusicVolume(Current.musicVolume);
        SetQuality(Current.qualityLevel);
        SetFullscreen(Current.fullscreen);
    }
}