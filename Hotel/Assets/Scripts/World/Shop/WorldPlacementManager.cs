using UnityEngine;
using UnityEngine.InputSystem;

public class WorldPlacementManager : MonoBehaviour
{
    
    public LayerMask shelfLayer;
    public LayerMask placedBooksLayer;
    public float holdTimeToPickUp = 0.5f;
    public float lerpSpeed = 15f;

   // private BookData _currentBook;
    private GameObject _phantomVisual;
    private GameObject _currentPrefabCandidate;
    private Shelf[] _allShelves;

    private float _pressStartTime;
    private BookWorldItem _targetBookToPickUp;
    private bool _isPressing = false;
/*
    void Awake() => _allShelves = Object.FindObjectsByType<Shelf>(FindObjectsSortMode.None);
    void OnEnable() => BookEvents.OnBookSelectedForPlacement += EnterPlacementMode;
    void OnDisable() => BookEvents.OnBookSelectedForPlacement -= EnterPlacementMode;

    void Update()
    {
        if (_currentBook != null)
        {
            HandlePlacementLogic();
            if (Mouse.current.rightButton.wasPressedThisFrame) ExitPlacementMode(false);
            return;
        }
        HandleLongPressToPickUp();
    }

    private void HandlePlacementLogic()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 20f, shelfLayer))
        {
            Shelf shelf = hit.collider.GetComponentInParent<Shelf>();
            if (shelf != null && shelf.CanFitBook(_currentPrefabCandidate))
            {
                Vector3 targetPos = shelf.GetNextWorldPos(_currentPrefabCandidate);
                // Фантом має мати таке ж обертання, як і майбутня книга
                Quaternion targetRot = shelf.startPoint.rotation * Quaternion.Euler(0, 90, 0);

                _phantomVisual.transform.position = Vector3.Lerp(_phantomVisual.transform.position, targetPos, Time.deltaTime * lerpSpeed);
                _phantomVisual.transform.rotation = Quaternion.Lerp(_phantomVisual.transform.rotation, targetRot, Time.deltaTime * lerpSpeed);

                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    shelf.PlaceBook(_currentBook, _currentPrefabCandidate);
                    BookEvents.OnBookPlacedOnShelf?.Invoke(_currentBook);
                    ExitPlacementMode(true);
                }
                return;
            }
        }
        _phantomVisual.transform.position = Vector3.Lerp(_phantomVisual.transform.position, ray.GetPoint(2f), Time.deltaTime * lerpSpeed);
    }

    private void HandleLongPressToPickUp()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, placedBooksLayer))
            {
                _targetBookToPickUp = hit.collider.GetComponentInParent<BookWorldItem>();
                if (_targetBookToPickUp != null) { _pressStartTime = Time.time; _isPressing = true; }
            }
        }

        if (_isPressing && Mouse.current.leftButton.isPressed)
        {
            if (Time.time - _pressStartTime >= holdTimeToPickUp && _targetBookToPickUp != null)
            {
                BookData data = _targetBookToPickUp.parentShelf.TakeBook(_targetBookToPickUp.gameObject);
                if (data != null) 
                {
                    BookEvents.OnBookPickedUpFromShelf?.Invoke(data);
                    EnterPlacementMode(data);
                }
                ResetPressState();
            }
        }
        if (Mouse.current.leftButton.wasReleasedThisFrame) ResetPressState();
    }

    private void EnterPlacementMode(BookData book)
    {
        _currentBook = book;
        _currentPrefabCandidate = visualLibrary.GetRandomPrefab(book.rarity, book.heightSize, book.thicknessSize);
        if (_currentPrefabCandidate == null) return;

        _phantomVisual = Instantiate(_currentPrefabCandidate);
        foreach (var c in _phantomVisual.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var s in _allShelves) s.SetIndicatorActive(true);
    }

    private void ExitPlacementMode(bool success)
    {
        if (_phantomVisual != null) Destroy(_phantomVisual);
        foreach (var s in _allShelves) s.SetIndicatorActive(false);
        if (!success && _currentBook != null) BookEvents.OnPlacementCanceled?.Invoke();
        _currentBook = null;
    }

    private void ResetPressState() { _isPressing = false; _targetBookToPickUp = null; }
    */
}