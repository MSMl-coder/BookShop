
Copy

// Assets/Scripts/Tutorial/InputBlocker.cs
// ФАЗА 1 — блокування вводу під час туторіалу (крок з blockInput = true)
//
// Блокує InteractionRouter + InvetoryDragDrop + будь-які інші системи вводу.
// Кожна система вводу перед обробкою перевіряє: if (InputBlocker.IsBlocked) return;
//
// ВИКОРИСТАННЯ:
//   InputBlocker.SetBlocked(true);   // заблокувати ввід
//   InputBlocker.SetBlocked(false);  // розблокувати
//   if (InputBlocker.IsBlocked) return; // перевірка
 
using UnityEngine;
 
public static class InputBlocker
{
    private static bool _isBlocked;
 
    public static bool IsBlocked => _isBlocked;
 
    public static void SetBlocked(bool blocked)
    {
        _isBlocked = blocked;
        Debug.Log($"[InputBlocker] Ввід {(blocked ? "ЗАБЛОКОВАНО" : "РОЗБЛОКОВАНО")}");
    }
}