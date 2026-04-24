using UnityEngine;

public class LightingManager : MonoBehaviour
{
    [SerializeField] private Light mainDirectionalLight;
    [SerializeField] private Material skyboxMaterial;
    
    // Викликайте цей метод при зміні сезону або рівня
    public void SetMorningLighting()
    {
        mainDirectionalLight.color = new Color(1f, 0.95f, 0.8f); // Тепле ранкове
        mainDirectionalLight.intensity = 1.2f;
        RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.3f);
        Debug.Log("[LightingManager]: Налаштовано ранкове освітлення.");
    }
}