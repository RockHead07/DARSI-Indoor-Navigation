using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VRM;

/// <summary>
/// EditMode Unit Tests untuk AvatarAudioClient (Fase 2 - Sesi 3).
/// Memvalidasi resolusi komponen, serialisasi payload TTS, fault isolation, dan transisi lead-follow.
/// </summary>
[TestFixture]
public class AvatarAudioClientTests
{
    private GameObject _avatarRoot;
    private GameObject _visualModel;
    private AvatarAudioClient _audioClient;
    private AvatarSpeechLipSync _lipSync;
    private AIAvatarGuideController _guideController;
    private AudioSource _audioSource;

    [SetUp]
    public void SetUp()
    {
        _avatarRoot = new GameObject("Avatar_Audio_Test");
        _visualModel = new GameObject("VisualModel");
        _visualModel.transform.SetParent(_avatarRoot.transform);

        _visualModel.AddComponent<VRMBlendShapeProxy>();
        _audioSource = _avatarRoot.AddComponent<AudioSource>();
        _lipSync = _avatarRoot.AddComponent<AvatarSpeechLipSync>();
        _guideController = _avatarRoot.AddComponent<AIAvatarGuideController>();
        _audioClient = _avatarRoot.AddComponent<AvatarAudioClient>();
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_avatarRoot);
    }

    [Test]
    public void ComponentResolution_AutoFindsLipSyncAndGuide()
    {
        _audioClient.ResolveComponents();

        Assert.IsNotNull(_audioClient.LipSyncDriver);
        Assert.AreEqual(_lipSync, _audioClient.LipSyncDriver);
        Assert.IsNotNull(_audioClient.GuideController);
        Assert.AreEqual(_guideController, _audioClient.GuideController);
        Assert.IsNotNull(_audioClient.AudioSource);
        Assert.AreEqual(_audioSource, _audioClient.AudioSource);
    }

    [Test]
    public void SpeakText_EmptyTextOrDisabled_InvokesCallbackImmediately()
    {
        bool finished1 = false;
        IEnumerator coroutine1 = _audioClient.SpeakText("", () => { finished1 = true; });
        while (coroutine1.MoveNext()) { }
        Assert.IsTrue(finished1, "Teks kosong harus memanggil onFinished seketika");

        _audioClient.EnableVoiceOutput = false;
        bool finished2 = false;
        IEnumerator coroutine2 = _audioClient.SpeakText("Halo", () => { finished2 = true; });
        while (coroutine2.MoveNext()) { }
        Assert.IsTrue(finished2, "Voice output non-aktif harus memanggil onFinished seketika");
    }

    [Test]
    public void SpeakAnswerAndGuide_NullOrEmptyAnswer_InvokesNavigationCallback()
    {
        bool navReady = false;
        IEnumerator coroutine = _audioClient.SpeakAnswerAndGuide(null, () => { navReady = true; });
        while (coroutine.MoveNext()) { }

        Assert.IsTrue(navReady, "Answer null harus memanggil onNavigationReady seketika");
    }

    [Test]
    public void StopSpeaking_StopsAudioAndLipSync()
    {
        _audioClient.StopSpeaking();
        Assert.IsFalse(_audioClient.IsSpeaking);
    }

    [Test]
    public void TTSPayload_Serialization_MatchesBackendContract()
    {
        var req = new AvatarAudioClient.TTSRequestPayload
        {
            text = "Poli Anak di Lantai 2",
            voice = "id-ID-GadisNeural"
        };
        string json = JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("\"text\":\"Poli Anak di Lantai 2\""));
        Assert.IsTrue(json.Contains("\"voice\":\"id-ID-GadisNeural\""));

        string responseJson = "{\"audio_url\":\"http://127.0.0.1:8000/static/tts/test.mp3\",\"engine_used\":\"edge-tts\"}";
        var resp = JsonUtility.FromJson<AvatarAudioClient.TTSResponsePayload>(responseJson);
        Assert.AreEqual("http://127.0.0.1:8000/static/tts/test.mp3", resp.audio_url);
        Assert.AreEqual("edge-tts", resp.engine_used);
    }
}
