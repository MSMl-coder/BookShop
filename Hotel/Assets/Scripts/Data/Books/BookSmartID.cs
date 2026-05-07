using UnityEngine;

public static class BookSmartID
{
    public static string Generate(BookTemplate t)
    {
        string year   = UnityEngine.Mathf.Clamp(t.writingYear, 1000, 2026).ToString("D4");
        string genre  = ((int)t.genre).ToString("D2");
        string rarity = ((int)t.rarity).ToString("D1");
        string volume = UnityEngine.Mathf.Clamp(t.volumeNumber, 1, 99).ToString("D2");

        // FIX: use a stable, platform-independent hash (djb2 variant)
        int hash = StableHash(t.title) % 1000;
        return $"{year}{genre}{rarity}{volume}{hash:D3}";
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return System.Math.Abs(h);
        }
    }
}