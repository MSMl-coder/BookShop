using UnityEngine;
using UnityEngine.UIElements;

public class InventoryItemUI
{
    public VisualElement Root { get; private set; }
    private BookInstance _instance;
    private BookTemplate _template;

    public InventoryItemUI(BookInstance instance, VisualTreeAsset xmlAsset)
    {
        // ВАЖЛИВО: використовуємо this, щоб відрізнити поле класу від параметра конструктора
        this._instance = instance;
        this._template = BookDatabase.Instance.GetBook(instance.templateID);

        if (_template == null)
        {
            Debug.LogError($"[UI] Не вдалося знайти шаблон для ID: {instance.templateID}");
            return;
        }

        // 1. Створюємо екземпляр з шаблону (.uxml)
        VisualElement container = xmlAsset.Instantiate();
        
        // 2. Отримуємо чистий VisualElement (минаючи TemplateContainer)
        Root = container.ElementAt(0); 

        // 3. Налаштовуємо іконку книги
        var icon = Root.Q<VisualElement>("BookIcon");
        if (icon != null && _template.icon != null)
        {
            icon.style.backgroundImage = new StyleBackground(_template.icon);
            // Запобігаємо спотворенню картинки
           // Це аналог ScaleToFit (картинка вписується, зберігаючи пропорції)
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }

        // 4. Налаштовуємо ціну (PriceLabel має бути в UXML)
        var label = Root.Q<Label>("PriceLabel");
        if (label != null) 
        {
            label.text = $"{_template.sellPrice}$";
        }

        // 5. Налаштовуємо SmartID або Назву (опціонально, якщо є лейбл для тексту)
        var titleLabel = Root.Q<Label>("BookNameLabel");
        if (titleLabel != null)
        {
            titleLabel.text = _template.title;
        }

        // 6. Подія кліку
        Root.RegisterCallback<PointerDownEvent>(evt => {
            // Тут логіка вибору книги в UI
            Debug.Log($"[UI] Вибрано книгу: {_template.title} (ID: {_template.bookID})");
        });

        // 7. Спливаюча підказка
        Root.tooltip = $"ID: {_template.bookID}\nНазва: {_template.title}\nАвтор: {_template.author}";
    } 
}