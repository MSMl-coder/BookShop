// Assets/Scripts/World/EditMode/PlacementController.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlacementController : MonoBehaviour
{
    public static PlacementController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera            mainCamera;
    [SerializeField] private GridOverlay       grid;
    [SerializeField] private SnapSystem        snapSystem;
    [SerializeField] private PlacementFeedback feedback;

    [Header("Settings")]
    [SerializeField] private float rotateStep     = 45f;
    [SerializeField] private float ghostLerpSpeed = 20f;
    [SerializeField] private LayerMask interactionLayer;

    [Header("Highlight")]
    [SerializeField] private Color highlightColor  = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color invalidColor    = new Color(0.9f, 0.2f, 0.2f, 0.4f);
    [SerializeField] private Color validColor      = new Color(0.4f, 0.9f, 0.5f, 0.4f);

    private PlacementPhantom  _phantom;
    private FurnitureInstance _currentInstance;
    private FurnitureTemplate _currentTemplate;
    private bool              _isPlacing;
    private bool              _isEnabled;
    private bool              _freePlacement;
    private float             _currentYRotation;

    // Hover підсвічування
    private PlacedObject      _hoveredObject;
    private List<Material>    _hoveredOriginalMats = new();

    public bool IsPlacing              => _isPlacing;
    public bool JustConfirmedThisFrame { get; private set; }

    public bool FreePlacement
    {
        get => _freePlacement;
        set { _freePlacement = value; feedback?.ShowFreePlaceIndicator(value); }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        JustConfirmedThisFrame = false;
        if (!_isEnabled) return;

        var keyboard = Keyboard.current;
        var mouse    = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (_isPlacing && _currentTemplate != null)
        {
            if (keyboard.leftAltKey.wasPressedThisFrame)
                FreePlacement = !FreePlacement;

            UpdateGhostPosition();
            HandleRotation(keyboard, mouse);
            HandlePlacementInput(keyboard, mouse);
        }
        else
        {
            // Не розміщуємо — обробляємо hover і ПКМ для повернення
            UpdateHover(mouse);

            if (mouse.rightButton.wasPressedThisFrame && _hoveredObject != null)
                ReturnToInventory(_hoveredObject);
        }
    }

    public void EnablePlacement()
    {
        _isEnabled = true;
    }

    public void DisablePlacement()
    {
        _isEnabled = false;
        ClearHover();
    }

    // ─────────────────────────────────────────────
    #region Hover Highlight
    // ─────────────────────────────────────────────

    private void UpdateHover(Mouse mouse)
    {
        Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 30f, interactionLayer))
        {
            var placedObj = hit.collider.GetComponentInParent<PlacedObject>()
                         ?? hit.collider.transform.root.GetComponentInChildren<PlacedObject>();

            if (placedObj != _hoveredObject)
            {
                ClearHover();
                if (placedObj != null)
                {
                    _hoveredObject = placedObj;
                    ApplyHighlight(placedObj.gameObject, highlightColor);
                }
            }
        }
        else
        {
            ClearHover();
        }
    }

    private void ClearHover()
    {
        if (_hoveredObject == null) return;
        RestoreHighlight(_hoveredObject.gameObject);
        _hoveredObject = null;
        _hoveredOriginalMats.Clear();
    }

    private void ApplyHighlight(GameObject go, Color color)
    {
        _hoveredOriginalMats.Clear();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            foreach (var m in r.materials)
            {
                // Зберігаємо оригінальний колір
                _hoveredOriginalMats.Add(m);
                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", color);
                else if (m.HasProperty("_Color"))
                    m.SetColor("_Color", color);
            }
        }
    }

    private void RestoreHighlight(GameObject go)
    {
        // Найпростіший спосіб — просто скинути матеріали через MaterialPropertyBlock
        // Або якщо матеріали інстанцірувались — вони вже змінились
        // Тут реімпортуємо оригінальний матеріал з prefab
        if (go == null) return;
        var placedObj = go.GetComponent<PlacedObject>()
                     ?? go.GetComponentInParent<PlacedObject>();
        if (placedObj?.Instance == null) return;

        var template = InventoryManager.Instance?.GetTemplate(placedObj.Instance.templateID);
        if (template?.prefab == null) return;

        // Відновлюємо матеріали з оригінального prefab
        var originalRenderers = template.prefab.GetComponentsInChildren<Renderer>();
        var currentRenderers  = go.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < Mathf.Min(originalRenderers.Length, currentRenderers.Length); i++)
        {
            currentRenderers[i].sharedMaterials = originalRenderers[i].sharedMaterials;
        }
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Placement
    // ─────────────────────────────────────────────

    public void BeginPlacement(FurnitureTemplate template)
    {
        var instance = InventoryManager.Instance?.GetFirstUnplaced(template.furnitureID);
        if (instance == null)
        {
            Debug.LogWarning($"[Placement] Немає доступних екземплярів: {template.furnitureName}");
            return;
        }
        BeginPlacementInternal(template, instance, Vector3.zero, Quaternion.identity, fromInventory: true);
    }

    public void PickUpExisting(PlacedObject obj)
    {
        if (obj?.Instance == null) return;

        var savedPos = obj.transform.position;
        var savedRot = obj.transform.rotation;
        var instance = obj.Instance;
        var template = InventoryManager.Instance?.GetTemplate(instance.templateID);

        if (template == null)
        {
            Debug.LogWarning($"[Placement] Шаблон не знайдено: templateID={instance.templateID}");
            return;
        }

        // Забираємо книжки з шафи перед знищенням
        ReturnBooksFromCabinet(obj.gameObject);

        ClearHover();
        PlacementRegistry.Instance?.Unregister(obj.gameObject);
        Destroy(obj.gameObject);

        BeginPlacementInternal(template, instance, savedPos, savedRot, fromInventory: false);
    }

    /// Повернути предмет в інвентар (ПКМ в EditMode)
    public void ReturnToInventory(PlacedObject obj)
    {
        if (obj?.Instance == null) return;

        // Забираємо книжки
        ReturnBooksFromCabinet(obj.gameObject);

        // Знімаємо isPlaced
        obj.Instance.isPlaced = false;

        ClearHover();
        PlacementRegistry.Instance?.Unregister(obj.gameObject);
        Destroy(obj.gameObject);

        feedback?.PlaySound(PlacementFeedback.SoundType.Place);
        Debug.Log($"[Placement] Повернуто в інвентар: templateID={obj.Instance.templateID}");

        // Оновлюємо панель якщо відкрита
      //  DecorationPanelUI.Instance?.BuildGridPublic();
    }

    /// Витягує всі книги з Cabinet компонента і повертає в інвентар
    private void ReturnBooksFromCabinet(GameObject furnitureGo)
    {
        var cabinet = furnitureGo.GetComponentInChildren<Cabinet>();
        if (cabinet == null) return;

        int returned = 0;
        foreach (var shelf in cabinet.shelves)
        {
            if (shelf == null) continue;
            BookInstance book;
            while ((book = shelf.TakeLastBook()) != null)
            {
                InventoryManager.Instance?.AddExistingBook(book);
                returned++;
            }
        }

        if (returned > 0)
            Debug.Log($"[Placement] Повернуто {returned} книг з шафи в інвентар");
    }

    private void BeginPlacementInternal(
        FurnitureTemplate template,
        FurnitureInstance instance,
        Vector3 pos, Quaternion rot,
        bool fromInventory)
    {
        CancelPlacement();
        ClearHover();

        _currentTemplate  = template;
        _currentInstance  = instance;
        _currentYRotation = rot.eulerAngles.y;
        _isPlacing        = true;

        _phantom = new PlacementPhantom();
        _phantom.Spawn(template.prefab);

        if (!fromInventory)
            _phantom.Visual.transform.SetPositionAndRotation(pos, rot);

        SetGhostMaterial(valid: true);
        feedback?.PlaySound(PlacementFeedback.SoundType.PickUp);
    }

    #endregion

    // ─────────────────────────────────────────────
    #region Ghost Movement
    // ─────────────────────────────────────────────

    private void UpdateGhostPosition()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

        if (snapSystem != null && snapSystem.TryGetSnapPoint(ray, _currentTemplate, out SnapResult snap))
        {
            Vector3    targetPos = _freePlacement ? snap.position : grid.SnapToGrid(snap.position);
            Quaternion targetRot = snap.rotation * Quaternion.Euler(0, _currentYRotation, 0);
            _phantom.Update(targetPos, targetRot, ghostLerpSpeed);
            SetGhostMaterial(snap.isValid);
        }
    }

    private void HandleRotation(Keyboard keyboard, Mouse mouse)
    {
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _currentYRotation += Mathf.Sign(scroll) * rotateStep;
            feedback?.PlaySound(PlacementFeedback.SoundType.Rotate);
        }
        if (keyboard.eKey.wasPressedThisFrame) { _currentYRotation += rotateStep; feedback?.PlaySound(PlacementFeedback.SoundType.Rotate); }
        if (keyboard.qKey.wasPressedThisFrame) { _currentYRotation -= rotateStep; feedback?.PlaySound(PlacementFeedback.SoundType.Rotate); }
    }

    private void HandlePlacementInput(Keyboard keyboard, Mouse mouse)
    {
        if (mouse.leftButton.wasPressedThisFrame)  ConfirmPlacement();
        if (mouse.rightButton.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            CancelPlacement();
    }

    private void ConfirmPlacement()
    {
        if (_phantom?.Visual == null) return;

        var pos = _phantom.Visual.transform.position;
        var rot = _phantom.Visual.transform.rotation;
        _phantom.Clear();

        var placed    = Instantiate(_currentTemplate.prefab, pos, rot);
        var placedObj = placed.AddComponent<PlacedObject>();
        placedObj.Init(_currentInstance);

        if (placed.GetComponentInChildren<Collider>() == null)
            Debug.LogWarning($"[Placement] {_currentTemplate.furnitureName} не має Collider!");

        PlacementRegistry.Instance?.Register(placed, _currentInstance);

        feedback?.PlaySound(PlacementFeedback.SoundType.Place);
        feedback?.SpawnPlaceParticles(pos);

        JustConfirmedThisFrame = true;
        _isPlacing             = false;
        _phantom               = null;
        _currentTemplate       = null;
        _currentInstance       = null;
    }

    public void CancelPlacement()
    {
        if (_phantom != null)
        {
            _phantom.Clear();
            Debug.Log($"[Placement] Скасовано — {_currentTemplate?.furnitureName} повернуто в інвентар");
        }
        _isPlacing       = false;
        _phantom         = null;
        _currentTemplate = null;
        _currentInstance = null;
    }

    private void SetGhostMaterial(bool valid)
    {
        if (_phantom?.Visual == null) return;
        var color = valid ? validColor : invalidColor;
        foreach (var r in _phantom.Visual.GetComponentsInChildren<Renderer>())
        {
            var mats = r.materials;
            foreach (var m in mats)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            }
            r.materials = mats;
        }
    }

    #endregion
}