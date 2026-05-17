// Assets/Scripts/Diagnostics/ShelfMathDiagnostic.cs
//
// ДІАГНОСТИКА: перевірка hit-test математики на реальній сцені.
// Додай на тимчасовий GO у сцені і натисни F2 щоб запустити тест.
//
// Що робить:
//   1. Бере перший Shelf на сцені.
//   2. Спробує HitTestRay з 10 різних кутів і друкує результат.
//   3. Підсвічує hit точку через Debug.DrawRay.

using UnityEngine;

[AddComponentMenu("Diagnostics/Shelf Math Diagnostic")]
public class ShelfMathDiagnostic : MonoBehaviour
{
    [SerializeField] private Shelf  targetShelf;
    [SerializeField] private bool   logEachRay = false;
    [SerializeField] private Camera testCamera;

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current?.f2Key.wasPressedThisFrame ?? false)
            RunTest();
    }

    private void RunTest()
    {
        if (targetShelf == null) targetShelf = FindFirstObjectByType<Shelf>();
        if (targetShelf == null) { Debug.LogError("[DIAG] Shelf не знайдено!"); return; }
        if (testCamera   == null) testCamera = Camera.main;

        Debug.Log($"[DIAG] Тестування Shelf '{targetShelf.name}' з {targetShelf.GetBookCount()} книгами.");

        int hits = 0;
        // 10 точок екрану — від лівого до правого через всю полицю
        for (int i = 0; i < 10; i++)
        {
            float u = i / 9f;
            Vector3 screenPt = new Vector3(Screen.width * u, Screen.height * 0.5f, 0);
            Ray ray = testCamera.ScreenPointToRay(screenPt);

            int idx = targetShelf.HitTestRay(ray);
            if (idx >= 0) hits++;

            Debug.DrawRay(ray.origin, ray.direction * 5f,
                idx >= 0 ? Color.green : Color.red, 2f);

            if (logEachRay)
                Debug.Log($"[DIAG] Ray #{i} screen=({screenPt.x:F0},{screenPt.y:F0}) → hit idx={idx}");
        }
        Debug.Log($"[DIAG] Результат: {hits}/10 променів зачепили книгу.");
    }
}
