// NPCState.cs
// ЗМІНИ: видалено ShowingHint (дублював WaitingForPlayer).
// WaitingForPlayer тепер єдиний стан "жду допомоги гравця".

public enum NPCState
{
    Entering,         // Входить у крамницю → вітальна хмаринка
    Browsing,         // Блукає між полицями (без хмаринки)
    Inspecting,       // Стоїть біля полиці → хмаринка з крапками
    WaitingForPlayer, // Не знайшов → хмаринка з запитом + drop-zone
    Buying,           // Іде до каси → хмаринка згортається
    Leaving           // Виходить без покупки → хмаринка зникає
}