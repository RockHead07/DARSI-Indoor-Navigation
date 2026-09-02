using System;
using System.Collections.Generic;
using UnityEngine;
using VRM;

/// <summary>
/// Driver lip-sync berbasis TIMELINE (Amandemen 033-B). Dipakai saat backend
/// mengirim batas waktu per kata (`words` dari `/api/assistant/tts`, engine
/// edge-tts). Saat `words` kosong (Tier 2 sherpa-onnx offline), pemanggil WAJIB
/// jatuh ke <see cref="AvatarSpeechLipSync"/> yang menganalisis audio — driver ini
/// tidak punya apa pun untuk dikerjakan tanpa timing.
///
/// Pendekatan lip-sync gabungan yang TIDAK butuh kalibrasi per suara:
///
/// 1. BENTUK viseme + KAPAN-nya diturunkan dari TEKS + batas waktu kata
///    (`WordBoundary` bawaan edge-tts). Ini deterministik: tidak menebak vokal dari
///    spektrum suara, jadi ganti suara TTS tidak menuntut kalibrasi ulang — persis
///    kelemahan uLipSync/MFCC yang mau dihindari.
/// 2. SEBERAPA LEBAR mulut membuka diambil dari amplitudo audio yang sedang diputar
///    (RMS). Batas kata saja tidak tahu keras-pelan; tanpa ini mulut membuka sama
///    lebar untuk bisikan maupun seruan, dan hasilnya terlihat mekanis.
///
/// Batas waktu kata dari edge-tts sudah divalidasi terhadap durasi audio sungguhan
/// (tick 100 ns; kalimat 20 kata, ekor hening 0,86 s masuk akal), jadi jangkar
/// waktunya dipercaya. Yang BELUM terbukti dan justru sedang diuji di sini adalah
/// pembagian durasi DI DALAM kata — tidak ada sumber yang memberi rasio baku, jadi
/// bobotnya sengaja dibuat bisa diatur lewat Inspector, bukan angka ajaib di kode.
///
/// Batas yang diketahui: rig VRM 0.x ini cuma punya 5 viseme vokal (A/I/U/E/O),
/// tidak ada bentuk konsonan. Konsonan karena itu cuma bisa dipetakan jadi "mulut
/// mengatup sebentar", bukan artikulasi bibir yang sebenarnya.
/// </summary>
[DisallowMultipleComponent]
public class VisemeTimelineLipSync : MonoBehaviour
{
    [Header("Sumber Data")]
    [Tooltip("JSON berisi batas waktu tiap kata (hasil WordBoundary edge-tts).")]
    [SerializeField] private TextAsset timelineJson;

    [Tooltip("Klip audio yang HARUS berpasangan dengan JSON di atas.")]
    [SerializeField] private AudioClip klip;

    [Header("Komponen Target")]
    [SerializeField] private VRMBlendShapeProxy blendShapeProxy;
    [SerializeField] private AudioSource audioSource;

    [Header("Pembagian Durasi di Dalam Kata")]
    [Tooltip("Bobot durasi untuk huruf vokal. Vokal ditahan lebih lama dari konsonan " +
             "pada bicara normal, tapi rasio pastinya tidak ada rujukan bakunya — " +
             "atur sambil melihat hasilnya, jangan percaya satu angka bawaan.")]
    [Range(1f, 5f)] [SerializeField] private float bobotVokal = 2.0f;

    [Tooltip("Bobot durasi untuk konsonan.")]
    [Range(0.2f, 3f)] [SerializeField] private float bobotKonsonan = 1.0f;

    [Header("Amplitudo (dari audio, bukan dari teks)")]
    [Tooltip("Penguatan amplitudo RMS menjadi lebar bukaan mulut.")]
    [Range(1f, 20f)] [SerializeField] private float penguatanAmplitudo = 8.0f;

