// Assets/Scripts/EditMode/Day1Bootstrapper.cs
using UnityEngine;

/// Вішається на GameObject у сцені.
/// При старті першого дня перевіряє, чи потрібна «розпаковка» і готує коробки.
public class Day1Bootstrapper : MonoBehaviour
{
    [Header("Starting Boxes")]
    [SerializeField] private InteractableBox furnitureBox; // коробка з меблями
    [SerializeField] private InteractableBox bookBox;      // коробка з книжками

    [Header("Day Condition")]
    [SerializeField] private int triggerOnDay = 1;

    private void Start()
    {
        if (GameLoopManager.Instance == null) return;
        if (GameLoopManager.Instance.CurrentDay != triggerOnDay) return;

        if (furnitureBox != null) furnitureBox.gameObject.SetActive(true);
        if (bookBox      != null) bookBox.gameObject.SetActive(true);

        Debug.Log("[Day1] Стартові коробки активовано");
    }
}