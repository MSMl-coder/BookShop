using System;

[Serializable]
public class SettingsData
{
    public float masterVolume = 1f;
    public float musicVolume = 0.8f;
    public float sfxVolume = 1f;
    public int qualityLevel = 2;
    public bool fullscreen = true;
    public int resolutionIndex = 0;
    public string language = "uk";
}