    [Tooltip("Bukaan minimum saat sebuah viseme sedang aktif, supaya mulut tidak " +
             "terlihat nyaris tertutup pada suku kata yang pelan.")]
    [Range(0f, 0.5f)] [SerializeField] private float bukaanMinimum = 0.15f;

    [Header("Koartikulasi")]
    [Tooltip("Bobot vokal BERIKUTNYA yang sudah mulai dibentuk selama konsonan biasa. " +
             "Mulut manusia tidak menutup di tiap konsonan -- ia sudah bersiap ke vokal " +
             "berikutnya. Nol berarti kembali ke perilaku lama (menutup di tiap konsonan), " +
             "yang terbukti membuat vokal sebelumnya 'bocor' karena peredaman tidak sempat " +
             "turun-naik dalam slot ~60 ms.")]
    [Range(0f, 1f)] [SerializeField] private float bobotKoartikulasi = 0.45f;

    [Header("Perataan (mengikuti kecepatan bicara, bukan konstanta)")]
    [Tooltip("Waktu perataan MEMBUKA sebagai pecahan dari panjang slot yang sedang " +
             "berjalan. Sengaja relatif, bukan angka tetap: suara yang berbeda bicara " +
             "dengan kecepatan berbeda (terukur: Thalita 21% lebih cepat dari " +
             "GadisNeural untuk kalimat yang sama), dan konstanta tetap membuat mulut " +
             "tidak sempat mencapai bentuk vokalnya pada suara yang cepat -- puncak I " +
             "sempat jatuh 0,877 ke 0,653 gara-gara ini. Nilai relatif membuat driver " +
             "menyesuaikan sendiri tanpa disetel ulang tiap ganti suara.")]
    [Range(0.1f, 0.8f)] [SerializeField] private float rasioPerataan = 0.35f;

    [Tooltip("Menutup diredam lebih cepat daripada membuka: mulut yang lambat menutup " +
             "terlihat menganga tertinggal di belakang suara.")]
    [Range(0.2f, 1f)] [SerializeField] private float rasioPerataanTutup = 0.55f;

    [Tooltip("Batas bawah dan atas waktu perataan (detik), pengaman untuk slot yang " +
             "ekstrem pendek/panjang.")]
    [SerializeField] private Vector2 batasPerataan = new Vector2(0.012f, 0.06f);

    // Viseme: 0=A 1=I 2=U 3=E 4=O, -1 = konsonan
    private struct Slot
    {
        public float mulai;
        public float akhir;
        public int viseme;          // >=0 kalau vokal
        public int visemeSambungan; // vokal berikutnya yang sudah mulai dibentuk (koartikulasi)
        public bool tutupPenuh;     // konsonan bibir (b/p/m): mulut benar-benar mengatup
        public string label;
    }

    private readonly List<Slot> _slot = new List<Slot>();
    private int _indeksSlot;

    private readonly float[] _sampel = new float[512];
    private float _a, _i, _u, _e, _o;
    private float _vA, _vI, _vU, _vE, _vO;

    private static readonly BlendShapeKey KeyA = BlendShapeKey.CreateFromPreset(BlendShapePreset.A);
    private static readonly BlendShapeKey KeyI = BlendShapeKey.CreateFromPreset(BlendShapePreset.I);
    private static readonly BlendShapeKey KeyU = BlendShapeKey.CreateFromPreset(BlendShapePreset.U);
    private static readonly BlendShapeKey KeyE = BlendShapeKey.CreateFromPreset(BlendShapePreset.E);
    private static readonly BlendShapeKey KeyO = BlendShapeKey.CreateFromPreset(BlendShapePreset.O);

    // Diagnostik, dibaca probe pengukur. Bukan untuk dipakai logika.
    public string VisemeAktif { get; private set; } = "-";
    public float AmplitudoTerakhir { get; private set; }
    public int JumlahSlot => _slot.Count;
    public bool SedangBicara => audioSource != null && audioSource.isPlaying;

