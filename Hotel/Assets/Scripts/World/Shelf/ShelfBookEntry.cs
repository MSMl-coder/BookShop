// Assets/Scripts/World/Shelf/ShelfBookEntry.cs
using UnityEngine;

[System.Serializable]
public struct ShelfBookEntry
{
    // ── Ідентифікація ─────────────────────────────────────────
    public string instanceID;
    public string templateID;

    // ── Фізичні параметри ─────────────────────────────────────
    public float thickness;     // товщина в world units
    public float height;        // висота в world units
    public float tilt;          // випадковий нахил (генерується один раз при PlaceBook)

    // ── Рендеринг ─────────────────────────────────────────────
    public int colorIndex;      // 0–15: індекс рядка в вертикальному Color Atlas
    public int prefabVariant;   // індекс варіанту prefab (для Ghost-on-Demand)

    // ── Layout ────────────────────────────────────────────────
    public Vector3  localPosition; // позиція відносно startPoint (pre-calculated)

    // Матриця для Instanced Renderer — береться з реального GO при спавні.
    // Містить правильний world transform з ротацією меша і масштабом prefab.
    public Matrix4x4 renderMatrix;

    // ── Стан ──────────────────────────────────────────────────
    public bool   isReserved;
    public string reservedByID;
}