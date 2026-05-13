// Assets/Scripts/World/Shelf/ShelfInteractionHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Bookstore/Shelf Interaction Handler")]
public class ShelfInteractionHandler : MonoBehaviour
{
    public static ShelfInteractionHandler Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask shelfLayer;

    [Header("Hover Settings")]
    [SerializeField] private float slideOutDistance = 0.06f;
    [SerializeField] private float slideSpeed       = 12f;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 25f;

    // ── Hover state ───────────────────────────────────────────────────────────
    private Shelf      _hoveredShelf;
    private int        _hoveredBookIndex = -1;
    private GameObject _slideGO;         // GO що зараз анімується
    private Vector3    _homePos;         // position на полиці
    private Vector3    _outPos;          // висунута position
    private float      _slideT    = 0f;
    private bool       _slidingOut = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        UpdateHover();
        UpdateSlide();
    }

    // ── Hover ─────────────────────────────────────────────────────────────────
    private void UpdateHover()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        bool hit = Physics.Raycast(ray, out RaycastHit rh, interactDistance, shelfLayer);
        Shelf shelf = hit ? rh.collider.GetComponentInParent<Shelf>() : null;
        int   idx   = shelf != null ? shelf.GetBookIndexAtPoint(rh.point) : -1;

        // Та сама книга — нічого не робимо
        if (shelf == _hoveredShelf && idx == _hoveredBookIndex) return;

        // Стара книга — slide back
        StartSlideBack();

        // Нова книга
        _hoveredShelf     = shelf;
        _hoveredBookIndex = idx;

        if (shelf == null || idx < 0)
        {
            BookInfoCardController.Instance?.Hide();
            return;
        }

        // Tooltip
        var data     = shelf.GetBookData(idx);
        var template = BookDatabase.Instance?.GetBook(data.templateID);
        if (template != null)
            BookInfoCardController.Instance?.Show(template);

        // Slide out
        GameObject go = shelf.GetBookGO(idx);
        if (go == null) return;

        _slideGO    = go;
        _homePos    = go.transform.position;
        _outPos     = _homePos - go.transform.forward * slideOutDistance;
        _slideT     = 0f;
        _slidingOut = true;
    }

    // ── Slide ─────────────────────────────────────────────────────────────────
    private void UpdateSlide()
    {
        if (_slideGO == null) return;

        float target = _slidingOut ? 1f : 0f;
        _slideT = Mathf.MoveTowards(_slideT, target, Time.deltaTime * slideSpeed);
        _slideGO.transform.position = Vector3.Lerp(_homePos, _outPos, _slideT);

        // Slide back завершено — скидаємо GO
        if (!_slidingOut && _slideT <= 0f)
        {
            _slideGO.transform.position = _homePos;
            _slideGO = null;
        }
    }

    private void StartSlideBack()
    {
        if (_slideGO == null) return;
        // GO що висувається — починаємо ховати назад
        // (не скидаємо _slideGO — UpdateSlide допрацює)
        _slidingOut = false;
    }

    // ── Public: клік (з InteractionRouter) ───────────────────────────────────
    public void HandleShelfClick(Shelf shelf, Vector3 hitPoint)
    {
        if (shelf == null) return;

        int idx = shelf.GetBookIndexAtPoint(hitPoint);
        if (idx < 0) return;

        var data     = shelf.GetBookData(idx);
        var template = BookDatabase.Instance?.GetBook(data.templateID);
        if (template?.containerPrefab == null) return;

        BookWorldItem worldItem = shelf.GetOrMaterializeBookForInteraction(idx, template.containerPrefab);
        if (worldItem == null) return;

        GameState state = EditModeManager.GetEffectiveState();
        ContextMenuUI.Instance?.ShowForBook(worldItem, hitPoint, state);
    }
}