using UnityEngine;
using TMPro;

// Attach to ThoughtBubble prefab
public class ThoughtBubbleVisual : MonoBehaviour
{
    [SerializeField] private TextMeshPro textMesh;

    public void SetText(string text)
    {
        if (textMesh != null)
            textMesh.text = text;
    }
}