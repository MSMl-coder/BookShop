using UnityEngine;
using UnityEngine.InputSystem; // Для Keyboard
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DebugGameController : MonoBehaviour
{
    [Header("Economy Debug")]
    [SerializeField] private int moneyToAdd = 1000;
    
    [Header("State Control")]
    [SerializeField] private GameState targetState;

    [Header("Inventory Debug")]
    [SerializeField] private BookTemplate testBook;

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame) AddMoney();
            if (Keyboard.current.f2Key.wasPressedThisFrame) SkipToNextState();
        }
    }

    #region Кнопки для Інспектора

    [ContextMenu("Add Money")]
    public void AddMoney()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddMoney(moneyToAdd);
        }
    }

    [ContextMenu("Change Game State")]
    public void ChangeState()
    {
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.ChangeState(targetState);
        }
    }

    [ContextMenu("Skip to Next State")]
    public void SkipToNextState()
    {
        if (GameLoopManager.Instance != null)
        {
            // Перемикаємо стани по колу
            int next = ((int)GameLoopManager.Instance.CurrentState + 1) % 3;
            GameLoopManager.Instance.ChangeState((GameState)next);
        }
    }

    [ContextMenu("Give Test Book")]
    public void GiveBook()
    {
        if (InventoryManager.Instance != null && testBook != null)
        {
            // Використовуємо .name як ID, якщо спеціального поля немає
            InventoryManager.Instance.AddBook(testBook.name);
            Debug.Log($"[Debug] Додано книгу: {testBook.name}");
        }
    }

    [ContextMenu("Reset Economy")]
    public void ResetEconomy()
    {
        if (EconomyManager.Instance != null)
        {
            // Скидаємо гроші через витрату поточної суми
            EconomyManager.Instance.SpendMoney(EconomyManager.Instance.Money);
            Debug.Log("[Debug] Баланс скинуто.");
        }
    }

    #endregion
}

#region Editor
#if UNITY_EDITOR
[CustomEditor(typeof(DebugGameController))]
public class DebugGameControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DebugGameController script = (DebugGameController)target;

        GUILayout.Space(15);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("💰 ADD MONEY", GUILayout.Height(30))) script.AddMoney();
        
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("⏳ CHANGE STATE", GUILayout.Height(30))) script.ChangeState();
        
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("📚 GIVE TEST BOOK", GUILayout.Height(30))) script.GiveBook();
        
        GUILayout.Space(10);
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("❌ RESET ECONOMY", GUILayout.Height(30))) script.ResetEconomy();
        GUI.backgroundColor = Color.white;
    }
}
#endif
#endregion