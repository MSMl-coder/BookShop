using UnityEngine;

// Attach to GenreIcon prefab (needs SpriteRenderer)
public class GenreIconVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer iconRenderer;

    public void SetIcon(Sprite sprite)
    {
        if (iconRenderer != null)
            iconRenderer.sprite = sprite;
    }
}