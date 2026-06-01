using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameStateSerializer serializer;

    private void Awake()
    {
        // Обмеження FPS до 60
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount  = 0; // vSync вимикаємо щоб targetFrameRate працював
    }
    private void Start()
    {
        // FIX: assertions moved to Start() — all Awake() have completed by now,
        // including BookDatabaseLoader.Awake() which sets BookDatabase.Instance.
        Debug.Assert(GameLoopManager.Instance != null, "GameLoopManager missing!");
        Debug.Assert(EconomyManager.Instance != null, "EconomyManager missing!");
        Debug.Assert(InventoryManager.Instance != null, "InventoryManager missing!");
        Debug.Assert(BookDatabase.Instance != null, "BookDatabase missing!");

        bool isNewGame = PlayerPrefs.GetInt("IsNewGame", 1) == 1;
        if (isNewGame)
        {
            Debug.Log("[Bootstrap] Starting new game.");
            TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnGameStart);
        }
        else
        {
            Debug.Log("[Bootstrap] Loading saved game.");
            SaveData data = SaveSystem.Load(0);
            serializer?.ApplySaveData(data);
        }
    }

    [ContextMenu("Fill Shelves Now")]
    public void FillShelvesNow()
    {
        DefaultShelfFiller.FillAll();
        Debug.Log("[Bootstrap] Shelves filled manually.");
    }
 
    /// Скинути прапор нової гри — при наступному запуску полиці заповняться автоматично.
    [ContextMenu("Reset to New Game")]
    public void ResetToNewGame()
    {
        PlayerPrefs.SetInt("IsNewGame", 1);
        PlayerPrefs.Save();
       // SaveSystem.DeleteSave(0);
        Debug.Log("[Bootstrap] Save deleted. Next launch = new game with default shelves.");
    }
 
    /// Заповнити полиці і зберегти стан.
    [ContextMenu("Fill Shelves + Save")]
    public void FillShelvesAndSave()
    {
        DefaultShelfFiller.FillAll();
        serializer?.QuickSave();
        Debug.Log("[Bootstrap] Shelves filled and saved.");
    }
}