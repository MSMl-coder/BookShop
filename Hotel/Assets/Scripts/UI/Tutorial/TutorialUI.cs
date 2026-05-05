using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class TutorialUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement _bubble;
    private Label _messageLabel;
    private Button _okButton;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _bubble = root.Q<VisualElement>("TutorialBubble");
        _messageLabel = root.Q<Label>("TutorialMessage");
        _okButton = root.Q<Button>("TutorialOK");

        _okButton?.RegisterCallback<ClickEvent>(_ =>
        {
            TutorialManager.Instance?.CompleteCurrentStep();
            HideBubble();
        });

        if (_bubble != null)
            _bubble.style.display = DisplayStyle.None;

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnStepStarted += ShowStep;
    }

    private void OnDisable()
    {
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnStepStarted -= ShowStep;
    }

    private void ShowStep(TutorialStep step)
    {
        if (_bubble == null || _messageLabel == null) return;
        _messageLabel.text = step.message;
        _bubble.style.display = DisplayStyle.Flex;
        Debug.Log($"[TutorialUI] Showing: {step.stepID}");
    }

    private void HideBubble()
    {
        if (_bubble != null)
            _bubble.style.display = DisplayStyle.None;
    }
}