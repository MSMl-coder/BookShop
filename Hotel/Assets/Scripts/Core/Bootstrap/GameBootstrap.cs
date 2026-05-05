using UnityEngine;

// Перший скрипт що запускається в ігровій сцені
// Вирішує: завантажити сейв чи почати нову гру
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameStateSerializer serializer;

    private void Awake()
    {
        // Перевірка наявності всіх критичних систем
        Debug.Assert(GameLoopManager.Instance != null, "GameLoopManager missing!");
        Debug.Assert(EconomyManager.Instance != null, "EconomyManager missing!");
        Debug.Assert(InventoryManager.Instance != null, "InventoryManager missing!");
        Debug.Assert(BookDatabase.Instance != null, "BookDatabase missing!");
    }
    private void Start()
    {
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