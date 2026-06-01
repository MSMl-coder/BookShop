// Assets/Scripts/World/NPC/NPCState.cs
// ЗМІНИ: додано стан Resting між Browsing та Buying.

public enum NPCState
{
    Entering,
    Browsing,
    Inspecting,
    CollectingBooks,   // [NEW v3] NPC взяв книгу, продовжує збирати решту
    Resting,
    WaitingForPlayer,
    Buying,            // йде до каси
    Leaving,
}