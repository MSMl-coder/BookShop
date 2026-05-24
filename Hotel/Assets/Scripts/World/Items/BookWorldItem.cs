// Assets/Scripts/World/Items/BookWorldItem.cs
// ОНОВЛЕНО: додано властивості-делегати IsReserved, Reserve(), Unreserve()
// що перенаправляють виклики з NPCBrain/ContextMenuUI до Shelf._books (data layer).
//
// Стара поведінка збережена:
//   NPCBrain:     targetBook.Reserve(npcID)   ✓
//                 targetBook.Unreserve()       ✓
//                 targetBook.IsReserved        ✓
//   ContextMenu:  item.IsReserved              ✓
//                 shelf.RemoveBook(item)        → замінено на shelf.TakeBookAt(item.bookIndex)
using UnityEngine;

public class BookWorldItem : MonoBehaviour
{
    // ── Основні поля (без змін — сумісність із усім існуючим кодом) ──────────
    public BookInstance instance;
    public Shelf        parentShelf;

    [HideInInspector] public float savedTilt;

    // ── Нові поля (Ghost-on-Demand архітектура) ────────────────────────────
    /// Індекс у Shelf._books — потрібен для TakeBookAt() і DematerializeBook()
    [HideInInspector] public int  bookIndex         = -1;

    /// True поки гравець або NPC активно взаємодіє з цією книгою
    [HideInInspector] public bool isBeingInteracted = false;

    // ── Резервування — делегати до Shelf data layer ────────────────────────
    // NPCBrain викликає: targetBook.Reserve(npcID) / targetBook.Unreserve()
    // Ці методи перенаправляють до Shelf.ReserveBook(index, npcID).

    /// Чи книга зарезервована NPC.
    public bool IsReserved
    {
        get
        {
            if (parentShelf == null || bookIndex < 0) return false;
            var data = parentShelf.GetBookData(bookIndex);
            return data.isReserved;
        }
    }

    /// Резервує книгу для NPC (викликається з NPCBrain.InspectShelf).
    public bool Reserve(string npcID = "")
    {
        if (parentShelf == null || bookIndex < 0) return false;
        return parentShelf.ReserveBook(bookIndex, npcID);
    }

    /// Знімає резервацію (викликається з NPCBrain при виході / відмові).
    public void Unreserve()
    {
        if (parentShelf == null || bookIndex < 0) return;
        parentShelf.UnreserveBook(bookIndex);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        // savedTilt тепер призначається ззовні (з ShelfBookEntry.tilt).
        // Генеруємо тільки як fallback якщо не призначено.
        if (savedTilt == 0f && parentShelf != null)
            savedTilt = Random.Range(-parentShelf.maxRandomTilt, parentShelf.maxRandomTilt);
    }

    private void OnDestroy()
    {
        // Повідомляємо Shelf що GO знищено (на випадок якщо не через DematerializeBook)
        if (parentShelf != null && parentShelf._materializedBookRef == this)
            parentShelf._materializedBookRef = null;
    }
}