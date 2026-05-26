 using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif



/// <summary>
/// Central debug controller for testing all game systems.
/// Hotkeys active only in Editor / Development builds.
/// </summary>
public class DebugGameController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    #region Inspector Fields
    // ═══════════════════════════════════════════════════════════════

    [Header("━━ Economy ━━")]
    [SerializeField] private int moneyToAdd = 1000;

    [Header("━━ Game State ━━")]
    [SerializeField] private GameState targetState = GameState.Preparation;

    [Header("━━ Inventory ━━")]
    [SerializeField] private BookTemplate testBook;
    [SerializeField] private int bookCountToAdd = 1;

    [Header("━━ Shelf Testing ━━")]
    [SerializeField] private Shelf targetShelf;
    [SerializeField] private Cabinet targetCabinet;

    [Header("━━ Loot Testing ━━")]
    [SerializeField] private int lootCardIndex = 0;

    [Header("━━ Hotkeys (Editor/Dev only) ━━")]
    [SerializeField] private bool enableHotkeys = true;
    [SerializeField] private bool showHotkeyHints = true;

    [Header("Day Override")]
    [SerializeField] private int startFromDay = 1;


    #endregion

    // ═══════════════════════════════════════════════════════════════
    #region Unity Lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void Start()
    {
        if (showHotkeyHints)
            PrintHotkeyMap();
    }

    private void Update()
    {
        if (!enableHotkeys) return;
        if (Keyboard.current == null) return;

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
            HandleHotkeys();
    #endif
        }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Hotkeys
    // ═══════════════════════════════════════════════════════════════

    private void HandleHotkeys()
    {
        var kb = Keyboard.current;

        // ── Economy ──────────────────────────────────────────────
        if (kb.f1Key.wasPressedThisFrame)  AddMoney();
        if (kb.f2Key.wasPressedThisFrame)  SpendMoney();
        if (kb.f3Key.wasPressedThisFrame)  ResetEconomy();

        // ── Game State ───────────────────────────────────────────
        if (kb.f4Key.wasPressedThisFrame)  GoToPreparation();
        if (kb.f5Key.wasPressedThisFrame)  GoToWorkDay();
        if (kb.f6Key.wasPressedThisFrame)  GoToLootPhase();
        if (kb.f7Key.wasPressedThisFrame)  SkipToNextState();
        if (kb.f8Key.wasPressedThisFrame)  ForceEndDay();

        // ── Inventory ────────────────────────────────────────────
        if (kb.f9Key.wasPressedThisFrame)  GiveTestBook();
        if (kb.f10Key.wasPressedThisFrame) GiveAllBooks();
        if (kb.f11Key.wasPressedThisFrame) ClearInventory();

        // ── Shelf ────────────────────────────────────────────────
        if (kb.digit1Key.wasPressedThisFrame && kb.ctrlKey.isPressed) PushOneToTargetShelf();
        if (kb.digit2Key.wasPressedThisFrame && kb.ctrlKey.isPressed) PushAllToTargetShelf();
        if (kb.digit3Key.wasPressedThisFrame && kb.ctrlKey.isPressed) PopOneFromTargetShelf();
        if (kb.digit4Key.wasPressedThisFrame && kb.ctrlKey.isPressed) PopAllFromTargetShelf();

        // ── Loot ─────────────────────────────────────────────────
        if (kb.f12Key.wasPressedThisFrame) ForceGenerateLoot();
        if (kb.lKey.wasPressedThisFrame && kb.ctrlKey.isPressed) SelectLootCard();

        // ── Info ─────────────────────────────────────────────────
        if (kb.tabKey.wasPressedThisFrame && kb.ctrlKey.isPressed) PrintFullStatus();
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Economy Methods
    // ═══════════════════════════════════════════════════════════════

    [ContextMenu("Economy / Add Money")]
    public void AddMoney()
    {
        if (!CheckManager(EconomyManager.Instance, "EconomyManager")) return;
        EconomyManager.Instance.AddMoney(moneyToAdd);
        Debug.Log($"[Debug] 💰 Added ${moneyToAdd} → Balance: ${EconomyManager.Instance.Money}");
    }

    [ContextMenu("Economy / Spend Money")]
    public void SpendMoney()
    {
        if (!CheckManager(EconomyManager.Instance, "EconomyManager")) return;
        bool success = EconomyManager.Instance.SpendMoney(moneyToAdd);
        Debug.Log($"[Debug] 💸 Spent ${moneyToAdd}: {(success ? "OK" : "FAILED (not enough)")} → Balance: ${EconomyManager.Instance.Money}");
    }

    [ContextMenu("Economy / Reset Economy")]
    public void ResetEconomy()
    {
        if (!CheckManager(EconomyManager.Instance, "EconomyManager")) return;
        EconomyManager.Instance.SpendMoney(EconomyManager.Instance.Money);
        EconomyManager.Instance.ResetDailyStats();
        Debug.Log("[Debug] 🔄 Economy reset to 0.");
    }

    [ContextMenu("Economy / Print Status")]
    public void PrintEconomyStatus()
    {
        if (!CheckManager(EconomyManager.Instance, "EconomyManager")) return;
        Debug.Log(
            $"[Debug] 📊 Economy Status:\n" +
            $"  Balance       : ${EconomyManager.Instance.Money}\n" +
            $"  Books Sold    : {EconomyManager.Instance.BooksSoldToday}\n" +
            $"  Earned Today  : ${EconomyManager.Instance.MoneyEarnedToday}"
        );
    }
    [ContextMenu("Economy / Apply Daily Rent")]
    public void ApplyDailyRent()
    {
        if (!CheckManager(EconomyManager.Instance, "EconomyManager")) return;
        var result = EconomyManager.Instance.ApplyDailyRent();
        Debug.Log(
            $"[Debug] 💸 Daily Rent Applied:\n" +
            $"  Rent         : -{result.RentAmount}\n" +
            $"  Before       : ${result.BalanceBefore}\n" +
            $"  After        : ${result.BalanceAfter}\n" +
            $"  In Debt      : {result.IsInDebt}\n" +
            $"  Debt Day     : {result.DebtDay}/{result.DebtAllowed}\n" +
            $"  Bankrupt     : {result.IsBankrupt}"
        );
    }
    
    [ContextMenu("Economy / Print Debt Status")]
    public void PrintDebtStatus()
    {
        if (!CheckManager(EconomyManager.Instance, "EconomyManager")) return;
        Debug.Log(
            $"[Debug] 📋 Debt Status:\n" +
            $"  Balance      : ${EconomyManager.Instance.Money}\n" +
            $"  In Debt      : {EconomyManager.Instance.IsInDebt}\n" +
            $"  Debt Days    : {EconomyManager.Instance.DebtDaysCurrent}/{EconomyManager.Instance.DebtDaysAllowed}\n" +
            $"  Daily Rent   : ${EconomyManager.Instance.DailyRent}"
        );
    }
    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Game State Methods
    // ═══════════════════════════════════════════════════════════════

    [ContextMenu("State / → Preparation")]
    public void GoToPreparation()
    {
        ChangeState(GameState.Preparation);
    }

    [ContextMenu("State / → Work Day")]
    public void GoToWorkDay()
    {
        ChangeState(GameState.WorkDay);
    }
    [ContextMenu("State / → DayStats")]
    public void GoToDayStats()  { ChangeState(GameState.DayStats);  }


    [ContextMenu("State / → Loot Phase")]
    public void GoToLootPhase()
    {
        ChangeState(GameState.LootPhase);
    }

 [ContextMenu("State / Skip To Next State")]
    public void SkipToNextState()
    {
        if (!CheckManager(GameLoopManager.Instance, "GameLoopManager")) return;
        // EditMode (1) пропускаємо — це підстан Preparation
        int current = (int)GameLoopManager.Instance.CurrentState;
        int next = (current + 1) % System.Enum.GetValues(typeof(GameState)).Length;
        if (next == (int)GameState.EditMode) next++; // пропускаємо EditMode
        GameState nextState = (GameState)next;
        ChangeState(nextState);
        Debug.Log($"[Debug] ⏭ Skipped to: {nextState}");
    }

    [ContextMenu("State / Force End Day")]
    public void ForceEndDay()
    {
        if (!CheckManager(GameLoopManager.Instance, "GameLoopManager")) return;
        GameLoopManager.Instance.EndWorkDay();
        Debug.Log($"[Debug] 🌙 Day {GameLoopManager.Instance.CurrentDay} force-ended.");
    }

    private void ChangeState(GameState state)
    {
        if (!CheckManager(GameLoopManager.Instance, "GameLoopManager")) return;
        GameState prev = GameLoopManager.Instance.CurrentState;
        GameLoopManager.Instance.ChangeState(state);
        Debug.Log($"[Debug] 🔄 State: {prev} → {state}");
    }

    [ContextMenu("State / Print Status")]
    public void PrintStateStatus()
    {
        if (!CheckManager(GameLoopManager.Instance, "GameLoopManager")) return;
        Debug.Log(
            $"[Debug] 🎮 Game State Status:\n" +
            $"  Current State : {GameLoopManager.Instance.CurrentState}\n" +
            $"  Current Day   : {GameLoopManager.Instance.CurrentDay}"
        );
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Inventory Methods
    // ═══════════════════════════════════════════════════════════════

    [ContextMenu("Inventory / Give Test Book")]
    public void GiveTestBook()
    {
        if (!CheckManager(InventoryManager.Instance, "InventoryManager")) return;
        if (testBook == null) { Debug.LogWarning("[Debug] testBook not assigned!"); return; }

        for (int i = 0; i < bookCountToAdd; i++)
            InventoryManager.Instance.AddBook(testBook.bookID);

        Debug.Log($"[Debug] 📚 Added {bookCountToAdd}x '{testBook.title}' to inventory.");
    }

    [ContextMenu("Inventory / Give ALL Books From Database")]
    public void GiveAllBooks()
    {
        if (!CheckManager(InventoryManager.Instance, "InventoryManager")) return;

        BookDatabase db = InventoryManager.Instance.GetDatabase();
        if (db == null || db.allBooks == null)
        {
            Debug.LogError("[Debug] BookDatabase not found!");
            return;
        }

        int count = 0;
        foreach (var book in db.allBooks)
        {
            if (book == null) continue;
            for (int i = 0; i < bookCountToAdd; i++)
                InventoryManager.Instance.AddBook(book.bookID);
            count++;
        }

        Debug.Log($"[Debug] 📚 Added {bookCountToAdd}x of each book ({count} titles) to inventory.");
    }

    [ContextMenu("Inventory / Clear Inventory")]
    public void ClearInventory()
    {
        if (!CheckManager(InventoryManager.Instance, "InventoryManager")) return;

        var books = InventoryManager.Instance.GetSortedInventory(SortType.ByTitle);
        foreach (var b in books)
            InventoryManager.Instance.RemoveBook(b);

        Debug.Log($"[Debug] 🗑 Inventory cleared ({books.Count} books removed).");
    }

    [ContextMenu("Inventory / Print Status")]
    public void PrintInventoryStatus()
    {
        if (!CheckManager(InventoryManager.Instance, "InventoryManager")) return;

        var books = InventoryManager.Instance.GetSortedInventory(SortType.ByTitle);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Debug] 📦 Inventory ({books.Count} books):");

        foreach (var b in books)
        {
            BookDatabase db = InventoryManager.Instance.GetDatabase();
            BookTemplate t  = db?.GetBook(b.templateID);
            sb.AppendLine($"  • {t?.title ?? "?"} [{b.templateID}]");
        }

        Debug.Log(sb.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Shelf Methods
    // ═══════════════════════════════════════════════════════════════

    [ContextMenu("Shelf / Push One → Target Shelf")]
    public void PushOneToTargetShelf()
    {
        if (!CheckManager(InventoryManager.Instance, "InventoryManager")) return;
        if (targetShelf == null) { Debug.LogWarning("[Debug] targetShelf not assigned!"); return; }

        InventoryManager.Instance.PushOneToShelf(targetShelf);
        Debug.Log($"[Debug] → Pushed 1 book to shelf '{targetShelf.name}'.");
    }

    [ContextMenu("Shelf / Push All → Target Shelf")]
    public void PushAllToTargetShelf()
    {
        if (!CheckManager(InventoryManager.Instance, "InventoryManager")) return;
        if (targetShelf == null) { Debug.LogWarning("[Debug] targetShelf not assigned!"); return; }

        InventoryManager.Instance.PushAllToShelf(targetShelf);
        Debug.Log($"[Debug] →→ Pushed all books to shelf '{targetShelf.name}'.");
    }

    [ContextMenu("Shelf / Pop One ← Target Shelf")]
    public void PopOneFromTargetShelf()
    {
        if (!CheckManager(InventoryManager.Instance, "InventoryManager")) return;
        if (targetShelf == null) { Debug.LogWarning("[Debug] targetShelf not assigned!"); return; }

        BookInstance data = targetShelf.TakeLastBook();
        if (data != null)
        {
            InventoryManager.Instance.AddExistingBook(data);
            Debug.Log($"[Debug] ← Popped 1 book from '{targetShelf.name}' to inventory.");
        }
        else
        {
            Debug.Log($"[Debug] ← Shelf '{targetShelf.name}' is already empty.");
        }
    }

    [ContextMenu("Shelf / Pop All ← Target Shelf")]
    public void PopAllFromTargetShelf()
    {
        if (!CheckManager(InventoryManager.Instance, "InventoryManager")) return;
        if (targetShelf == null) { Debug.LogWarning("[Debug] targetShelf not assigned!"); return; }

        int count = 0;
        BookInstance book;
        while ((book = targetShelf.TakeLastBook()) != null)
        {
            InventoryManager.Instance.AddExistingBook(book);
            count++;
        }

        Debug.Log($"[Debug] ←← Popped {count} books from '{targetShelf.name}' to inventory.");
    }

    [ContextMenu("Shelf / Print Shelf Status")]
    public void PrintShelfStatus()
    {
        if (targetShelf == null) { Debug.LogWarning("[Debug] targetShelf not assigned!"); return; }

        float used  = targetShelf.GetTotalUsedWidth();
        float total = targetShelf.GetShelfWorldWidth();

        Debug.Log(
            $"[Debug] 🗄 Shelf '{targetShelf.name}':\n" +
            $"  Used  : {used:F3} m\n" +
            $"  Total : {total:F3} m\n" +
            $"  Free  : {(total - used):F3} m\n" +
            $"  Fill  : {(used / total * 100f):F1}%"
        );
    }

    [ContextMenu("Shelf / Print ALL Cabinet Shelves")]
    public void PrintCabinetStatus()
    {
        if (targetCabinet == null) { Debug.LogWarning("[Debug] targetCabinet not assigned!"); return; }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Debug] 🏛 Cabinet '{targetCabinet.cabinetName}' ({targetCabinet.shelves.Count} shelves):");

        foreach (var shelf in targetCabinet.shelves)
        {
            if (shelf == null) continue;
            float used  = shelf.GetTotalUsedWidth();
            float total = shelf.GetShelfWorldWidth();
            float pct   = total > 0 ? (used / total * 100f) : 0f;
            sb.AppendLine($"  [{shelf.name}] {used:F2}/{total:F2} m  ({pct:F0}% full)");
        }

        Debug.Log(sb.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    #region Loot Methods
    // ═══════════════════════════════════════════════════════════════

    [ContextMenu("Loot / Force Generate Pool")]
    public void ForceGenerateLoot()
    {
        if (!CheckManager(LootManager.Instance, "LootManager")) return;

        // Trigger via state change so LootManager regenerates pool
        GameLoopManager.Instance.ChangeState(GameState.LootPhase);
        var pool = LootManager.Instance.GetCurrentPool();
        Debug.Log($"[Debug] 🎴 Loot pool generated: {pool.Count} cards.");

        // Print cards
        for (int i = 0; i < pool.Count; i++)
            Debug.Log($"  [{i}] {pool[i].cardName} (${pool[i].cost}) type={pool[i].type}");
    }

    [ContextMenu("Loot / Select Card By Index")]
    public void SelectLootCard()
    {
        if (!CheckManager(LootManager.Instance, "LootManager")) return;

        var pool = LootManager.Instance.GetCurrentPool();
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[Debug] Loot pool is empty. Run ForceGenerateLoot first.");
            return;
        }

        int idx = Mathf.Clamp(lootCardIndex, 0, pool.Count - 1);
        LootManager.Instance.SelectCard(pool[idx]);
        Debug.Log($"[Debug] 🎴 Selected loot card [{idx}]: '{pool[idx].cardName}'.");
    }

    [ContextMenu("Loot / Print Pool")]
    public void PrintLootPool()
    {
        if (!CheckManager(LootManager.Instance, "LootManager")) return;

        var pool = LootManager.Instance.GetCurrentPool();
        if (pool == null || pool.Count == 0)
        {
            Debug.Log("[Debug] Loot pool is empty.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Debug] 🎴 Current Loot Pool ({pool.Count} cards):");
        for (int i = 0; i < pool.Count; i++)
            sb.AppendLine($"  [{i}] {pool[i].cardName} | type={pool[i].type} | cost=${pool[i].cost} | gold={pool[i].isGold}");

        Debug.Log(sb.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion
    
    
    
    
    #region Full Status & Utilities
    // ═══════════════════════════════════════════════════════════════

    [ContextMenu("━ Print FULL Game Status ━")]
    public void PrintFullStatus()
    {
        Debug.Log("═══════════════════════════════════════");
        PrintStateStatus();
        PrintEconomyStatus();
        PrintInventoryStatus();
        if (targetShelf != null)   PrintShelfStatus();
        if (targetCabinet != null) PrintCabinetStatus();
        PrintLootPool();
        Debug.Log("═══════════════════════════════════════");
    }

    private void PrintHotkeyMap()
    {
        Debug.Log(
            "[Debug] 🎮 Hotkey Map:\n" +
            "  F1          → Add Money\n" +
            "  F2          → Spend Money\n" +
            "  F3          → Reset Economy\n" +
            "  F4          → → Preparation\n" +
            "  F5          → → Work Day\n" +
            "  F6          → → Loot Phase\n" +
            "  F7          → Skip to Next State\n" +
            "  F8          → Force End Day\n" +
            "  F9          → Give Test Book\n" +
            "  F10         → Give ALL Books\n" +
            "  F11         → Clear Inventory\n" +
            "  F12         → Force Generate Loot\n" +
            "  Ctrl+1      → Push One to Shelf\n" +
            "  Ctrl+2      → Push All to Shelf\n" +
            "  Ctrl+3      → Pop One from Shelf\n" +
            "  Ctrl+4      → Pop All from Shelf\n" +
            "  Ctrl+L      → Select Loot Card\n" +
            "  Ctrl+Tab    → Print Full Status"
        );
    }

    // Null-safe manager check with one line
    private bool CheckManager<T>(T manager, string name) where T : class
    {
        if (manager != null) return true;
        Debug.LogError($"[Debug] ❌ {name} not found in scene!");
        return false;
    }
    // Додати до DebugGameController.cs
    [ContextMenu("Test Save")]
    public void TestSave()
    {
        GameStateSerializer.Instance?.QuickSave();
        Debug.Log("[Debug] Manual save triggered.");
    }

    [ContextMenu("Test Load")]  
    public void TestLoad()
    {
        SaveData data = SaveSystem.Load(0);
        if (data != null)
            GameStateSerializer.Instance?.ApplySaveData(data);
    }





    [ContextMenu("Day / Set Start Day")]
    public void SetStartDay()
    {
        if (!CheckManager(GameLoopManager.Instance, "GameLoopManager")) return;
        // Рефлексія або internal метод — залежно від доступу
        typeof(GameLoopManager)
            .GetProperty("CurrentDay")
            ?.SetValue(GameLoopManager.Instance, startFromDay);
        Debug.Log($"[Debug] 📅 День встановлено: {startFromDay}");
    }
    
    #endregion
     
    
    #region Edit Mode
    

    [ContextMenu("EditMode / Enter Edit Mode")]

    
    public void EnterEditMode()
    {
        if (!CheckManager(EditModeManager.Instance, "EditModeManager")) return;
        EditModeManager.Instance.EnterEditMode();
        Debug.Log("[Debug] ✏️ Edit Mode увімкнено.");
    }

    [ContextMenu("EditMode / Exit Edit Mode")]
    public void ExitEditMode()
    {
        if (!CheckManager(EditModeManager.Instance, "EditModeManager")) return;
        EditModeManager.Instance.ExitEditMode();
        Debug.Log("[Debug] ✏️ Edit Mode вимкнено.");
    }

    [ContextMenu("EditMode / Toggle Edit Mode")]
    public void ToggleEditMode()
    {
        if (!CheckManager(EditModeManager.Instance, "EditModeManager")) return;
        EditModeManager.Instance.ToggleEditMode();
    }

    // ═══════════════════════════════════════════════════════════════
    #endregion

    
}

// ═══════════════════════════════════════════════════════════════════
#region Custom Editor
// ═══════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
[CustomEditor(typeof(DebugGameController))]
public class DebugGameControllerEditor : Editor
{
    // Section foldouts
    private bool _foldEconomy   = true;
    private bool _foldState     = true;
    private bool _foldInventory = true;
    private bool _foldShelf     = true;
    private bool _foldLoot      = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(10);

        var script = (DebugGameController)target;

        // ── Economy ──────────────────────────────────────────────
        _foldEconomy = EditorGUILayout.BeginFoldoutHeaderGroup(_foldEconomy, "💰 Economy");
        if (_foldEconomy)
        {
            EditorGUILayout.BeginHorizontal();
            DrawBtn("Add Money",    Color.green,  script.AddMoney);
            DrawBtn("Spend Money",  Color.yellow, script.SpendMoney);
            DrawBtn("Reset",        BtnRed,       script.ResetEconomy);
            EditorGUILayout.EndHorizontal();
            DrawBtn("Print Status", Color.cyan,   script.PrintEconomyStatus, fullWidth: true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(4);

        // ── Game State ───────────────────────────────────────────
        _foldState = EditorGUILayout.BeginFoldoutHeaderGroup(_foldState, "🎮 Game State");
        if (_foldState)
        {
            EditorGUILayout.BeginHorizontal();
            DrawBtn("Preparation", Color.cyan,   script.GoToPreparation);
            DrawBtn("Work Day",    Color.green,  script.GoToWorkDay);
            DrawBtn("Loot Phase",  Color.yellow, script.GoToLootPhase);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawBtn("Skip Next",   Color.white,  script.SkipToNextState);
            DrawBtn("End Day",     BtnRed,       script.ForceEndDay);
            EditorGUILayout.EndHorizontal();
            DrawBtn("Print Status", Color.cyan,  script.PrintStateStatus, fullWidth: true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(4);

        // ── Inventory ────────────────────────────────────────────
        _foldInventory = EditorGUILayout.BeginFoldoutHeaderGroup(_foldInventory, "📚 Inventory");
        if (_foldInventory)
        {
            EditorGUILayout.BeginHorizontal();
            DrawBtn("Give Book",   Color.green,  script.GiveTestBook);
            DrawBtn("Give All",    Color.yellow, script.GiveAllBooks);
            DrawBtn("Clear",       BtnRed,       script.ClearInventory);
            EditorGUILayout.EndHorizontal();
            DrawBtn("Print Status", Color.cyan,  script.PrintInventoryStatus, fullWidth: true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(4);

        // ── Shelf ────────────────────────────────────────────────
        _foldShelf = EditorGUILayout.BeginFoldoutHeaderGroup(_foldShelf, "🗄 Shelf");
        if (_foldShelf)
        {
            EditorGUILayout.BeginHorizontal();
            DrawBtn("Push One",  Color.green,  script.PushOneToTargetShelf);
            DrawBtn("Push All",  Color.green,  script.PushAllToTargetShelf);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawBtn("Pop One",   BtnRed,       script.PopOneFromTargetShelf);
            DrawBtn("Pop All",   BtnRed,       script.PopAllFromTargetShelf);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawBtn("Shelf Info",   Color.cyan, script.PrintShelfStatus);
            DrawBtn("Cabinet Info", Color.cyan, script.PrintCabinetStatus);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(4);

        // ── Loot ─────────────────────────────────────────────────
        _foldLoot = EditorGUILayout.BeginFoldoutHeaderGroup(_foldLoot, "🎴 Loot");
        if (_foldLoot)
        {
            EditorGUILayout.BeginHorizontal();
            DrawBtn("Generate Pool", Color.yellow, script.ForceGenerateLoot);
            DrawBtn("Select Card",   Color.green,  script.SelectLootCard);
            DrawBtn("Print Pool",    Color.cyan,   script.PrintLootPool);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(8);

        // ── Full Status ──────────────────────────────────────────
        GUI.backgroundColor = new Color(0.6f, 0.4f, 1f);
        if (GUILayout.Button("━━ PRINT FULL GAME STATUS ━━", GUILayout.Height(34)))
            script.PrintFullStatus();
        GUI.backgroundColor = Color.white;


        EditorGUILayout.Space(4);

        // ── Edit Mode ────────────────────────────────────────────────
        bool _foldEdit = EditorGUILayout.BeginFoldoutHeaderGroup(true, "✏️ Edit Mode");
        if (_foldEdit)
        {
            EditorGUILayout.BeginHorizontal();
            DrawBtn("Enter",  Color.cyan,   script.EnterEditMode);
            DrawBtn("Exit",   BtnRed,       script.ExitEditMode);
            DrawBtn("Toggle", Color.yellow, script.ToggleEditMode);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

    }

    // ── Helpers ──────────────────────────────────────────────────

    private static readonly Color BtnRed = new Color(1f, 0.35f, 0.35f);

    private static void DrawBtn(string label, Color color,
                                System.Action action, bool fullWidth = false)
    {
        GUI.backgroundColor = color;
        GUILayoutOption height = GUILayout.Height(28);
        if (fullWidth)
        {
            if (GUILayout.Button(label, height)) action?.Invoke();
        }
        else
        {
            if (GUILayout.Button(label, height)) action?.Invoke();
        }
        GUI.backgroundColor = Color.white;
    }


    
}

#endif

#endregion