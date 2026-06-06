// Assets/Scripts/UI/NPC/GenreCardController.cs
//
// Контролер ОДНОЇ жанрової картки у NPC Inspector.
// Інкапсулює: позицію, стан пошуку (gold glow), fulfilled.
//
// SETUP:
//   1. Призначити GenreCard.uxml у SerializeField поля genreCardTemplate
//      в компоненті який тримає NPCInspectorController.
//   2. При отриманні NPC:
//        var row = _root.Q<VisualElement>("GenreCardsRow");
//        row.Clear();
//        _genreCards.Clear();
//        for (int i = 0; i < npc.Personality.ShoppingList.Length; i++) {
//            // ShoppingList — це BookGenre[], кожен елемент = BookGenre
//            var genre = npc.Personality.ShoppingList[i];
//            var ctrl  = new GenreCardController(genreCardTemplate, genre);
//            ctrl.SetPosition(i * 60f);
//            row.Add(ctrl.Root);
//            _genreCards.Add(ctrl);
//        }
//   3. Коли NPC починає шукати slot index:
//        SetActiveCard(index); // переставляє картку вперед + вмикає glow
//   4. В Tick():
//        foreach (var c in _genreCards) c.Tick(); // анімація пульсування

using System;
using UnityEngine;
using UnityEngine.UIElements;

public class GenreCardController
{
    // ── Public ────────────────────────────────────────────────────
    /// Root element (genre-card-wrapper) — додавати в GenreCardsRow
    public VisualElement Root { get; }

    // ── Private refs ─────────────────────────────────────────────
    private readonly VisualElement _card;          // name="CardBody"
    private readonly VisualElement _ring;          // name="SearchingRing"
    private readonly VisualElement _genreIcon;     // name="GenreIconCenter"
    private readonly Label         _label;         // name="GenreName"
    private readonly Label         _price;         // name="GenrePrice"
    private readonly VisualElement _fulfilledMark; // name="FulfilledMark"

    // ── Glow animation state ──────────────────────────────────────
    private bool  _isSearching;
    private bool  _glowState;          // true = glow-on, false = glow-off
    private float _lastGlowToggle;
    private const float GLOW_INTERVAL = 0.5f;  // seconds

    // ── Constructor ───────────────────────────────────────────────
    public GenreCardController(VisualTreeAsset template, BookGenre genre)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template),
                "[GenreCardController] genreCardTemplate is null — assign in Inspector");

        var container = template.Instantiate();
        Root = container.ElementAt(0); // <ui:VisualElement name="GenreCardWrapper">

        // Query by NAME (reliable), not by class
        _card          = Root.Q<VisualElement>("CardBody");
        _ring          = Root.Q<VisualElement>("SearchingRing");
        _genreIcon     = Root.Q<VisualElement>("GenreIconCenter");
        _label         = Root.Q<Label>("GenreName");
        _price         = Root.Q<Label>("GenrePrice");
        _fulfilledMark = Root.Q<VisualElement>("FulfilledMark");

        // Apply genre color class (defined in NPCInspector.uss)
        _card?.AddToClassList($"genre--{genre}");
        if (_label != null) _label.text = genre.ToString();
    }

    // ── Positioning ───────────────────────────────────────────────

    /// Позиція у рядку. CSS transition-property:left забезпечує анімацію.
    public void SetPosition(float leftPx)
    {
        Root.style.left = leftPx;
    }

    // ── Search state ──────────────────────────────────────────────

    /// Вмикає/вимикає gold pulsing ring.
    /// Якщо searching=true: починає пульсацію в Tick().
    public void SetSearching(bool searching)
    {
        _isSearching = searching;
        Root.EnableInClassList("searching", searching);

        if (!searching)
        {
            // Одразу вимикаємо glow при скиданні стану
            _ring?.RemoveFromClassList("glow-on");
            _ring?.RemoveFromClassList("glow-off");
        }
    }

    // ── Fulfilled ─────────────────────────────────────────────────

    /// Позначити як виконаний (знайдено і передано книгу).
    public void SetFulfilled(bool fulfilled)
    {
        _card?.EnableInClassList("fulfilled", fulfilled);
        _fulfilledMark?.EnableInClassList("hidden", !fulfilled);
    }

    /// Показати ціну книги (після fulfilled).
    public void SetPrice(float price)
    {
        if (_price == null) return;
        _price.text = $"${price:0}";
        _price.RemoveFromClassList("hidden");
    }

    // ── Icon ──────────────────────────────────────────────────────

    /// Призначити спрайт жанру на центральну іконку.
    public void SetGenreIcon(Sprite sprite)
    {
        if (_genreIcon != null && sprite != null)
            _genreIcon.style.backgroundImage = new StyleBackground(sprite);
    }

    // ── Glow tick (call every frame when panel visible) ───────────

    /// Пульсуюча анімація: toggle glow-on/glow-off кожні GLOW_INTERVAL секунд.
    /// CSS transition-duration: 0.45s на .genre-card__searching-ring дає плавність.
    public void Tick()
    {
        if (!_isSearching || _ring == null) return;

        float now = Time.unscaledTime;
        if (now - _lastGlowToggle < GLOW_INTERVAL) return;

        _lastGlowToggle = now;
        _glowState      = !_glowState;

        _ring.EnableInClassList("glow-on",  _glowState);
        _ring.EnableInClassList("glow-off", !_glowState);
    }
}