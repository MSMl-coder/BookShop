using UnityEngine;
using System.Collections.Generic;

public class LootBox : MonoBehaviour
{
    /* public List<BookData> allPossibleBooks;
    public int booksPerBox = 3;

    void OnMouseDown()
    {
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        OpenBox();
    }

    public void OpenBox()
    {
        if (allPossibleBooks.Count == 0) return;
        List<BookData> wonBooks = new List<BookData>();
        for (int i = 0; i < booksPerBox; i++)
            wonBooks.Add(allPossibleBooks[Random.Range(0, allPossibleBooks.Count)]);

        BookEvents.OnLootBoxOpened?.Invoke(wonBooks);
    }
    */
}