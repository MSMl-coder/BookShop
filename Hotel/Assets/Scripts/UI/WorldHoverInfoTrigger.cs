// Assets/Scripts/UI/WorldHoverInfoTrigger.cs
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class WorldHoverInfoTrigger : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float  maxDistance = 200f;

    private BookTemplate _lastShown;
    private bool         _isShowing;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        var state = EditModeManager.GetEffectiveState();
        if (state == GameState.LootPhase || state == GameState.DayStats)
        { HideCard(); return; }

        var mouse = Mouse.current;
        if (mouse == null) { HideCard(); return; }

        if (IsPointerOverUI()) { HideCard(); return; }

        if (mainCamera == null) { mainCamera = Camera.main; return; }

        // Той самий ray що InteractionRouter — працює і для ortho і perspective
        Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

        BookTemplate found = null;

        // RaycastAll без маски — знаходимо все, потім фільтруємо
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, ~0,
                                               QueryTriggerInteraction.Collide);

        // Сортуємо по відстані (як InteractionRouter)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Ghost BookWorldItem
            var wi = hit.collider.GetComponentInParent<BookWorldItem>();
            if (wi?.instance != null)
            {
                found = BookDatabase.Instance?.GetBook(wi.instance.templateID);
                break;
            }

            // Shelf — hit-test по X
            var shelf = hit.collider.GetComponentInParent<Shelf>();
            if (shelf != null)
            {
                int idx = shelf.GetBookIndexAtPoint(hit.point);
                if (idx >= 0)
                {
                    var entry = shelf.GetBookData(idx);
                    found = BookDatabase.Instance?.GetBook(entry.templateID);
                    if (found != null) break;
                }
            }
        }

        if (found != null)
        {
            if (found != _lastShown)
            {
                _lastShown = found;
                _isShowing = true;
                BookInfoCardController.Instance?.Show(found);
            }
        }
        else
        {
            HideCard();
        }
    }

    private void HideCard()
    {
        if (!_isShowing) return;
        _isShowing = false;
        _lastShown = null;
        BookInfoCardController.Instance?.Hide();
    }

 
 
    private static bool IsPointerOverUI()
        => UIPointerChecker.IsOverUI();
}   