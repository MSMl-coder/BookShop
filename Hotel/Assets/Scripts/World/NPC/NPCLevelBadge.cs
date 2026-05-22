// Assets/Scripts/World/NPC/NPCLevelBadge.cs
// Зірка-prefab крутиться НАВКОЛО голови NPC по орбіті.
// Цифра рівня — TextMeshPro billboard по центру.
// Кольори рівнів налаштовуються в Inspector через LevelColorEntry[].

using UnityEngine;
using TMPro;
using System;

[RequireComponent(typeof(NPCBrain))]
public class NPCLevelBadge : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────

    [Header("Prefab")]
    [SerializeField] private GameObject starPrefab;

    [Header("Orbit")]
    [SerializeField] private float heightOffset = 2.3f;
    [SerializeField] private float orbitRadius  = 0.35f;
    [SerializeField] private float orbitSpeed   = 120f;
    [SerializeField] private float selfRotSpeed = 200f;
    [SerializeField] private float starScale    = 1f;

    [Header("Level Text")]
    [SerializeField] private TMP_FontAsset levelFont;
    [SerializeField] private float         fontSize  = 3.0f;
    [SerializeField] private Color         textColor = Color.white;

    [Header("Level Colors")]
    [Tooltip("Налаштуй кольори для кожного діапазону рівнів.\n" +
             "Записи сортуються по minLevel. Перший відповідний — використовується.\n" +
             "Дефолт: сірий 1-3, синій 4-6, золотий 7-9, фіолетовий 10.")]
    [SerializeField] private LevelColorEntry[] levelColors = new LevelColorEntry[]
    {
        new LevelColorEntry { minLevel = 1,  color = new Color(0.75f, 0.75f, 0.75f) }, // сірий
        new LevelColorEntry { minLevel = 4,  color = new Color(0.25f, 0.55f, 1.00f) }, // синій
        new LevelColorEntry { minLevel = 7,  color = new Color(1.00f, 0.80f, 0.10f) }, // золотий
        new LevelColorEntry { minLevel = 10, color = new Color(0.65f, 0.15f, 1.00f) }, // фіолетовий
    };

    [Header("Emission")]
    [Tooltip("Інтенсивність emission (glow) матеріалу зірки залежно від рівня.\n" +
             "Записи сортуються по minLevel.")]
    [SerializeField] private LevelEmissionEntry[] emissionLevels = new LevelEmissionEntry[]
    {
        new LevelEmissionEntry { minLevel = 1,  intensity = 0.5f },
        new LevelEmissionEntry { minLevel = 4,  intensity = 1.5f },
        new LevelEmissionEntry { minLevel = 7,  intensity = 3.0f },
        new LevelEmissionEntry { minLevel = 10, intensity = 5.0f },
    };

    // ── Serializable helpers ─────────────────────────────────────

    [Serializable]
    public struct LevelColorEntry
    {
        [Tooltip("Мінімальний рівень для цього кольору")]
        public int   minLevel;
        public Color color;
    }

    [Serializable]
    public struct LevelEmissionEntry
    {
        [Tooltip("Мінімальний рівень для цієї інтенсивності")]
        public int   minLevel;
        public float intensity;
    }

    // ── Runtime ──────────────────────────────────────────────────

    private GameObject _badgeRoot;
    private Transform  _orbitPivot;
    private GameObject _starInstance;
    private TMP_Text   _levelText;
    private Camera     _cam;
    private int        _currentLevel = 0;
    private bool       _initialized  = false;

    // ── Unity ────────────────────────────────────────────────────

    private void Start()
    {
        _cam = Camera.main;
        BuildBadge();
        _initialized = true;
    }

    private void OnDestroy()
    {
        if (_badgeRoot != null) Destroy(_badgeRoot);
    }

    private void LateUpdate()
    {
        if (!_initialized || _badgeRoot == null) return;

        // Слідкуємо за позицією NPC
        _badgeRoot.transform.position = transform.position + Vector3.up * heightOffset;

        // Орбіта
        if (_orbitPivot != null)
            _orbitPivot.Rotate(0f, orbitSpeed * Time.deltaTime, 0f, Space.Self);

        // Власне обертання зірки
        if (_starInstance != null)
            _starInstance.transform.Rotate(0f, selfRotSpeed * Time.deltaTime, 0f, Space.Self);

        // Billboard тексту
        if (_levelText != null)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
                _levelText.transform.rotation = Quaternion.LookRotation(
                    _levelText.transform.position - _cam.transform.position);
        }
    }

    // ── Public API ───────────────────────────────────────────────

    public void SetLevel(int level)
    {
        _currentLevel = Mathf.Clamp(level, 1, 10);

        if (_levelText != null)
            _levelText.text = _currentLevel.ToString();

        ApplyLevelColor(_currentLevel);
    }

    // ── Build ────────────────────────────────────────────────────

    private void BuildBadge()
    {
        _badgeRoot = new GameObject($"[LevelBadge] {gameObject.name}");
        _badgeRoot.transform.position = transform.position + Vector3.up * heightOffset;

        var pivotGO = new GameObject("OrbitPivot");
        pivotGO.transform.SetParent(_badgeRoot.transform, false);
        _orbitPivot = pivotGO.transform;

        if (starPrefab != null)
        {
            _starInstance = Instantiate(starPrefab, pivotGO.transform);
            _starInstance.name = "StarInstance";
            _starInstance.transform.localPosition = new Vector3(orbitRadius, 0f, 0f);
            _starInstance.transform.localScale    = Vector3.one * starScale;
        }
        else
        {
            Debug.LogWarning("[NPCLevelBadge] starPrefab не призначено!");
        }

        BuildLevelText();

        if (_currentLevel > 0) ApplyLevelColor(_currentLevel);
    }

    private void BuildLevelText()
    {
        var textGO = new GameObject("LevelText");
        textGO.transform.SetParent(_badgeRoot.transform, false);
        textGO.transform.localPosition = Vector3.zero;

        _levelText                    = textGO.AddComponent<TextMeshPro>();
        _levelText.text               = _currentLevel > 0 ? _currentLevel.ToString() : "?";
        _levelText.fontSize           = fontSize;
        _levelText.alignment          = TextAlignmentOptions.Center;
        _levelText.color              = textColor;
        _levelText.fontStyle          = FontStyles.Bold;
        _levelText.enableWordWrapping = false;

        if (levelFont != null) _levelText.font = levelFont;

        var rect = _levelText.GetComponent<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(0.5f, 0.5f);
    }

    // ── Color ────────────────────────────────────────────────────

    private void ApplyLevelColor(int level)
    {
        if (_starInstance == null) return;

        Color col       = GetColorForLevel(level);
        float emission  = GetEmissionForLevel(level);

        var block = new MaterialPropertyBlock();
        foreach (var r in _starInstance.GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            block.SetColor("_Color",         col);
            block.SetColor("_BaseColor",     col);
            block.SetColor("_EmissionColor", col * emission);
            r.SetPropertyBlock(block);

            foreach (var mat in r.materials)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", col * emission);
            }
        }
    }

    private Color GetColorForLevel(int level)
    {
        // Останній запис де minLevel <= level
        Color result = levelColors.Length > 0 ? levelColors[0].color : Color.white;
        foreach (var entry in levelColors)
        {
            if (level >= entry.minLevel) result = entry.color;
            else break;
        }
        return result;
    }

    private float GetEmissionForLevel(int level)
    {
        float result = emissionLevels.Length > 0 ? emissionLevels[0].intensity : 1f;
        foreach (var entry in emissionLevels)
        {
            if (level >= entry.minLevel) result = entry.intensity;
            else break;
        }
        return result;
    }
}