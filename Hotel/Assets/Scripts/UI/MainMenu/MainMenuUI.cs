// Assets/Scripts/UI/MainMenu/MainMenuUI.cs
using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private UIDocument           uiDocument;
    [SerializeField] private SettingsUIController settingsController;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        root.Q<Button>("BtnNewGame") ?.RegisterCallback<ClickEvent>(_ => NewGame());
        root.Q<Button>("BtnContinue")?.RegisterCallback<ClickEvent>(_ => Continue());
        root.Q<Button>("BtnSettings")?.RegisterCallback<ClickEvent>(_ => settingsController?.Toggle());
        root.Q<Button>("BtnQuit")    ?.RegisterCallback<ClickEvent>(_ => Application.Quit());
        
        
        
        root.Q<Button>("BtnOpenDecoration")?.RegisterCallback<ClickEvent>(_ =>
        {
            if (EditModeManager.Instance != null && EditModeManager.Instance.IsEditMode)
                DecorationPanelUI.Instance?.OpenInEditMode();
            else
                DecorationPanelUI.Instance?.Open();
        });

        

        // Continue — активна якщо є хоч один слот
        var continueBtn = root.Q<Button>("BtnContinue");
        if (continueBtn != null)
        {
            bool hasAny = false;
            for (int i = 0; i < SaveSystem.SlotCount; i++)
                if (SaveSystem.SaveExists(i)) { hasAny = true; break; }
            if (!hasAny) hasAny = SaveSystem.QuickSaveExists();
            continueBtn.SetEnabled(hasAny);
        }
    }

    private void NewGame()
    {
        PlayerPrefs.SetInt("IsNewGame", 1);
        PlayerPrefs.SetInt("LoadSlot", -1);
        SceneLoader.Instance?.LoadGame(0);
    }

    private void Continue()
    {
        // Знаходимо перший існуючий слот
        int slot = -1;
        for (int i = 0; i < SaveSystem.SlotCount; i++)
            if (SaveSystem.SaveExists(i)) { slot = i; break; }

        PlayerPrefs.SetInt("IsNewGame", 0);
        PlayerPrefs.SetInt("LoadSlot", slot);
        SceneLoader.Instance?.LoadGame(0);
    }
}
