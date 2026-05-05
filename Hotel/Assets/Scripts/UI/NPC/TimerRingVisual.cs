using UnityEngine;
using UnityEngine.UI;

// Attach to TimerRing prefab (needs Image with Fill type)
public class TimerRingVisual : MonoBehaviour
{
    [SerializeField] private Image fillImage; // Radial fill image

    public void SetFill(float normalized)
    {
        if (fillImage != null)
            fillImage.fillAmount = normalized;

        // Color: green → yellow → red
        fillImage.color = Color.Lerp(Color.red, Color.green, normalized);
    }
}