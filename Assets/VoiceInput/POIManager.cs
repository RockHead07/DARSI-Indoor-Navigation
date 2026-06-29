using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Manager untuk scanning dan pencarian POI di scene.
/// Assign poiRoot ke Transform parent yang berisi semua POI.
/// </summary>
public class POIManager : MonoBehaviour
{
    [Header("POI Root")]
    [Tooltip("Transform parent yang berisi semua POI. Semua children (termasuk inactive) akan di-scan.")]
    [SerializeField] private Transform poiRoot;

    private readonly List<POIData> poiList = new List<POIData>();
    private readonly Dictionary<string, POIData> lookupDict = new Dictionary<string, POIData>();

    // Dictionary terpisah untuk tracking apakah suatu key berasal dari sinonim.
    // Digunakan untuk memberi bobot lebih rendah pada match dari sinonim (0.8x)
    // dibanding match dari nama langsung (1.0x).
    private readonly Dictionary<string, bool> isSynonymKey = new Dictionary<string, bool>();

    private static readonly Dictionary<string, string[]> sinonimMap = new Dictionary<string, string[]>
    {
        { "MMB Studio", new[] { 
            "studio", "mmb", "multimedia", "lab multimedia", 
            "studio multimedia", "ruang mmb", "lab mmb" 
        }},
        { "Lab Teori 203", new[] { 
            "lab 203", "teori 203", "ruang 203", "dua kosong tiga",
            "laboratorium 203", "kelas 203"
        }},
        { "Lab Teori 202", new[] { 
            "lab 202", "teori 202", "ruang 202", "dua kosong dua",
            "laboratorium 202", "kelas 202"
        }},
        { "Lab Teori 201", new[] { 
            "lab 201", "teori 201", "ruang 201", "dua kosong satu",
            "laboratorium 201", "kelas 201"
        }},
        { "Lab Mikrotik", new[] { 
            "mikrotik", "lab jaringan", "jaringan", "networking",
            "lab network", "ruang mikrotik", "cisco"
        }},
        { "Mushola", new[] { 
            "masjid", "sholat", "solat", "salat", "ibadah",
            "musholla", "sembahyang", "ngaji", "wudhu",
            "mau salat", "mau sholat", "tempat sholat",
            "tempat ibadah", "surau"
        }},
        { "BAAK", new[] { 
            "administrasi", "tata usaha", "akademik", "surat",
            "baak", "biro", "administrasi akademik",
            "ngurus surat", "legalisir", "transkrip"
        }},
        { "Perpustakaan", new[] { 
            "perpus", "library", "buku", "pustaka",
            "ruang baca", "baca buku", "cari buku",
            "pinjam buku", "referensi"
        }},
        { "Lab 102", new[] { 
            "lab seratus dua", "ruang 102", "satu kosong dua",
            "laboratorium 102", "kelas 102", "102"
        }},
        { "Lab 103", new[] { 
            "lab seratus tiga", "ruang 103", "satu kosong tiga",
            "laboratorium 103", "kelas 103", "103"
        }},
    };

    void Awake()
    {
        ScanPOIs();
    }

