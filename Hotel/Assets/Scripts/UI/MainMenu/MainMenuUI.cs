using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private SettingsUIController settingsController;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        root.Q<Button>("BtnNewGame")?.RegisterCallback<ClickEvent>(_ => NewGame());
        root.Q<Button>("BtnContinue")?.RegisterCallback<ClickEvent>(_ => Continue());
        root.Q<Button>("BtnSettings")?.RegisterCallback<ClickEvent>(_ => 
            settingsController?.Toggle());
        root.Q<Button>("BtnQuit")?.RegisterCallback<ClickEvent>(_ => 
            Application.Quit());

        // Активуємо Continue тільки якщо є сейв
        var continueBtn = root.Q<Button>("BtnContinue");
        if (continueBtn != null)
            continueBtn.SetEnabled(SaveSystem.SaveExists(0));
    }

    private void NewGame()
    {
        SaveSystem.Delete(0); // Видаляємо старий сейв
        PlayerPrefs.SetInt("IsNewGame", 1);
        SceneLoader.Instance?.LoadGame(0);
    }

    private void Continue()
    {
        PlayerPrefs.SetInt("IsNewGame", 0);
        SceneLoader.Instance?.LoadGame(0);
    }
}