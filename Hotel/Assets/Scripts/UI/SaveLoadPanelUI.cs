// Assets/Scripts/UI/SaveLoad/SaveLoadPanelUI.cs
// ВИПРАВЛЕНО CS0079: ?.clicked += → if-guard (C# 9 не підтримує null-conditional на events)
using UnityEngine;
using UnityEngine.UIElements;

public enum SaveLoadMode { Save, Load }

public class SaveLoadPanelUI : MonoBehaviour
{
    public static SaveLoadPanelUI Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _overlay;
    private Label         _titleLabel;
    private Button        _tabSave, _tabLoad;
    private Button        _closeX, _closeBtn;
    private Label         _hintLabel;

    private SlotRow[] _slots = new SlotRow[SaveSystem.SlotCount];

    private VisualElement _confirmOverlay;
    private Label         _confirmText, _confirmSub;
    private Button        _confirmYes, _confirmNo;
    private System.Action _pendingAction;

    private SaveLoadMode _mode = SaveLoadMode.Save;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void OnEnable()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        _overlay    = root.Q<VisualElement>("SaveLoadOverlay");
        _titleLabel = root.Q<Label>("SaveLoadTitle");
        _tabSave    = root.Q<Button>("TabSave");
        _tabLoad    = root.Q<Button>("TabLoad");
        _closeX     = root.Q<Button>("SaveLoadCloseX");
        _closeBtn   = root.Q<Button>("SaveLoadClose");
        _hintLabel  = root.Q<Label>("SaveLoadHint");

        _confirmOverlay = root.Q<VisualElement>("ConfirmOverlay");
        _confirmText    = root.Q<Label>("ConfirmText");
        _confirmSub     = root.Q<Label>("ConfirmSub");
        _confirmYes     = root.Q<Button>("ConfirmYes");
        _confirmNo      = root.Q<Button>("ConfirmNo");

