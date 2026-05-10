
// Assets/Scripts/Tutorial/TutorialOutline.cs
// Простий компонент пульсації/обводки для 3D-об'єктів у туторіалі.
// При enabled=true: пульсує емісія матеріалу.
// При enabled=false: скидає до оригінального матеріалу.
 
using UnityEngine;
 
public class TutorialOutline : MonoBehaviour
{
    [SerializeField] private Color  highlightColor = new Color(1f, 0.84f, 0f, 1f); // золотий
    [SerializeField] private float  pulseSpeed     = 2f;
    [SerializeField] private float  pulseIntensity = 0.5f;
 
    private Renderer[]  _renderers;
    private Material[]  _originalMaterials;
    private Material[]  _highlightMaterials;
    private float       _time;
 
    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _originalMaterials = new Material[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originalMaterials[i] = _renderers[i].material;
    }
 
    private void OnEnable()
    {
        _time = 0f;
    }
 
    private void Update()
    {
        _time += Time.deltaTime * pulseSpeed;
        float emission = (Mathf.Sin(_time) * 0.5f + 0.5f) * pulseIntensity;
        Color emissive = highlightColor * emission;
 
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.material.SetColor("_EmissionColor", emissive);
        }
    }
 
    private void OnDisable()
    {
        // Відновлюємо оригінальний матеріал
        if (_renderers == null || _originalMaterials == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _originalMaterials[i] != null)
                _renderers[i].material = _originalMaterials[i];
        }
    }
}