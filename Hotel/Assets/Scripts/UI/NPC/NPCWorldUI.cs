// Assets/Scripts/UI/NPC/NPCWorldUI.cs
// REDESIGN v3:
//   - Прибрано всю логіку хмаринки (bubble, dots, want panel)
//   - Залишилась ТІЛЬКИ зірка над головою:
//       • обертається постійно
//       • колір змінюється по стану (зелений / жовтий / червоний)
//   - ShowRejectionFeedback() — тепер просто shake зірки
//
// PREFAB SETUP (World Space Canvas):
//   NPC_UI_Canvas
//   └── StarIcon  ← Image з спрайтом зірки (assign starImage)
//
// Хмаринка WaitingForPlayer більше не показується над NPC —
// вся інформація перенесена до NPCInspectorPanel (екранний UI лівий низ).

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NPCBrain))]
public class NPCWorldUI : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────
    [Header("Star icon (World Space Canvas)")]
    [SerializeField] private Image starImage;

    [Header("Star colors за станом")]
    [SerializeField] private Color colorNormal   = new Color(0.25f, 0.85f, 0.35f); // зелений
    [SerializeField] private Color colorSearching = new Color(1.00f, 0.80f, 0.10f); // жовтий
    [SerializeField] private Color colorLeaving  = new Color(0.90f, 0.25f, 0.20f); // червоний
    [SerializeField] private Color colorResting  = new Color(0.30f, 0.65f, 1.00f); // синій

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 90f; // градусів/сек

    [Header("Shake (rejection)")]
    [SerializeField] private float shakeIntensity = 8f;
    [SerializeField] private float shakeDuration  = 0.35f;

    // ── Private ──────────────────────────────────────────────────
    private NPCBrain    _brain;
    private Camera      _mainCamera;
    private Transform   _starTransform;
    private Coroutine   _shakeCoroutine;
    private Vector3     _starBasePos;

    // ── Unity ────────────────────────────────────────────────────
    private void Awake()
    {
        _brain = GetComponent<NPCBrain>();

        if (starImage != null)
        {
            _starTransform = starImage.rectTransform;
            _starBasePos   = _starTransform.localPosition;
        }
    }

    private void OnEnable()
    {
        if (_brain != null)
            _brain.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (_brain != null)
            _brain.OnStateChanged -= HandleStateChanged;
    }

    private void Update()
    {
        BillboardStar();
        RotateStar();
    }

    // ── State handler ─────────────────────────────────────────────
    private void HandleStateChanged(NPCState state)
    {
        if (starImage == null) return;

        starImage.color = state switch
        {
            NPCState.Entering         => colorNormal,
            NPCState.Browsing         => colorNormal,
            NPCState.Inspecting       => colorSearching,
            NPCState.WaitingForPlayer => colorSearching,
            NPCState.Resting          => colorResting,
            NPCState.Buying           => colorNormal,
            NPCState.Leaving          => colorLeaving,
            _                         => colorNormal
        };

        // Leaving — ховаємо зірку
        if (starImage.gameObject != null)
            starImage.gameObject.SetActive(state != NPCState.Leaving);
    }

    // ── Rejection feedback — тільки shake ─────────────────────────
    public void ShowRejectionFeedback(string reason = "")
    {
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(ShakeStar());

        // Зірка ненадовго стає червоною
        if (starImage != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashColor(colorLeaving, 0.5f));
        }
    }

    // ── Animations ────────────────────────────────────────────────
    private void RotateStar()
    {
        if (_starTransform == null) return;
        _starTransform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);
    }

    private void BillboardStar()
    {
        if (starImage == null) return;

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        // Canvas у World Space — повертаємо до камери
        Transform canvasParent = starImage.canvas?.transform;
        if (canvasParent == null) return;
        canvasParent.LookAt(canvasParent.position + _mainCamera.transform.rotation * Vector3.forward,
                            _mainCamera.transform.rotation * Vector3.up);
    }

    private IEnumerator ShakeStar()
    {
        if (_starTransform == null) yield break;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float s = shakeIntensity * (1f - elapsed / shakeDuration);
            _starTransform.localPosition = _starBasePos + (Vector3)(Random.insideUnitCircle * s);
            yield return null;
        }
        _starTransform.localPosition = _starBasePos;
    }

    private IEnumerator FlashColor(Color flash, float duration)
    {
        if (starImage == null) yield break;

        Color original = starImage.color;
        starImage.color = flash;
        yield return new WaitForSeconds(duration);
        if (starImage != null) starImage.color = original;
    }
}