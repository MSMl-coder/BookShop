// Assets/Scripts/Database/BookDatabaseLoader.cs
using UnityEngine;

// Завантажує та ініціалізує базу даних на старті сцени
public class BookDatabaseLoader : MonoBehaviour
{
    [SerializeField] private BookDatabase database;

    private void Awake()
    {
        if (database == null)
        {
            Debug.LogError("[DatabaseLoader] BookDatabase not assigned!");
            return;
        }

        database.Initialize();
        Debug.Log("[DatabaseLoader] Database ready.");
    }
}