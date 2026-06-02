// Assets/Scripts/Core/HoverHighlighter.cs
// Hover-підсвітка інтерактивних об'єктів.
//
// ПІДХІД: окремий "Outline" layer + URP RenderObjects feature.
// SG_LigneClaireOutline лишається для загального стилю сцени.
// Hover outline — окремий прохід що малює поверх через власний матеріал.
//
// UNITY SETUP (одноразово — 5 хвилин):
//
// 1. Edit → Project Settings → Tags and Layers
//    → додай Layer: "Outline" (будь-який вільний слот)
//
// 2. Створи матеріал "OutlineHoverMat":
//    Assets → Create → Material
//    Shader → Universal Render Pipeline → Unlit
//    Base Color: білий (1,1,1,1)  або будь-який колір hover
//    Surface: Opaque
//
// 3. URP Renderer → Add Renderer Feature → "Render Objects"
//    Name:              OutlineHoverPass
//    Event:             AfterRenderingOpaques
//    Layer Mask:        Outline                ← тільки наш шар
//    Override Material: OutlineHoverMat        ← матеріал з кроку 2
//    Depth → Write:     OFF
//    Depth → Test:      Always                 ← малюється поверх всього
//
// 4. На цей самий Manager GO → Add Component → HoverHighlighter
//    Assign: mainCamera, interactionLayer (ті самі що InteractionRouter)
//
// Як це виглядає:
//   - Об'єкт рендериться двічі: нормально + білим поверх
//   - Виглядає як силует/заливка а не тонкий контур
//   - Щоб отримати тонкий контур — треба в OutlineHoverMat використати
//     шейдер що малює тільки backface із розширенням (Inverted Hull):
//     Shader Graph → нова Unlit Graph → Vertex позиція зсувається
//     по нормалі назовні на 0.01 → Cull Front
//     (це дасть класичний тонкий білий контур)

using UnityEngine;

[DefaultExecutionOrder(-4)]
public class HoverHighlighter : MonoBehaviour
{
    public static HoverHighlighter Instance { get; private set; }

    [Header("Raycast — ті самі параметри що в InteractionRouter")]
    [SerializeField] private Camera    mainCamera;
    [SerializeField] private LayerMask interactionLayer;
    [SerializeField] private float     maxDistance = 100f;

    // Поточний підсвічений об'єкт
    private OutlineTarget _current;

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
        // Знімаємо підсвітку якщо ввід заблокований
        if (InputBlocker.IsBlocked) { SetCurrent(null); return; }

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;

        Ray           ray  = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
        RaycastHit[]  hits = Physics.RaycastAll(ray, maxDistance, interactionLayer,
                                                QueryTriggerInteraction.Collide);

        SetCurrent(hits.Length > 0 ? FindTarget(hits) : null);
    }

    private void OnDisable() => SetCurrent(null);

    // ── Private ─────────────────────────────────────────────────

    private OutlineTarget FindTarget(RaycastHit[] hits)
    {
        GameState state = EditModeManager.GetEffectiveState();

        // Пріоритет 0 — LootBox
        foreach (var h in hits)
        {
            var lb = h.collider.GetComponentInParent<LootBox>();
            if (lb != null && !lb.IsOpened)
                return GetOrAdd(lb.gameObject);
        }

        // Пріоритет 1 — BookWorldItem (Preparation + WorkDay)
        if (state == GameState.Preparation || state == GameState.WorkDay)
            foreach (var h in hits)
            {
                var bwi = h.collider.GetComponentInParent<BookWorldItem>();
                if (bwi != null) return GetOrAdd(bwi.gameObject);
            }

        // Пріоритет 2 — NPCBrain (WorkDay, тільки якщо шукає)
        if (state == GameState.WorkDay)
            foreach (var h in hits)
            {
                var npc = h.collider.GetComponentInParent<NPCBrain>();
                if (npc != null &&
                    (npc.CurrentState == NPCState.Browsing      ||
              //       npc.CurrentState == NPCState.ShowingHint   ||
                    npc.CurrentState == NPCState.WaitingForPlayer||
                    npc.CurrentState != NPCState.Leaving  ||
                    npc.CurrentState != NPCState.Buying))
                    return GetOrAdd(npc.gameObject);
            }

        // Пріоритет 3 — Cabinet (не LootPhase)
        if (state != GameState.LootPhase)
            foreach (var h in hits)
            {
                var cab = h.collider.GetComponentInParent<Cabinet>();
                if (cab != null) return GetOrAdd(cab.gameObject);
            }

        return null;
    }

    private void SetCurrent(OutlineTarget next)
    {
        if (next == _current) return;
        _current?.SetHighlight(false);
        _current = next;
        _current?.SetHighlight(true);
    }

    private static OutlineTarget GetOrAdd(GameObject go)
    {
        var t = go.GetComponent<OutlineTarget>();
        if (t == null) t = go.AddComponent<OutlineTarget>();
        return t;
    }
}