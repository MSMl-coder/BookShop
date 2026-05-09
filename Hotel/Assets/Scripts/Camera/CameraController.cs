// Assets/Scripts/Camera/CameraController.cs
// ═══════════════════════════════════════════════════════════════════════════
//  SimsCamera — The Sims 4 feel for Unity 6 (URP, Orthographic)
//  -----------------------------------------------------------------------
//  1. ROTATION — вільне перетягування ПКМ (як у Sims 4, не snap)
//     Q / E — дискретні кроки 45° як альтернатива
//
//  2. ZOOM — ортографічний, плавний, з автоматичним tilt X:
//     zoom IN  (наближення) → tilt 25° (майже зверху)
//     zoom OUT (віддалення) → tilt 50° (ближче до горизонту)
//     Колесо миші, + / - або PgUp / PgDn
//
//  3. PAN — WASD / стрілки / ПКМ drag (Sims 4 стиль)
//     Швидкість масштабується з zoom (далі = швидше)
//     Плавний SmoothDamp easing
//
//  4. EDGE SCROLLING — курсор на межі екрану → камера рухається
//
//  5. F / Home — повертає ТІЛЬКИ позицію до стартової точки
//     Кут камери та zoom НЕ скидаються
//
//  6. Tab — цикл по цілях (через статичну подію OnCycleTarget)
//
//  7. BOUNDS — опціональне обмеження руху (розмір рівня)
// ═══════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════
    #region Inspector
    // ════════════════════════════════════════════════════════════════

    [Header("━━ References ━━")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask floorLayer;
    [SerializeField] private Transform defaultTarget;   // Optional: стартова точка

    [Header("━━ Zoom (Orthographic) ━━")]
    [SerializeField] private float zoomMin       = 3f;
    [SerializeField] private float zoomMax       = 14f;
    [SerializeField] private float zoomDefault   = 7f;
    [SerializeField] private float zoomStep      = 1.5f;    // крок колесом
    [SerializeField] private float zoomSmooth    = 8f;      // lerp speed

    [Header("━━ Tilt (Auto by Zoom) ━━")]
    [Tooltip("Кут X при мінімальному zoom (zoom IN, близько) — майже зверху")]
    [SerializeField] private float tiltClose     = 25f;
    [Tooltip("Кут X при максимальному zoom (zoom OUT, далеко) — ближче до горизонту")]
    [SerializeField] private float tiltFar       = 50f;

    [Header("━━ Rotation (Free RMB + Q/E snap) ━━")]
    [Tooltip("Швидкість вільного повороту через ПКМ drag (градусів/піксель)")]
    [SerializeField] private float rotationSpeed = 0.3f;
    [Tooltip("Дискретний крок Q/E (градусів)")]
    [SerializeField] private float rotationSnap  = 45f;
    [SerializeField] private float rotationSmooth= 10f;

    [Header("━━ Pan (WASD / MMB Drag) ━━")]
    [SerializeField] private float panSpeedBase  = 10f;
    [SerializeField] private float panSmooth     = 12f;
    [SerializeField] private float panZoomScale  = 0.7f;   // множник швидкості залежно від zoom
    [Tooltip("Висота target над підлогою")]
    [SerializeField] private float targetHeightOffset = 0.5f;

    [Header("━━ Edge Scrolling ━━")]
    [SerializeField] private bool  edgeScrollEnabled  = true;
    [SerializeField] private float edgeScrollMargin   = 20f;  // пікселів від краю
    [SerializeField] private float edgeScrollSpeed    = 8f;

    [Header("━━ Bounds ━━")]
    [SerializeField] private bool  useBounds    = false;
    [SerializeField] private Rect  worldBounds  = new Rect(-50, -50, 100, 100); // xz площина

    [Header("━━ Camera Distance ━━")]
    [SerializeField] private float cameraDistance = 30f;

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Events (для зовнішніх систем)
    // ════════════════════════════════════════════════════════════════

    public static event Action OnCycleTarget;   // Tab → наступний персонаж

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Private State
    // ════════════════════════════════════════════════════════════════

    // Target (точка фокусу — камера дивиться на неї)
    private Transform _target;
    private Vector3   _targetPos;       // бажана позиція target
    private Vector3   _targetVel;       // для SmoothDamp

    // Zoom
    private float _zoomCurrent;
    private float _zoomTarget;

    // Rotation
    private float _rotYCurrent;         // фактичний кут Y (lerp-ається)
    private float _rotYTarget;          // бажаний кут Y (від drag або snap)

    // ПКМ drag rotate
    private bool _isDraggingRotate;

    // Флаги
    private bool _isRotating;

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Unity Lifecycle
    // ════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (mainCamera == null) mainCamera = GetComponent<Camera>();
    }

    private void Start()
    {
        // Ініціалізація target
        if (_target == null)
        {
            GameObject go = new GameObject("[CameraTarget]");
            go.hideFlags = HideFlags.HideInHierarchy;
            _target = go.transform;
        }

        // Стартова позиція
        Vector3 startPos = defaultTarget != null ? defaultTarget.position : Vector3.zero;
        _targetPos    = startPos;
        _target.position = startPos;

        // Zoom default
        _zoomCurrent = zoomDefault;
        _zoomTarget  = zoomDefault;
        mainCamera.orthographicSize = _zoomCurrent;

        // Rotation: ініціалізуємо з поточного eulerAngles
        _rotYCurrent = transform.eulerAngles.y;
        _rotYTarget  = _rotYCurrent;

        ApplyTransform();

        // Прив'язка до підлоги
        SnapToFloor(ref _targetPos);
    }

    private void LateUpdate()
    {
        HandleZoomInput();
        HandleRotationInput();
        HandlePanInput();
        HandleEdgeScroll();
        HandleHotkeys();

        SmoothApply();
        ApplyTransform();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Zoom
    // ════════════════════════════════════════════════════════════════

    private void HandleZoomInput()
    {
        var mouse    = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null && keyboard == null) return;

        // Блокуємо якщо курсор над UI
        if (IsPointerOverUI()) return;

        float delta = 0f;

        // Колесо миші
        if (mouse != null)
        {
            float scroll = mouse.scroll.y.ReadValue();
            if (Mathf.Abs(scroll) > 0.1f)
                delta -= Mathf.Sign(scroll) * zoomStep;
        }

        // Клавіатура: +/- або PgUp/PgDn
        if (keyboard != null)
        {
            if (keyboard.pageUpKey.wasPressedThisFrame   || keyboard.equalsKey.wasPressedThisFrame)
                delta -= zoomStep;
            if (keyboard.pageDownKey.wasPressedThisFrame || keyboard.minusKey.wasPressedThisFrame)
                delta += zoomStep;
        }

        if (Mathf.Abs(delta) > 0.001f)
            _zoomTarget = Mathf.Clamp(_zoomTarget + delta, zoomMin, zoomMax);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Rotation (Sims 4 snap-style)
    // ════════════════════════════════════════════════════════════════

    private void HandleRotationInput()
    {
        var keyboard = Keyboard.current;
        var mouse    = Mouse.current;

        // ── Q / E — дискретні кроки 45° ─────────────────────────
        if (keyboard != null)
        {
            if (keyboard.qKey.wasPressedThisFrame)  _rotYTarget -= rotationSnap;
            if (keyboard.eKey.wasPressedThisFrame)  _rotYTarget += rotationSnap;
        }

        // ── ПКМ — вільний безперервний поворот (як у Sims 4) ────
        if (mouse == null) return;

        if (mouse.rightButton.wasPressedThisFrame)
            _isDraggingRotate = true;

        if (_isDraggingRotate && mouse.rightButton.isPressed)
        {
            float dx = mouse.delta.x.ReadValue();
            _rotYTarget += dx * rotationSpeed;
        }

        if (mouse.rightButton.wasReleasedThisFrame)
            _isDraggingRotate = false;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Pan (WASD + MMB-free-drag)
    // ════════════════════════════════════════════════════════════════

    private void HandlePanInput()
    {
        Vector2 input = Vector2.zero;

        // ── WASD ────────────────────────────────────────────────
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            input.x += (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            input.y += (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
        }

        // ── Стрілки ─────────────────────────────────────────────
        if (keyboard != null)
        {
            input.x += (keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed  ? 1 : 0);
            input.y += (keyboard.upArrowKey.isPressed    ? 1 : 0) - (keyboard.downArrowKey.isPressed  ? 1 : 0);
        }

        // ── Pan вектор (враховує Y-поворот камери) ─────────────
        if (input.sqrMagnitude > 0.01f)
        {
            float panSpeed = PanSpeed();
            Vector3 forward = FlatForward();
            Vector3 right   = FlatRight();

            _targetPos += (forward * input.y + right * input.x).normalized
                          * (panSpeed * Time.deltaTime);

            ClampToBounds();
            SnapToFloor(ref _targetPos);
        }
    }

    private void HandleEdgeScroll()
    {
        if (!edgeScrollEnabled) return;
        if (IsPointerOverUI()) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mp    = mouse.position.ReadValue();
        float   sw    = Screen.width;
        float   sh    = Screen.height;
        Vector2 edge  = Vector2.zero;
        float   m     = edgeScrollMargin;

        if (mp.x < m)       edge.x = -1;
        if (mp.x > sw - m)  edge.x = +1;
        if (mp.y < m)       edge.y = -1;
        if (mp.y > sh - m)  edge.y = +1;

        if (edge.sqrMagnitude > 0.01f)
        {
            Vector3 forward = FlatForward();
            Vector3 right   = FlatRight();
            float   speed   = edgeScrollSpeed * (1f + (_zoomCurrent / zoomMax));

            _targetPos += (forward * edge.y + right * edge.x) * (speed * Time.deltaTime);
            ClampToBounds();
            SnapToFloor(ref _targetPos);
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Hotkeys
    // ════════════════════════════════════════════════════════════════

    private void HandleHotkeys()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Home / F — повернутись до початку
        if (keyboard.homeKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
            ResetCamera();

        // Tab — цикл по цілях (персонажах)
        if (keyboard.tabKey.wasPressedThisFrame)
            OnCycleTarget?.Invoke();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Smooth Apply
    // ════════════════════════════════════════════════════════════════

    private void SmoothApply()
    {
        float dt = Time.deltaTime;

        // Zoom
        _zoomCurrent = Mathf.Lerp(_zoomCurrent, _zoomTarget, zoomSmooth * dt);
        mainCamera.orthographicSize = _zoomCurrent;

        // Rotation — shortest-path lerp
        float diff = Mathf.DeltaAngle(_rotYCurrent, _rotYTarget);
        _rotYCurrent += diff * (rotationSmooth * dt);
        _rotYCurrent  = (_rotYCurrent % 360f + 360f) % 360f;

        // Target position — smooth damp для відчуття "ваги"
        _target.position = Vector3.SmoothDamp(
            _target.position, _targetPos, ref _targetVel,
            1f / panSmooth, float.MaxValue, dt);
    }

    private void ApplyTransform()
    {
        float tiltX    = GetTiltX();
        Quaternion rot = Quaternion.Euler(tiltX, _rotYCurrent, 0f);
        Vector3   pos  = _target.position - rot * Vector3.forward * cameraDistance;

        transform.SetPositionAndRotation(pos, rot);
    }

    private float GetTiltX()
    {
        float t = Mathf.InverseLerp(zoomMin, zoomMax, _zoomCurrent);
        return Mathf.Lerp(tiltClose, tiltFar, t);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Helpers
    // ════════════════════════════════════════════════════════════════

    private float PanSpeed()
    {
        // Швидкість масштабується з zoom (більший zoom = швидший pan)
        float zoomT = Mathf.InverseLerp(zoomMin, zoomMax, _zoomCurrent);
        return panSpeedBase * (1f + zoomT * panZoomScale);
    }

    private Vector3 FlatForward()
    {
        Vector3 f = Quaternion.Euler(0, _rotYCurrent, 0) * Vector3.forward;
        f.y = 0;
        return f.normalized;
    }

    private Vector3 FlatRight()
    {
        Vector3 r = Quaternion.Euler(0, _rotYCurrent, 0) * Vector3.right;
        r.y = 0;
        return r.normalized;
    }

    private void SnapToFloor(ref Vector3 pos)
    {
        Ray ray = new Ray(pos + Vector3.up * 50f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, floorLayer))
            pos = hit.point + Vector3.up * targetHeightOffset;
    }

    private void ClampToBounds()
    {
        if (!useBounds) return;
        _targetPos.x = Mathf.Clamp(_targetPos.x, worldBounds.xMin, worldBounds.xMax);
        _targetPos.z = Mathf.Clamp(_targetPos.z, worldBounds.yMin, worldBounds.yMax);
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Public API
    // ════════════════════════════════════════════════════════════════

    /// Миттєво перемістити камеру до позиції (наприклад при виборі персонажа)
    public void FocusOn(Vector3 worldPosition, bool instant = false)
    {
        _targetPos = worldPosition;
        SnapToFloor(ref _targetPos);
        ClampToBounds();

        if (instant)
        {
            _target.position = _targetPos;
            _targetVel       = Vector3.zero;
        }
    }

    /// Скинути ТІЛЬКИ позицію камери. Кут та zoom не змінюються.
    public void ResetCamera()
    {
        Vector3 resetPos = defaultTarget != null ? defaultTarget.position : Vector3.zero;
        FocusOn(resetPos);
    }

    /// Поточний target (для WallOcclusionManager та інших систем)
    public Transform GetCameraTarget() => _target;

    /// Поточний zoom [0..1] нормалізований
    public float GetZoomNormalized() =>
        Mathf.InverseLerp(zoomMin, zoomMax, _zoomCurrent);

    #endregion
}