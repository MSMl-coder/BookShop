// Assets/Scripts/UI/ContextMenu/ContextMenuUI.cs
// ПОВНІСТЮ ПЕРЕПИСАНО під реальний ContextMenuUI.uxml:
//
//   UXML структура:
//     ContextMenu           — root панель (display:none за замовчуванням)
//     └─ ContextMenuContainer — порожній контейнер, кнопки додаються динамічно
//
//   Меню будується програмно: ClearContainer() → Add(кнопки/лейбли)
//   залежно від того що передано: Book / NPC / Cabinet.
//
//   Сигнатури відповідають InteractionRouter.cs:
//     ShowForBook(BookWorldItem, Vector3, GameState)
//     ShowForNPC(NPCBrain, Vector3, GameState)
//     ShowForCabinet(Cabinet, Vector3, GameState)
using UnityEngine;
using UnityEngine.UIElements;

public class ContextMenuUI : MonoBehaviour
{
    public static ContextMenuUI Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    // ── UXML елементи ─────────────────────────────────────────────────────────
    private VisualElement _menu;       // name="ContextMenu"
    private VisualElement _container;  // name="ContextMenuContainer"

    // ── Поточний контекст ─────────────────────────────────────────────────────
    private BookWorldItem _currentItem;
    private NPCBrain      _currentNPC;
    private Cabinet       _currentCabinet;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Update()
    {
        // Закрити меню по ЛКМ поза його межами
        if (_menu == null || _menu.style.display == DisplayStyle.None) return;
        if (!UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) return;

        var mouse = UnityEngine.InputSystem.Mouse.current;
        var panel = uiDocument?.rootVisualElement?.panel;
        if (panel == null) return;

        Vector2 screenPos = mouse.position.ReadValue();
        Vector2 panelPos  = RuntimePanelUtils.ScreenToPanel(
            panel, new Vector2(screenPos.x, Screen.height - screenPos.y));

        // Якщо клік НЕ по меню — ховаємо
        var picked = panel.Pick(panelPos);
        if (picked == null || !IsChildOf(_menu, picked))
            Hide();
    }

    private static bool IsChildOf(VisualElement parent, VisualElement child)
    {
        var current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
    }

