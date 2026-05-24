// ═══════════════════════════════════════════════════════════
// ToastController.cs — Text-only toast notifications
// Path: Assets/Scripts/UI/Bookshop/Controllers/ToastController.cs
// ═══════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public enum ToastType { Info, Warn, Good }

public class ToastController : MonoBehaviour
{
    public static ToastController Instance { get; private set; }

    [SerializeField] private float toastDuration = 2.8f;

    private VisualElement _area;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    public void Initialize(VisualElement root)
    {
        _area = root.Q<VisualElement>("ToastArea");
    }

    public void Show(string icon, string message, ToastType type = ToastType.Info)
    {
        if (_area == null) return;

        var toast = new VisualElement();
        toast.AddToClassList("toast");
        if (type == ToastType.Warn) toast.AddToClassList("warn");
        if (type == ToastType.Good) toast.AddToClassList("good");

        if (!string.IsNullOrEmpty(icon))
        {
            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("toast-icon");
            iconLabel.style.marginRight = 6;
            toast.Add(iconLabel);
        }

        var msgLabel = new Label(message);
        toast.Add(msgLabel);

        _area.Add(toast);
        StartCoroutine(RemoveAfter(toast, toastDuration));
    }

    private IEnumerator RemoveAfter(VisualElement element, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (element != null && _area.Contains(element))
            _area.Remove(element);
    }
}
