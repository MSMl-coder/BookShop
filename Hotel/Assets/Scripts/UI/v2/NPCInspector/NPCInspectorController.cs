// Assets/Scripts/UI/Components/NPCInspector/NPCInspectorController.cs

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class NPCInspectorController
{
    private const string CLS_HIDDEN   = "hidden";
    private const string CLS_EXPANDED = "expanded";
    private const string CLS_FULFILLED= "fulfilled";
    private const string CLS_UP       = "up";
    private const string CLS_DOWN     = "down";

    // Root — сам елемент NPCInspector
    private readonly VisualElement _root;

    // Speech bubble
    private VisualElement _speechBubble;
    private Label         _speechBubbleText;
    private System.Threading.CancellationTokenSource _bubbleCts;

    // Stat tracks — контейнери для сегментів
    private SegmentedBar _moodBar;
    private SegmentedBar _comfortBar;
    private SegmentedBar _patienceBar;
    private SegmentedBar _walletBar;

    // Trend labels
    private readonly Label _moodTrend;
    private readonly Label _comfortTrend;
    private readonly Label _patienceTrend;

    // Genre cards
    private readonly VisualElement _genreCardsRow;
    private readonly VisualElement _tooltip;
    private readonly Label         _tooltipTitle;
    private readonly Label         _tooltipPrice;

    private NPCBrain _currentNPC;
    private bool     _isVisible;
    private bool     _cardsExpanded;

    private float _prevMood;
    private float _prevComfort;
    private float _prevPatience;

    private readonly List<VisualElement> _cards        = new(); // wrappers — для left
    private readonly List<VisualElement> _cardElements = new(); // самі картки — для класів
    private readonly List<CardData>      _cardData     = new();

    // ── Constructor ──────────────────────────────────────────────
    public NPCInspectorController(VisualElement templateContainer)
    {
        // TemplateContainer → шукаємо NPCInspector всередині
        // Якщо templateContainer сам і є NPCInspector (напр. вже розгорнутий) — беремо його
        _root = templateContainer.Q<VisualElement>("NPCInspector");
        if (_root == null)
        {
            // Fallback: перший дочірній елемент
            _root = templateContainer.childCount > 0
                ? templateContainer.ElementAt(0)
                : templateContainer;
        }

        Debug.Log($"[NPCInspector] root found: {_root?.name} ({_root?.GetType().Name})");

        // ── Trend labels ──
        _moodTrend     = _root.Q<Label>("MoodTrend");
        _comfortTrend  = _root.Q<Label>("ComfortTrend");
        _patienceTrend = _root.Q<Label>("PatienceTrend");

        // ── Tooltip ──
        _tooltip      = _root.Q<VisualElement>("CardTooltip");
        _tooltipTitle = _root.Q<Label>("TooltipBookTitle");
        _tooltipPrice = _root.Q<Label>("TooltipBookPrice");

        // ── Genre cards row ──
        _genreCardsRow = _root.Q<VisualElement>("GenreCardsRow");

        // Speech bubble
        _speechBubble     = _root.Q<VisualElement>("SpeechBubble");
        _speechBubbleText = _root.Q<Label>("SpeechBubbleText");

        if (_genreCardsRow != null)
        {
            _genreCardsRow.RegisterCallback<PointerEnterEvent>(_ => SetCardsExpanded(true));
            _genreCardsRow.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                SetCardsExpanded(false);
                HideTooltip();
            });
        }

        // ── Segmented bars ──
        // Шукаємо tracks і будуємо сегменти
        InitBars();

        // ── Log all found elements ──
        Debug.Log($"[NPCInspector] MoodTrack: {_root.Q("MoodTrack") != null}" +
                  $" | ComfortTrack: {_root.Q("ComfortTrack") != null}" +
                  $" | GenreCardsRow: {_genreCardsRow != null}" +
                  $" | MoodTrend: {_moodTrend != null}");

        // Start hidden — C# контролює видимість
        _root.AddToClassList(CLS_HIDDEN);
        _root.pickingMode = PickingMode.Ignore;

        // Закриття по кліку на порожнє місце реєструється при Show()
        // через батьківський HUDRoot (щоб не блокувати до відкриття)
    }

    // ── Public API ───────────────────────────────────────────────

    public bool IsVisible => _isVisible;

    public void Show(NPCBrain npc)
    {
        if (npc == null) return;
        Detach();
        _currentNPC = npc;
        _currentNPC.OnStateChanged += OnNPCStateChanged;

        var stats = npc.Ticker?.Stats;
        if (stats != null)
        {
            _prevMood     = Norm(stats.Mood);
            _prevComfort  = Norm(stats.Comfort);
            _prevPatience = Norm(stats.Patience);
        }

        RebuildGenreCards();
        RefreshBars();
        SetVisible(true);
        RegisterClickOutside();

        // Підписуємось на OnBookPickedUp (існує в NPCBrain після мінімального патчу)
        // і на OnBookFound як fallback якщо патч ще не застосовано
        _currentNPC.OnBookPickedUp += OnNPCPickedUpBook;
        _currentNPC.OnBookFound    += OnNPCBookFound;
        _currentNPC.OnNPCLeft      += OnNPCLeft;
    }

    public void Hide()
    {
        Detach();
        SetVisible(false);
        HideSpeechBubble();
    }

    /// Показати хмаринку з текстом над іконкою настрою.
    /// autoDismissSeconds: 0 = не ховати автоматично.
    public void ShowSpeechBubble(string text, float autoDismissSeconds = 3f)
    {
        if (_speechBubble == null)
        {
            Debug.LogWarning("[NPCInspector] SpeechBubble element not found in UXML");
            return;
        }

        if (_speechBubbleText != null)
            _speechBubbleText.text = text;

        // Показуємо через opacity transition (position:absolute — не штовхає layout)
        _speechBubble.RemoveFromClassList(CLS_HIDDEN);

        _bubbleCts?.Cancel();
        if (autoDismissSeconds > 0f)
        {
            _bubbleCts = new System.Threading.CancellationTokenSource();
            var token  = _bubbleCts.Token;
            _speechBubble.schedule.Execute(() =>
            {
                if (!token.IsCancellationRequested)
                    HideSpeechBubble();
            }).StartingIn((long)(autoDismissSeconds * 1000));
        }

        Debug.Log($"[NPCInspector] SpeechBubble shown: '{text}'");
    }

    public void HideSpeechBubble()
    {
        _bubbleCts?.Cancel();
        if (_speechBubble == null) return;
        _speechBubble.AddToClassList(CLS_HIDDEN);
    }

    public void HideIfShowing(NPCBrain npc)
    {
        if (_currentNPC == npc) Hide();
    }

    public void Tick()
    {
        if (!_isVisible || _currentNPC == null) return;
        RefreshBars();
    }

    public void MarkCardFulfilled(int index)
    {
        if (index < 0 || index >= _cardElements.Count) return;
        var card = _cardElements[index];
        if (card == null) return;
        card.AddToClassList(CLS_FULFILLED);
        card.Q<VisualElement>("CardCheck")?.RemoveFromClassList(CLS_HIDDEN);
        if (index < _cardData.Count) _cardData[index].IsFulfilled = true;
    }

    public void UpdateCardBook(int index, string bookTitle, float price, string genreCssClass)
    {
        if (index < 0 || index >= _cardElements.Count) return;
        var card = _cardElements[index];
        if (card == null) return;
        RemoveGenreClasses(card);
        if (!string.IsNullOrEmpty(genreCssClass))
            card.AddToClassList(genreCssClass);
        if (index < _cardData.Count)
        {
            _cardData[index].BookTitle = bookTitle;
            _cardData[index].Price     = price;
        }
    }

    /// Показати з тестовими даними без NPCBrain
    public void ShowForPreview()
    {
        // Bars
        if (_moodBar == null) InitBars(); // re-try якщо не вдалось в constructor

        _moodBar?.SetValue(0.65f);
        _comfortBar?.SetValue(0.55f);
        _patienceBar?.SetValue(0.40f);
        _walletBar?.SetValue(0.30f);

        // Trends
        SetTrendLabel(_moodTrend,     "↑", true);
        SetTrendLabel(_comfortTrend,  "",  false);
        SetTrendLabel(_patienceTrend, "↓", false);

        // Genre cards — 2 картки Mystery
        BuildPreviewCards();

        SetVisible(true);
        Debug.Log("[NPCInspector] ShowForPreview called");

        // Тест хмаринки — показуємо одразу з preview
        ShowSpeechBubble("Шукаю книгу детективу!", 5f);
    }

    // ── Visibility ────────────────────────────────────────────────

    private void SetVisible(bool visible)
    {
        _isVisible = visible;
        _root.EnableInClassList(CLS_HIDDEN, !visible);
        _root.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    }

    // ── Bars ─────────────────────────────────────────────────────

    private void InitBars()
    {
        var moodTrack     = _root.Q<VisualElement>("MoodTrack");
        var comfortTrack  = _root.Q<VisualElement>("ComfortTrack");
        var patienceTrack = _root.Q<VisualElement>("PatienceTrack");
        var walletTrack   = _root.Q<VisualElement>("WalletTrack");

        // Якщо tracks ще не знайдені — спробуємо через _root.parent (TemplateContainer)
        if (moodTrack == null && _root.parent != null)
        {
            moodTrack     = _root.parent.Q<VisualElement>("MoodTrack");
            comfortTrack  = _root.parent.Q<VisualElement>("ComfortTrack");
            patienceTrack = _root.parent.Q<VisualElement>("PatienceTrack");
            walletTrack   = _root.parent.Q<VisualElement>("WalletTrack");
        }

        if (moodTrack    != null) _moodBar     = new SegmentedBar(moodTrack,     "mood");
        if (comfortTrack != null) _comfortBar  = new SegmentedBar(comfortTrack,  "comfort");
        if (patienceTrack!= null) _patienceBar = new SegmentedBar(patienceTrack, "patience");
        if (walletTrack  != null) _walletBar   = new SegmentedBar(walletTrack,   "wallet");

        Debug.Log($"[NPCInspector] Bars: mood={_moodBar != null} comfort={_comfortBar != null} " +
                  $"patience={_patienceBar != null} wallet={_walletBar != null}");
    }

    private void RefreshBars()
    {
        var stats = _currentNPC?.Ticker?.Stats;
        if (stats == null) return;

        float mood     = Norm(stats.Mood);
        float comfort  = Norm(stats.Comfort);
        float patience = Norm(stats.Patience);
        float wallet   = Mathf.Clamp01(stats.Wallet / NPCStats.WALLET_MAX);

        _moodBar?.SetValue(mood);
        _comfortBar?.SetValue(comfort);
        _patienceBar?.SetValue(patience);
        _walletBar?.SetValue(wallet);

        UpdateTrend(_moodTrend,     mood,     ref _prevMood);
        UpdateTrend(_comfortTrend,  comfort,  ref _prevComfort);
        UpdateTrend(_patienceTrend, patience, ref _prevPatience);
    }

    private static void UpdateTrend(Label label, float current, ref float prev)
    {
        if (label == null) return;
        float delta = current - prev;
        if (Mathf.Abs(delta) > 0.02f)
        {
            bool up = delta > 0;
            SetTrendLabel(label, up ? "↑" : "↓", up);
        }
        prev = current;
    }

    private static void SetTrendLabel(Label label, string text, bool up)
    {
        if (label == null) return;
        label.text = text;
        label.EnableInClassList(CLS_UP,   up   && !string.IsNullOrEmpty(text));
        label.EnableInClassList(CLS_DOWN, !up  && !string.IsNullOrEmpty(text));
    }

    private static float Norm(float v) =>
        Mathf.InverseLerp(NPCStats.MIN, NPCStats.MAX, v);

    // ── Genre cards ───────────────────────────────────────────────

    private void RebuildGenreCards()
    {
        if (_genreCardsRow == null) return;
        _genreCardsRow.Clear();
        _cards.Clear();
        _cardElements.Clear();
        _cardData.Clear();

        if (_currentNPC == null) return;

        int total    = _currentNPC.Personality?.WantsToBuy ?? 1;
        var basket   = _currentNPC.Personality?.Basket;     // нові: зібрані книги
        int inBasket = basket?.Count ?? 0;
        var genre    = _currentNPC.DesiredGenre;

        // Будуємо дані: якщо книга вже в кошику — fulfilled + показуємо назву/ціну
        for (int i = 0; i < total; i++)
        {
            bool fulfilled = i < inBasket;
 
            // ✅ ЗМІНА: жанр з ShoppingList[i] — кожна картка свого кольору/жанру
            BookGenre slotGenre = (_currentNPC.Personality?.ShoppingList != null
                                   && i < _currentNPC.Personality.ShoppingList.Length)
                ? _currentNPC.Personality.ShoppingList[i]
                : _currentNPC.DesiredGenre; // fallback
 
            _cardData.Add(new CardData
            {
                Genre       = slotGenre,
                IsFulfilled = fulfilled,
                BookTitle   = (fulfilled && basket != null) ? basket[i].title     : null,
                Price       = (fulfilled && basket != null) ? basket[i].sellPrice : 0f
            });
            _cards.Add(null);
        }
        // Додаємо в DOM у зворотньому порядку (картка 0 = зверху)
        for (int i = total - 1; i >= 0; i--)
        {
            var card = BuildCard(_cardData[i], i);
            _cards[i] = card;
            _genreCardsRow.Add(card);
        }

        SnapToCollapsed();
    }

    private void BuildPreviewCards()
    {
        if (_genreCardsRow == null) return;
        _genreCardsRow.Clear();
        _cards.Clear();
        _cardElements.Clear();
        _cardData.Clear();

        // Логічний порядок: 0=Mystery (front), 1=Fantasy, 2=SciFi (back)
        _cardData.Add(new CardData { Genre = BookGenre.Mystery, IsFulfilled = true,  BookTitle = "Ім'я Рози", Price = 48f });
        _cardData.Add(new CardData { Genre = BookGenre.Fantasy, IsFulfilled = false });
        _cardData.Add(new CardData { Genre = BookGenre.SciFi,   IsFulfilled = false });

        _cards.Add(null);
        _cards.Add(null);
        _cards.Add(null);

        // Додаємо в DOM у зворотньому порядку (SciFi → Fantasy → Mystery)
        // Mystery додається останньою → малюється зверху
        for (int i = _cardData.Count - 1; i >= 0; i--)
        {
            var card = BuildCard(_cardData[i], i);
            _cards[i] = card;
            _genreCardsRow.Add(card);
        }

        // Встановлюємо collapsed позиції БЕЗ анімації
        SnapToCollapsed();
    }

    /// Повертає wrapper (для _cards / style.left).
    /// Сама картка зберігається в _cardElements[index].
    private VisualElement BuildCard(CardData data, int index)
    {
        var genre     = data.Genre;
        var fulfilled = data.IsFulfilled;
        var bookTitle = data.BookTitle;
        var price     = data.Price;

        // ── Wrapper ───────────────────────────────────────────────
        var wrapper = new VisualElement();
        wrapper.AddToClassList("genre-card-wrapper");
        wrapper.pickingMode = PickingMode.Position;

        // ── Shadow ────────────────────────────────────────────────
        var shadow = new VisualElement();
        shadow.AddToClassList("genre-card-shadow");
        shadow.pickingMode = PickingMode.Ignore;
        wrapper.Add(shadow);

        // ── Card ──────────────────────────────────────────────────
        var card = new VisualElement();
        card.AddToClassList("genre-card");
        card.AddToClassList($"genre--{genre}");
        if (fulfilled) card.AddToClassList(CLS_FULFILLED);
        card.pickingMode = PickingMode.Position;

        var icon = new VisualElement();
        icon.AddToClassList("genre-card__icon");
        card.Add(icon);

        // Якщо книга взята — показуємо назву, інакше — жанр
        var labelText = (!string.IsNullOrEmpty(bookTitle)) ? bookTitle : genre.ToString();
        var label = new Label(labelText);
        label.AddToClassList("genre-card__label");
        card.Add(label);

        // Ціна (показується якщо книга взята)
        if (fulfilled && price > 0f)
        {
            var priceLabel = new Label($"${price:0}");
            priceLabel.AddToClassList("genre-card__price");
            card.Add(priceLabel);
        }

        var check = new VisualElement();
        check.name = "CardCheck";
        check.AddToClassList("genre-card__check");
        if (!fulfilled) check.AddToClassList(CLS_HIDDEN);
        card.Add(check);

        wrapper.Add(card);

        // Зберігаємо картку для зовнішнього доступу (MarkCardFulfilled тощо)
        // Розширюємо _cardElements до потрібного розміру
        while (_cardElements.Count <= index) _cardElements.Add(null);
        _cardElements[index] = card;

        // Tooltip + hover sound — на wrapper
        int idx = index;
        wrapper.RegisterCallback<PointerEnterEvent>(evt =>
        {
            UIHoverSound.PlayHover();
            if (_cardsExpanded) ShowTooltip(idx, evt.position);
        });
        wrapper.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_cardsExpanded && _tooltip != null && !_tooltip.ClassListContains(CLS_HIDDEN))
                MoveTooltip(evt.position);
        });
        wrapper.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());

        return wrapper; // _cards зберігає wrapper
    }

    private const float CARD_WIDTH        = 72f;
    private const float CARD_GAP_EXPANDED = 5f;   // відступ між картками при розкладанні
    private const float CARD_PEEK         = 12f;  // скільки виступає кожна наступна в колоді

    private void SetCardsExpanded(bool expanded)
    {
        _cardsExpanded = expanded;

        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            card.EnableInClassList(CLS_EXPANDED, expanded);

            // Позиціонуємо через style.left (USS transition: left 0.25s).
            // Картка 0 ЗАВЖДИ на left=0 — не рухається.
            // Collapsed: кожна наступна виступає на CARD_PEEK (12px).
            // Expanded:  рівні відступи (72+5)px між картками.
            float left = expanded
                ? i * (CARD_WIDTH + CARD_GAP_EXPANDED)
                : i * CARD_PEEK;

            card.style.left = left;
        }
    }

    /// Встановити позиції БЕЗ анімації (при першому показі).
    private void SnapToCollapsed()
    {
        // Встановлюємо left одразу — без transition.
        // style.left не анімується до першого reflow,
        // тому просто ставимо значення і USS transition почне
        // працювати тільки при НАСТУПНІЙ зміні.
        for (int i = 0; i < _cards.Count; i++)
            _cards[i].style.left = i * CARD_PEEK;
    }

    // ── Tooltip ───────────────────────────────────────────────────

    private void ShowTooltip(int index, Vector2 pos)
    {
        if (_tooltip == null || index >= _cardData.Count) return;
        var data = _cardData[index];
        if (_tooltipTitle != null)
            _tooltipTitle.text = string.IsNullOrEmpty(data.BookTitle) ? data.Genre.ToString() : data.BookTitle;
        if (_tooltipPrice != null)
            _tooltipPrice.text = data.Price > 0f ? $"$ {data.Price:0}" : "—";
        _tooltip.RemoveFromClassList(CLS_HIDDEN);
        MoveTooltip(pos);
    }

    private void MoveTooltip(Vector2 pos)
    {
        if (_tooltip == null) return;
        _tooltip.style.left = pos.x - 65f;
        _tooltip.style.top  = pos.y + 10f;
    }

    private void HideTooltip() => _tooltip?.AddToClassList(CLS_HIDDEN);

    // ── Helpers ───────────────────────────────────────────────────

    private void RegisterClickOutside()
    {
        var parent = _root.parent;
        while (parent != null && parent.name != "HUDRoot" && parent.parent != null)
            parent = parent.parent;

        if (parent == null) return;

        parent.RegisterCallback<PointerDownEvent>(OnClickOutside, TrickleDown.TrickleDown);
    }

    private void OnClickOutside(PointerDownEvent evt)
    {
        if (!_isVisible) return;

        // Не закриваємо якщо клік в межах нашої панелі
        var localPos = _root.WorldToLocal(evt.position);
        if (_root.ContainsPoint(localPos)) return;

        // Не закриваємо якщо клік потрапив на будь-який інший UI елемент
        // (кнопки, меблі в UI, інвентар тощо)
        // Перевіряємо через UIPointerChecker — якщо над UI → не закриваємо
        if (UIPointerChecker.IsOverUI()) return;

        // Не закриваємо якщо клік на 3D об'єкт (меблі, NPC, полиці)
        // Перевіряємо через Physics raycast
        if (IsPointerOver3DObject(evt.position)) return;

        // Клік на порожній простір → закриваємо
        Hide();
    }

    private static bool IsPointerOver3DObject(Vector2 screenPos)
    {
        // Конвертуємо UI позицію в screen координати для Physics raycast
        var camera = Camera.main;
        if (camera == null) return false;

        // UI координати: origin top-left. Physics raycast: origin bottom-left.
        var ray = camera.ScreenPointToRay(
            new Vector3(screenPos.x, Screen.height - screenPos.y, 0f));

        return Physics.Raycast(ray, 100f);
    }

    private void OnNPCPickedUpBook(BookTemplate book)
    {
        // Книга взята в кошик → оновлюємо картки + хмаринка
        RebuildGenreCards();
        ShowSpeechBubble($"Беру «{book?.title ?? "..."}»!", 2.5f);
    }

    private void OnNPCBookFound(BookTemplate book)
    {
        // Fallback для v8: NPC знайшов книгу і йде до каси → позначаємо картку
        // (OnBookPickedUp ще не існує — спрацьовує OnBookFound)
        if (book == null) return;
        MarkNextCardFulfilled(book);
        ShowSpeechBubble($"Беру «{book.title}»!", 2.5f);
    }

    /// Знаходить першу незаповнену картку і заповнює її даними книги.
    /// Fallback для старого NPCBrain без Basket.
    public void MarkNextCardFulfilled(BookTemplate book)
    {
        if (book == null) return;
        // При новому flow — просто перебудовуємо всі картки з актуального Basket
        // При старому flow (fallback) — оновлюємо першу порожню картку вручну
        if (_currentNPC?.Personality?.Basket != null)
        {
            RebuildGenreCards();
            return;
        }

        // Fallback: старий NPCBrain без Basket
        for (int i = 0; i < _cardData.Count; i++)
        {
            if (_cardData[i].IsFulfilled) continue;

            _cardData[i].IsFulfilled = true;
            _cardData[i].BookTitle   = book.title;
            _cardData[i].Price       = book.sellPrice;

            if (i < _cardElements.Count && _cardElements[i] != null)
            {
                var card = _cardElements[i];
                card.AddToClassList(CLS_FULFILLED);
                card.Q<VisualElement>("CardCheck")?.RemoveFromClassList(CLS_HIDDEN);
                var lbl = card.Q<Label>("genre-card__label");
                if (lbl != null) lbl.text = book.title;
            }
            Debug.Log($"[NPCInspector] Card {i} fulfilled: {book.title} ${book.sellPrice}");
            return;
        }
    }

        private void Detach()
    {
        if (_currentNPC != null)
        {
            _currentNPC.OnStateChanged  -= OnNPCStateChanged;
            _currentNPC.OnBookPickedUp  -= OnNPCPickedUpBook;
            _currentNPC.OnBookFound     -= OnNPCBookFound;
            _currentNPC.OnNPCLeft       -= OnNPCLeft;
        }
        _currentNPC = null;
    }

    private void OnNPCStateChanged(NPCState state)
    {
        // Не закриваємо при Leaving — чекаємо OnNPCLeft
        switch (state)
        {
            case NPCState.Leaving:
                ShowSpeechBubble("До побачення!", 99f); // залишається до закриття
                break;
            case NPCState.Buying:
                ShowSpeechBubble("Йду на касу!", 3f);
                break;
            case NPCState.CollectingBooks:
                ShowSpeechBubble("Пошукаю ще...", 2f);
                break;
            case NPCState.WaitingForPlayer:
                ShowSpeechBubble("Чи є у вас щось для мене?", 99f);
                break;
        }
    }

    private void OnNPCLeft()
    {
        // NPC знищений зі сцени → тепер закриваємо вікно
        Hide();
    }

    private static void RemoveGenreClasses(VisualElement el)
    {
        var toRemove = new List<string>();
        foreach (var cls in el.GetClasses())
            if (cls.StartsWith("genre--")) toRemove.Add(cls);
        foreach (var cls in toRemove) el.RemoveFromClassList(cls);
    }

    private class CardData
    {
        public BookGenre Genre;
        public bool      IsFulfilled;
        public string    BookTitle;
        public float     Price;
    }
}