using UnityEngine;

// ScriptableObject з конфігурацією типу покупця
[CreateAssetMenu(fileName = "NPC_", menuName = "Bookstore/NPC Data")]
public class NPCData : ScriptableObject
{
    [Header("Identity")]
    public string npcName = "Visitor";
    public Sprite portrait;
    public GameObject prefab;

    [Header("Behavior")]
    [Tooltip("How long NPC stays in the shop (seconds)")]
    public float stayDuration = 60f;

    [Tooltip("Chance to buy if preferred genre is found (0-1)")]
    [Range(0f, 1f)] public float buyChance = 0.75f;

    [Tooltip("Max price NPC is willing to pay")]
    public float maxBudget = 50f;

    [Header("Preferences")]
    public BookGenre[] preferredGenres;

    [Header("Browse Settings")]
    [Tooltip("Time spent browsing before showing genre hint icon")]
    public float browseTimeBeforeHint = 30f;

    [Tooltip("Number of shelves to inspect")]
    [Range(1, 5)] public int shelvesToInspect = 3;
}