    private void Awake()
    {
        if (blendShapeProxy == null) blendShapeProxy = GetComponentInChildren<VRMBlendShapeProxy>(true);
        if (blendShapeProxy == null) blendShapeProxy = GetComponentInParent<VRMBlendShapeProxy>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>(true);
    }

    /// <summary>Bangun timeline dari file JSON uji (dipakai untuk pengujian mandiri
    /// di Editor via ContextMenu/panggilan manual -- BUKAN jalur produksi, dan
    /// sengaja TIDAK dipanggil dari Awake: timelineJson kosong di produksi, jadi
    /// memanggilnya otomatis cuma menghasilkan log error palsu tiap scene dimuat.</summary>
    [ContextMenu("Debug/Bangun Timeline dari Fixture")]
    public void BangunTimeline()
    {
        if (timelineJson == null)
        {
            Debug.LogError("[VisemeTimeline] timelineJson kosong, tidak ada yang bisa dibangun.");
            return;
        }

        var fixtureData = JsonUtility.FromJson<FixtureJson>(timelineJson.text);
        if (fixtureData?.words == null || fixtureData.words.Length == 0)
        {
            Debug.LogError("[VisemeTimeline] JSON tidak berisi daftar kata yang bisa dibaca.");
            return;
        }

        BangunDariKata(fixtureData.words);
    }

    /// <summary>Jalur PRODUKSI: bangun timeline dari batas kata yang dikirim backend.
    /// Mengembalikan false kalau tidak ada yang bisa dibangun, supaya pemanggil tahu
    /// harus jatuh ke driver berbasis analisis audio.</summary>
    public bool BangunDariKata(KataJson[] kata)
    {
        _slot.Clear();
        _indeksSlot = 0;

        if (kata == null || kata.Length == 0) return false;

        foreach (var k in kata) TambahSlotUntukKata(k);

        return _slot.Count > 0;
    }

    /// <summary>Hentikan timeline dan kembalikan mulut ke posisi diam. Wajib dipanggil
    /// saat beralih ke driver lain, kalau tidak slot lama masih menggerakkan bibir.</summary>
    public void HentikanTimeline()
    {
        _slot.Clear();
        _indeksSlot = 0;
        VisemeAktif = "-";
    }

    private void TambahSlotUntukKata(KataJson kata)
    {
        {
            var unit = PecahJadiUnit(kata.text);
            if (unit.Count == 0) return;

            // Durasi kata dibagi proporsional menurut bobot tiap unit.
            float totalBobot = 0f;
            foreach (var u in unit) totalBobot += u.vokal >= 0 ? bobotVokal : bobotKonsonan;

            float t = kata.start;
            float durasiKata = Mathf.Max(kata.end - kata.start, 0.0001f);
            for (int n = 0; n < unit.Count; n++)
            {
                var u = unit[n];
                float bobot = u.vokal >= 0 ? bobotVokal : bobotKonsonan;
                float panjang = durasiKata * (bobot / totalBobot);

                // Untuk konsonan biasa, cari vokal BERIKUTNYA di kata yang sama:
                // itulah bentuk yang sudah mulai disiapkan mulut selama konsonan.
                int sambungan = -1;
                if (u.vokal < 0 && !u.bibir)
                {
                    for (int m = n + 1; m < unit.Count; m++)
                        if (unit[m].vokal >= 0) { sambungan = unit[m].vokal; break; }
                }

                _slot.Add(new Slot
                {
                    mulai = t,
                    akhir = t + panjang,
                    viseme = u.vokal,
                    visemeSambungan = sambungan,
                    tutupPenuh = u.vokal < 0 && u.bibir,
                    label = u.teks,
                });
                t += panjang;
            }
        }
    }

    /// <summary>Mulai memutar klip fixture dan menjalankan timeline dari awal.
    /// Jalur PENGUJIAN MANDIRI di Editor; di produksi audio diputar AvatarAudioClient.</summary>
    public void Putar()
    {
        if (audioSource == null || klip == null)
        {
            Debug.LogError("[VisemeTimeline] audioSource atau klip belum diisi.");
            return;
        }
        _indeksSlot = 0;
        audioSource.clip = klip;
        audioSource.Play();
    }

