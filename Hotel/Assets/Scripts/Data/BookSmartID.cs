using UnityEngine;

public static class BookSmartID
{
    public static string Generate(BookTemplate t)
    {
        // 1-4: Рік (наприклад, 1920)
        string year = Mathf.Clamp(t.writingYear, 1000, 2026).ToString("D4");
        
        // 5-6: Жанр (через Enum)
        string genre = ((int)t.genre).ToString("D2");
        
        // 7: Рарність (0-4)
        string rarity = ((int)t.rarity).ToString("D1");
        
        // 8-9: Номер тому (01 якщо немає)
        string volume = Mathf.Clamp(t.volumeNumber, 1, 99).ToString("D2");
        
        // 10-12: Хеш від назви (щоб різні книги одного року/жанру мали різні ID)
        int hash = Mathf.Abs(t.title.GetHashCode() % 1000);
        string unique = hash.ToString("D3");

        return $"{year}{genre}{rarity}{volume}{unique}";
    }
}