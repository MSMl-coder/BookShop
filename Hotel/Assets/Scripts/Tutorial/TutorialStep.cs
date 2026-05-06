using UnityEngine;

[CreateAssetMenu(fileName = "TutStep_", menuName = "Bookstore/Tutorial Step")]
public class TutorialStep : ScriptableObject
{
    public string stepID;           // Унікальний ID ("open_first_box")
    [TextArea] public string message;
    public string highlightTarget;  // Ім'я UI елемента для підсвічування
    public TutorialTrigger trigger; // Коли показувати
    public bool blockInput = false; // Чи блокувати ввід до виконання
    public string icon = "📖";
}

public enum TutorialTrigger
{
    OnGameStart,
    OnFirstBook,
    OnFirstSale,
    OnDayEnd,
    Manual
}