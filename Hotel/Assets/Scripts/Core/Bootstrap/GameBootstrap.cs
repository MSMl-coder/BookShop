using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameStateSerializer serializer;

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
}