// Assets/Scripts/Core/GameLoop/GameState.cs
public enum GameState
{
    Preparation,   // підготовка, розміщення книг
    EditMode,      // підстан Preparation: редагування меблів (Sims4-стиль)
    WorkDay,       // торгівля, NPC активні, таймер іде
    DayStats,      // екран підсумків дня + списання боргу
    LootPhase,     // вибір нагород (карти)
}