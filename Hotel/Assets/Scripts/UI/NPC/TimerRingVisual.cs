using UnityEngine;
using UnityEngine.UI;

// Attach to TimerRing prefab (needs Image with Fill type)
public class TimerRingVisual : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    public void SetFill(float normalized)
    {
        // ВИПРАВЛЕНО: обидві операції тепер всередині null-перевірки
        // Раніше fillImage.color зверталось до fillImage поза блоком if — NullReferenceException
        if (fillImage == null) return;

        fillImage.fillAmount = normalized;
        fillImage.color = Color.Lerp(Color.red, Color.green, normalized);
    }
}
