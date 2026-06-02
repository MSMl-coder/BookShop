// Assets/Scripts/UI/v2/GenreCardListController.cs
//
// Керує колодою жанрових карток NPC.
// ВИПРАВЛЕНО:
//   - BookGenre: тільки { Classic, Fantasy, SciFi, Horror, Mystery, Biography, Academic }
//   - ForEach на IEnumerable → ToList().ForEach або foreach loop
//   - GenreCardData.Genre тип BookGenre

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class GenreCardListController
{
    private const string CLASS_EXPANDED  = "expanded";
    private const string CLASS_FULFILLED = "genre-card--fulfilled";
    private const string CLASS_HIDDEN    = "hidden";

    private readonly VisualElement _row;
    private readonly VisualElement _tooltip;
    private readonly Label         _tooltipTitle;
    private readonly Label         _tooltipPrice;

    private readonly List<GenreCardData>  _data  = new();
    private readonly List<VisualElement>  _cards = new();
    private bool _isExpanded = false;

    public GenreCardListController(VisualElement root)
    {
        _row          = root.Q<VisualElement>("GenreCardsRow");
        _tooltip      = root.Q<VisualElement>("GenreCardTooltip");
        _tooltipTitle = root.Q<Label>("TooltipTitle");
        _tooltipPrice = root.Q<Label>("TooltipPrice");

        if (_row == null)
        {
            Debug.LogError("[GenreCardList] GenreCardsRow не знайдено в UXML");
            return;
        }

        _row.RegisterCallback<PointerEnterEvent>(_ => SetExpanded(true));
        _row.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            SetExpanded(false);
            HideTooltip();
        });
    }

    // ── Public API ────────────────────────────────────────────────

    public void BuildCards(List<GenreCardData> desiredBooks)
    {
        _row.Clear();
        _cards.Clear();
        _data.Clear();

        if (desiredBooks == null || desiredBooks.Count == 0)
        {
            _row.style.display = DisplayStyle.None;
            return;
        }

        _row.style.display = DisplayStyle.Flex;
        _data.AddRange(desiredBooks);

        for (int i = 0; i < desiredBooks.Count; i++)
        {
            var card = CreateCard(desiredBooks[i], i);
            _row.Add(card);
            _cards.Add(card);
        }
    }

    public void SetCardFulfilled(int index, bool fulfilled)
    {
        if (index < 0 || index >= _cards.Count) return;
        var card = _cards[index];
        card.EnableInClassList(CLASS_FULFILLED, fulfilled);

        var mark = card.Q<VisualElement>("FulfilledMark");
        if (mark != null)
            mark.EnableInClassList(CLASS_HIDDEN, !fulfilled);
    }

    public void SetCardBookColor(int index, string genreCssClass)
    {
        if (index < 0 || index >= _cards.Count) return;
        var card = _cards[index];
        RemoveAllGenreClasses(card);
        if (!string.IsNullOrEmpty(genreCssClass))
            card.AddToClassList(genreCssClass);
    }

    // ── Private ───────────────────────────────────────────────────

    private VisualElement CreateCard(GenreCardData data, int index)
    {
        var card = new VisualElement();
        card.AddToClassList("genre-card");
        card.AddToClassList(GenreColorRegistry.GetCssClass(data.Genre));
        card.pickingMode = PickingMode.Position;

        var icon = new VisualElement();
        icon.AddToClassList("genre-card__icon");
        card.Add(icon);

        var label = new Label(data.GenreName);
        label.name = "GenreName";
        label.AddToClassList("genre-card__label");
        card.Add(label);

        var fulfilled = new VisualElement();
        fulfilled.name = "FulfilledMark";
        fulfilled.AddToClassList("genre-card__fulfilled");
        fulfilled.AddToClassList(CLASS_HIDDEN);
        card.Add(fulfilled);

        if (data.IsFulfilled)
        {
            card.AddToClassList(CLASS_FULFILLED);
            fulfilled.RemoveFromClassList(CLASS_HIDDEN);
        }

        int capturedIndex = index;
        card.RegisterCallback<PointerEnterEvent>(evt =>
        {
            if (_isExpanded)
                ShowTooltip(capturedIndex, evt.position);
        });
        card.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
        card.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_isExpanded && _tooltip != null && !_tooltip.ClassListContains(CLASS_HIDDEN))
                UpdateTooltipPosition(evt.position);
        });

        return card;
    }

    private void SetExpanded(bool expanded)
    {
        _isExpanded = expanded;
        foreach (var card in _cards)
            card.EnableInClassList(CLASS_EXPANDED, expanded);
    }

    private void ShowTooltip(int index, Vector2 screenPos)
    {
        if (_tooltip == null || index >= _data.Count) return;

        var data = _data[index];
        if (_tooltipTitle != null)
            _tooltipTitle.text = data.BookTitle ?? data.GenreName;
        if (_tooltipPrice != null)
            _tooltipPrice.text = data.Price > 0 ? $"$ {data.Price:0}" : "—";

        _tooltip.RemoveFromClassList(CLASS_HIDDEN);
        UpdateTooltipPosition(screenPos);
    }

    private void UpdateTooltipPosition(Vector2 screenPos)
    {
        if (_tooltip == null) return;
        _tooltip.style.left = screenPos.x - 60f;
        _tooltip.style.top  = screenPos.y + 8f;
    }

    private void HideTooltip() => _tooltip?.AddToClassList(CLASS_HIDDEN);

    private static void RemoveAllGenreClasses(VisualElement el)
    {
        // Збираємо класи що треба прибрати в окремий список (щоб не міняти колекцію під час ітерації)
        var toRemove = new List<string>();
        foreach (var c in el.GetClasses())
        {
            if (c.StartsWith("genre-card--"))
                toRemove.Add(c);
        }
        foreach (var c in toRemove)
            el.RemoveFromClassList(c);
    }
}

// ── Data container ────────────────────────────────────────────────
[System.Serializable]
public class GenreCardData
{
    public BookGenre Genre;
    public string    GenreName;
    public string    BookTitle;    // null якщо книгу ще не обрано гравцем
    public float     Price;        // 0 якщо книгу ще не обрано
    public bool      IsFulfilled;

    public GenreCardData(BookGenre genre, string genreName)
    {
        Genre     = genre;
        GenreName = genreName;
    }
}

// ── Genre CSS class registry ──────────────────────────────────────
/// Маппінг BookGenre → CSS клас картки.
/// BookGenre: Classic, Fantasy, SciFi, Horror, Mystery, Biography, Academic
public static class GenreColorRegistry
{
    private static readonly Dictionary<BookGenre, string> _map = new()
    {
        { BookGenre.Classic,   "genre-card--classic"   },
        { BookGenre.Fantasy,   "genre-card--fantasy"   },
        { BookGenre.SciFi,     "genre-card--scifi"     },
        { BookGenre.Horror,    "genre-card--horror"    },
        { BookGenre.Mystery,   "genre-card--mystery"   },
        { BookGenre.Biography, "genre-card--biography" },
        { BookGenre.Academic,  "genre-card--academic"  },
    };

    public static string GetCssClass(BookGenre genre)
        => _map.TryGetValue(genre, out var cls) ? cls : "genre-card--classic";
}