using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Coverage notice out-of-bounds (ADR-019). Saat kamera user keluar area ter-scan
/// (di luar NavMesh, > threshold), tampilkan billboard "Di luar jangkauan navigasi"
/// menghadap user + panah balik ke titik NavMesh terdekat.
///
/// PENTING (ADR-019): ini notice COVERAGE, bukan larangan fisik — lorongnya nyata &
/// bisa dijalani, jadi copy = "kami tak bisa memandu di sini", bukan "tidak boleh lewat".
/// Deteksi auto-derive dari tepi NavMesh (nol authoring); zona manual ditunda (YAGNI).
///
/// Aktif hanya setelah localize (ADR-007). Angka threshold di bawah cuma tebakan awal —
/// WAJIB di-tune saat scan RSI asli masuk (tepi map dummy kampus != tepi RSI). Caveat
/// NavMesh multi-lantai (ADR-018): kalau mesh gabungan, SamplePosition bisa cocok lantai
/// lain -> false in-bounds; cek saat scan asli.
/// </summary>
public class NavBoundaryNotifier : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Billboard world-space (Canvas uGUI) berisi pesan. Mulai nonaktif.")]
    [SerializeField] private GameObject sign;
    [Tooltip("Opsional: transform panah 'kembali ke jalur'. Diarahkan ke titik NavMesh terdekat.")]
    [SerializeField] private Transform returnArrow;

    [Header("Tuning (tune saat scan RSI asli — ADR-019)")]
    [Tooltip("Jarak off-mesh (m) untuk MUNCULKAN notice.")]
    [SerializeField] private float showAtDistance = 1.5f;
    [Tooltip("Jarak off-mesh (m) untuk SEMBUNYIKAN — < showAt (hysteresis, anti-kedip).")]
    [SerializeField] private float hideAtDistance = 0.8f;
    [Tooltip("Jarak (m) sign ditempatkan di depan user.")]
    [SerializeField] private float signAhead = 1.2f;
    [Tooltip("Interval cek (detik) — throttle, tak perlu tiap frame.")]
    [SerializeField] private float checkInterval = 0.15f;
    [Tooltip("Radius maksimum pencarian NavMesh terdekat.")]
    [SerializeField] private float sampleRadius = 5f;

    private Camera _cam;
    private bool _out;
    private float _nextCheck;
    private Vector3 _nearestOnMesh; // titik NavMesh terdekat saat terakhir dicek (arah balik)

    private void Start()
    {
        _cam = Camera.main;
        if (sign != null) sign.SetActive(false);
    }

    private void Update()
    {
        // Posisi cuma valid setelah localize (ADR-007). Sebelum itu, jangan deteksi apa-apa.
        if (_cam == null || sign == null ||
            UaaLEntryPoint.Instance == null || !UaaLEntryPoint.Instance.IsLocalized)
            return;

        if (Time.time >= _nextCheck)
        {
            _nextCheck = Time.time + checkInterval;
            Evaluate();
        }

        if (_out) FaceUser();
    }

    private void Evaluate()
    {
        Vector3 pos = _cam.transform.position;
        pos.y = 0f;

        float dist;
        if (NavMesh.SamplePosition(pos, out var hit, sampleRadius, NavMesh.AllAreas))
        {
            _nearestOnMesh = new Vector3(hit.position.x, 0f, hit.position.z);
            dist = Vector3.Distance(pos, _nearestOnMesh);
        }
        else
        {
            dist = sampleRadius; // tak ada mesh dalam radius → jelas di luar
        }

        // Hysteresis: threshold muncul != sembunyi, supaya tak kedip di tepi (localization drift).
        if (!_out && dist > showAtDistance) SetOut(true);
        else if (_out && dist < hideAtDistance) SetOut(false);
    }

    private void SetOut(bool value)
    {
        _out = value;
        sign.SetActive(value);
        if (value) PlaceAhead();
    }

    private void PlaceAhead()
    {
        Vector3 fwd = _cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        sign.transform.position = _cam.transform.position + fwd.normalized * signAhead;
    }

    // Sign menghadap user; panah (kalau ada) menunjuk balik ke titik NavMesh terdekat.
    private void FaceUser()
    {
        Vector3 look = sign.transform.position - _cam.transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            sign.transform.rotation = Quaternion.LookRotation(look);

        if (returnArrow != null)
        {
            Vector3 back = _nearestOnMesh - sign.transform.position;
            back.y = 0f;
            if (back.sqrMagnitude > 0.001f)
                returnArrow.rotation = Quaternion.LookRotation(back);
        }
    }

    // ponytail: self-check hysteresis tanpa scene/AR. Klik-kanan header komponen di editor.
    [ContextMenu("Debug/Self-check hysteresis")]
    private void DebugSelfCheck()
    {
        _out = false;
        // Naik melewati showAt → harus jadi out.
        SimulateDist(showAtDistance + 0.1f);
        Debug.Assert(_out, "[NavBoundaryNotifier] harus OUT saat dist > showAt");
        // Turun ke antara hideAt..showAt → tetap out (zona mati hysteresis).
        SimulateDist((showAtDistance + hideAtDistance) * 0.5f);
        Debug.Assert(_out, "[NavBoundaryNotifier] harus TETAP out di zona hysteresis");
        // Turun di bawah hideAt → kembali in.
        SimulateDist(hideAtDistance - 0.1f);
        Debug.Assert(!_out, "[NavBoundaryNotifier] harus IN saat dist < hideAt");
        Debug.Log("[NavBoundaryNotifier] self-check hysteresis OK");
    }

    private void SimulateDist(float dist)
    {
        if (!_out && dist > showAtDistance) _out = true;
        else if (_out && dist < hideAtDistance) _out = false;
    }
}
