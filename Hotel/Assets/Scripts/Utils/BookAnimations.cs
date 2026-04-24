using UnityEngine;
using System.Collections;

public static class BookAnimations
{
    public static IEnumerator AnimateEntry(GameObject book, Transform startPoint, float speed)
    {
        if (book == null) yield break;

        Vector3 finalPos = book.transform.position;
        Vector3 startPos = finalPos - startPoint.forward * 0.15f; 
        Vector3 finalScale = book.transform.localScale;
        Vector3 startScale = new Vector3(0, finalScale.y, finalScale.z);

        book.transform.position = startPos;
        book.transform.localScale = startScale;

        float elapsed = 0;
        while (elapsed < 1f)
        {
            if (book == null) yield break;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.SmoothStep(0, 1, elapsed);
            book.transform.position = Vector3.Lerp(startPos, finalPos, t);
            book.transform.localScale = Vector3.Lerp(startScale, finalScale, t);
            yield return null;
        }
        if (book != null) book.transform.localScale = finalScale;
    }
}