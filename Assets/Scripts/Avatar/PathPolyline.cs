using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Matematika murni untuk "rel" yang dinaiki avatar pemandu (ADR-034 keputusan 2).
/// Dipisah dari MonoBehaviour supaya bisa diuji tanpa scene — lihat PathPolylineTests.
///
/// Seluruh proyeksi MENGABAIKAN sumbu Y. Pengguna memegang HP setinggi ~1,5 m sementara
/// rute berada di lantai, jadi yang ditanyakan adalah "sejauh mana pengguna sudah menyusuri
/// koridor", bukan selisih tingginya. Ini sah karena tiap lantai adalah pulau NavMesh
/// terpisah (ADR-020 amandemen 020-B), sehingga satu polyline tidak pernah lintas lantai.
/// </summary>
public static class PathPolyline
{
    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    /// <summary>Panjang total polyline (diukur datar).</summary>
    public static float Length(IReadOnlyList<Vector3> pts)
    {
        if (pts == null || pts.Count < 2) return 0f;
        float total = 0f;
        for (int i = 1; i < pts.Count; i++)
            total += Vector3.Distance(Flat(pts[i - 1]), Flat(pts[i]));
        return total;
    }

    /// <summary>
    /// Jarak-sepanjang-rute (arc length) dari titik pada polyline yang paling dekat ke <paramref name="p"/>.
    /// Inilah "pengguna sudah sampai mana".
    /// </summary>
    public static float Project(IReadOnlyList<Vector3> pts, Vector3 p)
    {
        if (pts == null || pts.Count == 0) return 0f;
        if (pts.Count == 1) return 0f;

        Vector3 fp = Flat(p);
        float travelled = 0f, bestS = 0f, bestSqr = float.MaxValue;

        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 a = Flat(pts[i - 1]), b = Flat(pts[i]);
            Vector3 seg = b - a;
            float segLen = seg.magnitude;
            if (segLen > 1e-5f)
            {
                // t diklamp supaya proyeksi tidak keluar dari ruas.
                float t = Mathf.Clamp01(Vector3.Dot(fp - a, seg) / (segLen * segLen));
                Vector3 closest = a + seg * t;
                float sqr = (fp - closest).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; bestS = travelled + segLen * t; }
            }
            travelled += segLen;
        }
        return bestS;
    }

    /// <summary>
    /// Titik pada polyline sejauh <paramref name="s"/> dari awal. Di-clamp ke [0, Length],
    /// jadi s negatif menghasilkan titik awal dan s berlebih menghasilkan titik akhir.
    /// Y diinterpolasi mengikuti ruasnya supaya avatar tetap menempel lantai.
    /// </summary>
    public static Vector3 PointAt(IReadOnlyList<Vector3> pts, float s)
    {
        if (pts == null || pts.Count == 0) return Vector3.zero;
        if (pts.Count == 1 || s <= 0f) return pts[0];

        float travelled = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            float segLen = Vector3.Distance(Flat(pts[i - 1]), Flat(pts[i]));
            if (segLen > 1e-5f && s <= travelled + segLen)
                return Vector3.Lerp(pts[i - 1], pts[i], (s - travelled) / segLen);
            travelled += segLen;
        }
        return pts[pts.Count - 1];
    }
}
