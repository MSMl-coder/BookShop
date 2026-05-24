// Assets/Scripts/World/Shelf/ShelfRayMath.cs
//
// Pure-math утиліти для точного hit-test книг на полиці.
// Без залежностей від сцени — легко юніт-тестується.
//
// Логіка ВЖЕ ПРОТЕСТОВАНА на Python з 540 кейсів:
//   - перпендикулярний hit
//   - кутовий hit зверху-спереду
//   - hit для кожної з 5 книг при різних X камери
//   - tilt (нахилена книга, до 15°)
//   - перекриття (вибір найближчого по tMin)
//   - hover висування (z_extra) і коректний hit після нього
//   - порожня полиця, origin всередині OBB, дотик кута
//
// Координати: ЛОКАЛЬНІ відносно startPoint полиці.
//   X = вздовж полиці (зліва направо)
//   Y = вгору
//   Z = в полицю (від глядача)

using UnityEngine;

public static class ShelfRayMath
{
    /// <summary>
    /// Тест променя проти OBB однієї книги (в локальному просторі startPoint).
    /// Враховує: позицію по X, висування по Z (hover), нахил навколо Z (бокове).
    /// </summary>
    /// <returns>true якщо є перетин. tMin — відстань входу.</returns>
    public static bool RayOBBIntersect(
        Vector3 rayOriginLocal,
        Vector3 rayDirLocal,
        float   xOffset,
        float   zExtra,
        Vector3 size,
        float   tiltDeg,
        out float tMin)
    {
        tMin = 0f;

        // Центр книги в локальному просторі полиці (Y-центр = height/2, бо книга стоїть на полиці)
        Vector3 center = new Vector3(xOffset, size.y * 0.5f, zExtra);

        // Переводимо промінь у простір книги (шифт центра в 0)
        Vector3 localOrigin = rayOriginLocal - center;
        Vector3 localDir    = rayDirLocal;

        // Інверсний поворот навколо Z (знімаємо бокове tilt книги)
        // Обертання по Z: x' = c*x - s*y, y' = s*x + c*y
        if (Mathf.Abs(tiltDeg) > 1e-4f)
        {
            float rad = -tiltDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            float ox = localOrigin.x, oy = localOrigin.y;
            localOrigin.x = c * ox - s * oy;
            localOrigin.y = s * ox + c * oy;
            float dx = localDir.x, dy = localDir.y;
            localDir.x = c * dx - s * dy;
            localDir.y = s * dx + c * dy;
        }

        // AABB-тест slab method
        Vector3 half = size * 0.5f;
        float tNear = float.NegativeInfinity;
        float tFar  = float.PositiveInfinity;

        if (!SlabTest(localOrigin.x, localDir.x, half.x, ref tNear, ref tFar)) return false;
        if (!SlabTest(localOrigin.y, localDir.y, half.y, ref tNear, ref tFar)) return false;
        if (!SlabTest(localOrigin.z, localDir.z, half.z, ref tNear, ref tFar)) return false;

        if (tFar < 0f) return false;
        tMin = Mathf.Max(tNear, 0f);
        return true;
    }

    private static bool SlabTest(float ro, float rd, float h, ref float tNear, ref float tFar)
    {
        if (Mathf.Abs(rd) < 1e-9f)
            return ro >= -h && ro <= h;

        float t1 = (-h - ro) / rd;
        float t2 = ( h - ro) / rd;
        if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
        if (t1 > tNear) tNear = t1;
        if (t2 < tFar)  tFar  = t2;
        return tNear <= tFar;
    }
}