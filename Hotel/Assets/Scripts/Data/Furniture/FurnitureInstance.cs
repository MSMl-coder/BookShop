// Assets/Scripts/Data/Furniture/FurnitureInstance.cs
using UnityEngine;

[System.Serializable]
public class FurnitureInstance
{
    public int    templateID;   // FurnitureTemplate.furnitureID
    public string instanceID;  // унікальний GUID

    // Стан розміщення — живе тут, не в SO
    public bool      isPlaced;
    public Vector3   placedPosition;
    public Quaternion placedRotation;

 // Для відновлення із збереження

 // Новий екземпляр — генерує свій GUID
    public FurnitureInstance(int templateID)
    {
        this.templateID    = templateID;
        this.instanceID    = System.Guid.NewGuid().ToString();
        isPlaced           = false;
        placedPosition     = Vector3.zero;
        placedRotation     = Quaternion.identity;
    }

    // Відновлення із збереження — зберігає існуючий GUID
    public FurnitureInstance(int templateID, string existingInstanceID)
    {
        this.templateID    = templateID;
        this.instanceID    = existingInstanceID;
        isPlaced           = false;
        placedPosition     = Vector3.zero;
        placedRotation     = Quaternion.identity;
    }
 



}