    /// <summary>
    /// Scan ulang semua POI di bawah poiRoot.
    /// Dipanggil otomatis di Awake, bisa dipanggil manual jika POI berubah runtime.
    /// </summary>
    public void ScanPOIs()
    {
        poiList.Clear();
        lookupDict.Clear();
        isSynonymKey.Clear(); // Bersihkan juga tracking sinonim saat scan ulang

        if (poiRoot == null)
        {
            Debug.LogWarning("[POIManager] poiRoot belum di-assign! Tidak bisa scan POI.");
            return;
        }

        // GetComponentsInChildren dengan includeInactive = true
        POIData[] allPOIs = poiRoot.GetComponentsInChildren<POIData>(true);

        foreach (POIData poi in allPOIs)
        {
            poiList.Add(poi);

            // Register EffectiveName
            string normalizedName = Normalize(poi.EffectiveName);
            if (!string.IsNullOrEmpty(normalizedName) && !lookupDict.ContainsKey(normalizedName))
            {
                lookupDict[normalizedName] = poi;
                // Tandai bahwa key ini bukan dari sinonim (nama langsung)
                isSynonymKey[normalizedName] = false;
            }

            // Register sinonim dari data inspector
            if (poi.sinonim != null)
            {
                foreach (string syn in poi.sinonim)
                {
                    if (string.IsNullOrWhiteSpace(syn)) continue;
                    string normalizedSyn = Normalize(syn);
                    if (!lookupDict.ContainsKey(normalizedSyn))
                    {
                        lookupDict[normalizedSyn] = poi;
                        // Tandai bahwa key ini berasal dari sinonim
                        isSynonymKey[normalizedSyn] = true;
                    }
                }
            }

            // Register sinonim tambahan dari sinonimMap
            foreach (var kvp in sinonimMap)
            {
                // Cocokkan nama POI dengan key di sinonimMap
                if (normalizedName == Normalize(kvp.Key))
                {
                    foreach (string syn in kvp.Value)
                    {
                        if (string.IsNullOrWhiteSpace(syn)) continue;
                        string normalizedSyn = Normalize(syn);
                        if (!lookupDict.ContainsKey(normalizedSyn))
                        {
                            lookupDict[normalizedSyn] = poi;
                            // Tandai bahwa key ini berasal dari sinonim
                            isSynonymKey[normalizedSyn] = true;
                        }
                    }
                }
            }
        }

        Debug.Log($"[POIManager] Scanned {poiList.Count} POI(s) dari '{poiRoot.name}', {lookupDict.Count} entri lookup.");
    }

    /// <summary>
    /// Cari POI terbaik berdasarkan query string.
    /// Urutan prioritas: exact → contains → levenshtein → word overlap (dengan bobot sinonim) → kategori tiebreaker.
    /// </summary>
    public POIData FindBestMatch(string query)
    {
        // Panggil versi lengkap tanpa hint kategori
        return FindBestMatchWithContext(query, null);
    }

    /// <summary>
    /// Cari POI terbaik dengan hint kategori opsional.
    /// Jika kategoriHint diberikan, filter kandidat ke kategori itu dulu sebelum matching.
    /// </summary>
    /// <param name="query">Teks query dari Ollama atau user input.</param>
    /// <param name="kategoriHint">Hint kategori opsional (boleh null/kosong). Jika diisi, hanya POI dengan kategori tersebut yang dicari.</param>
    /// <returns>POIData yang paling cocok, atau null jika tidak ditemukan.</returns>
    public POIData FindBestMatchWithContext(string query, string kategoriHint)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        string normalizedQuery = Normalize(query);

        // Tentukan dictionary yang digunakan untuk pencarian.
        // Jika kategoriHint diberikan, filter hanya entri yang kategorinya cocok.
        Dictionary<string, POIData> searchDict;
        if (!string.IsNullOrWhiteSpace(kategoriHint))
        {
            // Normalisasi hint kategori agar perbandingan konsisten
            string normalizedHint = Normalize(kategoriHint);
            searchDict = new Dictionary<string, POIData>();
            foreach (var kvp in lookupDict)
            {
                // Cek apakah POI memiliki kategori yang cocok dengan hint
                string poiKategori = Normalize(kvp.Value.kategori);
                if (poiKategori == normalizedHint)
                {
                    searchDict[kvp.Key] = kvp.Value;
                }
            }
            Debug.Log($"[POIManager] Filter kategori '{kategoriHint}': {searchDict.Count} kandidat dari {lookupDict.Count} total.");
        }
        else
        {
            // Tidak ada filter, gunakan semua entri
            searchDict = lookupDict;
        }

        // 1) Exact match di dictionary — prioritas tertinggi
        if (searchDict.TryGetValue(normalizedQuery, out POIData exactMatch))
        {
            Debug.Log($"[POIManager] Exact match: '{query}' -> '{exactMatch.EffectiveName}'");
            return exactMatch;
        }

        // 2) Contains match — query ada di dalam key, atau key ada di dalam query
        POIData bestContains = null;
        int bestContainsLen = 0;

