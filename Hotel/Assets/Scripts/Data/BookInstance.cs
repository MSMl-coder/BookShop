[System.Serializable]
public class BookInstance
{
    public string templateID; // Посилання на SO через ID
    public string instanceID; // GUID кожної копії

    public BookInstance(string tID)
    {
        templateID = tID;
        instanceID = System.Guid.NewGuid().ToString();
    }
}