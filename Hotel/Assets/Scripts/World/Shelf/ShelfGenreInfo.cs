// ShelfGenreInfo.cs
// Утилітний клас — підраховує жанри на полиці і формує текст мітки.
// Використовується в CabinetPanelUI для відображення статистики.
// НЕ залежить від BookWorldItem GO — читає через GetAllBookData().

using System.Collections.Generic;
using UnityEngine;

public static class ShelfGenreInfo
{
    // Рядки жанрів українською
    private static readonly Dictionary<BookGenre, string> GenreNames =
        new Dictionary<BookGenre, string>
    {
        { BookGenre.Fantasy,   "Фентезі"    },
        { BookGenre.Horror,    "Жахи"       },
        { BookGenre.Mystery,   "Детектив"   },
        { BookGenre.Classic,   "Класика"    },
        { BookGenre.SciFi,     "Фантастика" },
        { BookGenre.Biography, "Біографія"  },
        { BookGenre.Academic,  "Наукова"    },
    };

    /// Повертає словник жанр → кількість книг на полиці.
    public static Dictionary<BookGenre, int> CountGenres(Shelf shelf)
    {
        var result = new Dictionary<BookGenre, int>();
        if (shelf == null || BookDatabase.Instance == null) return result;

        var books = shelf.GetAllBookData();
        foreach (var entry in books)
        {
            if (entry.isReserved) continue;
            var template = BookDatabase.Instance.GetBook(entry.templateID);
            if (template == null) continue;

            if (!result.ContainsKey(template.genre))
                result[template.genre] = 0;
            result[template.genre]++;
        }
        return result;
    }

    /// Формує короткий рядок для мітки полиці.
    /// Приклади:
    ///   "Фентезі ×5"
    ///   "Фентезі (осн.) · Жахи ×2 · Детектив ×1"
    ///   "Порожня"
    public static string BuildLabel(Shelf shelf)
    {
        var counts = CountGenres(shelf);
        if (counts.Count == 0) return "Порожня";

        int total = 0;
        BookGenre topGenre = default;
        int topCount = 0;

        foreach (var kv in counts)
        {
            total += kv.Value;
            if (kv.Value > topCount) { topCount = kv.Value; topGenre = kv.Key; }
        }

        // Один жанр
        if (counts.Count == 1)
        {
            string name = GenreName(topGenre);
            return $"{name} ×{total}";
        }

        // Кілька жанрів — перевіряємо чи один домінує (>50%)
        bool hasDominant = topCount > total * 0.5f;
        string topName = GenreName(topGenre);

        // Будуємо список решти жанрів відсортований за кількістю
        var others = new List<(BookGenre genre, int count)>();
        foreach (var kv in counts)
            if (kv.Key != topGenre) others.Add((kv.Key, kv.Value));
        others.Sort((a, b) => b.count.CompareTo(a.count));

        if (hasDominant)
        {
            // "Фентезі (осн.) та інші"  — якщо решта маленька
            // "Фентезі (осн.) · Жахи ×2" — якщо показати всіх компактно
            if (others.Count <= 2)
            {
                var parts = new System.Text.StringBuilder();
                parts.Append($"{topName} ×{topCount} (осн.)");
                foreach (var o in others)
                    parts.Append($" · {GenreName(o.genre)} ×{o.count}");
                return parts.ToString();
            }
            else
            {
                return $"{topName} (осн.) та інші";
            }
        }
        else
        {
            // Немає домінанта — "мішані" з переліком
            if (counts.Count <= 3)
            {
                var parts = new System.Text.StringBuilder();
                bool first = true;
                // Топ жанр перший
                parts.Append($"{topName} ×{topCount}");
                foreach (var o in others)
                {
                    parts.Append($" · {GenreName(o.genre)} ×{o.count}");
                }
                return parts.ToString();
            }
            else
            {
                return $"Мішані ({counts.Count} жанри, {total} кн.)";
            }
        }
    }

    /// Чи є на полиці хоч одна книга вказаного жанру.
    public static bool HasGenre(Shelf shelf, BookGenre genre)
    {
        if (shelf == null || BookDatabase.Instance == null) return false;
        var books = shelf.GetAllBookData();
        foreach (var entry in books)
        {
            if (entry.isReserved) continue;
            var t = BookDatabase.Instance.GetBook(entry.templateID);
            if (t != null && t.genre == genre) return true;
        }
        return false;
    }

    private static string GenreName(BookGenre g) =>
        GenreNames.TryGetValue(g, out string n) ? n : g.ToString();
}


// ─────────────────────────────────────────────────────────────────
// Нижче: CSS клас для shelf-item__genre — додати в CabinetStyles.uss
// ─────────────────────────────────────────────────────────────────
/*
.shelf-item__genre {
    font-size: 9px;
    color: rgb(58, 42, 26);
    -unity-font-style: italic;
    flex-grow: 1;
    white-space: normal;
    margin-left: 6px;
    margin-right: 4px;
}
*/