// Assets/Scripts/World/EditMode/PlacementController.cs
using UnityEngine;
using UnityEngine.InputSystem; // ← єдиний namespace для input

public class PlacementController : MonoBehaviour
{
    public static PlacementController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GridOverlay grid;
    [SerializeField] private SnapSystem snapSystem;
    [SerializeField] private PlacementFeedback feedback;

    [Header("Settings")]
    [SerializeField] private float rotateStep = 45f;
    [SerializeField] private float ghostLerpSpeed = 20f;

    private PlacementPhantom _phantom;
    private FurnitureTemplate _currentTemplate;
    private bool _isPlacing;
    private bool _isEnabled;
    private bool _freePlacement;
    private float _currentYRotation;

    public bool IsPlacing => _isPlacing;
    // Прапор що цього кадру щойно підтвердили розміщення
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
        JustConfirmedThisFrame = false; // скидаємо кожен кадр
 
        if (!_isEnabled || !_isPlacing || _currentTemplate == null) return;

        var keyboard = Keyboard.current;
        var mouse    = Mouse.current;
        if (keyboard == null || mouse == null) return;

        // Toggle FreePlacement — Alt
        if (keyboard.leftAltKey.wasPressedThisFrame)
            FreePlacement = !FreePlacement;

        UpdateGhostPosition();
        HandleRotation(keyboard, mouse);
        HandlePlacementInput(keyboard, mouse);
    }

    public void EnablePlacement()  => _isEnabled = true;
    public void DisablePlacement() => _isEnabled = false;

    public void BeginPlacement(FurnitureTemplate template)
    {
        CancelPlacement();
        _currentTemplate = template;
        _currentYRotation = 0f;
        _isPlacing = true;

        _phantom = new PlacementPhantom();
        _phantom.Spawn(template.prefab);
        SetGhostMaterial(valid: true);

        feedback?.PlaySound(PlacementFeedback.SoundType.PickUp);
    }

    private void UpdateGhostPosition()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

        if (snapSystem != null && snapSystem.TryGetSnapPoint(ray, _currentTemplate, out SnapResult snap))
        {
            Vector3 targetPos = _freePlacement
                ? snap.position
                : grid.SnapToGrid(snap.position);

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

        if (keyboard.eKey.wasPressedThisFrame)
        {
            _currentYRotation += rotateStep;
            feedback?.PlaySound(PlacementFeedback.SoundType.Rotate);
        }

        if (keyboard.qKey.wasPressedThisFrame)
        {
            _currentYRotation -= rotateStep;
            feedback?.PlaySound(PlacementFeedback.SoundType.Rotate);
        }
    }

    private void HandlePlacementInput(Keyboard keyboard, Mouse mouse)
    {
        if (mouse.leftButton.wasPressedThisFrame)
            ConfirmPlacement();

        if (mouse.rightButton.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            CancelPlacement();
    }

    private void ConfirmPlacement()
    {
        if (_phantom?.Visual == null) return;

        var pos = _phantom.Visual.transform.position;
        var rot = _phantom.Visual.transform.rotation;
        _phantom.Clear();

        var placed = Instantiate(_currentTemplate.prefab, pos, rot);

        // Додаємо мітку — шукаємо collider щоб переконатись що він є
        var placedObj = placed.AddComponent<PlacedObject>();
        placedObj.sourceTemplate = _currentTemplate;
        placedObj.originalPrefab = _currentTemplate.prefab;

        if (placed.GetComponentInChildren<Collider>() == null)
            Debug.LogWarning($"[Placement] Prefab {_currentTemplate.furnitureName} не має Collider — клік не буде працювати!");

        PlacementRegistry.Instance?.Register(placed, _currentTemplate);
        _currentTemplate.IsPlaced = true;

        feedback?.PlaySound(PlacementFeedback.SoundType.Place);
        feedback?.SpawnPlaceParticles(pos);

        _isPlacing = false;
        _phantom = null;
        _currentTemplate = null;
        JustConfirmedThisFrame = true; // ← встановити перед завершенням
    }

    public void CancelPlacement()
    {
        if (_phantom != null)
    {
        _phantom.Clear();
        
        // Повертаємо предмет — знімаємо IsPlaced
        if (_currentTemplate != null)
        {
            _currentTemplate.IsPlaced = false;
            Debug.Log($"[Placement] Скасовано — {_currentTemplate.furnitureName} повернуто в інвентар");
        }
    }

    _isPlacing = false;
    _phantom = null;
    _currentTemplate = null;
    }

    private void SetGhostMaterial(bool valid)
    {
        if (_phantom?.Visual == null) return;
        var color = valid
            ? new Color(0.4f, 0.9f, 0.5f, 0.4f)
            : new Color(0.9f, 0.2f, 0.2f, 0.4f);

        foreach (var r in _phantom.Visual.GetComponentsInChildren<Renderer>())
        {
            var mats = r.materials;
            foreach (var m in mats)
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            r.materials = mats;
        }
    }

   public void PickUpExisting(PlacedObject obj)
    {
        if (obj == null) return;

        // Зберігаємо трансформ ДО знищення
        Vector3 savedPos = obj.transform.position;
        Quaternion savedRot = obj.transform.rotation;
        FurnitureTemplate template = obj.sourceTemplate;

        PlacementRegistry.Instance?.Unregister(obj.gameObject);
        if (template != null) template.IsPlaced = false;
        Destroy(obj.gameObject);

        if (template == null) return;

        // Починаємо розміщення зі збереженою трансформацією
        BeginPlacementAtTransform(template, savedPos, savedRot);

    
    }

    public void BeginPlacementAtTransform(FurnitureTemplate template, Vector3 pos, Quaternion rot)
    {
        CancelPlacement();
        _currentTemplate = template;
        // Витягуємо кут Y з існуючого повороту
        _currentYRotation = rot.eulerAngles.y;
        _isPlacing = true;

        _phantom = new PlacementPhantom();
        _phantom.Spawn(template.prefab);
        // Одразу ставимо ghost на збережену позицію
        _phantom.Visual.transform.SetPositionAndRotation(pos, rot);
        SetGhostMaterial(valid: true);

        feedback?.PlaySound(PlacementFeedback.SoundType.PickUp);
    }
    
}