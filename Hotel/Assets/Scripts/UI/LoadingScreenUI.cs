using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class LoadingScreenUI : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image fillBar;
    [SerializeField] private TMPro.TextMeshProUGUI loadingText;

    private void OnEnable()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.OnLoadProgress += UpdateProgress;
    }

    private void OnDisable()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.OnLoadProgress -= UpdateProgress;
    }

    private void UpdateProgress(float progress)
    {
        if (fillBar != null) fillBar.fillAmount = progress;
        if (loadingText != null) 
            loadingText.text = $"Завантаження... {Mathf.Round(progress * 100)}%";
    }
}