        foreach (var kvp in searchDict)
        {
            // Key yang terkandung dalam query (misal query="pergi ke mmb studio", key="mmb studio")
            if (normalizedQuery.Contains(kvp.Key) && kvp.Key.Length > bestContainsLen)
            {
                bestContains = kvp.Value;
                bestContainsLen = kvp.Key.Length;
            }
            // Query yang terkandung dalam key (misal query="mmb", key="mmb studio")
            else if (kvp.Key.Contains(normalizedQuery) && normalizedQuery.Length > bestContainsLen)
            {
                bestContains = kvp.Value;
                bestContainsLen = normalizedQuery.Length;
            }
        }

        if (bestContains != null)
        {
            Debug.Log($"[POIManager] Contains match: '{query}' -> '{bestContains.EffectiveName}'");
            return bestContains;
        }

        // 3) Levenshtein distance matching — toleransi typo hingga jarak 2.
        // Berguna saat pengguna mengetik/mengucapkan nama POI dengan sedikit salah,
        // misalnya "BAAK" vs "BAK" (jarak 1), atau "tolet" vs "toilet" (jarak 1).
        POIData bestLevenshtein = null;
        int bestLevenshteinDist = int.MaxValue; // Mulai dari jarak tak hingga

        foreach (var kvp in searchDict)
        {
            // Hitung jarak Levenshtein antara query dan setiap key
            int dist = ComputeLevenshteinDistance(normalizedQuery, kvp.Key);
            // Terima match jika jaraknya <= 2 (toleransi 2 karakter typo)
            if (dist <= 2 && dist < bestLevenshteinDist)
            {
                bestLevenshteinDist = dist;
                bestLevenshtein = kvp.Value;
            }
        }

        if (bestLevenshtein != null)
        {
            Debug.Log($"[POIManager] Levenshtein match: '{query}' -> '{bestLevenshtein.EffectiveName}' (jarak: {bestLevenshteinDist})");
            return bestLevenshtein;
        }

        // 4) Word overlap scoring dengan bobot sinonim.
        // Kata yang cocok dari nama langsung mendapat bobot 1.0x,
        // kata yang cocok dari sinonim mendapat bobot 0.8x.
        // Ini memastikan match nama langsung selalu menang vs sinonim dengan jumlah kata sama.
        string[] queryWords = normalizedQuery.Split(' ');
        POIData bestScored = null;
        float bestScore = 0f; // Diubah ke float untuk mendukung bobot desimal

        // Dictionary untuk tracking skor per POI (untuk tiebreaker kategori nanti)
        List<(POIData poi, float score)> scoredCandidates = new List<(POIData, float)>();

        foreach (var kvp in searchDict)
        {
            float score = 0f;
            // Cek apakah key ini berasal dari sinonim atau nama langsung
            bool isSynonym = isSynonymKey.ContainsKey(kvp.Key) && isSynonymKey[kvp.Key];
            // Bobot: 1.0 untuk nama langsung, 0.8 untuk sinonim
            float weightMultiplier = isSynonym ? 0.8f : 1.0f;

            foreach (string word in queryWords)
            {
                if (word.Length < 2) continue; // Skip karakter tunggal
                if (kvp.Key.Contains(word))
                {
                    // Skor = panjang kata * bobot (sinonim lebih rendah)
                    score += word.Length * weightMultiplier;
                }
            }

            if (score > 0f)
            {
                scoredCandidates.Add((kvp.Value, score));
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestScored = kvp.Value;
            }
        }

