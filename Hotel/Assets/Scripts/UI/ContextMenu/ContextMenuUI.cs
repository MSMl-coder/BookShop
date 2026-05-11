// Assets/Scripts/UI/ContextMenu/ContextMenuUI.cs  [Фаза 1 — фінальна версія]
// ВИПРАВЛЕННЯ:
//   - shelf.RemoveBook(item.gameObject) — тепер Shelf.RemoveBook існує (додано в Shelf.cs)
//   - NPCBrain замість CustomerBrain
//   - Тільки GameState.Preparation / WorkDay / LootPhase (без EditMode/DayStats)

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

    /// Книга на полиці
    public void ShowForBook(BookWorldItem bookItem, Vector3 worldPos, GameState state)
    {
        // Книга: тільки Preparation + WorkDay (ТЗ матриця)
        if (state != GameState.Preparation && state != GameState.WorkDay)
        {
            Hide();
            return;
        }

        var btns = new List<ContextButton>();

        // Інформація — Preparation + WorkDay
        btns.Add(new ContextButton("📖 Інформація", () =>
        {
            Debug.Log($"[ContextMenu] Інфо: {bookItem.instance?.templateID}");
            Hide();
        }));

        // Забрати в інвентар
        if (!bookItem.IsReserved)
        {
        var tpl = BookDatabase.Instance?.GetBook(bookItem.instance?.templateID);
        string sizeTag = tpl != null
            ? $" [{BookSizeHelper.ToIcon(tpl.size)} {tpl.size}]"
            : "";
        
            btns.Add(new ContextButton($"🎒 Забрати в інвентар{sizeTag}", () =>  
                {
                    TakeBookToInventory(bookItem);
                    Hide();
                }));
        }
        else
        {
            btns.Add(new ContextButton("🔒 Зарезервована NPC", null, disabled: true));
        }

        Show(worldPos, btns);
    }


    public static bool CheckBookFitsShelf(BookTemplate template, Shelf shelf)
        {
            if (template == null || shelf == null) return true;
            string reason = shelf.GetSizeRejectReason(template);
            if (string.IsNullOrEmpty(reason)) return true;
    
            // TODO: замінити на правильну сигнатуру NotificationSystem.Show() з проекту
    // Поки що — лог в консоль
    Debug.LogWarning($"[BookSize] {reason}");
   // NotificationSystem.Instance?.ShowWarning($"❌ {reason}");
            return false;
        }


    /// Шафа / меблі
    /// Матриця (ТЗ):
    ///   Інвентар книги  → Preparation + WorkDay
    ///   Інформація      → Preparation + EditMode + WorkDay
    ///   Перемістити     → EditMode
    ///   Обернути        → EditMode
    ///   Змінити колір   → EditMode
    ///   Продати         → EditMode
    public void ShowForCabinet(Cabinet cabinet, Vector3 worldPos, GameState state)
    {
        if (state == GameState.LootPhase || state == GameState.DayStats)
        {
            Hide();
            return;
        }

        var btns = new List<ContextButton>();

        // ── Preparation + WorkDay ────────────────────────────────
        if (state == GameState.Preparation || state == GameState.WorkDay)
        {
            btns.Add(new ContextButton("📦 Інвентар / книги", () =>
            {
                ShopUIManager.Instance?.OpenCabinetUI(cabinet);
                Hide();
            }));
        }

        // ── Preparation + EditMode + WorkDay ─────────────────────
        if (state == GameState.Preparation || state == GameState.EditMode || state == GameState.WorkDay)
        {
            btns.Add(new ContextButton("ℹ️ Інформація", () =>
            {
                Debug.Log($"[ContextMenu] Шафа: {cabinet.cabinetName}");
                Hide();
            }));
        }

        // ── EditMode тільки ──────────────────────────────────────
        if (state == GameState.EditMode)
        {
            btns.Add(new ContextButton("↔️ Перемістити", () =>
            {
                Debug.Log($"[ContextMenu] Перемістити: {cabinet.cabinetName}");
                // TODO: DecorationPanelUI / EditModeManager.StartMove(cabinet)
                Hide();
            }));

            btns.Add(new ContextButton("🔄 Обернути", () =>
            {
                Debug.Log($"[ContextMenu] Обернути: {cabinet.cabinetName}");
                // TODO: EditModeManager.StartRotate(cabinet)
                Hide();
            }));

            btns.Add(new ContextButton("🎨 Змінити колір", () =>
            {
                Debug.Log($"[ContextMenu] Змінити колір: {cabinet.cabinetName}");
                // TODO: ColorPickerUI.Instance?.Open(cabinet)
                Hide();
            }));

            btns.Add(new ContextButton("💰 Продати", () =>
            {
                Debug.Log($"[ContextMenu] Продати: {cabinet.cabinetName}");
                // TODO: EconomyManager.Instance?.SellFurniture(cabinet)
                Hide();
            }));
        }

        Show(worldPos, btns);
    }

    /// NPC-покупець — тільки WorkDay
    public void ShowForNPC(NPCBrain npc, Vector3 worldPos, GameState state)
    {
        if (state != GameState.WorkDay) return;

        bool canOffer = npc.CurrentState == NPCState.Browsing
                     || npc.CurrentState == NPCState.ShowingHint
                     || npc.CurrentState == NPCState.WaitingForPlayer;

        if (!canOffer) return;

        var btns = new List<ContextButton>
        {
            new ContextButton("📚 Запропонувати книгу", () =>
            {
                Debug.Log($"[ContextMenu] Пропозиція → {npc.Data?.npcName}");
                // TODO: BookOfferUI.Instance?.OpenFor(npc);
                Hide();
            })
        };

        Show(worldPos, btns);
    }

    // ── Private ────────────────────────────────────────────────

    // Зберігаємо worldPos для позиціонування
    private Vector3 _pendingWorldPos;

    private void Show(Vector3 worldPos, List<ContextButton> btns)
    {
        if (btns.Count == 0) { Hide(); return; }

        if (!_isReady)
        {
            foreach (var b in btns) Debug.Log($"[ContextMenu-fallback] {b.Label}");
            return;
        }

        _container.Clear();

        foreach (var btn in btns)
        {
            var b = new Button { text = btn.Label };
            b.AddToClassList("ctx-btn");

            if (btn.IsDisabled)
            {
                b.SetEnabled(false);
                b.AddToClassList("ctx-btn--disabled");
            }
            else
            {
                var cap = btn;
                b.clicked += () => cap.Action?.Invoke();
            }
            _container.Add(b);
        }

        _pendingWorldPos = worldPos;

        // Показуємо поза екраном — після layout перемістимо на правильну позицію
        _panel.style.left    = -9999;
        _panel.style.top     = -9999;
        _panel.style.display = DisplayStyle.Flex;

        // GeometryChangedEvent спрацьовує після того як UIToolkit розрахує розмір панелі
        _panel.RegisterCallback<GeometryChangedEvent>(OnPanelGeometryReady);
    }

    private void OnPanelGeometryReady(GeometryChangedEvent evt)
    {
        _panel.UnregisterCallback<GeometryChangedEvent>(OnPanelGeometryReady);

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;

        Vector2 m = mouse.position.ReadValue();

        // UIToolkit: Y інвертований відносно Screen
        float x = m.x + screenOffset.x;
        float y = (Screen.height - m.y) + screenOffset.y;

        float w = _panel.resolvedStyle.width;
        float h = _panel.resolvedStyle.height;
        float margin = 6f;

        // Якщо виходить за правий край — показуємо лівіше від курсора
        if (x + w > Screen.width - margin)
            x = m.x - w - Mathf.Abs(screenOffset.x);

        // Якщо виходить за нижній край — показуємо вище курсора
        if (y + h > Screen.height - margin)
            y = (Screen.height - m.y) - h - Mathf.Abs(screenOffset.y);

        // Гарантуємо що не за лівим і верхнім краями
        x = Mathf.Max(margin, x);
        y = Mathf.Max(margin, y);

        _panel.style.left = x;
        _panel.style.top  = y;
    }

    /// Забрати книгу з полиці в інвентар гравця.
    private static void TakeBookToInventory(BookWorldItem item)
    {
        if (item == null || item.instance == null)
        {
            Debug.LogWarning("[ContextMenu] TakeBookToInventory: item або instance == null");
            return;
        }

        BookInstance inst = null;

        if (item.parentShelf != null)
        {
            // Передаємо BookWorldItem напряму — найнадійніший пошук
            inst = item.parentShelf.RemoveBook(item);
        }

        if (inst == null)
        {
            // parentShelf не призначено — забираємо дані і знищуємо GO вручну
            Debug.LogWarning($"[ContextMenu] parentShelf null для '{item.instance.templateID}', знищуємо GO напряму");
            inst = item.instance;
            Object.Destroy(item.gameObject);
        }

        InventoryManager.Instance?.AddExistingBook(inst);
        Debug.Log($"[ContextMenu] '{inst.templateID}' → інвентар. Всього: {InventoryManager.Instance?.GetBookCount()}");

        // Відкриваємо інвентарну панель
        ShopUIManager.Instance?.OpenInventoryPanel();
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