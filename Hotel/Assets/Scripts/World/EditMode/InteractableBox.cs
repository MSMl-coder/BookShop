// Assets/Scripts/World/EditMode/InteractableBox.cs
using UnityEngine;
using System.Collections.Generic;

public class InteractableBox : MonoBehaviour
{
    public enum BoxType { Furniture, Books }

    [Header("Type")]
    [SerializeField] public BoxType boxType;

    [Header("Furniture Box")]
    [SerializeField] private GameObject furnitureBundlePrefab;

    [Header("Books Box")]
    [Tooltip("templateID книг з BookDatabase")]
    [SerializeField] private List<string> bookTemplateIDs;

    [Header("VFX")]
    [SerializeField] private ParticleSystem unboxFX;

    public bool IsOpened { get; private set; }

    // Викликаєтьсяззовні (з PlayerInteraction)
    public void TryOpen()
    {
        if (IsOpened) return;
        IsOpened = true;
        Unbox();
    }

    private void Unbox()
    {
        if (unboxFX != null) unboxFX.Play(); // ← додати null-check
        PlacementFeedback.Instance?.PlaySound(PlacementFeedback.SoundType.Place);

        if (boxType == BoxType.Furniture) UnboxFurniture();
        else UnboxBooks();

        Destroy(gameObject, 0.5f);
    }

    private void UnboxFurniture()
    {
        if (furnitureBundlePrefab == null)
        {
            Debug.LogWarning("[Box] furnitureBundlePrefab не призначено!");
            return;
        }
        Instantiate(furnitureBundlePrefab, transform.position, Quaternion.identity);
        Debug.Log("[Box] Меблі розпаковано");
    }

    private void UnboxBooks()
    {
        if (bookTemplateIDs == null || bookTemplateIDs.Count == 0) return;
        foreach (var id in bookTemplateIDs)
            if (!string.IsNullOrEmpty(id))
                InventoryManager.Instance?.AddBook(id);
        Debug.Log($"[Box] Передано {bookTemplateIDs.Count} книг в інвентар");
    }
}