// Assets/Scripts/Core/Bootstrap/GameBootstrap.cs
// ФІКС: IsNewGame тепер скидається в 0 після першого запуску.
//   До виправлення — PlayerPrefs.GetInt("IsNewGame", 1) завжди повертав 1,
//   тому збереження НІКОЛИ не завантажувалось.

using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameStateSerializer serializer;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount  = 0;
    }

    private void Start()
    {
        // Assertions у Start() — всі Awake() вже виконались (включно з BookDatabaseLoader)
        Debug.Assert(GameLoopManager.Instance  != null, "GameLoopManager missing!");
        Debug.Assert(EconomyManager.Instance   != null, "EconomyManager missing!");
        Debug.Assert(InventoryManager.Instance != null, "InventoryManager missing!");
        Debug.Assert(BookDatabase.Instance     != null, "BookDatabase missing!");

        bool isNewGame = PlayerPrefs.GetInt("IsNewGame", 1) == 1;

        if (isNewGame)
        {
            Debug.Log("[Bootstrap] Нова гра — заповнюємо полиці.");

            // ✅ ФІКС: Скидаємо прапор щоб наступний запуск завантажував збереження
            PlayerPrefs.SetInt("IsNewGame", 0);
            PlayerPrefs.Save();

            DefaultShelfFiller.FillAll();
            TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnGameStart);

            // Зберігаємо одразу щоб новий стан не загубився
            serializer?.QuickSave();
        }
        else
        {
            Debug.Log("[Bootstrap] Завантажуємо збереження (слот 0).");
            SaveData data = SaveSystem.Load(0);

            if (data != null)
                serializer?.ApplySaveData(data);
            else
            {
                // Збереження пошкоджено або відсутнє — стартуємо як нова гра
                Debug.LogWarning("[Bootstrap] Слот 0 порожній — починаємо нову гру.");
                DefaultShelfFiller.FillAll();
                TutorialManager.Instance?.TryTrigger(TutorialTrigger.OnGameStart);
                serializer?.QuickSave();
            }
        }
    }

    // ── ContextMenu helpers ────────────────────────────────────────────

    [ContextMenu("Fill Shelves Now")]
    public void FillShelvesNow()
    {
        DefaultShelfFiller.FillAll();
        Debug.Log("[Bootstrap] Полиці заповнені вручну.");
    }

    /// Скинути до нової гри — наступний запуск стартує з нуля.
    [ContextMenu("Reset to New Game")]
    public void ResetToNewGame()
    {
        PlayerPrefs.SetInt("IsNewGame", 1);
        PlayerPrefs.Save();
        Debug.Log("[Bootstrap] Прапор нової гри встановлено. Наступний запуск = нова гра.");
    }

    /// Заповнити полиці і зберегти.
    [ContextMenu("Fill Shelves + Save")]
    public void FillShelvesAndSave()
    {
        DefaultShelfFiller.FillAll();
        serializer?.QuickSave();
        Debug.Log("[Bootstrap] Полиці заповнені та збережені.");
    }
}