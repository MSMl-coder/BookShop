// Enum станів NPC — окремий файл для чистоти
public enum NPCState
{
    Entering,    // NPC входить у крамницю
    Browsing,    // Блукає біля полиць
    Inspecting,  // Розглядає конкретну полицю
    ShowingHint, // Показує іконку жанру (не знайшов сам)
    WaitingForPlayer, // Чекає поки гравець запропонує книгу
    Buying,      // Іде до каси
    Leaving      // Виходить без покупки
}