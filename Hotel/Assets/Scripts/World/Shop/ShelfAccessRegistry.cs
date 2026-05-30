// Assets/Scripts/World/Shop/ShelfAccessRegistry.cs
// Singleton — обмежує кількість NPC що одночасно йдуть до однієї полиці.
// Патерн ідентичний SeatRegistry: TryClaim / Release.
//
// Ліміт MAX_PER_SHELF = 2 — задається в Inspector або константою.
// NPC резервує слот перед тим як рушити до полиці.
// Звільняє при виході з Inspecting (або при знищенні).

using System.Collections.Generic;
using UnityEngine;

public class ShelfAccessRegistry : MonoBehaviour
{
    public static ShelfAccessRegistry Instance { get; private set; }

    [Tooltip("Максимальна кількість NPC що одночасно можуть йти до однієї полиці.")]
    [SerializeField] [Range(1, 4)] private int maxPerShelf = 2;

    // shelf → множина NPC instanceID що зараз її таргетують
    private readonly Dictionary<Shelf, HashSet<string>> _claims = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ── Public API ────────────────────────────────────────────────

    /// NPC намагається зайняти слот на полиці.
    /// Повертає true якщо дозволено, false якщо повна.
    public bool TryClaim(Shelf shelf, string npcInstanceID)
    {
        if (shelf == null) return false;

        if (!_claims.TryGetValue(shelf, out var set))
        {
            set = new HashSet<string>();
            _claims[shelf] = set;
        }

        // Вже зарезервував цю полицю — повторний claim без проблем
        if (set.Contains(npcInstanceID)) return true;

        if (set.Count >= maxPerShelf)
        {
            Debug.Log($"[ShelfAccess] '{shelf.name}' full ({set.Count}/{maxPerShelf}), " +
                      $"NPC {npcInstanceID} skip");
            return false;
        }

        set.Add(npcInstanceID);
        Debug.Log($"[ShelfAccess] '{shelf.name}' claimed by {npcInstanceID} " +
                  $"({set.Count}/{maxPerShelf})");
        return true;
    }

    /// NPC звільняє слот (перейшов у інший стан або знищений).
    public void Release(Shelf shelf, string npcInstanceID)
    {
        if (shelf == null) return;
        if (!_claims.TryGetValue(shelf, out var set)) return;

        if (set.Remove(npcInstanceID))
            Debug.Log($"[ShelfAccess] '{shelf.name}' released by {npcInstanceID} " +
                      $"({set.Count}/{maxPerShelf})");
    }

    /// Звільнити всі слоти конкретного NPC (при знищенні).
    public void ReleaseAll(string npcInstanceID)
    {
        foreach (var set in _claims.Values)
            set.Remove(npcInstanceID);
    }

    /// Скільки NPC зараз таргетують цю полицю.
    public int GetCount(Shelf shelf)
    {
        return (_claims.TryGetValue(shelf, out var set)) ? set.Count : 0;
    }

    /// Чи є вільний слот.
    public bool HasSlot(Shelf shelf) => GetCount(shelf) < maxPerShelf;
}