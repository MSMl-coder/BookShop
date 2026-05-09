// Assets/Scripts/UI/HudTagController.cs
// ВИПРАВЛЕНО:
//   1. Пошук елементів всередині ui:Instance ("HudTagInstance")
//   2. Коректна підписка на flip без ref (closure-based)
//   3. Додано null-guard у всіх зверненнях до _phaseCard
//   4. Сумісний з PhaseWidgetController (не дублює GameLoop-виклики)

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class HudTagController : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────
    [SerializeField] private UIDocument  uiDocument;
    [SerializeField] private ShopTagData tagData;
    [SerializeField] private bool        showPrestigeTag = true;

    // ── Cached elements ──────────────────────────────────────────

    // Коренева точка пошуку — сам інстанс HudTag
    private VisualElement _hudRoot;

    // Tag A
    private VisualElement _tagAFlipper;
    private Label         _tagAAmount;
    private Label         _tagADay;
    private Label         _tagABackLevel;
    private Label         _tagABackRank;
    private Label         _tagABackClubStars;
    private Label         _tagABackClubMembers;

    // Tag B
    private VisualElement _tagBAssembly;
    private VisualElement _tagBFlipper;
    private Label         _tagBLevel;
    private Label         _tagBRank;
    private VisualElement _tagBBarFill;
    private Label         _tagBBarMin;
    private Label         _tagBBarMax;
    private Label         _tagBBackNextInfo;

    // Phase envelope
    private VisualElement _phaseCard;
    private Label         _phaseCardName;
    private Label         _phaseCardStep;
    private Label         _phaseCardStamp;
    private VisualElement _phaseDot0, _phaseDot1, _phaseDot2;

    // Bonus strip
    private VisualElement _bonusStrip;

    // ── State ────────────────────────────────────────────────────
    private bool _tagAFlipped;
    private bool _tagBFlipped;
    private bool _phaseAnimating;

    private static readonly string[] PhaseNames   = { "ПІДГОТОВКА", "ТОРГІВЛЯ", "НАГОРОДИ" };
    private static readonly string[] PhaseSteps   = { "1 з 3",      "2 з 3",    "3 з 3"   };
    private static readonly string[] StampTexts   = { "ЗАКРИТО",    "АКТИВНО",  "ВИКОНАНО" };
    private static readonly string[] StampClasses = { "stamp--prep","stamp--trade","stamp--reward" };

    // ── Unity lifecycle ──────────────────────────────────────────

    private void OnEnable()
    {
        var docRoot = uiDocument?.rootVisualElement;
        if (docRoot == null) return;

        // HudTag вставлений як ui:Instance → шукаємо його контейнер
        // Unity створює VisualElement з ім'ям, що вказане в name="" атрибуті Instance
        _hudRoot = docRoot.Q<VisualElement>("HudTagInstance");

        // Якщо Instance не знайдено — спробуємо шукати HudTagRoot напряму
        // (на випадок коли HudTag підключений як окремий UIDocument)
        if (_hudRoot == null)
            _hudRoot = docRoot.Q<VisualElement>("HudTagRoot") ?? docRoot;

        CacheElements();
        InitTagData();
        BindClicks();

        // Events
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged += UpdateMoney;
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged  += UpdatePhase;
            GameLoopManager.Instance.OnNewDayStarted += UpdateDay;
        }
        if (BonusManager.Instance != null)
            BonusManager.Instance.OnBonusesChanged += RebuildBonusStrip;

        // Initial
        UpdateMoney(EconomyManager.Instance?.Money ?? 0);
        UpdateDay(GameLoopManager.Instance?.CurrentDay ?? 1);
        UpdatePhase(GameLoopManager.Instance?.CurrentState ?? GameState.Preparation);

        var bonuses = BonusManager.Instance?.GetBonuses() ?? new List<ActiveBonus>();
        RebuildBonusStrip(bonuses);

        UpdatePrestigeTag(ClubPrestigeCalculator.Instance?.CurrentPrestige ?? 0, 1000);
    }

    private void OnDisable()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged -= UpdateMoney;
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged  -= UpdatePhase;
            GameLoopManager.Instance.OnNewDayStarted -= UpdateDay;
        }
        if (BonusManager.Instance != null)
            BonusManager.Instance.OnBonusesChanged -= RebuildBonusStrip;
    }

    // ── Cache ────────────────────────────────────────────────────

    private void CacheElements()
    {
        if (_hudRoot == null) return;

        // Tag A
        _tagAFlipper         = _hudRoot.Q<VisualElement>("TagAFlipper");
        _tagAAmount          = _hudRoot.Q<Label>("TagAAmount");
        _tagADay             = _hudRoot.Q<Label>("TagADay");
        _tagABackLevel       = _hudRoot.Q<Label>("TagABackLevel");
        _tagABackRank        = _hudRoot.Q<Label>("TagABackRank");
        _tagABackClubStars   = _hudRoot.Q<Label>("TagABackClubStars");
        _tagABackClubMembers = _hudRoot.Q<Label>("TagABackClubMembers");

        // Tag B
        _tagBAssembly   = _hudRoot.Q<VisualElement>("TagBAssembly");
        _tagBFlipper    = _hudRoot.Q<VisualElement>("TagBFlipper");
        _tagBLevel      = _hudRoot.Q<Label>("TagBLevel");
        _tagBRank       = _hudRoot.Q<Label>("TagBRank");
        _tagBBarFill    = _hudRoot.Q<VisualElement>("TagBBarFill");
        _tagBBarMin     = _hudRoot.Q<Label>("TagBBarMin");
        _tagBBarMax     = _hudRoot.Q<Label>("TagBBarMax");
        _tagBBackNextInfo = _hudRoot.Q<Label>("TagBBackNextInfo");

        // Phase
        _phaseCard      = _hudRoot.Q<VisualElement>("PhaseCardActive");
        _phaseCardName  = _hudRoot.Q<Label>("PhaseCardName");
        _phaseCardStep  = _hudRoot.Q<Label>("PhaseCardStep");
        _phaseCardStamp = _hudRoot.Q<Label>("PhaseCardStamp");
        _phaseDot0      = _hudRoot.Q<VisualElement>("PhaseDot0");
        _phaseDot1      = _hudRoot.Q<VisualElement>("PhaseDot1");
        _phaseDot2      = _hudRoot.Q<VisualElement>("PhaseDot2");

        // Bonus
        _bonusStrip = _hudRoot.Q<VisualElement>("BonusStrip");

        // Tag B visibility
        if (_tagBAssembly != null)
            _tagBAssembly.style.display = showPrestigeTag ? DisplayStyle.Flex : DisplayStyle.None;

        // Debug
        if (_tagAFlipper == null) Debug.LogWarning("[HudTag] TagAFlipper не знайдено");
        if (_phaseCard   == null) Debug.LogWarning("[HudTag] PhaseCardActive не знайдено");
        if (_bonusStrip  == null) Debug.LogWarning("[HudTag] BonusStrip не знайдено");
    }

    private void InitTagData()
    {
        if (tagData == null) return;
        var brand  = _hudRoot?.Q<Label>("TagABrand");
        var series = _hudRoot?.Q<Label>("TagASeries");
        if (brand  != null) brand.text  = tagData.shopName;
        if (series != null) series.text = tagData.shopSubtitle;
    }

    // ── Click / Flip ─────────────────────────────────────────────

    private void BindClicks()
    {
        // Tag A flip
        if (_tagAFlipper != null)
            _tagAFlipper.RegisterCallback<ClickEvent>(_ =>
            {
                _tagAFlipped = !_tagAFlipped;
                _tagAFlipper.EnableInClassList("flipped", _tagAFlipped);
            });

        // Tag B flip
        if (_tagBFlipper != null)
            _tagBFlipper.RegisterCallback<ClickEvent>(_ =>
            {
                _tagBFlipped = !_tagBFlipped;
                _tagBFlipper.EnableInClassList("flipped", _tagBFlipped);
            });

        // Phase envelope click
        var envelope = _hudRoot?.Q<VisualElement>("PhaseEnvelope");
        envelope?.RegisterCallback<ClickEvent>(_ => TryAdvancePhase());

        _phaseCard?.RegisterCallback<ClickEvent>(_ => TryAdvancePhase());
    }

    // ── Money / Day ──────────────────────────────────────────────

    private void UpdateMoney(int amount)
    {
        if (_tagAAmount != null)
            _tagAAmount.text = amount.ToString("N0");
    }

    private void UpdateDay(int day)
    {
        if (_tagADay != null)
            _tagADay.text = day.ToString();
    }

    // ── Phase ────────────────────────────────────────────────────

    private void UpdatePhase(GameState state)
    {
        // Застосовуємо одразу без анімації при зовнішній зміні
        ApplyPhaseData((int)state);
    }

    /// Клік по конверту: просить GameLoopManager перейти до наступного стану
    /// (не дублює PhaseWidgetController — просто запускає офіційний перехід)
    private void TryAdvancePhase()
    {
        if (_phaseAnimating) return;

        var loop = GameLoopManager.Instance;
        if (loop == null) return;

        switch (loop.CurrentState)
        {
            case GameState.Preparation:
                loop.StartWorkDay();
                break;
            case GameState.WorkDay:
                loop.EndWorkDay();
                break;
            // LootPhase — натискання на конверт нічого не робить,
            // щоб не конфліктувати з LootPanelUI
        }
    }

    private IEnumerator AnimatePhaseTransition(int nextIdx)
    {
        _phaseAnimating = true;

        if (_phaseCard != null)
        {
            _phaseCard.AddToClassList("phase-card--hiding");
            yield return new WaitForSeconds(0.4f);
        }

        ApplyPhaseData(nextIdx);

        if (_phaseCard != null)
        {
            _phaseCard.RemoveFromClassList("phase-card--hiding");
            _phaseCard.AddToClassList("phase-card--entering");
            yield return null;
            _phaseCard.RemoveFromClassList("phase-card--entering");
            _phaseCard.AddToClassList("phase-card--show");
            yield return new WaitForSeconds(0.55f);
            _phaseCard.RemoveFromClassList("phase-card--show");
        }

        _phaseAnimating = false;
    }

    private void ApplyPhaseData(int idx)
    {
        if (idx < 0 || idx >= PhaseNames.Length) return;

        if (_phaseCardName  != null) _phaseCardName.text  = PhaseNames[idx];
        if (_phaseCardStep  != null) _phaseCardStep.text  = PhaseSteps[idx];

        if (_phaseCardStamp != null)
        {
            foreach (var c in StampClasses) _phaseCardStamp.RemoveFromClassList(c);
            _phaseCardStamp.AddToClassList(StampClasses[idx]);
            _phaseCardStamp.text = StampTexts[idx];
        }

        UpdatePhaseDots(idx);
    }

    private void UpdatePhaseDots(int activeIdx)
    {
        var dots = new[] { _phaseDot0, _phaseDot1, _phaseDot2 };
        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null) continue;
            dots[i].RemoveFromClassList("phase-dot--active");
            dots[i].RemoveFromClassList("phase-dot--done");
            if      (i == activeIdx) dots[i].AddToClassList("phase-dot--active");
            else if (i <  activeIdx) dots[i].AddToClassList("phase-dot--done");
        }
    }

    // ── Prestige ─────────────────────────────────────────────────

    public void UpdatePrestigeTag(int current, int max)
    {
        if (tagData == null) return;

        var lvl  = tagData.GetLevelForPrestige(current);
        var next = tagData.GetNextLevel(lvl.level);

        if (_tagBLevel  != null) _tagBLevel.text = RomanNumeral(lvl.level);
        if (_tagBRank   != null) _tagBRank.text  = lvl.rankName.ToUpper();
        if (_tagBBarMin != null) _tagBBarMin.text = current.ToString();
        if (_tagBBarMax != null) _tagBBarMax.text = max.ToString();

        if (_tagBBarFill != null)
        {
            float pct = max > 0 ? Mathf.Clamp01((float)current / max) * 100f : 0f;
            _tagBBarFill.style.width = Length.Percent(pct);
        }

        if (_tagBBackNextInfo != null && next != null)
            _tagBBackNextInfo.text =
                $"До {RomanNumeral(next.level)} рівня: {next.prestigeRequired - current} ★\n" +
                $"Наступний: {next.rankName}";

        if (_tagABackLevel != null) _tagABackLevel.text = RomanNumeral(lvl.level);
        if (_tagABackRank  != null) _tagABackRank.text  = lvl.rankName;
    }

    // ── Club ─────────────────────────────────────────────────────

    public void UpdateClub(int stars, int members, bool founded)
    {
        if (_tagABackClubStars != null)
            _tagABackClubStars.text = founded
                ? new string('★', stars) + new string('☆', Mathf.Max(0, 5 - stars))
                : "— не засновано —";

        if (_tagABackClubMembers != null)
            _tagABackClubMembers.text = founded ? members.ToString() : "";
    }

    // ── Bonus strip ──────────────────────────────────────────────

    private void RebuildBonusStrip(IReadOnlyList<ActiveBonus> bonuses)
    {
        if (_bonusStrip == null) return;
        _bonusStrip.Clear();

        foreach (var bonus in bonuses)
            _bonusStrip.Add(BuildBonusElement(bonus));
    }

    private VisualElement BuildBonusElement(ActiveBonus bonus)
    {
        var item = new VisualElement();
        item.AddToClassList("bonus-item");

        var str = new VisualElement();
        str.AddToClassList("bonus-string");
        item.Add(str);

        var body = new VisualElement();
        body.AddToClassList("bonus-body");
        body.AddToClassList($"bonus-body--{bonus.shape.ToString().ToLower()}");
        body.AddToClassList($"bonus-body--{bonus.color.ToString().ToLower()}");

        var hole = new VisualElement();
        hole.AddToClassList("bonus-hole");
        body.Add(hole);

        body.Add(new Label(bonus.label) { name = "bonus-lbl" });
        body.Q<Label>("bonus-lbl")?.AddToClassList("bonus-label");

        body.Add(new Label(bonus.value) { name = "bonus-val" });
        body.Q<Label>("bonus-val")?.AddToClassList("bonus-value");

        var tooltip = BuildTooltip(bonus.tooltipTitle, bonus.tooltipBody);
        body.Add(tooltip);
        body.RegisterCallback<MouseEnterEvent>(_ => tooltip.style.display = DisplayStyle.Flex);
        body.RegisterCallback<MouseLeaveEvent>(_ => tooltip.style.display = DisplayStyle.None);

        item.Add(body);
        return item;
    }

    private VisualElement BuildTooltip(string title, string body)
    {
        var tt = new VisualElement();
        tt.AddToClassList("hud-tooltip");
        tt.style.display = DisplayStyle.None;

        var inner = new VisualElement();
        inner.AddToClassList("hud-tooltip__inner");

        var titleLbl = new Label(title);
        titleLbl.AddToClassList("hud-tooltip__title");
        inner.Add(titleLbl);

        foreach (var line in body.Split('\n'))
        {
            var row = new VisualElement();
            row.AddToClassList("hud-tooltip__row");
            var l = new Label(line);
            l.AddToClassList("hud-tooltip__line");
            row.Add(l);
            inner.Add(row);
        }

        tt.Add(inner);
        return tt;
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static string RomanNumeral(int n) => n switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V",
        _ => n.ToString()
    };
}
