// Assets/Scripts/EditMode/GridOverlay.cs
using UnityEngine;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class GridOverlay : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private float cellSize = 0.5f;
    [SerializeField] private int gridRadius = 20; // кількість клітинок від центру
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.06f); // ледь помітний
    [SerializeField] private float fadeInDuration = 0.4f;

    private MeshRenderer _renderer;
    private Material _gridMat;
    private float _alpha;
    private bool _fadingIn;
    private bool _fadingOut;

    // Шейдер: Shader Graphs → Unlit Grid або через MaterialPropertyBlock
    // Простий варіант через проекційний матеріал з тайлінгом
    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _gridMat = new Material(_renderer.sharedMaterial); // інстанс
        _renderer.material = _gridMat;
        _gridMat.SetFloat("_GridCellSize", cellSize);
        _gridMat.SetColor("_LineColor", lineColor);
        _renderer.enabled = false;
        _alpha = 0f;
    }

    public void Show()
    {
        _renderer.enabled = true;
        _fadingIn = true;
        _fadingOut = false;
    }

    public void Hide()
    {
        _fadingOut = true;
        _fadingIn = false;
    }

    private void Update()
    {
        if (_fadingIn)
        {
            _alpha = Mathf.MoveTowards(_alpha, 1f, Time.deltaTime / fadeInDuration);
            _gridMat.SetFloat("_Opacity", _alpha);
            if (_alpha >= 1f) _fadingIn = false;
        }
        else if (_fadingOut)
        {
            _alpha = Mathf.MoveTowards(_alpha, 0f, Time.deltaTime / fadeInDuration);
            _gridMat.SetFloat("_Opacity", _alpha);
            if (_alpha <= 0f) { _fadingOut = false; _renderer.enabled = false; }
        }
    }

    /// Привʼязує позицію до найближчого вузла гриду
    public Vector3 SnapToGrid(Vector3 worldPos)
    {
        float x = Mathf.Round(worldPos.x / cellSize) * cellSize;
        float z = Mathf.Round(worldPos.z / cellSize) * cellSize;
        return new Vector3(x, worldPos.y, z);
    }

    public float CellSize => cellSize;
}