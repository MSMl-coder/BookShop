// BookDropZoneUI.cs
// Розміщується на GameObject BookDropZone всередині ContentWant панелі хмаринки NPC.
//
// Взаємодіє з системою drag-and-drop:
//   - При drop книги на цю зону → викликає NPCBrain.ReceiveBookOffer()
//   - Підсвічується коли гравець тягне книгу поблизу (викликається з BookDragHandler)
//
// SETUP:
//   1. Add Component → BookDropZoneUI на GameObject BookDropZone
//   2. Add Component → Image (щоб UnityEngine.EventSystems бачив його як UI елемент)
//   3. BookDragHandler (на книгах) при drop перевіряє всі BookDropZoneUI через FindObjectsByType

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
public class BookDropZoneUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor   = new Color(0.80f, 0.76f, 0.70f, 0.30f);
    [SerializeField] private Color hoverColor    = new Color(0.10f, 0.37f, 0.71f, 0.25f);
    [SerializeField] private Color dropReadyColor= new Color(0.10f, 0.37f, 0.71f, 0.50f);

    private Image    _image;
    private NPCBrain _npcBrain;

    private void Awake()
    {
        _image    = GetComponent<Image>();
        _npcBrain = GetComponentInParent<NPCBrain>();

        if (_image != null) _image.color = normalColor;
    }

    // ── Unity EventSystem callbacks ──────────────────────────────

    // Викликається коли гравець відпускає dragged об'єкт на цій зоні
    public void OnDrop(PointerEventData eventData)
    {
        if (_image != null) _image.color = normalColor;

        // Перевіряємо чи dragged об'єкт є книгою
        var dragged = eventData.pointerDrag;
        if (dragged == null) return;

        // Шукаємо BookTemplate на dragged об'єкті або його батьківському GO
        var bookWorldItem = dragged.GetComponent<BookWorldItem>()
                         ?? dragged.GetComponentInParent<BookWorldItem>();

        if (bookWorldItem?.instance == null)
        {
            Debug.LogWarning("[DropZone] OnDrop: dragged об'єкт не є BookWorldItem.");
            return;
        }

        BookTemplate template = BookDatabase.Instance?.GetBook(bookWorldItem.instance.templateID);
        if (template == null)
        {
            Debug.LogWarning("[DropZone] OnDrop: шаблон книги не знайдено.");
            return;
        }

        Debug.Log($"[DropZone] Book dropped: {template.title} → NPC {_npcBrain?.Data?.npcName}");
        _npcBrain?.ReceiveBookOffer(template);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_image != null) _image.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_image != null) _image.color = normalColor;
    }

    // ── Public API для BookDragHandler ───────────────────────────

    /// Викликається BookDragHandler при наближенні книги до зони.
    public void SetDragOver(bool isDragOver)
    {
        if (_image == null) return;
        _image.color = isDragOver ? dropReadyColor : normalColor;
    }

    /// Перевірка чи NPC в стані WaitingForPlayer (зона активна).
    public bool IsActive => _npcBrain != null
                         && _npcBrain.CurrentState == NPCState.WaitingForPlayer;

    /// Повертає NPCBrain цієї drop-zone (для BookDragHandler).
    public NPCBrain Brain => _npcBrain;
}