// Assets/Scripts/UI/ContextMenu/ContextMenuUI.cs
// FIX 1: Контекстне меню позиціонується по курсору миші (не по worldPos)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class ContextMenuUI : MonoBehaviour
{
    public static ContextMenuUI Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Vector2    offset = new Vector2(8f, 8f); // від курсора

    private VisualElement _panel;
    private VisualElement _container;
    private bool          _isReady;

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

    // ── Public API ──────────────────────────────────────────────

    public void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    public void ShowForBook(BookWorldItem bookItem, Vector3 worldPos, GameState state)
    {
        if (state != GameState.Preparation && state != GameState.WorkDay)
        { Hide(); return; }

        var btns = new List<ContextButton>();

        btns.Add(new ContextButton("ℹ️ Інформація", () =>
        {
            var tpl = BookDatabase.Instance?.GetBook(bookItem.instance?.templateID);
            if (tpl != null) BookInfoCardController.Instance?.Show(tpl);
            Hide();
        }));

        if (!bookItem.IsReserved)
        {
            btns.Add(new ContextButton("🎒 Забрати в інвентар", () =>
            {
                TakeBookToInventory(bookItem);
                Hide();
            }));
        }
        else
        {
            btns.Add(new ContextButton("🔒 Зарезервована NPC", null, disabled: true));
        }

        ShowAtCursor(btns);
    }

    public void ShowForCabinet(Cabinet cabinet, Vector3 worldPos, GameState state)
    {
        if (state == GameState.LootPhase || state == GameState.DayStats)
        { Hide(); return; }

        var btns = new List<ContextButton>();

        if (state == GameState.Preparation || state == GameState.WorkDay)
        {
            btns.Add(new ContextButton("📚 Управління полицями", () =>
            {
                ShelfManagerPanelController.Instance?.Open(cabinet);
                Hide();
            }));
        }

        if (state == GameState.Preparation || state == GameState.EditMode || state == GameState.WorkDay)
        {
            btns.Add(new ContextButton("ℹ️ Інформація", () =>
            {
                Debug.Log($"[ContextMenu] Шафа: {cabinet.cabinetName}");
                Hide();
            }));
        }

        if (state == GameState.EditMode)
        {
            btns.Add(new ContextButton("↔️ Перемістити", () =>
            {
                Debug.Log($"[ContextMenu] Перемістити: {cabinet.cabinetName}");
                Hide();
            }));
            btns.Add(new ContextButton("🔄 Обернути", () =>
            {
                Debug.Log($"[ContextMenu] Обернути: {cabinet.cabinetName}");
                Hide();
            }));
            btns.Add(new ContextButton("🎨 Змінити колір", () =>
            {
                Debug.Log($"[ContextMenu] Колір: {cabinet.cabinetName}");
                Hide();
            }));
            btns.Add(new ContextButton("💰 Продати", () =>
            {
                Debug.Log($"[ContextMenu] Продати: {cabinet.cabinetName}");
                Hide();
            }));
        }

        ShowAtCursor(btns);
    }

    public void ShowForNPC(NPCBrain npc, Vector3 worldPos, GameState state)
    {
        if (state != GameState.WorkDay) { Hide(); return; }

        ShowAtCursor(new List<ContextButton>
        {
            new ContextButton("💬 Говорити", () =>
            {
                Debug.Log($"[ContextMenu] Talk: {npc.name}");
                Hide();
            })
        });
    }

    // ── Internal ─────────────────────────────────────────────────

    /// Показати меню точно біля курсора миші (не через worldPos → screen конвертацію)
    private void ShowAtCursor(List<ContextButton> buttons)
    {
        if (!_isReady || _panel == null || buttons == null || buttons.Count == 0)
        { Hide(); return; }

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
                var b = new Button(btn.Action) { text = btn.Label };
                b.AddToClassList("context-btn");
                _container?.Add(b);
            }
        }

        // Позиція = поточний cursor у screen-space → UIToolkit-space
        Vector2 mouseScreen = Mouse.current?.position.ReadValue() ?? Vector2.zero;

        // UIToolkit: Y = 0 зверху, Screen: Y = 0 знизу → інвертуємо
        float uiX = mouseScreen.x + offset.x;
        float uiY = (Screen.height - mouseScreen.y) + offset.y;

        // Клампуємо щоб не вилізти за край екрану
        // Розмір панелі невідомий заздалегідь — беремо запас 200×300
        float panelW = 200f;
        float panelH = 300f;
        uiX = Mathf.Clamp(uiX, 0f, Screen.width  - panelW);
        uiY = Mathf.Clamp(uiY, 0f, Screen.height - panelH);

        _panel.style.left = uiX;
        _panel.style.top  = uiY;
        _panel.style.display = DisplayStyle.Flex;
    }

    private static void TakeBookToInventory(BookWorldItem item)
    {
        if (item?.instance == null) return;

        BookInstance inst = null;
        if (item.parentShelf != null)
            inst = item.parentShelf.TakeBookAt(item.bookIndex);

        if (inst == null)
        {
            inst = item.instance;
            Object.Destroy(item.gameObject);
        }

        InventoryManager.Instance?.AddExistingBook(inst);
        Debug.Log($"[ContextMenu] '{inst.templateID}' → інвентар.");
    }

    public static bool CheckBookFitsShelf(BookTemplate template, Shelf shelf)
    {
        if (template == null || shelf == null) return true;
        string reason = shelf.GetSizeRejectReason(template);
        if (!string.IsNullOrEmpty(reason)) Debug.LogWarning($"[BookSize] {reason}");
        return string.IsNullOrEmpty(reason);
    }

    private class ContextButton
    {
        public string        Label;
        public System.Action Action;
        public bool          IsDisabled;
        public ContextButton(string label, System.Action action, bool disabled = false)
        { Label = label; Action = action; IsDisabled = disabled; }
    }
}