    private void Update()
    {
        float targetA = 0f, targetI = 0f, targetU = 0f, targetE = 0f, targetO = 0f;

        if (audioSource != null && audioSource.isPlaying && _slot.Count > 0)
        {
            float t = audioSource.time;

            // Timeline maju searah, jadi cukup geser indeks — tidak perlu cari ulang
            // dari awal tiap frame.
            while (_indeksSlot < _slot.Count - 1 && t >= _slot[_indeksSlot].akhir) _indeksSlot++;
            while (_indeksSlot > 0 && t < _slot[_indeksSlot].mulai) _indeksSlot--;

            var slot = _slot[_indeksSlot];
            bool didalamSlot = t >= slot.mulai && t < slot.akhir;
            _panjangSlotKini = Mathf.Max(slot.akhir - slot.mulai, 0.001f);

            // Amplitudo diambil dari audio yang benar-benar terdengar. Inilah bagian
            // yang tidak bisa diketahui dari teks: keras-pelannya ucapan.
            audioSource.GetOutputData(_sampel, 0);
            float jumlah = 0f;
            for (int n = 0; n < _sampel.Length; n++) jumlah += _sampel[n] * _sampel[n];
            float rms = Mathf.Sqrt(jumlah / _sampel.Length);
            AmplitudoTerakhir = rms;

            float bukaan = Mathf.Clamp01(bukaanMinimum + rms * penguatanAmplitudo);

            if (didalamSlot && slot.viseme >= 0)
            {
                Terapkan(slot.viseme, bukaan, ref targetA, ref targetI, ref targetU, ref targetE, ref targetO);
                VisemeAktif = slot.label;
            }
            else if (didalamSlot && !slot.tutupPenuh && slot.visemeSambungan >= 0)
            {
                // Konsonan biasa: mulut TIDAK menutup, tapi sudah mulai membentuk
                // vokal berikutnya. Ini memperbaiki cacat yang terukur di uji pertama --
                // memaksa target nol tiap konsonan membuat peredaman tidak sempat
                // turun-naik dalam slot ~60 ms, sehingga vokal sebelumnya terlihat bocor.
                Terapkan(slot.visemeSambungan, bukaan * bobotKoartikulasi,
                         ref targetA, ref targetI, ref targetU, ref targetE, ref targetO);
                VisemeAktif = slot.label + "~" + NamaViseme(slot.visemeSambungan);
            }
            else
            {
                // Konsonan bibir (b/p/m) atau di luar slot: mulut benar-benar mengatup.
                VisemeAktif = didalamSlot ? slot.label + "(tutup)" : "-";
            }
        }
        else
        {
            VisemeAktif = "-";
            AmplitudoTerakhir = 0f;
        }

        // Menutup diredam lebih cepat daripada membuka: mulut yang lambat menutup
        // terlihat menganga tertinggal di belakang suara.
        float dt = Time.deltaTime;
        _a = Redam(_a, targetA, ref _vA, dt);
        _i = Redam(_i, targetI, ref _vI, dt);
        _u = Redam(_u, targetU, ref _vU, dt);
        _e = Redam(_e, targetE, ref _vE, dt);
        _o = Redam(_o, targetO, ref _vO, dt);

        if (blendShapeProxy == null) return;
        blendShapeProxy.ImmediatelySetValue(KeyA, _a);
        blendShapeProxy.ImmediatelySetValue(KeyI, _i);
        blendShapeProxy.ImmediatelySetValue(KeyU, _u);
        blendShapeProxy.ImmediatelySetValue(KeyE, _e);
        blendShapeProxy.ImmediatelySetValue(KeyO, _o);
    }

