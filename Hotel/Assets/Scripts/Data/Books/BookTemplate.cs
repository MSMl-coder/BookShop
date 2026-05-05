// Assets/Scripts/Data/Books/BookTemplate.cs
using UnityEngine;

[CreateAssetMenu(fileName = "BT_", menuName = "Bookstore/Book Template")]
public class BookTemplate : ScriptableObject
{
    [Header("Identity")]
    public string bookID;
    public string title;
    public string author;
    public BookEnums.BookGenre genre;
    public BookEnums.BookRarity rarity;

    [Header("Smart ID Data")]
    public int writingYear = 2024;
    public int volumeNumber = 1;

    [Header("Visuals")]
    public GameObject containerPrefab;
    public Sprite icon;

    [Header("Physical Stats")]
    [Tooltip("Товщина книги в метрах (Z)")]
    public float thickness = 0.03f;

    [Tooltip("Висота книги в метрах (Y)")]
    public float height = 0.24f;

    [Header("Economics")]
    public float buyPrice;
    public float sellPrice;

    private void OnValidate()
    {
        bookID = BookSmartID.Generate(this);
    }
}