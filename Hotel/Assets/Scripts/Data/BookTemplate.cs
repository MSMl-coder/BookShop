using UnityEngine;

[CreateAssetMenu(fileName = "BT_", menuName = "Bookstore/Book Template")]
public class BookTemplate : ScriptableObject
{
    [Header("Identity")]
    public string bookID; // Буде генеруватися автоматично
    public string title;
    public string author;
    public BookEnums.BookGenre genre;
    public BookEnums.BookRarity rarity;

    [Header("Smart ID Data")]
    public int writingYear = 2024;
    public int volumeNumber = 1;

    [Header("Visuals")]
    public GameObject containerPrefab; // Префаб-контейнер (Варіант 1)
    public Sprite icon;

    [Header("Physical Stats")]
    [Tooltip("Товщина книги в метрах (Z)")] public float thickness = 0.03f;
    [Tooltip("Висота книги в метрах (X)")] public float height = 0.24f;

    [Header("Economics")]
    public float buyPrice;
    public float sellPrice;

    // Автоматично оновлюємо ID при зміні даних в інспекторі
    private void OnValidate()
    {
        bookID = BookSmartID.Generate(this);
    }
}