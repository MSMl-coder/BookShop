// Assets/Scripts/UI/ContextMenu/ContextMenuUI.cs
// ФАЗА 1 — контекстне меню (ЛКМ на об'єкті → набір кнопок від стану)
//
// Матриця доступності (з ТЗ):
//
// ── На Cabinet (шафа/меблі) ──────────────────────────
//   Кнопка          Prep  Edit  Work  Loot
//   Інформація       так   так   так   ні
//   Перемістити      ні    так   ні    ні
//   Обернути         ні    так   ні    ні
//   Змінити колір    ні    так   ні    ні
//   Продати          ні    так   ні    ні
//
// ── На BookWorldItem (книга на полиці) ───────────────
//   Забрати в інв.   так   ні    так*  ні    (* якщо НЕ зарезервована)
//   Інформація       так   ні    так   ні
//
// ── На NPC ───────────────────────────────────────────
//   Запропонувати книгу  так (якщо NPC у стані Browsing)
//
// UNITY SETUP:
// 1. Створи GameObject "ContextMenuUI" у сцені.
// 2. Додай UIDocument з UXML що має елемент id="ContextMenu" (панель)
//    та id="ContextMenuContainer" (контейнер для кнопок).
// 3. АБО використовуй динамічний режим (без UXML) — меню будується з коду.
// 4. Признач цей компонент та панель у Inspector.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ContextMenuUI : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────
    public static ContextMenuUI Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────
    [Header("UI Document (опційно якщо використовуєш UIToolkit)")]
    [SerializeField] private UIDocument uiDocument;

    [Header("World-space offset від точки кліку")]
    [SerializeField] private Vector2 screenOffset = new Vector2(10f, -10f);

    // ── Private ────────────────────────────────────────────────
    private VisualElement _root;
    private VisualElement _panel;       // #ContextMenu
    private VisualElement _container;   // #ContextMenuContainer

    private bool _isVisible;

    // ── Unity ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (uiDocument == null) return;

        _root      = uiDocument.rootVisualElement;
        _panel     = _root.Q<VisualElement>("ContextMenu");
        _container = _root.Q<VisualElement>("ContextMenuContainer");

        if (_panel == null)
            Debug.LogError("[ContextMenuUI] Елемент 'ContextMenu' не знайдено у UXML!");

        Hide();
    }

    // ── Public API ─────────────────────────────────────────────

    public void Hide()
    {
        _isVisible = false;
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    /// Контекстне меню для книги на полиці
    public void ShowForBook(BookWorldItem bookItem, Vector3 worldPos, GameState state)
    {
        var buttons = new List<ContextButton>();

        bool isReserved = bookItem.IsReserved;

        // Інформація — Prep та Work
        if (state == GameState.Preparation || state == GameState.WorkDay)
        {
            buttons.Add(new ContextButton("📖 Інформація", () =>
            {
                Debug.Log($"[ContextMenu] Інфо: {bookItem.instance?.templateID}");
                // TODO: відкрити BookInfoPanel
                Hide();
            }));
        }

        // Забрати в інвентар — Prep та Work (якщо не зарезервована)
        if ((state == GameState.Preparation || state == GameState.WorkDay) && !isReserved)
        {
            buttons.Add(new ContextButton("🎒 Забрати в інвентар", () =>
            {
                PickupBookToInventory(bookItem);
                Hide();
            }));
        }
        else if (state == GameState.WorkDay && isReserved)
        {
            // Показуємо disabled кнопку з причиною
            buttons.Add(new ContextButton("🔒 Зарезервована NPC", null, disabled: true));
        }

        Show(worldPos, buttons);
    }

    /// Контекстне меню для шафи/меблів
    public void ShowForCabinet(Cabinet cabinet, Vector3 worldPos, GameState state)
    {
        var buttons = new List<ContextButton>();

        // Інформація — Prep, Edit, Work
        if (state != GameState.LootPhase)
        {
            buttons.Add(new ContextButton("ℹ️ Інформація", () =>
            {
                Debug.Log($"[ContextMenu] Інфо шафи: {cabinet.cabinetName}");
                Hide();
            }));
        }

        // Кнопки EditMode
        if (state == GameState.EditMode)
        {
            buttons.Add(new ContextButton("↔️ Перемістити", () =>
            {
                Debug.Log($"[ContextMenu] Перемістити: {cabinet.cabinetName}");
                // TODO: активувати гізмо переміщення
                Hide();
            }));

            buttons.Add(new ContextButton("🔄 Обернути", () =>
            {
                Debug.Log($"[ContextMenu] Обернути: {cabinet.cabinetName}");
                // TODO: активувати гізмо обертання
                Hide();
            }));

            buttons.Add(new ContextButton("🎨 Змінити колір", () =>
            {
                Debug.Log($"[ContextMenu] Змінити колір: {cabinet.cabinetName}");
                // TODO: відкрити ColorPicker
                Hide();
            }));

            buttons.Add(new ContextButton("💰 Продати", () =>
            {
                SellFurniture(cabinet);
                Hide();
            }));
        }

        // Відкрити інвентар шафи — Prep та Work
        if (state == GameState.Preparation || state == GameState.WorkDay)
        {
            buttons.Add(new ContextButton("📦 Відкрити шафу", () =>
            {
                ShopUIManager.Instance?.OpenCabinetUI(cabinet);
                Hide();
            }));
        }

        Show(worldPos, buttons);
    }

    /// Контекстне меню для NPC (тільки WorkDay)
    public void ShowForNPC(CustomerBrain npc, Vector3 worldPos, GameState state)
    {
        if (state != GameState.WorkDay) return;

        var buttons = new List<ContextButton>();

        buttons.Add(new ContextButton("📚 Запропонувати книгу", () =>
        {
            Debug.Log($"[ContextMenu] Пропонуємо книгу NPC: {npc.name}");
            // TODO: відкрити інвентар з можливістю вибору книги для пропозиції
            // BookOfferUI.Instance?.OpenFor(npc);
            Hide();
        }));

        Show(worldPos, buttons);
    }

    // ── Private ────────────────────────────────────────────────

    private void Show(Vector3 worldPos, List<ContextButton> buttons)
    {
        if (buttons.Count == 0) { Hide(); return; }

        _isVisible = true;

        if (_panel == null || _container == null)
        {
            // Fallback: логування якщо UI не налаштований
            Debug.Log("[ContextMenuUI] UI не готовий, виводимо кнопки в лог:");
            foreach (var b in buttons) Debug.Log($"  - {b.Label}");
            return;
        }

        // Очищаємо попередні кнопки
        _container.Clear();

        // Будуємо кнопки
        foreach (var btn in buttons)
        {
            var b = new Button();
            b.text = btn.Label;
            b.AddToClassList("context-menu__btn");

            if (btn.IsDisabled)
            {
                b.SetEnabled(false);
                b.AddToClassList("context-menu__btn--disabled");
            }
            else
            {
                var captured = btn;
                b.clicked += () => captured.Action?.Invoke();
            }

            _container.Add(b);
        }

        // Позиція на екрані (конвертуємо з world-space)
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        screenPos.y = Screen.height - screenPos.y; // Інверсія Y для UIToolkit

        _panel.style.left = screenPos.x + screenOffset.x;
        _panel.style.top  = screenPos.y + screenOffset.y;
        _panel.style.display = DisplayStyle.Flex;
    }

    // ── Дії ────────────────────────────────────────────────────

    private void PickupBookToInventory(BookWorldItem bookItem)
    {
        if (bookItem?.instance == null) return;

        Shelf shelf = bookItem.parentShelf;
        if (shelf == null) return;

        // Беремо книгу з полиці
        BookInstance instance = bookItem.instance;
        shelf.RemoveBook(bookItem.gameObject);
        InventoryManager.Instance?.AddExistingBook(instance);

        Debug.Log($"[ContextMenu] Книгу '{instance.templateID}' забрано в інвентар.");
    }

    private void SellFurniture(Cabinet cabinet)
    {
        // TODO: логіка продажу меблів через EconomyManager
        Debug.Log($"[ContextMenu] Продаємо меблі: {cabinet.cabinetName}");
        // EconomyManager.Instance?.AddMoney(sellPrice);
        // Destroy(cabinet.gameObject);
    }

    // ── Внутрішній клас ────────────────────────────────────────

    private class ContextButton
    {
        public string        Label      { get; }
        public System.Action Action     { get; }
        public bool          IsDisabled { get; }

        public ContextButton(string label, System.Action action, bool disabled = false)
        {
            Label      = label;
            Action     = action;
            IsDisabled = disabled;
        }
    }
}