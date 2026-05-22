// Assets/Scripts/UI/ContextMenu/ContextMenuUI.cs  [Фаза 1 — фінальна версія]
// ВИПРАВЛЕННЯ:
//   - shelf.RemoveBook(item.gameObject) — тепер Shelf.RemoveBook існує (додано в Shelf.cs)
//   - NPCBrain замість CustomerBrain
//   - Тільки GameState.Preparation / WorkDay / LootPhase (без EditMode/DayStats)
//   - [v2] ShowForCabinet → BookshopUIController.OpenInventoryFromShelf()
//   - [v2] TakeBookToInventory → BookshopUIController.OpenModal("inv")

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ContextMenuUI : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────
    public static ContextMenuUI Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Vector2    screenOffset = new Vector2(12f, -12f);

    // ── Runtime ────────────────────────────────────────────────
    private VisualElement _panel;
    private VisualElement _container;
    private bool          _isReady;

    // ── Unity ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (uiDocument == null) return;

        var root   = uiDocument.rootVisualElement;
        _panel     = root.Q<VisualElement>("ContextMenu");
        _container = root.Q<VisualElement>("ContextMenuContainer");

        if (_panel == null)
        {
            Debug.LogError("[ContextMenuUI] #ContextMenu не знайдено у UXML!");
            return;
        }

        _isReady = true;
        Hide();
    }

    // ── Public API ─────────────────────────────────────────────

    public void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    // ── Книга на полиці ────────────────────────────────────────
    public void ShowForBook(BookWorldItem bookItem, Vector3 worldPos, GameState state)
    {
        if (state != GameState.Preparation && state != GameState.WorkDay)
        {
            Hide();
            return;
        }

        var btns = new List<ContextButton>();

        // Інформація
        btns.Add(new ContextButton("Info", () =>
        {
            Debug.Log($"[ContextMenu] Info: {bookItem.instance?.templateID}");
            Hide();
        }));

        // Забрати в інвентар
        if (!bookItem.IsReserved)
        {
            var tpl = BookDatabase.Instance?.GetBook(bookItem.instance?.templateID);
          //  string sizeTag = tpl != null
              //  ? $" [{BookSizeHelper.ToIcon(tpl.size)} {tpl.size}]"
             //  : "";

            btns.Add(new ContextButton($"Take to inventory", () =>
            {
                TakeBookToInventory(bookItem);
                Hide();
            }));
        }
        else
        {
            btns.Add(new ContextButton("Reserved by NPC", null, disabled: true));
        }

        Show(worldPos, btns);
    }

    // ── Перевірка розміру книги ────────────────────────────────
    public static bool CheckBookFitsShelf(BookTemplate template, Shelf shelf)
    {
        if (template == null || shelf == null) return true;
        string reason = shelf.GetSizeRejectReason(template);
        if (string.IsNullOrEmpty(reason)) return true;
        Debug.LogWarning($"[BookSize] {reason}");
        return false;
    }

    // ── Шафа / меблі ──────────────────────────────────────────
    public void ShowForCabinet(Cabinet cabinet, Vector3 worldPos, GameState state)
    {
        if (state == GameState.LootPhase || state == GameState.DayStats)
        {
            Hide();
            return;
        }

        var btns = new List<ContextButton>();

        // Preparation + WorkDay — відкрити інвентар шафи
        if (state == GameState.Preparation || state == GameState.WorkDay)
        {
            btns.Add(new ContextButton("Inventory / Books", () =>
            {
                // ── Відкриваємо новий BookshopUI з фільтром по шафі ──
                if (BookshopUIController.Instance != null)
                    BookshopUIController.Instance.OpenInventoryFromShelf(cabinet?.cabinetName);
                else
                    ShopUIManager.Instance?.OpenCabinetUI(cabinet); // fallback
                Hide();
            }));
        }

        // Preparation + EditMode + WorkDay — інформація
        if (state == GameState.Preparation || state == GameState.EditMode || state == GameState.WorkDay)
        {
            btns.Add(new ContextButton("Information", () =>
            {
                Debug.Log($"[ContextMenu] Cabinet: {cabinet.cabinetName}");
                Hide();
            }));
        }

        // EditMode тільки
        if (state == GameState.EditMode)
        {
            btns.Add(new ContextButton("Move", () =>
            {
                Debug.Log($"[ContextMenu] Move: {cabinet.cabinetName}");
                Hide();
            }));

            btns.Add(new ContextButton("Rotate", () =>
            {
                Debug.Log($"[ContextMenu] Rotate: {cabinet.cabinetName}");
                Hide();
            }));

            btns.Add(new ContextButton("Change Color", () =>
            {
                Debug.Log($"[ContextMenu] Color: {cabinet.cabinetName}");
                Hide();
            }));

            btns.Add(new ContextButton("Sell", () =>
            {
                Debug.Log($"[ContextMenu] Sell: {cabinet.cabinetName}");
                Hide();
            }));
        }

        Show(worldPos, btns);
    }

    // ── NPC ────────────────────────────────────────────────────
    public void ShowForNPC(NPCBrain npc, Vector3 worldPos, GameState state)
    {
        if (state != GameState.WorkDay) { Hide(); return; }

        var btns = new List<ContextButton>();
        btns.Add(new ContextButton("Talk", () =>
        {
            Debug.Log($"[ContextMenu] Talk NPC: {npc.name}");
            Hide();
        }));

        Show(worldPos, btns);
    }

    // ── Internal ───────────────────────────────────────────────
    private void Show(Vector3 worldPos, List<ContextButton> buttons)
    {
        if (!_isReady || _panel == null || buttons == null || buttons.Count == 0)
        {
            Hide();
            return;
        }

        _container?.Clear();

        foreach (var btn in buttons)
        {
            if (btn.IsDisabled)
            {
                var lbl = new Label(btn.Label);
                lbl.AddToClassList("context-btn");
                lbl.AddToClassList("disabled");
                _container?.Add(lbl);
            }
            else
            {
                var b = new Button(btn.Action);
                b.text = btn.Label;
                b.AddToClassList("context-btn");
                _container?.Add(b);
            }
        }

        // Позиція: конвертуємо world → screen → UI-координати
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector2 screenPos = cam.WorldToScreenPoint(worldPos);
            // UIToolkit координати (Y інвертований)
            float uiY = Screen.height - screenPos.y;
            _panel.style.left = screenPos.x + screenOffset.x;
            _panel.style.top  = uiY        + screenOffset.y;
        }

        _panel.style.display = DisplayStyle.Flex;
    }

    private void TakeBookToInventory(BookWorldItem bookItem)
    {
        if (bookItem == null) return;

        // Знімаємо книгу з полиці
        var shelf = bookItem.GetComponentInParent<Shelf>();
        if (shelf != null) shelf.RemoveBook(bookItem.gameObject);

        // Додаємо в інвентар
        if (bookItem.instance != null)
            InventoryManager.Instance?.AddExistingBook(bookItem.instance);

        Destroy(bookItem.gameObject);
        Debug.Log($"[ContextMenu] Taken to inventory. Total: {InventoryManager.Instance?.GetBookCount()}");

        // ── Відкриваємо BookshopUI інвентар ──
        if (BookshopUIController.Instance != null)
            BookshopUIController.Instance.OpenModal("inv");
        else
            ShopUIManager.Instance?.OpenInventoryPanel(); // fallback
    }

    // ── Inner class ────────────────────────────────────────────
    private class ContextButton
    {
        public string        Label;
        public System.Action Action;
        public bool          IsDisabled;

        public ContextButton(string label, System.Action action, bool disabled = false)
        {
            Label      = label;
            Action     = action;
            IsDisabled = disabled;
        }
    }
}
