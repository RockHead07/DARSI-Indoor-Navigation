using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests untuk matematika rel avatar pemandu (ADR-034 keputusan 2).
/// Ini logika yang paling mungkin salah diam-diam: kalau Project() atau PointAt()
/// meleset, avatar tetap "berjalan" tapi di tempat yang keliru, dan itu tidak
/// kelihatan sebagai error di console.
/// </summary>
[TestFixture]
public class PathPolylineTests
{
    // Rute berbentuk L: 10 m ke Z, lalu 10 m ke X. Total 20 m.
    private static List<Vector3> LShape() => new List<Vector3>
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(0f, 0f, 10f),
        new Vector3(10f, 0f, 10f),
    };

    [Test]
    public void Length_MenjumlahkanSemuaRuas()
    {
        Assert.AreEqual(20f, PathPolyline.Length(LShape()), 1e-3f);
    }

    [Test]
    public void Length_AmanUntukInputKosongDanSatuTitik()
    {
        Assert.AreEqual(0f, PathPolyline.Length(null));
        Assert.AreEqual(0f, PathPolyline.Length(new List<Vector3>()));
        Assert.AreEqual(0f, PathPolyline.Length(new List<Vector3> { Vector3.zero }));
    }

    [Test]
    public void PointAt_DiKlampDiKeduaUjung()
    {
        var p = LShape();
        Assert.AreEqual(new Vector3(0f, 0f, 0f), PathPolyline.PointAt(p, -5f));
        Assert.AreEqual(new Vector3(10f, 0f, 10f), PathPolyline.PointAt(p, 999f));
    }

    [Test]
    public void PointAt_MenyusuriRuasKeduaSetelahTikungan()
    {
        // 15 m = 10 m ruas pertama + 5 m masuk ruas kedua.
        var got = PathPolyline.PointAt(LShape(), 15f);
        Assert.AreEqual(5f, got.x, 1e-3f);
        Assert.AreEqual(10f, got.z, 1e-3f);
    }

    [Test]
    public void Project_MengabaikanKetinggianPengguna()
    {
        // Pengguna memegang HP setinggi 1,5 m tepat di atas titik 5 m pada rute.
        float s = PathPolyline.Project(LShape(), new Vector3(0f, 1.5f, 5f));
        Assert.AreEqual(5f, s, 1e-3f);
    }

    [Test]
    public void Project_MengklampKeRuasTerdekat_BukanGarisTakHingga()
    {
        // Titik jauh di belakang awal rute harus memetakan ke 0, bukan nilai negatif.
        Assert.AreEqual(0f, PathPolyline.Project(LShape(), new Vector3(0f, 0f, -50f)), 1e-3f);
    }

    [Test]
    public void Project_MemilihRuasYangBenarPadaRuteBerbentukL()
    {
        // Titik di dekat ujung ruas kedua, bukan di ruas pertama meski sama-sama dekat sumbu.
        float s = PathPolyline.Project(LShape(), new Vector3(8f, 0f, 10.2f));
        Assert.AreEqual(18f, s, 0.2f);
    }

    [Test]
    public void ProjectLaluPointAt_SalingKonsisten()
    {
        var p = LShape();
        var titik = new Vector3(0f, 0f, 7f);
        float s = PathPolyline.Project(p, titik);
        var balik = PathPolyline.PointAt(p, s);
        Assert.Less(Vector3.Distance(titik, balik), 1e-3f);
    }

    [Test]
    public void PointAt_TidakMeledakSaatTitikBerulang()
    {
        // Rute dari SDK bisa berisi corner kembar; ruas nol-panjang tidak boleh bikin NaN.
        var p = new List<Vector3> { Vector3.zero, Vector3.zero, new Vector3(0f, 0f, 5f) };
        var got = PathPolyline.PointAt(p, 2.5f);
        Assert.IsFalse(float.IsNaN(got.x) || float.IsNaN(got.z));
        Assert.AreEqual(2.5f, got.z, 1e-3f);
    }
}