    private void OnEnable()
    {
        if (uiDocument == null) return;
        var root   = uiDocument.rootVisualElement;
        _menu      = root.Q<VisualElement>("ContextMenu");
        _container = root.Q<VisualElement>("ContextMenuContainer");

        if (_menu == null)
            Debug.LogWarning("[ContextMenuUI] 'ContextMenu' не знайдено у UXML!");
        if (_container == null)
            Debug.LogWarning("[ContextMenuUI] 'ContextMenuContainer' не знайдено у UXML!");

        Hide();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // InteractionRouter рядок 117
    public void ShowForBook(BookWorldItem item, Vector3 hitPoint, GameState state)
    {
        if (item == null) { Hide(); return; }
        _currentItem    = item;
        _currentNPC     = null;
        _currentCabinet = null;

        BookTemplate template = BookDatabase.Instance?.GetBook(item.instance?.templateID);
        if (template == null) { Hide(); return; }

        Build(() =>
        {
            // Заголовок
            AddHeader(template.title);
            AddSubLabel($"{template.author}  •  {template.bookSize}  •  {template.genre}");

            if (item.IsReserved)
                AddSubLabel("🔒 Зарезервовано NPC");

            AddDivider();

            // "Забрати в інвентар"
            bool canTake = !item.IsReserved &&
                           (state == GameState.Preparation || state == GameState.WorkDay);
            AddButton("Забрати в інвентар", OnTakeClicked, enabled: canTake);

            // "Інформація"
            AddButton("Інформація", OnInfoClicked);
        });
    }

    // InteractionRouter рядок 129
    public void ShowForNPC(NPCBrain npc, Vector3 hitPoint, GameState state)
    {
        if (npc == null) { Hide(); return; }
        _currentItem    = null;
        _currentNPC     = npc;
        _currentCabinet = null;

        Build(() =>
        {
            AddHeader(npc.Data?.npcName ?? "NPC");
            AddSubLabel($"Шукає: {npc.DesiredGenre}");
            AddDivider();

            bool canOffer = state == GameState.WorkDay;
            AddButton("Запропонувати книгу", OnOfferBookClicked, enabled: canOffer);
        });
    }

    // InteractionRouter рядок 141
    public void ShowForCabinet(Cabinet cabinet, Vector3 hitPoint, GameState state)
    {
        if (cabinet == null) { Hide(); return; }
        _currentItem    = null;
        _currentNPC     = null;
        _currentCabinet = cabinet;

        Build(() =>
        {
            AddHeader(cabinet.cabinetName);

            int total = 0;
            foreach (var s in cabinet.shelves) if (s != null) total += s.GetBookCount();
            AddSubLabel($"{cabinet.shelves.Count} полиць  •  {total} книг");

            AddDivider();

            bool canOpen = state == GameState.Preparation ||
                           state == GameState.WorkDay     ||
                           state == GameState.LootPhase;
            AddButton("Відкрити шафу", OnOpenCabinetClicked, enabled: canOpen);
        });
    }

    public void Hide()
    {
        _currentItem    = null;
        _currentNPC     = null;
        _currentCabinet = null;

        if (_menu != null)
            _menu.style.display = DisplayStyle.None;
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private void Build(System.Action populate)
    {
        if (_menu == null || _container == null) return;

        _container.Clear();
        populate();

        PositionAtMouse();
        _menu.style.display = DisplayStyle.Flex;
    }

    // ── Позиціонування ────────────────────────────────────────────────────────

    private void PositionAtMouse()
    {
        if (_menu == null || uiDocument == null) return;

        var panel = uiDocument.rootVisualElement?.panel;
        if (panel == null) return;

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;

        Vector2 screenPos = mouse.position.ReadValue();

        // Screen → Panel coordinates (UIToolkit Y інвертований)
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
            panel,
            new Vector2(screenPos.x, Screen.height - screenPos.y)
        );

        // Зсув -5/-5 як раніше
        _menu.style.left = panelPos.x - 5f;
        _menu.style.top  = panelPos.y - 5f;
    }

    private void AddHeader(string text)
    {
        var lbl = new Label(text);
        lbl.style.fontSize        = 13;
        lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        lbl.style.color           = new StyleColor(new Color(1f, 1f, 1f, 0.95f));
        lbl.style.paddingLeft     = 8;
        lbl.style.paddingRight    = 8;
        lbl.style.paddingTop      = 6;
        lbl.style.paddingBottom   = 2;
        lbl.style.whiteSpace      = WhiteSpace.Normal;
        _container.Add(lbl);
    }

    private void AddSubLabel(string text)
    {
        var lbl = new Label(text);
        lbl.style.fontSize  = 10;
        lbl.style.color     = new StyleColor(new Color(1f, 1f, 1f, 0.45f));
        lbl.style.paddingLeft  = 8;
        lbl.style.paddingRight = 8;
        lbl.style.paddingBottom = 4;
        lbl.style.whiteSpace = WhiteSpace.Normal;
        _container.Add(lbl);
    }

    private void AddDivider()
    {
        var div = new VisualElement();
        div.style.height           = 1;
        div.style.marginTop        = 2;
        div.style.marginBottom     = 2;
        div.style.backgroundColor  = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
        _container.Add(div);
    }

    private void AddButton(string text, System.Action onClick, bool enabled = true)
    {
        var btn = new Button(onClick) { text = text };
        btn.style.paddingLeft   = 8;
        btn.style.paddingRight  = 8;
        btn.style.paddingTop    = 7;
        btn.style.paddingBottom = 7;
        btn.style.marginTop     = 1;
        btn.style.marginBottom  = 1;
        btn.style.backgroundColor = StyleKeyword.None;
        btn.style.borderTopWidth = btn.style.borderBottomWidth =
        btn.style.borderLeftWidth = btn.style.borderRightWidth = 0;
        btn.style.unityTextAlign  = TextAnchor.MiddleLeft;
        btn.style.fontSize        = 12;

        if (enabled)
        {
            btn.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.88f));
            btn.RegisterCallback<MouseEnterEvent>(_ =>
                btn.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.07f)));
            btn.RegisterCallback<MouseLeaveEvent>(_ =>
                btn.style.backgroundColor = StyleKeyword.None);
        }
        else
        {
            btn.style.color   = new StyleColor(new Color(1f, 1f, 1f, 0.25f));
            btn.SetEnabled(false);
        }

        _container.Add(btn);
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private void OnTakeClicked()
    {
        if (_currentItem == null) return;
        if (_currentItem.IsReserved) { Hide(); return; }

        Shelf shelf = _currentItem.parentShelf;
        int   idx   = _currentItem.bookIndex;

        if (shelf == null || idx < 0) { Hide(); return; }

        shelf.DematerializeBook(_currentItem);
        BookInstance removed = shelf.TakeBookAt(idx);
        if (removed != null)
            InventoryManager.Instance?.AddExistingBook(removed);

        Hide();
    }

    private void OnInfoClicked()
    {
        if (_currentItem?.instance == null) return;
        var tmpl = BookDatabase.Instance?.GetBook(_currentItem.instance.templateID);
        if (tmpl != null) BookInfoCardController.Instance?.Show(tmpl);
        Hide();
    }

    private void OnOfferBookClicked()
    {
        if (_currentNPC == null) return;

        var inv   = InventoryManager.Instance;
        var books = inv?.GetSortedInventory(SortType.ByGenre);
        if (books == null || books.Count == 0) { Hide(); return; }

        foreach (var b in books)
        {
            var tmpl = BookDatabase.Instance?.GetBook(b.templateID);
            if (tmpl?.genre == _currentNPC.DesiredGenre &&
                tmpl.sellPrice <= (_currentNPC.Data?.maxBudget ?? float.MaxValue))
            {
                _currentNPC.ReceiveBookOffer(tmpl);
                if (_currentNPC.FoundBook == tmpl)
                    inv.RemoveBook(b);
                break;
            }
        }

        Hide();
    }

    private void OnOpenCabinetClicked()
    {
        if (_currentCabinet == null) return;
        ShopUIManager.Instance?.OpenCabinetUI(_currentCabinet);
        Hide();
    }
}