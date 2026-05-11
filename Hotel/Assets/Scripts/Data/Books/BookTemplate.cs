// Assets/Scripts/Data/Books/BookTemplate.cs
using UnityEngine;

[CreateAssetMenu(fileName = "BT_", menuName = "Bookstore/Book Template")]
public class BookTemplate : ScriptableObject
{
    [Header("Identity")]
    public string     bookID;
    public string     title;
    public string     author;
    public BookGenre  genre;
    public BookRarity rarity;

    [Header("Smart ID Data")]
    public int writingYear  = 2024;
    public int volumeNumber = 1;

    [Header("Visuals")]
    public GameObject containerPrefab;
    public Sprite     icon;

    [Header("Physical Stats")]
    [Tooltip("Розмір книги — визначає висоту і чи вміститься в полицю.\n" +
             "Small = кишенькова (<0.20m)\n" +
             "Medium = стандарт (0.20–0.26m)\n" +
             "Large = великоформатна (>0.26m)")]
    public BookSize bookSize = BookSize.Medium;

    [Header("Economics")]
    public float buyPrice;
    public float sellPrice;

    private void OnValidate()
    {
        bookID = BookSmartID.Generate(this);
    }
}