using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ShelfBookOptimizer : MonoBehaviour
{
    private MeshFilter shelfMeshFilter;
    private MeshRenderer shelfMeshRenderer;
    
    [Header("Settings")]
    public Material bookMaterial; // Спільний матеріал для всіх книг
    private List<GameObject> sourceBooks = new List<GameObject>();
    private bool isCombined = false;

    void Awake()
    {
        shelfMeshFilter = GetComponent<MeshFilter>();
        shelfMeshRenderer = GetComponent<MeshRenderer>();
    }

    // Викликайте цей метод, коли гравець закінчив розставляти книги
    public void BakeBooks()
    {
        if (isCombined) UnbakeBooks();

        // 1. Знаходимо всі книги (MeshFilters) у дочірніх об'єктах
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        List<CombineInstance> combine = new List<CombineInstance>();

        sourceBooks.Clear();

        foreach (var filter in meshFilters)
        {
            // Пропускаємо саму полицю, щоб не запекти її саму в себе
            if (filter.gameObject == gameObject) continue;

            CombineInstance ci = new CombineInstance();
            ci.mesh = filter.sharedMesh;
            // Перераховуємо локальні координати книг у координати полиці
            ci.transform = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            combine.Add(ci);

            sourceBooks.Add(filter.gameObject);
            filter.gameObject.SetActive(false); // Ховаємо оригінали
        }

        // 2. Створюємо новий об'єднаний меш
        Mesh combinedMesh = new Mesh();
        combinedMesh.name = "Combined_Books_" + gameObject.name;
        combinedMesh.CombineMeshes(combine.ToArray(), true, true);

        shelfMeshFilter.mesh = combinedMesh;
        shelfMeshRenderer.material = bookMaterial;
        
        isCombined = true;
        Debug.Log($"[Optimizer] {gameObject.name}: Об'єднано {sourceBooks.Count} книг в 1 батч.");
    }

    // Викликайте цей метод, якщо гравцеві треба знову взаємодіяти з книгами
    public void UnbakeBooks()
    {
        if (!isCombined) return;

        foreach (var book in sourceBooks)
        {
            if (book != null) book.SetActive(true);
        }

        shelfMeshFilter.mesh = null;
        isCombined = false;
    }
}