// Assets/Scripts/UI/NPC/NPCInspectorPanel.cs
// SELF-CONTAINED — не потребує UIDocument, UXML або USS.
// Весь UI будується в коді при Awake().
//
// UNITY SETUP (мінімальний):
//   1. Create Empty GameObject на сцені → назви "NPCInspectorPanel"
//   2. Add Component → NPCInspectorPanel
//   ГОТОВО. Більше нічого не потрібно.
//
// Панель з'являється знизу-зліва при кліку на NPC.
// InteractionRouter → ContextMenuUI.ShowForNPC → npc.OnNPCClicked() → тут.

using System;
using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-20)] // ініціалізується раніше за NPCBrain
public class NPCInspectorPanel : MonoBehaviour
{
    public static NPCInspectorPanel Instance { get; private set; }

    // ── UI elements ───────────────────────────────────────────────
    private UIDocument     _doc;
    private VisualElement  _panel;
    private Label          _nameLabel;
    private Label          _stateLabel;
    private Label          _genreLabel;
    private VisualElement  _moodFill;
    private VisualElement  _comfortFill;
    private VisualElement  _patienceFill;
    private VisualElement  _walletFill;

    // ── State ─────────────────────────────────────────────────────
    private NPCBrain _currentNPC;
    private bool     _visible;
    private bool     _ready;

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void Update()
    {
        if (!_visible || _currentNPC == null || !_ready) return;
        if (_currentNPC.gameObject == null) { Hide(); return; }
        RefreshBars();
    }

    // ── Public API ────────────────────────────────────────────────

    public void Show(NPCBrain npc)
    {
        if (npc == null) { Debug.LogWarning("[NPCInspector] Show: npc=null"); return; }
        if (!_ready)     { Debug.LogError("[NPCInspector] Show: UI не збудовано!"); return; }

        // Відписуємось від попереднього
        if (_currentNPC != null)
            _currentNPC.OnStateChanged -= OnNPCStateChanged;

        _currentNPC = npc;
        _currentNPC.OnStateChanged += OnNPCStateChanged;

        // Заповнюємо header
        if (_nameLabel  != null) _nameLabel.text  = npc.Data?.npcName ?? "NPC";
        if (_stateLabel != null) _stateLabel.text = StateLabel(npc.CurrentState);
        if (_genreLabel != null) _genreLabel.text = $"Жанр: {npc.DesiredGenre}";
        RefreshBars();

        // Показати
        _panel.style.display  = DisplayStyle.Flex;
        _panel.pickingMode    = PickingMode.Position;
        _visible = true;

        Debug.Log($"[NPCInspector] Showing: {npc.Data?.npcName}");
    }

    public void Hide()
    {
        if (_currentNPC != null)
        {
            _currentNPC.OnStateChanged -= OnNPCStateChanged;
            _currentNPC = null;
        }
        if (_panel != null)
        {
            _panel.style.display = DisplayStyle.None;
            _panel.pickingMode   = PickingMode.Ignore;
        }
        _visible = false;
    }

    public void HideIfShowing(NPCBrain npc)
    {
        if (_currentNPC == npc) Hide();
    }

    // ── Build UI entirely in code ─────────────────────────────────

    private void BuildUI()
    {
        // Створюємо UIDocument на цьому ж GO
        _doc = gameObject.AddComponent<UIDocument>();

        // PanelSettings — шукаємо будь-який існуючий
        var existingDoc = FindAnyOtherUIDocument();
        if (existingDoc?.panelSettings != null)
            _doc.panelSettings = existingDoc.panelSettings;
        else
            Debug.LogWarning("[NPCInspector] PanelSettings не знайдено. " +
                             "Призначте вручну: NPCInspectorPanel → UIDocument → Panel Settings.");

        _doc.sortingOrder = 50; // над основним UI

        var root = _doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.width    = new Length(100, LengthUnit.Percent);
        root.style.height   = new Length(100, LengthUnit.Percent);

        // ── Панель ────────────────────────────────────────────────
        _panel = new VisualElement();
        _panel.style.position       = Position.Absolute;
        _panel.style.bottom         = 28;
        _panel.style.left           = 20;
        _panel.style.width          = 250;
        _panel.style.backgroundColor = new Color(0.11f, 0.09f, 0.06f, 0.96f);
        _panel.style.borderTopWidth = _panel.style.borderBottomWidth =
        _panel.style.borderLeftWidth = _panel.style.borderRightWidth = 1;
        SetBorderColor(_panel, new Color(0.62f, 0.47f, 0.20f));
        _panel.style.borderTopLeftRadius = _panel.style.borderTopRightRadius =
        _panel.style.borderBottomLeftRadius = _panel.style.borderBottomRightRadius = 6;
        _panel.style.paddingTop = _panel.style.paddingBottom = 10;
        _panel.style.paddingLeft = _panel.style.paddingRight = 12;
        _panel.style.display  = DisplayStyle.None;
        _panel.pickingMode    = PickingMode.Ignore;
        root.Add(_panel);

        // ── Header row ────────────────────────────────────────────
        var headerRow = Row();
        _nameLabel = Label("Відвідувач",
            new Color(0.91f, 0.82f, 0.58f), 13, bold: true);
        _nameLabel.style.flexGrow = 1;
        headerRow.Add(_nameLabel);

        var closeBtn = new Button(Hide) { text = "✕" };
        closeBtn.style.backgroundColor = new Color(0,0,0,0);
        closeBtn.style.borderTopWidth = closeBtn.style.borderBottomWidth =
        closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth = 0;
        closeBtn.style.color   = new StyleColor(new Color(0.62f, 0.47f, 0.20f));
        closeBtn.style.fontSize = 12;
        closeBtn.style.width  = 22;
        closeBtn.style.height = 22;
        closeBtn.style.paddingTop = closeBtn.style.paddingBottom =
        closeBtn.style.paddingLeft = closeBtn.style.paddingRight = 0;
        closeBtn.pickingMode = PickingMode.Position;
        headerRow.Add(closeBtn);
        _panel.Add(headerRow);

        _stateLabel = Label("Розглядає", new Color(0.70f, 0.63f, 0.39f), 10);
        _stateLabel.style.marginTop = 3;
        _panel.Add(_stateLabel);

        _genreLabel = Label("Жанр: —", new Color(0.51f, 0.43f, 0.27f), 9);
        _genreLabel.style.marginTop = 1;
        _panel.Add(_genreLabel);

        // ── Divider ───────────────────────────────────────────────
        var div = new VisualElement();
        div.style.height = 1;
        div.style.backgroundColor = new Color(0.27f, 0.21f, 0.11f);
        div.style.marginTop = div.style.marginBottom = 7;
        _panel.Add(div);

        // ── Stat bars ─────────────────────────────────────────────
        _moodFill    = AddStatRow("😊", "Настрій",  new Color(0.31f, 0.71f, 0.35f));
        _comfortFill = AddStatRow("🛋", "Комфорт",  new Color(0.24f, 0.55f, 0.82f));
        _patienceFill = AddStatRow("⏳", "Терпіння", new Color(0.86f, 0.71f, 0.20f));
        _walletFill  = AddStatRow("💰", "Бюджет",   new Color(0.71f, 0.55f, 0.20f));

        _ready = true;
        Debug.Log("[NPCInspector] UI збудовано успішно");
    }

