// Assets/Scripts/World/Items/BookWorldItem.cs
using UnityEngine;

public class BookWorldItem : MonoBehaviour
{
    public BookInstance instance;
    public Shelf parentShelf;

    // ВИПРАВЛЕНО: tilt зберігається в компоненті, не перераховується щоразу
    [HideInInspector]
    public float savedTilt;

    private void Awake()
    {
        // Генеруємо tilt один раз при створенні
        if (parentShelf != null)
            savedTilt = Random.Range(-parentShelf.maxRandomTilt, parentShelf.maxRandomTilt);
    }
}