// Assets/Scripts/World/Shelf/ShelfBookEntry.cs
// НОВИЙ ФАЙЛ: чиста структура даних — замінює роль BookWorldItem як носія даних.
// Жодного MonoBehaviour, жодного GameObject. Лише дані для рендерингу та збереження.
using UnityEngine;

[System.Serializable]
public struct ShelfBookEntry
{
    // ── Ідентифікація ────────────────────────────────────────
    public string instanceID;   // GUID екземпляру книги (BookInstance.instanceID)
    public string templateID;   // ID шаблону для BookDatabase.GetBook()

    // ── Фізичні параметри (pre-calculated при PlaceBook) ─────
    public float  thickness;    // товщина в локальних одиницях startPoint
    public float  height;       // висота для bottom-align
    public float  tilt;         // випадковий нахил (генерується один раз)

    // ── Рендеринг ────────────────────────────────────────────
    public Color  coverColor;   // колір для GPU instancing (_BaseColor)
    public int    prefabVariant; // індекс варіанту prefab (rarity/size)

    // ── Layout (перераховується в RebuildLayout) ──────────────
    public Vector3 localPosition; // позиція відносно startPoint

    // ── Стан ─────────────────────────────────────────────────
    public bool   isReserved;   // NPC зарезервував цю книгу
    public string reservedByID; // ID NPC що зарезервував (порожній якщо вільна)
}