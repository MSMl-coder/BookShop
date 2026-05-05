using UnityEngine;

public static class ShelfLayoutHandler
{
    public static Vector3 GetNextPosition(Transform startPoint, float usedLength, float thickness, float spacing)
    {
        float offset = usedLength + (thickness / 2f) + spacing;
        // Використовуємо -right, бо ваша червона вісь дивиться вліво
        return startPoint.position + (-startPoint.right * offset);
    }
}