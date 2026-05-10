// Assets/Scripts/Data/LootBox.cs  [Фаза 1 — фінальна версія]
//
// Два режими (визначається в Inspector полем boxType):
//
// ── BookBox ──────────────────────────────────────────────────────
//   ЛКМ → всі книги з possibleBooks йдуть в інвентар → коробка знищується
//   Поля: possibleBooks, booksPerBox
//
// ── FurnitureBox ─────────────────────────────────────────────────
//   ЛКМ → спавниться furnitureSpawnPrefab на місці коробки
//         (готовий набір меблів що гравець потім розставляє через EditMode)
//       → коробка знищується
//   Поля: furnitureSpawnPrefab, spawnOffset
//
// UNITY SETUP:
//   На prefab коробки:
//   1. Компонент LootBox
//   2. Collider в шарі що входить в interactionLayer InteractionRouter
//   3. Для BookBox: заповнити possibleBooks + booksPerBox
//   4. Для FurnitureBox: призначити furnitureSpawnPrefab

using UnityEngine;
using System.Collections.Generic;

public enum LootBoxType { BookBox, FurnitureBox }

public class LootBox : MonoBehaviour
{
    [Header("Тип коробки")]
    [SerializeField] private LootBoxType boxType = LootBoxType.BookBox;

    // ── BookBox ─────────────────────────────────────────────────
    [Header("BookBox — книги")]
    [Tooltip("Пул книг. Booksperbox штук видається випадково.")]
    [SerializeField] private List<BookTemplate> possibleBooks = new List<BookTemplate>();
    [SerializeField] private int booksPerBox = 3;

    // ── FurnitureBox ────────────────────────────────────────────
    [Header("FurnitureBox — меблі")]
    [Tooltip("Prefab з набором меблів що спавниться замість коробки.")]
    [SerializeField] private GameObject furnitureSpawnPrefab;
    [Tooltip("Зміщення спавну відносно позиції коробки (зазвичай Vector3.zero).")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    public bool IsOpened { get; private set; }

    // ── Public API (викликається з InteractionRouter) ───────────

    public void OpenBox()
    {
        if (IsOpened) return;
        IsOpened = true;

        switch (boxType)
        {
            case LootBoxType.BookBox:
                OpenBookBox();
                break;
            case LootBoxType.FurnitureBox:
                OpenFurnitureBox();
                break;
        }

        Destroy(gameObject);
    }

    // ── Private ─────────────────────────────────────────────────

    private void OpenBookBox()
    {
        if (possibleBooks == null || possibleBooks.Count == 0)
        {
            Debug.LogWarning($"[LootBox] '{gameObject.name}': possibleBooks порожній!");
            return;
        }

        int added = 0;
        for (int i = 0; i < booksPerBox; i++)
        {
            BookTemplate template = possibleBooks[Random.Range(0, possibleBooks.Count)];
            if (template == null) continue;
            InventoryManager.Instance?.AddBook(template.bookID);
            added++;
        }

        Debug.Log($"[LootBox] BookBox '{gameObject.name}': +{added} книг в інвентар.");
        // Інвентар НЕ відкривається — гравець бачить книги коли сам відкриє
    }

    private void OpenFurnitureBox()
    {
        if (furnitureSpawnPrefab == null)
        {
            Debug.LogWarning($"[LootBox] '{gameObject.name}': furnitureSpawnPrefab не призначено!");
            return;
        }

        Vector3    pos = transform.position + spawnOffset;
        Quaternion rot = transform.rotation;

        Instantiate(furnitureSpawnPrefab, pos, rot);
        Debug.Log($"[LootBox] FurnitureBox '{gameObject.name}': спавнено '{furnitureSpawnPrefab.name}' на {pos}.");
    }
}