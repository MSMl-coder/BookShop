// Assets/Scripts/UI/HUD/HudTagController.cs
//
// Керує трьома зонами нового HUD:
//   1. Бірка з грошима (+ flip → рівень/клуб)
//   2. Бірка рівня/престижу (опціонально — variant 2)
//   3. Конверт із картками фаз (NOTES-стиль)
//   4. Рядок бонусів угорі
//
// UNITY SETUP:
//   • Додай на той самий GameObject що і UIDocument (MainShopUI)
//   • Призначи [SerializeField] uiDocument і tagData
//   • Переконайся що UXML містить елементи за іменами нижче

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class HudTagController : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────
    [SerializeField] private UIDocument    uiDocument;
    [SerializeField] private ShopTagData   tagData;
    [SerializeField] private bool          showPrestigeTag = true; // Variant 2

    // ── Cached elements ──────────────────────────────────────────

    // Tag A — money/day
    private VisualElement _tagAFlipper;
    private Label         _tagAAmount;
    private Label         _tagADay;
    private Label         _tagABackLevel;
    private Label         _tagABackRank;
    private Label         _tagABackClubStars;
    private Label         _tagABackClubMembers;

    // Tag B — level/prestige (variant 2)
    private VisualElement _tagBFlipper;
    private Label         _tagBLevel;
    private Label         _tagBRank;
    private VisualElement _tagBBarFill;
    private Label         _tagBBarMin;
    private Label         _tagBBarMax;
    private Label         _tagBBackNextInfo;

    // Phase envelope
    private VisualElement _phaseCard;        // активна картка
    private Label         _phaseCardName;
    private Label         _phaseCardStep;
    private Label         _phaseCardStamp;
    private VisualElement _phaseDot0, _phaseDot1, _phaseDot2;

    // Bonus strip
    private VisualElement _bonusStrip;

    // ── State ────────────────────────────────────────────────────
    private bool  _tagAFlipped;
    private bool  _tagBFlipped;
    private bool  _phaseAnimating;

    private static readonly string[] PhaseNames   = { "ПІДГОТОВКА", "ТОРГІВЛЯ", "НАГОРОДИ" };
    private static readonly string[] PhaseSteps   = { "1 з 3",      "2 з 3",    "3 з 3"   };
    private static readonly string[] StampTexts   = { "ЗАКРИТО",    "АКТИВНО",  "ВИКОНАНО" };
    private static readonly string[] StampClasses = { "stamp--prep","stamp--trade","stamp--reward" };

    // ── Unity lifecycle ──────────────────────────────────────────

    private void OnEnable()
    {
        var root = uiDocument?.rootVisualElement;
        if (root == null) return;

        CacheElements(root);
        InitTagData();

        // Events
        SubscribeTagFlip(root, "TagAFlipper", ref _tagAFlipped);
        SubscribeTagFlip(root, "TagBFlipper", ref _tagBFlipped);
        root.Q<VisualElement>("PhaseEnvelope")?.RegisterCallback<ClickEvent>(_ => CyclePhase());
        _phaseCard?.RegisterCallback<ClickEvent>(_ => CyclePhase());

        // Managers
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnMoneyChanged += UpdateMoney;
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnStateChanged  += UpdatePhase;
            GameLoopManager.Instance.OnNewDayStarted += UpdateDay;
        }
        if (BonusManager.Instance != null)
            BonusManager.Instance.OnBonusesChanged += RebuildBonusStrip;

        // Initial state
        UpdateMoney(EconomyManager.Instance?.Money ?? 0);
        UpdateDay(GameLoopManager.Instance?.CurrentDay ?? 1);
        UpdatePhase(GameLoopManager.Instance?.CurrentState ?? GameState.Preparation);
        RebuildBonusStrip(BonusManager.Instance?.GetBonuses() ?? new List<ActiveBonus>());
        UpdatePrestigeTag(
            ClubPrestigeCalculator.Instance?.CurrentPrestige ?? 0,
            1000
        );
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

    private void CacheElements(VisualElement root)
    {
        // Tag A
        _tagAFlipper          = root.Q<VisualElement>("TagAFlipper");
        _tagAAmount           = root.Q<Label>("TagAAmount");
        _tagADay              = root.Q<Label>("TagADay");
        _tagABackLevel        = root.Q<Label>("TagABackLevel");
        _tagABackRank         = root.Q<Label>("TagABackRank");
        _tagABackClubStars    = root.Q<Label>("TagABackClubStars");
        _tagABackClubMembers  = root.Q<Label>("TagABackClubMembers");

        // Tag B
        _tagBFlipper    = root.Q<VisualElement>("TagBFlipper");
        _tagBLevel      = root.Q<Label>("TagBLevel");
        _tagBRank       = root.Q<Label>("TagBRank");
        _tagBBarFill    = root.Q<VisualElement>("TagBBarFill");
        _tagBBarMin     = root.Q<Label>("TagBBarMin");
        _tagBBarMax     = root.Q<Label>("TagBBarMax");
        _tagBBackNextInfo = root.Q<Label>("TagBBackNextInfo");

        // Phase card
        _phaseCard      = root.Q<VisualElement>("PhaseCardActive");
        _phaseCardName  = root.Q<Label>("PhaseCardName");
        _phaseCardStep  = root.Q<Label>("PhaseCardStep");
        _phaseCardStamp = root.Q<Label>("PhaseCardStamp");
        _phaseDot0      = root.Q<VisualElement>("PhaseDot0");
        _phaseDot1      = root.Q<VisualElement>("PhaseDot1");
        _phaseDot2      = root.Q<VisualElement>("PhaseDot2");

        // Bonus
        _bonusStrip = root.Q<VisualElement>("BonusStrip");

        // Show/hide tag B
 //       if (_tagBFlipper != null)
 //           _tagBFlipper.parent?.style.display =
 //               showPrestigeTag ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void InitTagData()
    {
        if (tagData == null) return;
        // статичні написи бірки
        SetText(root: null, "TagABrand",    tagData.shopName);
        SetText(root: null, "TagASeries",   tagData.shopSubtitle);
    }

    // ── Tag flip ─────────────────────────────────────────────────

    private void SubscribeTagFlip(VisualElement root, string name, ref bool flipped)
    {
        bool localFlipped = flipped; // capture
        var flipper = root.Q<VisualElement>(name);
        if (flipper == null) return;

        flipper.RegisterCallback<ClickEvent>(_ =>
        {
            localFlipped = !localFlipped;
            if (localFlipped) flipper.AddToClassList("flipped");
            else              flipper.RemoveFromClassList("flipped");
        });
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

    // ── Prestige tag ─────────────────────────────────────────────

    public void UpdatePrestigeTag(int current, int max)
    {
        if (tagData == null) return;

        var lvl  = tagData.GetLevelForPrestige(current);
        var next = tagData.GetNextLevel(lvl.level);

        if (_tagBLevel  != null) _tagBLevel.text = lvl.level.ToString("D2");
        if (_tagBRank   != null) _tagBRank.text  = lvl.rankName.ToUpper();
        if (_tagBBarMin != null) _tagBBarMin.text = current.ToString();
        if (_tagBBarMax != null) _tagBBarMax.text = max.ToString();

        if (_tagBBarFill != null)
        {
            float pct = max > 0 ? Mathf.Clamp01((float)current / max) * 100f : 0f;
            _tagBBarFill.style.width = Length.Percent(pct);
        }

        // Зворотній бік Tag B
        if (_tagBBackNextInfo != null && next != null)
            _tagBBackNextInfo.text =
                $"До {RomanNumeral(next.level)} рівня: {next.prestigeRequired - current} ★\n" +
                $"Наступний: {next.rankName}";

        // Зворотній бік Tag A
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

    // ── Phase card animation ─────────────────────────────────────

    private void UpdatePhase(GameState state)
    {
        // Миттєво без анімації (при старті або зміні ззовні без кліку)
        ApplyPhaseData((int)state);
    }

    private void CyclePhase()
    {
        if (_phaseAnimating) return;

        // Визначаємо наступний стан
        int current = GameLoopManager.Instance != null
            ? (int)GameLoopManager.Instance.CurrentState
            : 0;
        int next = (current + 1) % 3;

        // В реальній грі — викликаємо GameLoopManager
        // (тут лише анімація; логіку фази запускає PhaseWidgetController)
        StartCoroutine(AnimatePhaseTransition(next));
    }

    private IEnumerator AnimatePhaseTransition(int nextIdx)
    {
        _phaseAnimating = true;

        // 1. Слайд картки вниз в конверт
        if (_phaseCard != null)
        {
            _phaseCard.AddToClassList("phase-card--hiding");
            yield return new WaitForSeconds(0.4f);
        }

        // 2. Оновлюємо дані
        ApplyPhaseData(nextIdx);

        // 3. Слайд нової картки вгору з конверту
        if (_phaseCard != null)
        {
            _phaseCard.RemoveFromClassList("phase-card--hiding");
            _phaseCard.AddToClassList("phase-card--entering");
            // Форсуємо рефлоу
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

        // Мотузка
        var str = new VisualElement();
        str.AddToClassList("bonus-string");
        item.Add(str);

        // Тіло бірки
        var body = new VisualElement();
        body.AddToClassList("bonus-body");
        body.AddToClassList($"bonus-body--{bonus.shape.ToString().ToLower()}");
        body.AddToClassList($"bonus-body--{bonus.color.ToString().ToLower()}");

        var hole = new VisualElement();
        hole.AddToClassList("bonus-hole");
        body.Add(hole);

        var lbl = new Label(bonus.label);
        lbl.AddToClassList("bonus-label");
        body.Add(lbl);

        var val = new Label(bonus.value);
        val.AddToClassList("bonus-value");
        body.Add(val);

        // Тултіп при наведенні
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
            var lineLbl = new Label(line);
            lineLbl.AddToClassList("hud-tooltip__line");
            row.Add(lineLbl);
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

    private void SetText(VisualElement root, string name, string text)
    {
        var el = (root ?? uiDocument?.rootVisualElement)?.Q<Label>(name);
        if (el != null) el.text = text;
    }
}
