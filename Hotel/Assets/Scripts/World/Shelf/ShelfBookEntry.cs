// Assets/Scripts/World/Shelf/ShelfBookEntry.cs
//
// Внутрішня структура полиці.
//
// v2.2 ЗМІНИ:
//   • ПРИБРАНО zExtra — висування видалено (підсвітка тепер через ghost+OutlineTarget).
//
// thickness/height/depth беруться з containerPrefab.localScale × baseSize у Shelf.PlaceBook.

using UnityEngine;

[System.Serializable]
public struct ShelfBookEntry
{
    // ── Ідентифікація ─────────────────────────────────────────
    public string instanceID;
    public string templateID;

    // ── Фізичні параметри (реальні розміри у world units) ─────
    public float thickness;     // X — товщина (вздовж полиці)
    public float height;        // Y — висота
    public float depth;         // Z — глибина (в полицю)
    public float tilt;          // нахил X-axis у градусах

    // ── Рендеринг ─────────────────────────────────────────────
    public int colorIndex;      // 0–15: індекс варіанта обкладинки

    /// <summary>LEGACY: не використовується в data-driven архітектурі.</summary>
    public int prefabVariant;

    // ── Layout ────────────────────────────────────────────────
    public Vector3 localPosition;   // позиція відносно startPoint

    /// <summary>LEGACY: матриця тепер будується в Renderer.</summary>
    public Matrix4x4 renderMatrix;

    // ── Стан ──────────────────────────────────────────────────
    public bool   isReserved;
    public string reservedByID;
}