        if (_overlay == null)
        {
            Debug.LogWarning("[SaveLoadUI] 'SaveLoadOverlay' не знайдено.");
            return;
        }

        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            int idx = i;
            _slots[i] = new SlotRow
            {
                root = root.Q<VisualElement>($"Slot{i}"),
                name = root.Q<Label>($"Slot{i}Name"),
                date = root.Q<Label>($"Slot{i}Date"),
                btn  = root.Q<Button>($"Slot{i}Btn"),
                del  = root.Q<Button>($"Slot{i}Del"),
            };
            _slots[i].btn?.RegisterCallback<ClickEvent>(_ => OnSlotAction(idx));
            _slots[i].del?.RegisterCallback<ClickEvent>(_ => AskDelete(idx));
        }

        _tabSave?.RegisterCallback<ClickEvent>(_ => SetMode(SaveLoadMode.Save));
        _tabLoad?.RegisterCallback<ClickEvent>(_ => SetMode(SaveLoadMode.Load));

        // ВИПРАВЛЕНО: if-guard замість ?.clicked +=
        if (_closeX   != null) _closeX.clicked   += Close;
        if (_closeBtn != null) _closeBtn.clicked  += Close;

        _overlay.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _overlay) Close();
        });

        _confirmYes?.RegisterCallback<ClickEvent>(_ => { _pendingAction?.Invoke(); HideConfirm(); });
        _confirmNo ?.RegisterCallback<ClickEvent>(_ => HideConfirm());

        _overlay.AddToClassList("hidden");
        _confirmOverlay?.AddToClassList("hidden");
    }

    private void OnDisable()
    {
        // Відписуємось щоб уникнути memory leak
        if (_closeX   != null) _closeX.clicked   -= Close;
        if (_closeBtn != null) _closeBtn.clicked  -= Close;
    }

    // ─────────────────────────────────────────────────
    #region Public API
    // ─────────────────────────────────────────────────

    public void Open(SaveLoadMode mode = SaveLoadMode.Save)
    {
        if (_overlay == null) return;
        SetMode(mode);
        RefreshSlots();
        _overlay.RemoveFromClassList("hidden");
    }

    public void Close()
    {
        _overlay?.AddToClassList("hidden");
        HideConfirm();
    }

    #endregion

    // ─────────────────────────────────────────────────
    #region Slots
    // ─────────────────────────────────────────────────

    private void SetMode(SaveLoadMode mode)
    {
        _mode = mode;
        bool isSave = mode == SaveLoadMode.Save;

        if (_titleLabel != null)
            _titleLabel.text = isSave ? "ЗБЕРЕЖЕННЯ" : "ЗАВАНТАЖЕННЯ";

        _tabSave?.EnableInClassList("active", isSave);
        _tabLoad?.EnableInClassList("active", !isSave);

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            var s = _slots[i];
            if (s.root == null) continue;

            bool     exists = SaveSystem.SaveExists(i);
            SaveData data   = exists ? SaveSystem.Peek(i) : null;

            s.root.EnableInClassList("empty", !exists);

            if (s.name != null) s.name.text = exists ? data.saveName    : "Порожній слот";
            if (s.date != null) s.date.text = exists ? data.saveDateTime : "";

            if (s.btn != null)
            {
                s.btn.text = _mode == SaveLoadMode.Save ? "💾" : (exists ? "▶" : "—");
                s.btn.SetEnabled(_mode == SaveLoadMode.Save || exists);
            }

            if (s.del != null)
                s.del.style.display = (exists && _mode == SaveLoadMode.Save)
                    ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void OnSlotAction(int idx)
    {
        if (_mode == SaveLoadMode.Save) DoSave(idx);
        else                            DoLoad(idx);
    }

    #endregion

    // ─────────────────────────────────────────────────
    #region Save / Load / Delete
    // ─────────────────────────────────────────────────

    private void DoSave(int idx)
    {
        if (SaveSystem.SaveExists(idx))
            AskConfirm($"Перезаписати слот {idx + 1}?",
                       "Поточне збереження буде втрачено.",
                       () => ExecuteSave(idx));
        else
            ExecuteSave(idx);
    }

    private void ExecuteSave(int idx)
    {
        if (GameStateSerializer.Instance != null)
        {
            var data = GameStateSerializer.Instance.CollectSaveData();
            data.slotIndex = idx;
            data.saveName  = $"День {data.currentDay} · {data.money:N0} грн";
            SaveSystem.Save(data);
        }
        else
        {
            SaveSystem.SaveToSlot(idx);
        }
        RefreshSlots();
        Hint("Збережено!");
    }

    private void DoLoad(int idx)
    {
        if (!SaveSystem.SaveExists(idx)) return;
        AskConfirm($"Завантажити слот {idx + 1}?",
                   "Незбережені зміни будуть втрачені.",
                   () =>
                   {
                       var data = SaveSystem.Load(idx);
                       if (GameStateSerializer.Instance != null)
                           GameStateSerializer.Instance.ApplySaveData(data);
                       Close();
                       Hint("Завантажено!");
                   });
    }

    private void AskDelete(int idx)
    {
        AskConfirm($"Видалити слот {idx + 1}?",
                   "Цю дію не можна скасувати.",
                   () => { SaveSystem.Delete(idx); RefreshSlots(); Hint("Видалено."); });
    }

    #endregion

    // ─────────────────────────────────────────────────
    #region Confirm & Hint
    // ─────────────────────────────────────────────────

    private void AskConfirm(string text, string sub, System.Action onYes)
    {
        if (_confirmOverlay == null) { onYes?.Invoke(); return; }
        _pendingAction = onYes;
        if (_confirmText != null) _confirmText.text = text;
        if (_confirmSub  != null) _confirmSub.text  = sub;
        _confirmOverlay.RemoveFromClassList("hidden");
    }

    private void HideConfirm()
    {
        _confirmOverlay?.AddToClassList("hidden");
        _pendingAction = null;
    }

    private void Hint(string text)
    {
        if (_hintLabel != null) _hintLabel.text = text;
    }

    #endregion

    private class SlotRow
    {
        public VisualElement root;
        public Label         name, date;
        public Button        btn, del;
    }
}
