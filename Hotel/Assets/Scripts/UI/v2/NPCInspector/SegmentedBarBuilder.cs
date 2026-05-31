// Assets/Scripts/UI/Components/NPCInspector/SegmentedBarBuilder.cs
//
// Варіант 2: псевдо-тінь через обгортку.
// Кожен сегмент = shadow wrapper (темний зсунутий фон) + сам сегмент зверху.

using UnityEngine;
using UnityEngine.UIElements;

public class SegmentedBar
{
    public const int DEFAULT_SEGMENTS = 10;

    private readonly VisualElement   _track;
    private readonly VisualElement[] _segments; // самі капсули (не shadow)
    private readonly string          _statName;
    private float                    _currentValue = -1f;

    public SegmentedBar(VisualElement track, string statName, int segmentCount = DEFAULT_SEGMENTS)
    {
        _track     = track;
        _statName  = statName;
        _segments  = new VisualElement[segmentCount];
        Build(segmentCount);
    }

    public void SetValue(float value01)
    {
        value01 = Mathf.Clamp01(value01);
        if (Mathf.Abs(value01 - _currentValue) < (1f / _segments.Length) * 0.5f) return;
        _currentValue = value01;

        int  activeCount = Mathf.RoundToInt(value01 * _segments.Length);
        bool isLow       = value01 < 0.30f;
        string activeClass = $"active--{_statName}";

        for (int i = 0; i < _segments.Length; i++)
        {
            bool active = i < activeCount;
            var seg = _segments[i];
            seg.EnableInClassList(activeClass, active);
            seg.EnableInClassList("low", active && isLow);
        }
    }

    public void Reset()
    {
        _currentValue = 0f;
        string activeClass = $"active--{_statName}";
        foreach (var seg in _segments)
        {
            seg.RemoveFromClassList(activeClass);
            seg.RemoveFromClassList("low");
        }
    }

    private void Build(int count)
    {
        _track.Clear();

        for (int i = 0; i < count; i++)
        {
            // ── Shadow wrapper ──────────────────────────────────
            var wrapper = new VisualElement();
            wrapper.AddToClassList("stat-segment-shadow");
            if (i == count - 1) wrapper.AddToClassList("last");

            // ── Сегмент поверх shadow ───────────────────────────
            var seg = new VisualElement();
            seg.AddToClassList("stat-segment");

            wrapper.Add(seg);
            _track.Add(wrapper);
            _segments[i] = seg; // зберігаємо сегмент для SetValue
        }
    }
}
