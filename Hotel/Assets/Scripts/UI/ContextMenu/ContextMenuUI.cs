// Assets/Scripts/UI/ContextMenu/ContextMenuUI.cs  [ВИПРАВЛЕНО v2 — Фаза 1]
// ВИПРАВЛЕННЯ:
//   - CustomerBrain → NPCBrain (правильна назва класу)
//   - EditMode/DayStats прибрано (GameState має лише Preparation/WorkDay/LootPhase)
//   - ShowForNPC перевіряє NPCState правильно
//
// UNITY SETUP:
//   1. GameObject "ContextMenuUI" → Add Component → ContextMenuUI
//   2. Assign UIDocument з UXML що має #ContextMenu та #ContextMenuContainer
//   3. Дивись ContextMenuPanel.uxml нижче в коментарях

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ContextMenuUI : MonoBehaviour
{
    public static ContextMenuUI Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Vector2    screenOffset = new Vector2(12f, -12f);

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
        if (_panel != null) _isReady = true;
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
        var btns = new List<ContextButton>();

        bool showButtons = (state == GameState.Preparation || state == GameState.WorkDay);
        if (!showButtons) { Hide(); return; }

        btns.Add(new ContextButton("📖 Інформація", () =>
        {
            Debug.Log($"[ContextMenu] Інфо книги: {bookItem.instance?.templateID}");
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

        Show(worldPos, btns);
    }

    /// Шафа/меблі
    public void ShowForCabinet(Cabinet cabinet, Vector3 worldPos, GameState state)
    {
        if (state == GameState.LootPhase) { Hide(); return; }

        var btns = new List<ContextButton>
        {
            new ContextButton("📦 Відкрити шафу", () =>
            {
                ShopUIManager.Instance?.OpenCabinetUI(cabinet);
                Hide();
            }),
            new ContextButton("ℹ️ Інформація", () =>
            {
                Debug.Log($"[ContextMenu] Шафа: {cabinet.cabinetName}");
                Hide();
            })
        };

        Show(worldPos, btns);
    }

    /// NPC-покупець (тільки WorkDay)
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
                Debug.Log($"[ContextMenu] Пропозиція книги → {npc.Data?.npcName}");
                // TODO: BookOfferUI.Instance?.OpenFor(npc);
                Hide();
            })
        };

        Show(worldPos, btns);
    }

    // ── Private ────────────────────────────────────────────────

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

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        _panel.style.left    = sp.x + screenOffset.x;
        _panel.style.top     = (Screen.height - sp.y) + screenOffset.y;
        _panel.style.display = DisplayStyle.Flex;
    }

    private static void TakeBookToInventory(BookWorldItem item)
    {
        if (item?.instance == null || item.parentShelf == null) return;
        var inst = item.instance;
        item.parentShelf.RemoveBook(item.gameObject);
        InventoryManager.Instance?.AddExistingBook(inst);
        Debug.Log($"[ContextMenu] '{inst.templateID}' → інвентар.");
    }

    private class ContextButton
    {
        public string        Label;
        public System.Action Action;
        public bool          IsDisabled;
        public ContextButton(string l, System.Action a, bool d = false)
        { Label = l; Action = a; IsDisabled = d; }
    }
}