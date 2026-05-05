using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

// Централізоване керування сценами з loading screen
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Scene Names")]
    public const string MAIN_MENU = "MainMenu";
    public const string GAME_SCENE = "BookshopScene";
    public const string LOADING_SCREEN = "LoadingScreen";

    public event System.Action OnSceneLoadStart;
    public event System.Action<float> OnLoadProgress;
    public event System.Action OnSceneLoadComplete;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    public void LoadGame(int saveSlot = 0)
    {
        PlayerPrefs.SetInt("LoadSlot", saveSlot);
        StartCoroutine(LoadSceneAsync(GAME_SCENE));
    }

    public void LoadMainMenu()
    {
        // Автозберігаємо перед виходом
        GameStateSerializer.Instance?.QuickSave();
        StartCoroutine(LoadSceneAsync(MAIN_MENU));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        OnSceneLoadStart?.Invoke();

        // Спочатку завантажуємо Loading Screen
        yield return SceneManager.LoadSceneAsync(LOADING_SCREEN);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            OnLoadProgress?.Invoke(op.progress);
            yield return null;
        }

        OnLoadProgress?.Invoke(1f);
        yield return new WaitForSeconds(0.5f); // мінімальний час loading screen

        op.allowSceneActivation = true;
        OnSceneLoadComplete?.Invoke();
    }
}