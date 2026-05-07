// Assets/Scripts/Data/Books/BookEnums.cs
// ВИПРАВЛЕНО: прибрано MonoBehaviour — енами не потребують компонента.
// Раніше клас успадковував MonoBehaviour що не має сенсу для простих enum-ів
// і потенційно міг бути помилково доданий на GameObject.

public enum BookGenre    { Classic, Fantasy, SciFi, Horror, Mystery, Biography, Academic }
public enum BookRarity   { Common, Uncommon, Rare, Epic, Legendary }
public enum BookSize     { Small, Medium, Large }

// Зворотна сумісність: зберігаємо клас BookEnums для доступу через BookEnums.BookGenre
// щоб не ламати існуючий код що вже використовує цей синтаксис
public   class BookEnums
{
    // Псевдоніми щоб старий код BookEnums.BookGenre / BookEnums.BookRarity продовжував працювати
    public static readonly System.Type BookGenre  = typeof(global::BookGenre);
    public static readonly System.Type BookRarity = typeof(global::BookRarity);
    public static readonly System.Type BookSize   = typeof(global::BookSize);
 
}


