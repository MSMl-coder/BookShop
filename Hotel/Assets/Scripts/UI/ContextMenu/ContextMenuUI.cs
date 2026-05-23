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

        ShowAtWorldPoint(worldPos, btns);
    }

    public void ShowForCabinet(Cabinet cabinet, Vector3 worldPos, GameState state)
    {
        Debug.Log($"[ContextMenu] ShowForCabinet: {cabinet?.cabinetName} | state={state}");
    
        if (state == GameState.LootPhase || state == GameState.DayStats)
        { Hide(); return; }
    
        var btns = new List<ContextButton>();
    
        if (state == GameState.Preparation || state == GameState.WorkDay)
        {
            btns.Add(new ContextButton("📚 Управління полицями", () =>
            {
                Debug.Log($"[ContextMenu] → Open ShelfManager for {cabinet?.cabinetName}");
                Debug.Log($"[ContextMenu] ShelfMgrInstance: {ShelfManagerPanelController.Instance != null}");
    
                ShelfManagerPanelController.Instance?.Open(cabinet);
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

        ShowAtWorldPoint(worldPos, btns);
        Debug.Log($"[ContextMenu] Menu shown with {btns.Count} buttons");
    }

    public void ShowForNPC(NPCBrain npc, Vector3 worldPos, GameState state)
    {
        if (state != GameState.WorkDay) { Hide(); return; }

        ShowAtWorldPoint(worldPos, new List<ContextButton>
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
   private void ShowAtWorldPoint(Vector3 worldPos, List<ContextButton> buttons)
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
    
        // 1. Конвертуємо world → screen (пікселі, Y=0 знизу)
        Camera cam = Camera.main;
        Vector2 screenPos = cam != null
            ? (Vector2)cam.WorldToScreenPoint(worldPos)
            : Mouse.current?.position.ReadValue() ?? Vector2.zero;
    
        // 2. Screen → UIToolkit (Y інвертований: 0 зверху)
        float uiX = screenPos.x;
        float uiY = Screen.height - screenPos.y;
    
        // 3. Зсуваємо від точки кліку трохи вправо-вниз
        uiX += 12f;
        uiY += 12f;
    
        // 4. Клампуємо щоб не вилізти за край
        // Беремо розміри панелі після layout — або fallback 200×250
        float pw = _panel.resolvedStyle.width;
        float ph = _panel.resolvedStyle.height;
        if (pw < 10f) pw = 200f;
        if (ph < 10f) ph = 250f;
    
        uiX = Mathf.Clamp(uiX, 4f, Screen.width  - pw - 4f);
        uiY = Mathf.Clamp(uiY, 4f, Screen.height - ph - 4f);
    
        _panel.style.left    = uiX;
        _panel.style.top     = uiY;
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