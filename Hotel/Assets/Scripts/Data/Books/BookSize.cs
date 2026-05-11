// Assets/Scripts/Data/Books/BookSize.cs
// BookSize enum вже визначений в BookEnums.cs (Small, Medium, Large)
// Цей файл містить тільки helper методи для UI

public static class BookSizeHelper
{
    public static string ToUkrainian(BookSize size) => size switch
    {
        BookSize.Small  => "S (мала)",
        BookSize.Medium => "M (середня)",
        BookSize.Large  => "L (велика)",
        _               => size.ToString()
    };

    public static string ToIcon(BookSize size) => size switch
    {
        BookSize.Small  => "📗",
        BookSize.Medium => "📘",
        BookSize.Large  => "📙",
        _               => "📚"
    };
}