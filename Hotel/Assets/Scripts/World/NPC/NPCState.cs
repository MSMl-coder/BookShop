// Assets/Scripts/World/NPC/NPCState.cs
// ЗМІНИ: додано стан Resting між Browsing та Buying.

public enum NPCState
{
    Entering,         // Входить у крамницю → вітальна хмаринка
    Browsing,         // Блукає між полицями (таймер видимий)
    Inspecting,       // Стоїть біля полиці → крапки пошуку
    Resting,          // [NEW] Сидить на меблях → Comfort росте, Patience відновлюється
    WaitingForPlayer, // Не знайшов → хмаринка з запитом + drop-zone
    Buying,           // Іде до каси
    Leaving           // Виходить
}