// Assets/Scripts/UI/Inventory/InventoryItemUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using System;

/// Заповнює одну картку книги (BookItem.uxml) даними.
public class InventoryItemUI
{
    public VisualElement Root { get; private set; }

    private readonly BookInstance _instance;
    private readonly BookTemplate _template;

    // Кольори корінців за жанром
    private static readonly System.Collections.Generic.Dictionary<BookEnums.BookGenre, Color>
        SpineColors = new System.Collections.Generic.Dictionary<BookEnums.BookGenre, Color>
    {
        { BookEnums.BookGenre.Fantasy,    new Color(0.31f, 0.33f, 0.75f) },
        { BookEnums.BookGenre.Horror,     new Color(0.55f, 0.10f, 0.10f) },
        { BookEnums.BookGenre.Mystery,    new Color(0.25f, 0.25f, 0.35f) },
        { BookEnums.BookGenre.Classic,    new Color(0.47f, 0.28f, 0.10f) },
        { BookEnums.BookGenre.SciFi,      new Color(0.10f, 0.40f, 0.55f) },
        { BookEnums.BookGenre.Biography,  new Color(0.30f, 0.50f, 0.25f) },
        { BookEnums.BookGenre.Academic,   new Color(0.55f, 0.45f, 0.15f) },
    };

    public event Action<BookInstance, BookTemplate> OnClicked;

    public InventoryItemUI(BookInstance instance, VisualTreeAsset xmlAsset,
                           Action<BookTemplate> onHoverEnter = null,
                           Action onHoverExit = null)
    {
        _instance = instance;
        _template = BookDatabase.Instance?.GetBook(instance.templateID);

        if (_template == null)
        {
            Debug.LogError($"[InventoryItemUI] Шаблон не знайдено: {instance.templateID}");
            return;
        }

        // Інстанціюємо шаблон
        VisualElement container = xmlAsset.Instantiate();
        Root = container.ElementAt(0);

        // ── Заповнення ──

        // 1. Іконка обкладинки
        var icon = Root.Q<VisualElement>("BookIcon");
        if (icon != null && _template.icon != null)
        {
            icon.style.backgroundImage  = new StyleBackground(_template.icon);
            icon.style.backgroundSize   = new BackgroundSize(BackgroundSizeType.Contain);
        }

        // 2. Резервний текст якщо іконки немає
        var nameLabel = Root.Q<Label>("BookNameLabel");
        if (nameLabel != null)
        {
            nameLabel.text = _template.icon == null ? _template.title : "";
            nameLabel.style.display = _template.icon == null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // 3. Ціна
        var priceLabel = Root.Q<Label>("PriceLabel");
        if (priceLabel != null)
            priceLabel.text = $"{_template.sellPrice:F0}₴";

        // 4. Корінець (колір за жанром)
        var spine = Root.Q<VisualElement>("BookSpine");
        if (spine != null && SpineColors.TryGetValue(_template.genre, out Color spineColor))
            spine.style.backgroundColor = new StyleColor(spineColor);

        // 5. Точка рідкості
        var dot = Root.Q<VisualElement>("RarityDot");
        if (dot != null)
        {
            foreach (var c in new[]{"common","uncommon","rare","epic","legendary"})
                dot.RemoveFromClassList(c);
            dot.AddToClassList(_template.rarity.ToString().ToLower());
        }

        // 6. Tooltip
        Root.tooltip = $"{_template.title}\n{_template.author}\n{_template.rarity} | {_template.genre}";

        // 7. Hover → Book Info Card
        if (onHoverEnter != null)
            Root.RegisterCallback<MouseEnterEvent>(_ => onHoverEnter(_template));
        if (onHoverExit != null)
            Root.RegisterCallback<MouseLeaveEvent>(_ => onHoverExit());

        // 8. Клік
        Root.RegisterCallback<PointerDownEvent>(_ =>
        {
            OnClicked?.Invoke(_instance, _template);
            Debug.Log($"[InventoryItemUI] Обрано: {_template.title}");
        });
    }
}
