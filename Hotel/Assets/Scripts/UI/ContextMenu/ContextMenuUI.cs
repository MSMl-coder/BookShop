// Assets/Scripts/UI/ContextMenu/ContextMenuUI.cs  v4
// ЗМІНИ:
//   [1] ShowAtMousePos → ShowAtBottomCenter: меню фіксується по центру знизу
//       замість того щоб слідувати за курсором (де його легко перекрити UI)
//   [2] Логіка Show* методів не змінена — тільки позиціонування

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ContextMenuUI : MonoBehaviour
{
    public static ContextMenuUI Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

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
        if (uiDocument == null) { Debug.LogError("[ContextMenuUI] UIDocument не призначено!"); return; }

        var root   = uiDocument.rootVisualElement;
        _panel     = root.Q<VisualElement>("ContextMenu");
        _container = root.Q<VisualElement>("ContextMenuContainer");

        if (_panel == null) { Debug.LogError("[ContextMenuUI] #ContextMenu не знайдено у UXML!"); return; }

        _isReady = true;
        Hide();
    }

    // ── Public API ────────────────────────────────────────────────

    public void Hide()
    {
        if (_panel != null)
        {
            _panel.style.display  = DisplayStyle.None;
            _panel.pickingMode    = PickingMode.Ignore;
        }
    }

    public void ShowForBook(BookWorldItem bookItem, Vector3 worldPos, GameState state)
    {
        if (state != GameState.Preparation && state != GameState.WorkDay)
        { Hide(); return; }

        var btns = new List<ContextButton>();

        var shelf   = bookItem?.parentShelf;
        var cabinet = shelf != null ? shelf.GetComponentInParent<Cabinet>() : null;
        if (cabinet != null || shelf != null)
        {
            string cabinetName = cabinet?.cabinetName ?? shelf?.name ?? "Полиця";
            btns.Add(new ContextButton("📦 Переглянути полицю", () =>
            {
                if (cabinet != null)
                    ShelfManagerPanelController.Instance?.Open(cabinet);
                else
                    BookshopUIController.Instance?.OpenInventoryFromShelf(shelf.name);
                Hide();
            }));
        }

        if (!bookItem.IsReserved)
        {
            btns.Add(new ContextButton("📖 Забрати в інвентар", () =>
            {
                TakeBookToInventory(bookItem);
                Hide();
            }));
        }
        else
        {
            btns.Add(new ContextButton("🔒 Зарезервована NPC", null, disabled: true));
        }

        btns.Add(new ContextButton("ℹ️ Інформація", () =>
        {
            var tpl = BookDatabase.Instance?.GetBook(bookItem.instance?.templateID);
            if (tpl != null) BookInfoCardController.Instance?.Show(tpl);
            Hide();
        }));

        ShowAtBottomCenter(btns);
    }

    public void ShowForCabinet(Cabinet cabinet, Vector3 worldPos, GameState state)
    {
        if (state == GameState.LootPhase || state == GameState.DayStats) { Hide(); return; }

        var btns = new List<ContextButton>();

        if (state == GameState.Preparation || state == GameState.WorkDay)
        {
            btns.Add(new ContextButton("📦 Управління полицями", () =>
            {
                ShelfManagerPanelController.Instance?.Open(cabinet);
                Hide();
            }));
        }

        if (state == GameState.EditMode)
        {
            btns.Add(new ContextButton("↔️ Перемістити",  () => { Hide(); }));
            btns.Add(new ContextButton("🔄 Обернути",     () => { Hide(); }));
            btns.Add(new ContextButton("🎨 Змінити колір",() => { Hide(); }));
            btns.Add(new ContextButton("💰 Продати",      () => { Hide(); }));
        }

        if (btns.Count > 0) ShowAtBottomCenter(btns);
    }

    public void ShowForNPC(NPCBrain npc, Vector3 worldPos, GameState state)
    {
        // NPC тепер відкриває NPCInspectorPanel — не потрібне контекстне меню
        Hide();
        npc?.OnNPCClicked();
    }

    // ── [FIX 1] Позиціонування — фіксовано знизу по центру ───────

    private void ShowAtBottomCenter(List<ContextButton> buttons)
    {
        if (!_isReady || _panel == null || buttons == null || buttons.Count == 0)
        { Hide(); return; }

        _container?.Clear();
        foreach (var btn in buttons)
        {
            if (btn.IsDisabled)
            {
                var lbl = new Label(btn.Label);
                lbl.AddToClassList("ctx-btn");
                lbl.AddToClassList("ctx-btn--disabled");
                _container?.Add(lbl);
            }
            else
            {
                var b = new Button(btn.Action) { text = btn.Label };
                b.AddToClassList("ctx-btn");
                _container?.Add(b);
            }
        }

        // [FIX 1] Знімаємо попередні inline стилі позиції —
        // позиція тепер повністю керується USS (.ctx-panel)
        _panel.style.left   = StyleKeyword.Null;
        _panel.style.top    = StyleKeyword.Null;
        _panel.style.right  = StyleKeyword.Null;
        _panel.style.bottom = StyleKeyword.Null;

        _panel.style.display = DisplayStyle.Flex;
        _panel.pickingMode   = PickingMode.Position;
    }

    // ── Helpers ───────────────────────────────────────────────────

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
    }

    // ── Internal ContextButton ────────────────────────────────────
    private class ContextButton
    {
        public string  Label;
        public System.Action Action;
        public bool    IsDisabled;

        public ContextButton(string label, System.Action action, bool disabled = false)
        {
            Label      = label;
            Action     = action;
            IsDisabled = disabled;
        }
    }
}