        // 5) Kategori tiebreaker — jika ada beberapa POI dengan skor sama,
        // pilih yang kategorinya cocok dengan kata dalam query.
        if (bestScored != null && bestScore >= 3f)
        {
            // Kumpulkan semua kandidat dengan skor tertinggi yang sama
            List<POIData> tiedPOIs = new List<POIData>();
            foreach (var candidate in scoredCandidates)
            {
                // Bandingkan skor dengan toleransi float (0.001)
                if (Math.Abs(candidate.score - bestScore) < 0.001f)
                {
                    // Hindari duplikasi POI yang sama (bisa muncul dari multiple keys)
                    if (!tiedPOIs.Contains(candidate.poi))
                    {
                        tiedPOIs.Add(candidate.poi);
                    }
                }
            }

            // Jika ada 2+ POI dengan skor sama, gunakan kategori sebagai tiebreaker
            if (tiedPOIs.Count > 1)
            {
                foreach (POIData tiedPoi in tiedPOIs)
                {
                    // Normalisasi kategori POI
                    string poiKategori = Normalize(tiedPoi.kategori);
                    if (string.IsNullOrEmpty(poiKategori)) continue;

                    // Cek apakah salah satu kata dalam query cocok dengan kategori
                    foreach (string word in queryWords)
                    {
                        if (word.Length < 2) continue;
                        if (poiKategori.Contains(word))
                        {
                            // Pilih POI ini karena kategorinya cocok dengan query
                            Debug.Log($"[POIManager] Kategori tiebreaker: '{query}' -> '{tiedPoi.EffectiveName}' (kategori: {tiedPoi.kategori}, skor: {bestScore})");
                            return tiedPoi;
                        }
                    }
                }
            }

            // Tidak ada tiebreaker kategori, gunakan best score biasa
            Debug.Log($"[POIManager] Scored match: '{query}' -> '{bestScored.EffectiveName}' (skor: {bestScore})");
            return bestScored;
        }

        Debug.LogWarning($"[POIManager] Tidak ditemukan match untuk: '{query}'");
        return null;
    }

    /// <summary>
    /// Hitung jarak Levenshtein antara dua string menggunakan dynamic programming.
    /// Jarak Levenshtein = jumlah minimum operasi (insert, delete, replace) untuk mengubah
    /// string sumber menjadi string target. Semakin kecil jaraknya, semakin mirip kedua string.
    /// Implementasi dari scratch tanpa library eksternal.
    /// </summary>
    /// <param name="source">String sumber.</param>
    /// <param name="target">String target.</param>
    /// <returns>Jarak Levenshtein (integer >= 0).</returns>
    private static int ComputeLevenshteinDistance(string source, string target)
    {
        // Jika salah satu string kosong, jaraknya = panjang string yang lain
        if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
        if (string.IsNullOrEmpty(target)) return source.Length;

        int sourceLen = source.Length; // Panjang string sumber
        int targetLen = target.Length; // Panjang string target

        // Buat matriks DP berukuran (sourceLen+1) x (targetLen+1).
        // dp[i,j] = jarak minimum untuk mengubah source[0..i-1] menjadi target[0..j-1].
        int[,] dp = new int[sourceLen + 1, targetLen + 1];

        // Inisialisasi baris pertama: mengubah string kosong menjadi target[0..j-1]
        // membutuhkan j operasi insert.
        for (int j = 0; j <= targetLen; j++)
        {
            dp[0, j] = j;
        }

        // Inisialisasi kolom pertama: mengubah source[0..i-1] menjadi string kosong
        // membutuhkan i operasi delete.
        for (int i = 0; i <= sourceLen; i++)
        {
            dp[i, 0] = i;
        }

        // Isi matriks DP dengan pendekatan bottom-up
        for (int i = 1; i <= sourceLen; i++)
        {
            for (int j = 1; j <= targetLen; j++)
            {
                // Biaya substitusi: 0 jika karakter sama, 1 jika berbeda
                int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;

                // Ambil minimum dari 3 operasi:
                // 1. Delete: dp[i-1, j] + 1 (hapus karakter dari source)
                // 2. Insert: dp[i, j-1] + 1 (sisipkan karakter ke source)
                // 3. Replace: dp[i-1, j-1] + cost (ganti karakter, gratis jika sama)
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost
                );
            }
        }

        // Hasil akhir ada di pojok kanan bawah matriks
        return dp[sourceLen, targetLen];
    }

    /// <summary>
    /// Normalisasi string: lowercase, hapus punctuation, collapse whitespace.
    /// </summary>
    private static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Lowercase
        string result = input.ToLowerInvariant();

        // Hapus semua karakter non-alphanumeric kecuali spasi
        result = Regex.Replace(result, @"[^\w\s]", "", RegexOptions.None);

        // Collapse whitespace
        result = Regex.Replace(result, @"\s+", " ").Trim();

        return result;
    }

    /// <summary>
    /// Mendapatkan semua POI yang sudah di-scan. Readonly.
    /// </summary>
    public IReadOnlyList<POIData> GetAllPOIs()
    {
        return poiList.AsReadOnly();
    }
}
