using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class SettingsUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement _panel;
    private Slider _masterSlider;
    private Slider _musicSlider;
    private Slider _sfxSlider;
    private DropdownField _qualityDropdown;
    private bool _isOpen = false;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _panel = root.Q<VisualElement>("SettingsPanel");

        _masterSlider = root.Q<Slider>("MasterSlider");
        _musicSlider  = root.Q<Slider>("MusicSlider");
        _sfxSlider    = root.Q<Slider>("SFXSlider");
        _qualityDropdown = root.Q<DropdownField>("QualityDropdown");

        // Заповнити якість
        _qualityDropdown.choices = new List<string>(QualitySettings.names);
        _qualityDropdown.index = SettingsManager.Instance?.Current.qualityLevel ?? 2;

        // Завантажити поточні значення
        var s = SettingsManager.Instance?.Current;
        if (s != null)
        {
            _masterSlider.value = s.masterVolume;
            _musicSlider.value  = s.musicVolume;
            _sfxSlider.value    = s.sfxVolume;
        }

        // Підписки
        _masterSlider.RegisterValueChangedCallback(e =>
            SettingsManager.Instance?.SetMasterVolume(e.newValue));

        _musicSlider.RegisterValueChangedCallback(e =>
            SettingsManager.Instance?.SetMusicVolume(e.newValue));

        _qualityDropdown.RegisterValueChangedCallback(e =>
            SettingsManager.Instance?.SetQuality(_qualityDropdown.index));

        root.Q<Button>("BtnSave")?.RegisterCallback<ClickEvent>(_ => Save());
        root.Q<Button>("BtnClose")?.RegisterCallback<ClickEvent>(_ => Close());

        _panel.style.display = DisplayStyle.None;
    }

    public void Toggle()
    {
        _isOpen = !_isOpen;
        _panel.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void Save()
    {
        SettingsManager.Instance?.Save();
        Close();
    }

    private void Close()
    {
        _isOpen = false;
        _panel.style.display = DisplayStyle.None;
    }
}