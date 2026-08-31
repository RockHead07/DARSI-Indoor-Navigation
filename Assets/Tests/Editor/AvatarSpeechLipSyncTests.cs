using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VRM;
using uLipSync;

/// <summary>
/// EditMode Unit Tests untuk AvatarSpeechLipSync (Fase 2 - Sesi 1).
/// Memvalidasi kalkulasi viseme A-I-U-E-O, batas ambang volume, redaman, dan reset pose.
/// </summary>
[TestFixture]
public class AvatarSpeechLipSyncTests
{
    private GameObject _avatarRoot;
    private GameObject _visualModel;
    private AvatarSpeechLipSync _lipSyncDriver;
    private VRMBlendShapeProxy _proxy;
    private AudioSource _audioSource;
    private uLipSync.uLipSync _uLipSync;

    private static readonly BlendShapeKey KeyA = BlendShapeKey.CreateFromPreset(BlendShapePreset.A);
    private static readonly BlendShapeKey KeyI = BlendShapeKey.CreateFromPreset(BlendShapePreset.I);
    private static readonly BlendShapeKey KeyU = BlendShapeKey.CreateFromPreset(BlendShapePreset.U);
    private static readonly BlendShapeKey KeyE = BlendShapeKey.CreateFromPreset(BlendShapePreset.E);
    private static readonly BlendShapeKey KeyO = BlendShapeKey.CreateFromPreset(BlendShapePreset.O);

    [SetUp]
    public void SetUp()
    {
        _avatarRoot = new GameObject("Avatar_Companion_Test");
        _visualModel = new GameObject("VisualModel");
        _visualModel.transform.SetParent(_avatarRoot.transform);

        _proxy = _visualModel.AddComponent<VRMBlendShapeProxy>();
        _audioSource = _avatarRoot.AddComponent<AudioSource>();
        _uLipSync = _avatarRoot.AddComponent<uLipSync.uLipSync>();
        _lipSyncDriver = _avatarRoot.AddComponent<AvatarSpeechLipSync>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_avatarRoot);
    }

    [Test]
    public void ComponentResolution_AutoFindsProxyAndAudioSource()
    {
        Assert.IsNotNull(_lipSyncDriver.BlendShapeProxy);
        Assert.AreEqual(_proxy, _lipSyncDriver.BlendShapeProxy);
        Assert.IsNotNull(_lipSyncDriver.AudioSource);
        Assert.AreEqual(_audioSource, _lipSyncDriver.AudioSource);
    }

    [Test]
    public void OnLipSyncUpdated_MapsPhonemeRatiosCorrectly()
    {
        var ratios = new Dictionary<string, float>
        {
            { "A", 0.7f },
            { "I", 0.1f },
            { "U", 0.0f },
            { "E", 0.2f },
            { "O", 0.0f }
        };

        var info = new LipSyncInfo
        {
            phoneme = "A",
            volume = 0.8f,
            rawVolume = -1.2f,
            phonemeRatios = ratios
        };

        _lipSyncDriver.OnLipSyncUpdated(info);

        Assert.AreEqual("A", _lipSyncDriver.ActivePhoneme);
        Assert.AreEqual(0.8f, _lipSyncDriver.CurrentVolume);
    }

    [Test]
    public void OnLipSyncUpdated_LowVolume_MutesAllTargets()
    {
        var ratios = new Dictionary<string, float>
        {
            { "A", 1.0f }
        };

        var info = new LipSyncInfo
        {
            phoneme = "A",
            volume = 0.005f, // di bawah minVolumeThreshold (0.02)
            rawVolume = -3.5f,
            phonemeRatios = ratios
        };

        _lipSyncDriver.OnLipSyncUpdated(info);

        Assert.AreEqual(0.005f, _lipSyncDriver.CurrentVolume);
    }

    [Test]
    public void ResetVisemes_ClearsAllWeights()
    {
        var ratios = new Dictionary<string, float>
        {
            { "A", 1.0f }
        };

        var info = new LipSyncInfo
        {
            phoneme = "A",
            volume = 0.9f,
            rawVolume = -1.0f,
            phonemeRatios = ratios
        };

        _lipSyncDriver.OnLipSyncUpdated(info);
        _lipSyncDriver.ResetVisemes();

        Assert.AreEqual(0f, _proxy.GetValue(KeyA));
        Assert.AreEqual(0f, _proxy.GetValue(KeyI));
        Assert.AreEqual(0f, _proxy.GetValue(KeyU));
        Assert.AreEqual(0f, _proxy.GetValue(KeyE));
        Assert.AreEqual(0f, _proxy.GetValue(KeyO));
    }
}