    // ── Helpers ───────────────────────────────────────────────────

    private VisualElement AddStatRow(string icon, string labelText, Color fillColor)
    {
        var row = Row();
        row.style.marginBottom = 5;

        var iconLbl = Label(icon, Color.white, 11);
        iconLbl.style.width = 16;
        iconLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
        row.Add(iconLbl);

        var nameLbl = Label(labelText, new Color(0.63f, 0.55f, 0.35f), 9);
        nameLbl.style.width = 60;
        nameLbl.style.marginLeft = 4;
        row.Add(nameLbl);

        var track = new VisualElement();
        track.style.flexGrow = 1;
        track.style.height = 6;
        track.style.backgroundColor = new Color(0.20f, 0.16f, 0.09f);
        track.style.borderTopLeftRadius = track.style.borderTopRightRadius =
        track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 3;
        track.style.overflow = Overflow.Hidden;
        track.style.marginLeft = 6;
        row.Add(track);

        var fill = new VisualElement();
        fill.style.height = new Length(100, LengthUnit.Percent);
        fill.style.width  = new Length(50, LengthUnit.Percent);
        fill.style.backgroundColor = fillColor;
        fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius =
        fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius = 3;
        track.Add(fill);

        _panel.Add(row);
        return fill;
    }

    private void RefreshBars()
    {
        var s = _currentNPC?.Ticker?.Stats;
        if (s == null) return;
        SetFill(_moodFill,     s.Mood,     NPCStats.MIN, NPCStats.MAX);
        SetFill(_comfortFill,  s.Comfort,  NPCStats.MIN, NPCStats.MAX);
        SetFill(_patienceFill, s.Patience, NPCStats.MIN, NPCStats.MAX);
        SetFill(_walletFill,   s.Wallet,   NPCStats.WALLET_MIN, NPCStats.WALLET_MAX);
    }

    private static void SetFill(VisualElement fill, float value, float min, float max)
    {
        if (fill == null) return;
        float t = Mathf.InverseLerp(min, max, value);
        fill.style.width = new Length(Mathf.Max(t * 100f, 1f), LengthUnit.Percent);

        Color c = t < 0.3f ? new Color(0.82f, 0.27f, 0.20f)
                : t < 0.6f ? new Color(0.82f, 0.63f, 0.20f)
                :             fill.style.backgroundColor.value; // залишаємо оригінальний
        if (t < 0.6f) fill.style.backgroundColor = c;
    }

    private void OnNPCStateChanged(NPCState state)
    {
        if (_stateLabel != null) _stateLabel.text = StateLabel(state);
        if (state == NPCState.Leaving) Invoke(nameof(Hide), 1.5f);
    }

    private static VisualElement Row()
    {
        var ve = new VisualElement();
        ve.style.flexDirection = FlexDirection.Row;
        ve.style.alignItems    = Align.Center;
        return ve;
    }

    private static Label Label(string text, Color color, int fontSize, bool bold = false)
    {
        var lbl = new UnityEngine.UIElements.Label(text);
        lbl.style.color   = new StyleColor(color);
        lbl.style.fontSize = fontSize;
        if (bold) lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        return lbl;
    }

    private static void SetBorderColor(VisualElement ve, Color c)
    {
        ve.style.borderTopColor    = new StyleColor(c);
        ve.style.borderBottomColor = new StyleColor(c);
        ve.style.borderLeftColor   = new StyleColor(c);
        ve.style.borderRightColor  = new StyleColor(c);
    }

    private static UIDocument FindAnyOtherUIDocument()
    {
        foreach (var d in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
            if (d != null && d.panelSettings != null) return d;
        return null;
    }

    private static string StateLabel(NPCState s) => s switch
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