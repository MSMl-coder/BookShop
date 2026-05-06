// Assets/Scripts/Data/Books/BookInstance.cs
[System.Serializable]
public class BookInstance
{
    public string templateID;
    public string instanceID;

    public BookInstance(string templateID)
    {
        this.templateID = templateID;
        this.instanceID = System.Guid.NewGuid().ToString();

        
    }
}