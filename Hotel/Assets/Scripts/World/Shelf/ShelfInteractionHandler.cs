// Assets/Scripts/World/Shelf/ShelfInteractionHandler.cs
// НОВИЙ ФАЙЛ: центральна точка взаємодії гравця з книгами на полицях.
// Замінює пряму обробку кліків по BookWorldItem.
//
// Принцип роботи:
//   Hover: Raycast по BoxCollider полиці → математичний hit-test → показати UI tooltip
//   Клік:  той самий hit-test → Shelf.MaterializeBookForInteraction() → контекст-меню
//   Pickup: DematerializeBook() + TakeBookAt() → книга в інвентар
//
// UNITY SETUP:
//   1. Додати компонент на той самий GO що і Camera або GameManager.
//   2. Призначити mainCamera та shelfLayer у Inspector.
//   3. shelfLayer — Layer "Shelves" (BoxCollider на Shelf, НЕ на книгах).
//   4. Призначити посилання на prefabLibrary (щоб знати який prefab для якої книги).
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[AddComponentMenu("Bookstore/Shelf Interaction Handler")]
public class ShelfInteractionHandler : MonoBehaviour
{
    public static ShelfInteractionHandler Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask shelfLayer;

    [Header("Interaction")]
    [Tooltip("Максимальна дистанція взаємодії")]
    [SerializeField] private float interactDistance = 20f;

    [Tooltip("Час утримання ЛКМ для pickup (WorkDay фаза)")]
    [SerializeField] private float holdTimeToPickUp = 0.5f;

    // ── Private state ─────────────────────────────────────────────────────────
    // Hover state
    private Shelf _hoveredShelf;
    private int   _hoveredBookIndex = -1;

    // Click / Hold state
    private bool          _isPressing       = false;
    private float         _pressStartTime   = 0f;
    private Shelf         _pressShelf;
    private int           _pressBookIndex   = -1;
    private BookWorldItem _activeMaterialized;

    // Prefab lookup: templateID → prefab
    // Заповнюється через RegisterPrefab або автоматично з BookDatabase
    private readonly Dictionary<string, GameObject> _prefabCache = new();

    // ── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        HandleHover();

        if (Mouse.current.leftButton.wasPressedThisFrame)
            HandlePressStart();

        if (_isPressing && Mouse.current.leftButton.isPressed)
            HandleHoldPickup();

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            HandlePressRelease();
    }

    // ── Hover ─────────────────────────────────────────────────────────────────

    private void HandleHover()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, shelfLayer))
        {
            ClearHover();
            return;
        }

        Shelf shelf = hit.collider.GetComponentInParent<Shelf>();
        if (shelf == null) { ClearHover(); return; }

        int idx = shelf.GetBookIndexAtPoint(hit.point);

        if (shelf != _hoveredShelf || idx != _hoveredBookIndex)
        {
            _hoveredShelf     = shelf;
            _hoveredBookIndex = idx;

            if (idx >= 0)
            {
                var data     = shelf.GetBookData(idx);
                var template = BookDatabase.Instance?.GetBook(data.templateID);
                if (template != null)
                    BookInfoCardController.Instance?.Show(template);
            }
            else
            {
                BookInfoCardController.Instance?.Hide();
            }
        }
    }

    private void ClearHover()
    {
        if (_hoveredShelf != null || _hoveredBookIndex >= 0)
        {
            _hoveredShelf     = null;
            _hoveredBookIndex = -1;
            BookInfoCardController.Instance?.Hide();
        }
    }

    // ── Click / Hold ──────────────────────────────────────────────────────────

    private void HandlePressStart()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, shelfLayer)) return;

        Shelf shelf = hit.collider.GetComponentInParent<Shelf>();
        if (shelf == null) return;

        int idx = shelf.GetBookIndexAtPoint(hit.point);
        if (idx < 0) return;

        _pressShelf     = shelf;
        _pressBookIndex = idx;
        _pressStartTime = Time.time;
        _isPressing     = true;

        // Матеріалізуємо книгу для взаємодії
        var data   = shelf.GetBookData(idx);
        var prefab = GetPrefabForTemplate(data.templateID);
        if (prefab != null)
        {
            shelf.MaterializeBookForInteraction(idx, prefab);
            _activeMaterialized = shelf._materializedBookRef;
        }

        // Показати контекстне меню
        ShowContextMenu(shelf, idx);
    }

    private void HandleHoldPickup()
    {
        if (_pressShelf == null || _pressBookIndex < 0) return;
        if (Time.time - _pressStartTime < holdTimeToPickUp) return;

        // Hold завершився — pickup книги
        PickUpBook(_pressShelf, _pressBookIndex);
        ResetPressState();
    }

    private void HandlePressRelease()
    {
        // Короткий клік — залишаємо матеріалізований GO для контекст-меню
        // (ContextMenu сам викличе DematerializeOrPickup)
        _isPressing = false;
    }

    // ── Pickup ────────────────────────────────────────────────────────────────

    /// Забирає книгу з полиці в інвентар. Публічний — може викликатись з ContextMenu.
    public void PickUpBook(Shelf shelf, int bookIndex)
    {
        if (shelf == null || bookIndex < 0) return;

        // Знищуємо матеріалізований GO
        if (_activeMaterialized != null)
            shelf.DematerializeBook(_activeMaterialized);
        _activeMaterialized = null;

        BookInstance instance = shelf.TakeBookAt(bookIndex);
        if (instance != null)
        {
            InventoryManager.Instance?.AddExistingBook(instance);
            Debug.Log($"[ShelfInteraction] Picked up: {instance.templateID}");
        }
    }

    // ── Context Menu ──────────────────────────────────────────────────────────

    private void ShowContextMenu(Shelf shelf, int bookIndex)
    {
        // TODO: відкрити контекстне меню (Забрати в інв. / Інформація)
        // ContextMenu.Instance?.ShowForShelfBook(shelf, bookIndex, this);
        Debug.Log($"[ShelfInteraction] Context menu for book[{bookIndex}] on {shelf.name}");
    }

    // ── Prefab Cache ──────────────────────────────────────────────────────────

    private GameObject GetPrefabForTemplate(string templateID)
    {
        if (_prefabCache.TryGetValue(templateID, out var cached))
            return cached;

        var template = BookDatabase.Instance?.GetBook(templateID);
        if (template?.containerPrefab != null)
        {
            _prefabCache[templateID] = template.containerPrefab;
            return template.containerPrefab;
        }

        Debug.LogWarning($"[ShelfInteraction] Prefab не знайдено для '{templateID}'");
        return null;
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private void ResetPressState()
    {
        _isPressing     = false;
        _pressShelf     = null;
        _pressBookIndex = -1;
    }
}