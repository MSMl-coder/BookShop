// Assets/Scripts/UI/ContextMenu/ContextMenuUI.cs
// v3.1 — Горизонтальна капсула з іконками
//
// АРХІТЕКТУРА (не змінюється від v2):
//   • _panel / _container знаходяться в Start() — стара надійна схема
//   • ShowAtMousePos() — позиціонування по курсору (як в оригіналі що працював)
//   • ContextButton — той самий internal class
//
// НОВЕ:
//   • ShowForBook() тепер додає кнопку "📦 Переглянути полицю" (якщо bookItem.parentShelf != null)
//   • ShowForCabinet() — без змін (вже мав кнопку управління полицями)
//   • BookshopUIBridge — ВИДАЛЕНО, не використовується
//   • Кнопки будуються через той самий _container (UXML не змінюється)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class ContextMenuUI : MonoBehaviour
{
    public static ContextMenuUI Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _panel;
    private VisualElement _container;
    private bool          _isReady;

    // ── Lifecycle ──────────────────────────────────────────────────────────

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

        if (_panel == null)
        {
            Debug.LogError("[ContextMenuUI] #ContextMenu не знайдено у UXML!");
            return;
        }

        _isReady = true;
        Hide();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    // Викликається з InteractionRouter коли рейкаст потрапив у BookWorldItem
    public void ShowForBook(BookWorldItem bookItem, Vector3 worldPos, GameState state)
    {
        if (state != GameState.Preparation && state != GameState.WorkDay)
        { Hide(); return; }

        var btns = new List<ContextButton>();

        // ── Кнопка 1: Переглянути полицю ──────────────────────────────────
        // Завжди перша — вирішує проблему "повна полиця → неможливо відкрити"
        var shelf   = bookItem?.parentShelf;
        var cabinet = shelf != null ? shelf.GetComponentInParent<Cabinet>() : null;
        if (cabinet != null || shelf != null)
        {
            string cabinetName = cabinet?.cabinetName ?? shelf?.name ?? "Полиця";
            btns.Add(new ContextButton("📦 Переглянути полицю", () =>
            {
                Debug.Log($"[ContextMenu] Відкрити ShelfManager: {cabinetName}");
                if (cabinet != null)
                    ShelfManagerPanelController.Instance?.Open(cabinet);
                else
                    BookshopUIController.Instance?.OpenInventoryFromShelf(shelf.name);
                Hide();
            }));
        }

        // ── Кнопка 2: Забрати книгу ───────────────────────────────────────
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

        // ── Кнопка 3: Інформація ──────────────────────────────────────────
        btns.Add(new ContextButton("ℹ️ Інформація", () =>
        {
            var tpl = BookDatabase.Instance?.GetBook(bookItem.instance?.templateID);
            if (tpl != null) BookInfoCardController.Instance?.Show(tpl);
            Hide();
        }));

        ShowAtMousePos(btns);
    }

    // Викликається з InteractionRouter коли рейкаст потрапив у Cabinet
    public void ShowForCabinet(Cabinet cabinet, Vector3 worldPos, GameState state)
    {
        Debug.Log($"[ContextMenu] ShowForCabinet: {cabinet?.cabinetName} | state={state}");

        if (state == GameState.LootPhase || state == GameState.DayStats)
        { Hide(); return; }

        var btns = new List<ContextButton>();

        if (state == GameState.Preparation || state == GameState.WorkDay)
        {
            btns.Add(new ContextButton("📦 Управління полицями", () =>
            {
                Debug.Log($"[ContextMenu] → ShelfManager: {cabinet?.cabinetName}");
                ShelfManagerPanelController.Instance?.Open(cabinet);
                Hide();
            }));
        }

        if (state == GameState.EditMode)
        {
            btns.Add(new ContextButton("↔️ Перемістити", () => { Debug.Log("[ContextMenu] Перемістити"); Hide(); }));
            btns.Add(new ContextButton("🔄 Обернути",    () => { Debug.Log("[ContextMenu] Обернути");    Hide(); }));
            btns.Add(new ContextButton("🎨 Змінити колір",() => { Debug.Log("[ContextMenu] Колір");       Hide(); }));
            btns.Add(new ContextButton("💰 Продати",     () => { Debug.Log("[ContextMenu] Продати");     Hide(); }));
        }

        ShowAtMousePos(btns);
        Debug.Log($"[ContextMenu] {btns.Count} кнопок показано");
    }

    public void ShowForNPC(NPCBrain npc, Vector3 worldPos, GameState state)
    {
        if (state != GameState.WorkDay) { Hide(); return; }

        ShowAtMousePos(new List<ContextButton>
        {
            new ContextButton("💬 Говорити", () =>
            {
                Debug.Log($"[ContextMenu] Talk: {npc.name}");
                Hide();
            })
        });
    }

    // ── Internal ───────────────────────────────────────────────────────────

    private void ShowAtMousePos(List<ContextButton> buttons)
    {
        if (!_isReady || _panel == null || buttons == null || buttons.Count == 0)
        {
            Debug.LogWarning($"[ContextMenuUI] ShowAtMousePos скасовано: isReady={_isReady} panel={_panel != null} btns={buttons?.Count}");
            Hide();
            return;
        }

        // Будуємо кнопки
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

        // Позиціонуємо по курсору миші
        Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;

        // Screen → UIToolkit (Y інвертований: 0 зверху в UIToolkit)
        float uiX = mousePos.x + 12f;
        float uiY = (Screen.height - mousePos.y) + 12f;

        // Клампуємо щоб не вилізти за край (беремо розміри або fallback)
        float pw = _panel.resolvedStyle.width;
        float ph = _panel.resolvedStyle.height;
        if (pw < 10f) pw = 200f;
        if (ph < 10f) ph = 120f;

        uiX = Mathf.Clamp(uiX, 4f, Screen.width  - pw - 4f);
        uiY = Mathf.Clamp(uiY, 4f, Screen.height - ph - 4f);

        _panel.style.left    = uiX;
        _panel.style.top     = uiY;
        _panel.style.display = DisplayStyle.Flex;

        Debug.Log($"[ContextMenuUI] Показано {buttons.Count} кнопок на ({uiX:F0}, {uiY:F0})");
    }

    // ── Утиліти ────────────────────────────────────────────────────────────

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

    // ── Internal class ─────────────────────────────────────────────────────

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