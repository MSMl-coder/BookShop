// Assets/Scripts/Data/Props/PropClass.cs
// RENAME: FurnitureClass → PropClass
// ЗМІНИ: додано Seating як окремий клас

/// <summary>
/// Категорія пропа — визначає поведінку при розміщенні та вплив на NPC.
/// </summary>
public enum PropClass
{
    WallShelf,      // Полиця для книг — кріпиться до стіни
    CenterIsland,   // Острівна полиця — ставиться вільно
    Decor,          // Декоративний об'єкт — впливає на Mood NPC
    Seating         // Меблі для сидіння — впливають на Comfort/Patience NPC
}

/// <summary>
/// Підкатегорія декору для diversity bonus.
/// Якщо розміщено 2+ різних категорій — ShopAtmosphereService дає множник.
/// </summary>
public enum DecorCategory
{
    None,
    Plants,         // Рослини
    Lighting,       // Освітлення
    Art,            // Картини, скульптури
    Textiles,       // Килими, штори
    Accessories     // Дрібниці, книгові закладки тощо
}