    /// <summary>Panjang slot yang sedang berjalan, dipakai menurunkan waktu perataan.
    /// Diperbarui tiap frame di Update.</summary>
    private float _panjangSlotKini = 0.08f;

    private float Redam(float kini, float target, ref float kecepatan, float dt)
    {
        float rasio = target < kini ? rasioPerataanTutup : rasioPerataan;
        float waktu = Mathf.Clamp(_panjangSlotKini * rasio, batasPerataan.x, batasPerataan.y);
        return Mathf.SmoothDamp(kini, target, ref kecepatan, waktu, Mathf.Infinity, dt);
    }

    private static void Terapkan(int viseme, float nilai,
                                 ref float a, ref float i, ref float u, ref float e, ref float o)
    {
        switch (viseme)
        {
            case 0: a = nilai; break;
            case 1: i = nilai; break;
            case 2: u = nilai; break;
            case 3: e = nilai; break;
            case 4: o = nilai; break;
        }
    }

    private static string NamaViseme(int v)
    {
        switch (v)
        {
            case 0: return "A";
            case 1: return "I";
            case 2: return "U";
            case 3: return "E";
            case 4: return "O";
            default: return "-";
        }
    }

    // ── Pemecahan kata jadi unit bunyi ──

    private struct Unit
    {
        public string teks;
        public int vokal;   // -1 kalau konsonan
        public bool bibir;  // konsonan bilabial (b/p/m): satu-satunya yang benar-benar
                            // menutup bibir dan karena itu terlihat jelas di rig 5-viseme
    }

    // Digraf Indonesia: dua huruf yang mewakili SATU bunyi. Kalau dipecah per huruf,
    // "ruang" jadi r-u-a-n-g (dua konsonan terpisah di akhir) padahal "ng" satu bunyi,
    // dan porsi durasinya jadi melenceng.
    private static readonly string[] Digraf = { "ng", "ny", "sy", "kh" };

    private static List<Unit> PecahJadiUnit(string kata)
    {
        var hasil = new List<Unit>();
        if (string.IsNullOrEmpty(kata)) return hasil;

        string k = kata.ToLowerInvariant();
        int idx = 0;
        while (idx < k.Length)
        {
            char c = k[idx];

            if (!char.IsLetter(c)) { idx++; continue; }

            // Cek digraf lebih dulu, sebelum diperlakukan sebagai huruf tunggal.
            if (idx + 1 < k.Length)
            {
                string pasangan = k.Substring(idx, 2);
                bool cocok = false;
                foreach (var d in Digraf) if (pasangan == d) { cocok = true; break; }
                if (cocok)
                {
                    // Tidak ada digraf Indonesia yang bilabial, jadi bibir = false.
                    hasil.Add(new Unit { teks = pasangan, vokal = -1, bibir = false });
                    idx += 2;
                    continue;
                }
            }

            int v = IndeksVokal(c);
            hasil.Add(new Unit
            {
                teks = c.ToString(),
                vokal = v,
                bibir = v < 0 && (c == 'b' || c == 'p' || c == 'm'),
            });
            idx++;
        }
        return hasil;
    }

    private static int IndeksVokal(char c)
    {
        switch (c)
        {
            case 'a': return 0;
            case 'i': return 1;
            case 'u': return 2;
            case 'e': return 3;   // pepet /ə/ dan taling /e/ tidak dibedakan: rig cuma punya satu bentuk E
            case 'o': return 4;
            default: return -1;
        }
    }

    // ── Bentuk JSON fixture ──

    /// <summary>Batas waktu satu kata, dalam DETIK dari awal klip. Bentuknya sengaja
    /// sama persis dengan field `words` di response `/api/assistant/tts` supaya
    /// JsonUtility bisa memetakannya langsung tanpa lapisan konversi.</summary>
    [Serializable]
    public class KataJson
    {
        public string text;
        public float start;
        public float end;
    }

    [Serializable]
    private class FixtureJson
    {
        public string text;
        public string voice;
        public KataJson[] words;
    }
}
