// Assets/Scripts/UI/Components/UIHoverSound.cs
//
// Reusable статичний утиліт для звуку при hover на UI елементах.
// Використовується з будь-якого C# класу — NPCInspectorController,
// GameHUDController тощо.
//
// SETUP:
//   1. Create Empty GO "UIHoverSound"
//   2. Add UIHoverSound.cs
//   3. Add AudioSource component (призначиться автоматично)
//   4. Призначте hoverClip в Inspector
//   5. Опційно: окремий clip для кнопок (clickClip)
//
// ВИКОРИСТАННЯ:
//   UIHoverSound.PlayHover();   // при PointerEnter
//   UIHoverSound.PlayClick();   // при Click

using UnityEngine;
using UnityEngine.UIElements;

public class UIHoverSound : MonoBehaviour
{
    public static UIHoverSound Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;

    [Header("Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float hoverVolume = 0.4f;
    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 0.6f;

    private AudioSource _source;

    // Throttle: не грати звук частіше ніж раз на N секунд
    private float _lastHoverTime = -1f;
    private const float HOVER_COOLDOWN = 0.08f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _source = GetComponent<AudioSource>();
        if (_source == null) _source = gameObject.AddComponent<AudioSource>();

        _source.playOnAwake  = false;
        _source.spatialBlend = 0f; // 2D
    }

    // ── Static API ────────────────────────────────────────────────

    public static void PlayHover()
    {
        if (Instance == null) return;
        Instance.PlayHoverInternal();
    }

    public static void PlayClick()
    {
        if (Instance == null) return;
        Instance.PlayClickInternal();
    }

    /// Зареєструвати hover звук на будь-який VisualElement.
    /// Викликати один раз при створенні елементу.
    public static void RegisterHover(VisualElement element)
    {
        if (element == null) return;
        element.RegisterCallback<PointerEnterEvent>(_ => PlayHover());
    }

    /// Зареєструвати hover + click звук на Button.
    public static void RegisterButton(Button button)
    {
        if (button == null) return;
        button.RegisterCallback<PointerEnterEvent>(_ => PlayHover());
        button.RegisterCallback<ClickEvent>(_ => PlayClick());
    }

    // ── Private ───────────────────────────────────────────────────

    private void PlayHoverInternal()
    {
        if (hoverClip == null) return;

        // Throttle — не спамити звук при швидкому русі миші
        if (Time.unscaledTime - _lastHoverTime < HOVER_COOLDOWN) return;
        _lastHoverTime = Time.unscaledTime;

        _source.PlayOneShot(hoverClip, hoverVolume);
    }

    private void PlayClickInternal()
    {
        if (clickClip == null || _source == null) return;
        _source.PlayOneShot(clickClip, clickVolume);
    }
}