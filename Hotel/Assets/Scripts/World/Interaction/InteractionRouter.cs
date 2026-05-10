// Assets/Scripts/Core/InteractionRouter.cs  [Фаза 1 — v3, RaycastAll]
// ВИПРАВЛЕННЯ:
//   - Замість Physics.Raycast (перший hit) → Physics.RaycastAll + сортування по відстані
//   - Пріоритет: BookWorldItem > Cabinet > NPCBrain
//     (книга завжди важливіша за шафу навіть якщо шафа ближче до камери)
//   - Шафа більше не "з'їдає" клік по книзі що знаходиться всередині неї

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[DefaultExecutionOrder(-5)]
public class InteractionRouter : MonoBehaviour
{
    public static InteractionRouter Instance { get; private set; }

    [Header("Raycast")]
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask interactionLayer;
    [SerializeField] private float     maxDistance = 30f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        if (InputBlocker.IsBlocked) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        // Ігноруємо кліки по UI (EventSystem — для Legacy UI/UGUI)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        HandleClick(mouse.position.ReadValue());
    }

    public void SimulateClick(Vector2 screenPos) => HandleClick(screenPos);

    private void HandleClick(Vector2 screenPos)
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        // RaycastAll — отримуємо ВСІ об'єкти що перетинає промінь
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, interactionLayer);

        if (hits.Length == 0)
        {
            ContextMenuUI.Instance?.Hide();
            return;
        }

        // Сортуємо за відстанню (від найближчого до найдальшого)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        GameState state = GameLoopManager.Instance?.CurrentState ?? GameState.Preparation;

        // Пріоритет 1 — шукаємо BookWorldItem серед ВСІХ hits
        // (книга може бути за collider-ом шафи/полиці)
        foreach (var hit in hits)
        {
            var bookItem = hit.collider.GetComponentInParent<BookWorldItem>();
            if (bookItem != null)
            {
                Debug.Log($"[InteractionRouter] Book hit: {hit.collider.name} | State: {state}");
                ContextMenuUI.Instance?.ShowForBook(bookItem, hit.point, state);
                return;
            }
        }

        // Пріоритет 2 — NPC (тільки WorkDay)
        foreach (var hit in hits)
        {
            var npc = hit.collider.GetComponentInParent<NPCBrain>();
            if (npc != null && state == GameState.WorkDay)
            {
                Debug.Log($"[InteractionRouter] NPC hit: {hit.collider.name}");
                ContextMenuUI.Instance?.ShowForNPC(npc, hit.point, state);
                return;
            }
        }

        // Пріоритет 3 — Cabinet
        foreach (var hit in hits)
        {
            var cabinet = hit.collider.GetComponentInParent<Cabinet>();
            if (cabinet != null)
            {
                Debug.Log($"[InteractionRouter] Cabinet hit: {hit.collider.name} | State: {state}");
                ContextMenuUI.Instance?.ShowForCabinet(cabinet, hit.point, state);
                return;
            }
        }

        // Нічого не знайдено
        ContextMenuUI.Instance?.Hide();
    }
}