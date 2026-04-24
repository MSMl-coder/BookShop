using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform target;

    [Header("Movement (WASD)")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private LayerMask floorLayer; // Обов'язково виберіть шар підлоги!
    [SerializeField] private float targetHeightOffset = 1.0f;

    [Header("Zoom Settings (Orthographic)")]
    [SerializeField] private float minSize = 3f;
    [SerializeField] private float maxSize = 12f;
    [SerializeField] private float zoomSpeed = 6f;
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Rotation (Right Click)")]
    [SerializeField] private float rotationSpeed = 60f;

    private float _rotationY;
    private float _fixedTiltX;
    private float _targetSize;
    private float _currentSize;
    private float _zoomVelocity;
    private const float CAMERA_DISTANCE = 30f; // Фіксована дистанція відльоту камери

    void Start()
    {
        if (mainCamera == null) mainCamera = GetComponent<Camera>();
        
        // 1. Налаштовуємо початкові кути на основі того, як камера стоїть у Scene
        _fixedTiltX = transform.eulerAngles.x;
        _rotationY = transform.eulerAngles.y;
        
        // 2. Налаштовуємо початковий зум
        _currentSize = mainCamera.orthographicSize;
        _targetSize = _currentSize;

        // 3. Якщо ціль не призначена — створюємо її
        if (target == null)
        {
            GameObject go = new GameObject("CameraTarget_Auto");
            target = go.transform;
            target.position = Vector3.zero;
            Debug.Log("[Camera] Створено автоматичну точку цілі.");
        }

        // Прив'язка до підлоги на старті
        SnapTargetToFloor(target.position);
    }

    void LateUpdate()
    {
        if (target == null) return;

        HandleMovement();
        HandleRotation();
        HandleZoom();
        ApplyFinalTransform();
    }

    private void HandleMovement()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Зчитуємо WASD
        Vector2 input = new Vector2(
            (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
            (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)
        );

        if (input.sqrMagnitude > 0.01f)
        {
            // Рух відносно погляду камери
            Vector3 forward = transform.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0;
            right.Normalize();

            Vector3 moveDir = (forward * input.y + right * input.x).normalized;
            Vector3 potentialPosition = target.position + moveDir * moveSpeed * Time.deltaTime;

            // Перевірка, чи є під нами підлога (Floor Snapping)
            SnapTargetToFloor(potentialPosition);
        }
    }

    private void SnapTargetToFloor(Vector3 pos)
    {
        // Стріляємо зверху вниз
        Ray ray = new Ray(pos + Vector3.up * 50f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, floorLayer))
        {
            target.position = hit.point + Vector3.up * targetHeightOffset;
        }
        else
        {
            // Якщо підлоги немає — ми просто не змінюємо target.position (блокування руху)
            Debug.DrawRay(pos + Vector3.up * 5f, Vector3.down * 10f, Color.red);
        }
    }

    private void HandleRotation()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.rightButton.isPressed)
        {
            float deltaX = mouse.delta.x.ReadValue();
            _rotationY += deltaX * rotationSpeed * Time.deltaTime;
        }
    }

    private void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Блокуємо зум, якщо мишка над UI Toolkit або старим UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        float scroll = mouse.scroll.y.ReadValue();
        if (Mathf.Abs(scroll) > 0.1f)
        {
            _targetSize -= (scroll / 120f) * zoomSpeed;
            _targetSize = Mathf.Clamp(_targetSize, minSize, maxSize);
        }

        _currentSize = Mathf.SmoothDamp(_currentSize, _targetSize, ref _zoomVelocity, smoothTime);
        mainCamera.orthographicSize = _currentSize;
    }

    private void ApplyFinalTransform()
    {
        // Розраховуємо позицію камери як орбіту навколо Target
        Quaternion rotation = Quaternion.Euler(_fixedTiltX, _rotationY, 0);
        Vector3 position = target.position - (rotation * Vector3.forward * CAMERA_DISTANCE);

        transform.rotation = rotation;
        transform.position = position;
    }
}