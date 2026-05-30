// Assets/Scripts/UI/NPC/NPCInspectorPanel.cs
// Екранний панель у лівому нижньому куті — показує живі показники NPC при кліку.
//
// UNITY SETUP (Screen Space — Overlay Canvas):
//   InspectorPanel  ← assign до _root (RectTransform, anchored bottom-left)
//   ├── Header
//   │   ├── NPCNameText     ← TextMeshProUGUI
//   │   ├── StateText       ← TextMeshProUGUI (Browsing / Resting / ...)
//   │   └── CloseBtn        ← Button
//   ├── Stats
//   │   ├── MoodRow
//   │   │   ├── Label       "😊 Настрій"
//   │   │   └── MoodBar     ← Image (Filled, Horizontal) — assign moodBar
//   │   ├── ComfortRow
//   │   │   ├── Label       "🛋 Комфорт"
//   │   │   └── ComfortBar  ← assign comfortBar
//   │   ├── PatienceRow
//   │   │   ├── Label       "⏳ Терпіння"
//   │   │   └── PatienceBar ← assign patienceBar
//   │   └── WalletRow
//   │       ├── Label       "💰 Бюджет"
//   │       └── WalletBar   ← assign walletBar
//   └── Genre Row           ← TextMeshProUGUI genreText

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCInspectorPanel : MonoBehaviour
{
    public static NPCInspectorPanel Instance { get; private set; }

    // ── Inspector fields ──────────────────────────────────────────
    [Header("Root")]
    [SerializeField] private GameObject      _root;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI _npcNameText;
    [SerializeField] private TextMeshProUGUI _stateText;
    [SerializeField] private Button          _closeBtn;

    [Header("Stat bars (Image, Filled, Horizontal)")]
    [SerializeField] private Image _moodBar;
    [SerializeField] private Image _comfortBar;
    [SerializeField] private Image _patienceBar;
    [SerializeField] private Image _walletBar;

    [Header("Bar colors")]
    [SerializeField] private Color _positiveColor = new Color(0.25f, 0.80f, 0.35f);
    [SerializeField] private Color _neutralColor  = new Color(1.00f, 0.78f, 0.10f);
    [SerializeField] private Color _negativeColor = new Color(0.90f, 0.25f, 0.20f);

    [Header("Genre / info")]
    [SerializeField] private TextMeshProUGUI _genreText;

    // ── Private ───────────────────────────────────────────────────
    private NPCBrain _currentNPC;

    // ── Unity ─────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (_root != null) _root.SetActive(false);
        if (_closeBtn != null) _closeBtn.onClick.AddListener(Hide);
    }

    private void Update()
    {
        if (_currentNPC == null || _root == null || !_root.activeSelf) return;

        // NPC знищено під час відображення
        if (_currentNPC.gameObject == null)
        {
            Hide();
            return;
        }

        Refresh();
    }

    // ── Public API ────────────────────────────────────────────────

    public void Show(NPCBrain npc)
    {
        if (npc == null) return;

        // Відписуємось від попереднього
        if (_currentNPC != null)
            _currentNPC.OnStateChanged -= OnStateChanged;

        _currentNPC = npc;
        _currentNPC.OnStateChanged += OnStateChanged;

        if (_root != null) _root.SetActive(true);

        Refresh();
        Debug.Log($"[Inspector] Showing NPC: {npc.Data?.npcName}");
    }

    public void Hide()
    {
        if (_currentNPC != null)
            _currentNPC.OnStateChanged -= OnStateChanged;
        _currentNPC = null;

        if (_root != null) _root.SetActive(false);
    }

    /// Ховає панель тільки якщо відображає саме цього NPC (викликається при DestroyNPC)
    public void HideIfShowing(NPCBrain npc)
    {
        if (_currentNPC == npc) Hide();
    }

    // ── Private ───────────────────────────────────────────────────

    private void Refresh()
    {
        if (_currentNPC == null) return;

        var stats = _currentNPC.Ticker?.Stats;

        // Header
        if (_npcNameText != null)
            _npcNameText.text = _currentNPC.Data?.npcName ?? "NPC";

        if (_stateText != null)
            _stateText.text = StateLabel(_currentNPC.CurrentState);

        // Genre
        if (_genreText != null)
            _genreText.text = $"Жанр: {_currentNPC.DesiredGenre}";

        if (stats == null) return;

        // Stat bars: -100..+100 → 0..1
        SetBar(_moodBar,     stats.Mood);
        SetBar(_comfortBar,  stats.Comfort);
        SetBar(_patienceBar, stats.Patience);
        // Wallet вже 0..100
        SetBarDirect(_walletBar, stats.Wallet / 100f);
    }

    private void SetBar(Image bar, float value)
    {
        if (bar == null) return;
        float normalized = Mathf.InverseLerp(NPCStats.MIN, NPCStats.MAX, value);
        bar.fillAmount = normalized;
        bar.color      = ValueToColor(normalized);
    }

    private void SetBarDirect(Image bar, float normalized01)
    {
        if (bar == null) return;
        bar.fillAmount = Mathf.Clamp01(normalized01);
        bar.color      = ValueToColor(normalized01);
    }

    private Color ValueToColor(float t) => t switch
    {
        _ when t > 0.6f => _positiveColor,
        _ when t > 0.3f => _neutralColor,
        _               => _negativeColor
    };

    private void OnStateChanged(NPCState state)
    {
        if (_stateText != null)
            _stateText.text = StateLabel(state);

        // Якщо NPC виходить — закриваємо панель
        if (state == NPCState.Leaving)
        {
            Invoke(nameof(Hide), 1f);
        }
    }

    private static string StateLabel(NPCState state) => state switch
    {
        NPCState.Entering         => "🚪 Заходить",
        NPCState.Browsing         => "🔍 Розглядає",
        NPCState.Inspecting       => "📖 Вивчає полицю",
        NPCState.Resting          => "🛋 Відпочиває",
        NPCState.WaitingForPlayer => "💬 Чекає допомоги",
        NPCState.Buying           => "🛒 Купує",
        NPCState.Leaving          => "🚶 Виходить",
        _                         => "..."
    };
}