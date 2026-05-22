// ═══════════════════════════════════════════════════════════════════
// PATCH для ContextMenuUI.cs
// Замінити метод ShowForCabinet() цим кодом.
// Або застосувати як partial class / підклас.
//
// ЗМІНА: ShopUIManager.Instance?.OpenCabinetUI(cabinet)
//        → BookshopUIBridge.OpenCabinet(cabinet)
// ═══════════════════════════════════════════════════════════════════

// ЗНАЙДИ в ContextMenuUI.cs метод ShowForCabinet і замінни рядок:
//   ShopUIManager.Instance?.OpenCabinetUI(cabinet);
// НА:
//   BookshopUIBridge.OpenCabinet(cabinet);
//
// Також рядок TakeBookToInventory:
//   ShopUIManager.Instance?.OpenInventoryPanel();
// НА:
//   BookshopUIBridge.OpenInventory();

// ════ ПОВНИЙ ВИПРАВЛЕНИЙ ShowForCabinet ════

/*
public void ShowForCabinet(Cabinet cabinet, Vector3 worldPos, GameState state)
{
    if (state == GameState.LootPhase || state == GameState.DayStats)
    {
        Hide();
        return;
    }

    var btns = new List<ContextButton>();

    // Preparation + WorkDay — відкрити інвентар шафи
    if (state == GameState.Preparation || state == GameState.WorkDay)
    {
        btns.Add(new ContextButton("Inventory / Books", () =>
        {
            // ← ВИПРАВЛЕНО: використовуємо Bridge
            BookshopUIBridge.OpenCabinet(cabinet);
            Hide();
        }));
    }

    // Preparation + EditMode + WorkDay — інформація
    if (state == GameState.Preparation || state == GameState.EditMode || state == GameState.WorkDay)
    {
        btns.Add(new ContextButton("Information", () =>
        {
            Debug.Log($"[ContextMenu] Cabinet: {cabinet.cabinetName}");
            Hide();
        }));
    }

    // EditMode тільки
    if (state == GameState.EditMode)
    {
        btns.Add(new ContextButton("Move", () =>
        {
            Debug.Log($"[ContextMenu] Move: {cabinet.cabinetName}");
            Hide();
        }));

        btns.Add(new ContextButton("Rotate", () =>
        {
            Debug.Log($"[ContextMenu] Rotate: {cabinet.cabinetName}");
            Hide();
        }));

        btns.Add(new ContextButton("Change Color", () =>
        {
            Debug.Log($"[ContextMenu] Color: {cabinet.cabinetName}");
            Hide();
        }));

        btns.Add(new ContextButton("Sell", () =>
        {
            Debug.Log($"[ContextMenu] Sell: {cabinet.cabinetName}");
            Hide();
        }));
    }

    Show(worldPos, btns);
}
*/

// ════ ТАКОЖ: виправити TakeBookToInventory ════
/*
private void TakeBookToInventory(BookWorldItem bookItem)
{
    // ... існуючий код ...
    
    // ЗАМІНИТИ:
    // ShopUIManager.Instance?.OpenInventoryPanel();
    // НА:
    BookshopUIBridge.OpenInventory();
}
*/
