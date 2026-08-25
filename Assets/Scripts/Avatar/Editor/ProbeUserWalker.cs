// PROBE SEMENTARA - dihapus bersama GuideWalkProbe.
// Menggerakkan ARCamera menyusuri rute yang sudah ditangkap, DARI DALAM Play mode.
// Wajib di sini, bukan di EditorApplication.update, supaya Time.deltaTime-nya sama
// dengan Update() milik AIAvatarGuideController — kalau berbeda, pengukuran
// "avatar tertinggal berapa meter" tidak sah.
using System.Collections.Generic;
using UnityEngine;

public class ProbeUserWalker : MonoBehaviour
{
    private Transform _cam;
    private List<Vector3> _route;
    private float _speed, _height, _total;

    public float Travelled { get; private set; }

    public void Init(Transform cam, List<Vector3> route, float speed, float height)
    {
        _cam = cam; _route = route; _speed = speed; _height = height;
        _total = PathPolyline.Length(route);
    }

    private void Update()
    {
        if (_cam == null || _route == null || _route.Count < 2) return;
        Travelled = Mathf.Min(Travelled + _speed * Time.deltaTime, _total);
        var p = PathPolyline.PointAt(_route, Travelled);
        p.y += _height;
        _cam.position = p;
    }
}
