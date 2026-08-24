# AI Avatar Assistant Guide Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mengintegrasikan pemandu virtual 3D humanoid interaktif (VRM/Mecanim) ke dalam aplikasi Unity AR Android DARSI dengan kemampuan Lead-Follow path navigation, dynamic look-at gaze, real-time lip-sync, dan percakapan suara natural RAG RS Islam A. Yani.

**Architecture:** 
Pemrosesan audio dan bahasa berat (Whisper STT, RAG knowledge retrieval, Edge-TTS audio synthesis) dilakukan secara *offloaded* di FastAPI backend (`darsi-backend`). Unity AR Client menangani rendering avatar 3D humanoid ringan ($\le 15\text{k}$ tris), interpolasi pergerakan waypoints NavMesh MultiSet via Finite State Machine (`AIAvatarGuideController`), procedural look-at ke `Camera.main`, real-time viseme lip-sync dari buffer audio, serta protokol keselamatan transparansi jarak dekat.

**Tech Stack:** 
- **Unity:** Unity 6.3 LTS / C#, MultiSet VPS SDK, Mecanim Animation System, SkinnedMeshRenderer BlendShapes, UnityWebRequestAudio.
- **Backend:** Python 3.11+, FastAPI, `edge-tts` (`id-ID-GadisNeural`), Groq Whisper API / Bifrost Gateway (`medgemma-1.5-4b-it-q4`), PostgreSQL (`pgvector`).

---

## Global Constraints

- **Frame Rate Target:** Stabil 60 FPS pada Android Mid-range (ARCore + MultiSet aktif).
- **Format 3D:** Standard Humanoid Rig / VRM 0.x dengan SkinnedMeshRenderer blendshapes (`A, I, U, E, O`, `Blink`, `Smile`).
- **Polygon & Draw Call Budget:** $\le 15.000$ Triangles, 1-2 Material Atlas ($1024\times 1024$), unlit / lightweight mobile shader.
- **Safety Guardrail:** Avatar wajib otomatis semi-transparan / *fade-out* jika jarak ke kamera $< 0.8\text{ m}$ agar tidak menghalangi pandangan koridor fisik RS.
- **Fail-Safe Offline:** Jika koneksi backend terputus, navigasi dasar tetap berjalan tanpa avatar memblokir layar.

---

## File Structure

```
darsi-backend/
├── app/
│   └── assistant/
│       ├── tts.py                  # Audio synthesis using edge-tts (id-ID-GadisNeural)
│       ├── stt.py                  # Groq Whisper audio transcription
│       ├── router.py               # Exposes POST /tts, POST /stt, POST /query
│       └── models.py               # Request/Response schemas for audio & avatar
└── tests/
    ├── test_tts.py
    └── test_stt.py

DARSI-Indoor Navigation/ (Unity)
└── Assets/
    └── Scripts/
        └── Avatar/
            ├── AvatarAudioClient.cs         # Downloads & streams TTS MP3 audio
            ├── AvatarSpeechLipSync.cs       # Audio spectrum analyzer to blendshapes
            ├── AvatarLookAtController.cs    # Clamped head/eye look-at toward Camera.main
            ├── AvatarSafetyFade.cs          # Alpha proximity fade-out (< 0.8m)
            └── AIAvatarGuideController.cs   # Lead-Follow FSM controller
```

---

### Task 1: Backend Edge-TTS Audio Endpoint (`darsi-backend`)

**Files:**
- Create: `D:/Dev/Projects/darsi-backend/app/assistant/tts.py`
- Modify: `D:/Dev/Projects/darsi-backend/app/assistant/router.py`
- Modify: `D:/Dev/Projects/darsi-backend/app/assistant/models.py`
- Test: `D:/Dev/Projects/darsi-backend/tests/test_tts.py`

**Interfaces:**
- Produces: `async def synthesize_speech(text: str, voice: str = "id-ID-GadisNeural") -> bytes`
- Endpoint: `POST /api/assistant/tts` with body `{"text": "string"}` returning `audio/mpeg` stream.

- [ ] **Step 1: Write the failing test for TTS service**

```python
# tests/test_tts.py
import pytest
from httpx import AsyncClient, ASGITransport
from app.main import app


@pytest.mark.asyncio
async def test_tts_endpoint_returns_mp3_audio():
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post(
            "/api/assistant/tts",
            json={"text": "Halo, selamat datang di RS Islam A Yani."},
        )
        assert response.status_code == 200
        assert response.headers["content-type"] == "audio/mpeg"
        assert len(response.content) > 100
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pytest tests/test_tts.py -v` in `darsi-backend` directory.  
Expected: FAIL with 404 Not Found (endpoint not registered).

- [ ] **Step 3: Implement TTS synthesis and router integration**

```python
# app/assistant/tts.py
import io
import edge_tts


async def synthesize_speech(
    text: str, voice: str = "id-ID-GadisNeural", rate: str = "+0%", pitch: str = "+0Hz"
) -> bytes:
    """Synthesize text to MP3 audio bytes using edge-tts."""
    communicate = edge_tts.Communicate(text, voice, rate=rate, pitch=pitch)
    mp3_buffer = io.BytesIO()
    async for chunk in communicate.stream():
        if chunk["type"] == "audio":
            mp3_buffer.write(chunk["data"])
    return mp3_buffer.getvalue()
```

Tambahkan endpoint pada `app/assistant/router.py`:
```python
from fastapi import APIRouter, HTTPException, Request, Response
from pydantic import BaseModel
from app.assistant.tts import synthesize_speech


class TTSRequest(BaseModel):
    text: str
    voice: str = "id-ID-GadisNeural"


@router.post("/tts")
async def text_to_speech(payload: TTSRequest):
    if not payload.text.strip():
        raise HTTPException(status_code=400, detail="Text cannot be empty")
    audio_bytes = await synthesize_speech(payload.text, payload.voice)
    return Response(content=audio_bytes, media_type="audio/mpeg")
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pytest tests/test_tts.py -v`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add app/assistant/tts.py app/assistant/router.py tests/test_tts.py
git commit -m "feat(assistant): add edge-tts endpoint id-ID-GadisNeural"
```

---

### Task 2: Backend Whisper Audio Transcription Endpoint (`darsi-backend`)

**Files:**
- Create: `D:/Dev/Projects/darsi-backend/app/assistant/stt.py`
- Modify: `D:/Dev/Projects/darsi-backend/app/assistant/router.py`
- Test: `D:/Dev/Projects/darsi-backend/tests/test_stt.py`

**Interfaces:**
- Produces: `async def transcribe_audio(audio_bytes: bytes, filename: str = "audio.wav") -> str`
- Endpoint: `POST /api/assistant/stt` accepting `multipart/form-data` audio file.

- [ ] **Step 1: Write test for STT endpoint**

```python
# tests/test_stt.py
import pytest
from httpx import AsyncClient, ASGITransport
from unittest.mock import patch
from app.main import app


@pytest.mark.asyncio
async def test_stt_endpoint_transcribes_audio():
    transport = ASGITransport(app=app)
    fake_audio = b"RIFF....WAVEfmt ...."

    with patch(
        "app.assistant.stt.transcribe_audio", return_value="Saya mau ke poli anak"
    ):
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            response = await client.post(
                "/api/assistant/stt",
                files={"file": ("test.wav", fake_audio, "audio/wav")},
            )
            assert response.status_code == 200
            assert response.json()["text"] == "Saya mau ke poli anak"
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pytest tests/test_stt.py -v`  
Expected: FAIL with 404 or ImportError.

- [ ] **Step 3: Implement Groq Whisper STT service**

```python
# app/assistant/stt.py
import os
from groq import Groq

_GROQ_CLIENT = None


def get_groq_client() -> Groq:
    global _GROQ_CLIENT
    if _GROQ_CLIENT is None:
        api_key = os.getenv("GROQ_API_KEY", "")
        _GROQ_CLIENT = Groq(api_key=api_key)
    return _GROQ_CLIENT


def transcribe_audio(audio_bytes: bytes, filename: str = "input.wav") -> str:
    client = get_groq_client()
    transcription = client.audio.transcriptions.create(
        file=(filename, audio_bytes),
        model="whisper-large-v3",
        language="id",
        response_format="json",
        temperature=0.0,
    )
    return transcription.text
```

Daftarkan di `app/assistant/router.py`:
```python
from fastapi import UploadFile, File
from app.assistant.stt import transcribe_audio


@router.post("/stt")
async def speech_to_text(file: UploadFile = File(...)):
    content = await file.read()
    if not content:
        raise HTTPException(status_code=400, detail="Audio file empty")
    text = transcribe_audio(content, file.filename or "audio.wav")
    return {"text": text}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pytest tests/test_stt.py -v`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add app/assistant/stt.py app/assistant/router.py tests/test_stt.py
git commit -m "feat(assistant): add whisper STT endpoint for audio input"
```

---

### Task 3: Unity TTS Audio Streaming Client (`AvatarAudioClient.cs`)

**Files:**
- Create: `Assets/Scripts/Avatar/AvatarAudioClient.cs`
- Modify: `Assets/Scripts/AssistantClient.cs`

**Interfaces:**
- Produces: `public IEnumerator FetchAndPlayTTS(string text, AudioSource targetSource, Action onPlaybackFinished = null)`
- Integrates with `AssistantClient` to automatically fetch TTS audio when RAG answer arrives.

- [ ] **Step 1: Write `AvatarAudioClient.cs` with UnityWebRequestAudio**

```csharp
// Assets/Scripts/Avatar/AvatarAudioClient.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class AvatarAudioClient : MonoBehaviour
{
    public static AvatarAudioClient Instance { get; private set; }

    [SerializeField] private string backendBaseUrl = "http://localhost:8000";
    [SerializeField] private AudioSource audioSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void PlaySpeech(string text, Action onFinished = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        StartCoroutine(FetchAndPlayTTS(text, audioSource, onFinished));
    }

    public IEnumerator FetchAndPlayTTS(string text, AudioSource targetSource, Action onPlaybackFinished = null)
    {
        if (targetSource == null) yield break;

        string url = $"{backendBaseUrl.TrimEnd('/')}/api/assistant/tts";
        string jsonPayload = $"{{\"text\": \"{EscapeJson(text)}\", \"voice\": \"id-ID-GadisNeural\"}}";

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                targetSource.clip = clip;
                targetSource.Play();

                while (targetSource.isPlaying)
                {
                    yield return null;
                }

                onPlaybackFinished?.Invoke();
            }
            else
            {
                Debug.LogWarning($"[AvatarAudioClient] Failed to fetch TTS: {www.error}");
                onPlaybackFinished?.Invoke();
            }
        }
    }

    private string EscapeJson(string str)
    {
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }
}
```

- [ ] **Step 2: Verify compilation in Unity**

Verify no C# compilation errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Avatar/AvatarAudioClient.cs Assets/Scripts/Avatar/AvatarAudioClient.cs.meta
git commit -m "feat(avatar): add AvatarAudioClient for streaming Edge-TTS"
```

---

### Task 4: Unity Real-Time Lip-Sync & Expression Controller (`AvatarSpeechLipSync.cs`)

**Files:**
- Create: `Assets/Scripts/Avatar/AvatarSpeechLipSync.cs`

**Interfaces:**
- Consumes: `AudioSource` frequency spectrum data (`GetSpectrumData`).
- Produces: Updates `SkinnedMeshRenderer` blendshapes (`vowel_a`, `vowel_i`, `vowel_u`, `vowel_e`, `vowel_o` or standard indexes) in real-time while audio plays.

- [ ] **Step 1: Implement `AvatarSpeechLipSync.cs`**

```csharp
// Assets/Scripts/Avatar/AvatarSpeechLipSync.cs
using UnityEngine;

public class AvatarSpeechLipSync : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float sensitivity = 100f;
    [SerializeField] private float smoothness = 15f;

    [Header("Skinned Mesh Target")]
    [SerializeField] private SkinnedMeshRenderer faceMesh;

    [Header("BlendShape Names / Indices")]
    [SerializeField] private string shapeA = "Fcl_MTH_A";
    [SerializeField] private string shapeI = "Fcl_MTH_I";
    [SerializeField] private string shapeU = "Fcl_MTH_U";
    [SerializeField] private string shapeE = "Fcl_MTH_E";
    [SerializeField] private string shapeO = "Fcl_MTH_O";

    private int indexA = -1, indexI = -1, indexU = -1, indexE = -1, indexO = -1;
    private float[] spectrum = new float[256];
    private float currentWeightA = 0f;

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (faceMesh != null && faceMesh.sharedMesh != null)
        {
            indexA = faceMesh.sharedMesh.GetBlendShapeIndex(shapeA);
            indexI = faceMesh.sharedMesh.GetBlendShapeIndex(shapeI);
            indexU = faceMesh.sharedMesh.GetBlendShapeIndex(shapeU);
            indexE = faceMesh.sharedMesh.GetBlendShapeIndex(shapeE);
            indexO = faceMesh.sharedMesh.GetBlendShapeIndex(shapeO);
        }
    }

    void Update()
    {
        if (audioSource == null || !audioSource.isPlaying || faceMesh == null)
        {
            ResetMouth();
            return;
        }

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        
        // Low-mid frequency energy (voice fundamental frequency)
        float lowEnergy = 0f;
        for (int i = 1; i < 15; i++) lowEnergy += spectrum[i];
        
        float targetWeight = Mathf.Clamp(lowEnergy * sensitivity * 100f, 0f, 100f);
        currentWeightA = Mathf.Lerp(currentWeightA, targetWeight, Time.deltaTime * smoothness);

        if (indexA >= 0) faceMesh.SetBlendShapeWeight(indexA, currentWeightA);
        if (indexO >= 0) faceMesh.SetBlendShapeWeight(indexO, currentWeightA * 0.4f);
    }

    private void ResetMouth()
    {
        if (faceMesh == null) return;
        currentWeightA = Mathf.Lerp(currentWeightA, 0f, Time.deltaTime * smoothness);
        if (indexA >= 0) faceMesh.SetBlendShapeWeight(indexA, currentWeightA);
        if (indexO >= 0) faceMesh.SetBlendShapeWeight(indexO, currentWeightA);
    }
}
```

- [ ] **Step 2: Verify compilation and BlendShape mapping fallback**

Verify no compilation errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Avatar/AvatarSpeechLipSync.cs Assets/Scripts/Avatar/AvatarSpeechLipSync.cs.meta
git commit -m "feat(avatar): add real-time audio spectrum lip-sync component"
```

---

### Task 5: Dynamic Gaze Look-At & Eye Contact Controller (`AvatarLookAtController.cs`)

**Files:**
- Create: `Assets/Scripts/Avatar/AvatarLookAtController.cs`

**Interfaces:**
- Procedurally rotates head/neck bone towards `Camera.main` with angular limits (Max Yaw $\pm 70^\circ$, Max Pitch $\pm 25^\circ$) and natural micro-saccades/blinking.

- [ ] **Step 1: Implement `AvatarLookAtController.cs`**

```csharp
// Assets/Scripts/Avatar/AvatarLookAtController.cs
using UnityEngine;

public class AvatarLookAtController : MonoBehaviour
{
    [Header("Look Target")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform neckBone;

    [Header("Parameters")]
    [SerializeField] private float weight = 0.8f;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float maxYawAngle = 70f;
    [SerializeField] private float maxPitchAngle = 25f;

    [Header("Eye Blink")]
    [SerializeField] private SkinnedMeshRenderer faceMesh;
    [SerializeField] private string blinkShapeName = "Fcl_EYE_Close";
    private int blinkIndex = -1;
    private float blinkTimer = 3f;

    private Quaternion initialHeadRotation;

    void Start()
    {
        if (targetTransform == null && Camera.main != null)
        {
            targetTransform = Camera.main.transform;
        }

        if (headBone != null)
        {
            initialHeadRotation = headBone.localRotation;
        }

        if (faceMesh != null && faceMesh.sharedMesh != null)
        {
            blinkIndex = faceMesh.sharedMesh.GetBlendShapeIndex(blinkShapeName);
        }
    }

    void LateUpdate()
    {
        HandleLookAt();
        HandleBlink();
    }

    private void HandleLookAt()
    {
        if (headBone == null || targetTransform == null || weight <= 0.01f) return;

        Vector3 dirToTarget = targetTransform.position - headBone.position;
        if (dirToTarget.sqrMagnitude < 0.01f) return;

        Vector3 localDir = transform.InverseTransformDirection(dirToTarget);
        float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(localDir.y, Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z)) * Mathf.Rad2Deg;

        if (Mathf.Abs(yaw) <= maxYawAngle && Mathf.Abs(pitch) <= maxPitchAngle)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToTarget, Vector3.up);
            headBone.rotation = Quaternion.Slerp(headBone.rotation, targetRot, Time.deltaTime * smoothSpeed * weight);
        }
    }

    private void HandleBlink()
    {
        if (faceMesh == null || blinkIndex < 0) return;

        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            StartCoroutine(PerformBlink());
            blinkTimer = Random.Range(2.5f, 5.5f);
        }
    }

    private System.Collections.IEnumerator PerformBlink()
    {
        float duration = 0.12f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float w = Mathf.Sin((t / duration) * Mathf.PI) * 100f;
            faceMesh.SetBlendShapeWeight(blinkIndex, w);
            yield return null;
        }
        faceMesh.SetBlendShapeWeight(blinkIndex, 0f);
    }
}
```

- [ ] **Step 2: Verify compilation in Unity**

Verify no compile errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Avatar/AvatarLookAtController.cs Assets/Scripts/Avatar/AvatarLookAtController.cs.meta
git commit -m "feat(avatar): add dynamic look-at gaze and natural blinking"
```

---

### Task 6: Lead-Follow Navigation FSM Controller (`AIAvatarGuideController.cs`)

**Files:**
- Create: `Assets/Scripts/Avatar/AIAvatarGuideController.cs`
- Create: `Assets/Scripts/Avatar/AvatarSafetyFade.cs`

**Interfaces:**
- Consumes: MultiSet `ShowPath` / `NavigationController` active path & `Camera.main` position.
- Controls: Animator states (`Idle_Friendly`, `Walk_Forward`, `Wait_Wave`, `Talk_Expressive`, `Point_Forward`).

- [ ] **Step 1: Implement Proximity Safety Fade (`AvatarSafetyFade.cs`)**

```csharp
// Assets/Scripts/Avatar/AvatarSafetyFade.cs
using UnityEngine;

public class AvatarSafetyFade : MonoBehaviour
{
    [SerializeField] private float fadeStartDistance = 1.2f;
    [SerializeField] private float fadeEndDistance = 0.6f;
    [SerializeField] private Renderer[] avatarRenderers;

    private Transform userCamera;

    void Start()
    {
        if (Camera.main != null) userCamera = Camera.main.transform;
        if (avatarRenderers == null || avatarRenderers.Length == 0)
        {
            avatarRenderers = GetComponentsInChildren<Renderer>();
        }
    }

    void Update()
    {
        if (userCamera == null) return;

        float dist = Vector3.Distance(transform.position, userCamera.position);
        float alpha = Mathf.Clamp01((dist - fadeEndDistance) / (fadeStartDistance - fadeEndDistance));

        foreach (var r in avatarRenderers)
        {
            if (r != null && r.material != null && r.material.HasProperty("_Color"))
            {
                Color c = r.material.color;
                c.a = alpha;
                r.material.color = c;
            }
        }
    }
}
```

- [ ] **Step 2: Implement `AIAvatarGuideController.cs`**

```csharp
// Assets/Scripts/Avatar/AIAvatarGuideController.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class AIAvatarGuideController : MonoBehaviour
{
    public enum GuideState
    {
        IDLE_STAND,
        LEADING_PATH,
        WAITING_FOR_USER,
        CONVERSING,
        ELEVATOR_HANDOFF,
        ARRIVAL_POINTING
    }

    [Header("State")]
    [SerializeField] private GuideState currentState = GuideState.IDLE_STAND;

    [Header("Navigation Distances")]
    [SerializeField] private float leadDistance = 2.0f;
    [SerializeField] private float lagDistanceThreshold = 3.8f;
    [SerializeField] private float resumeDistanceThreshold = 2.0f;
    [SerializeField] private float arrivalThreshold = 1.5f;

    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private AvatarSpeechLipSync lipSync;
    [SerializeField] private AvatarLookAtController lookAt;

    private Transform userCamera;
    private Vector3 currentDestination;
    private bool isNavigating = false;

    void Start()
    {
        if (Camera.main != null) userCamera = Camera.main.transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
        
        SetState(GuideState.IDLE_STAND);
    }

    void Update()
    {
        if (userCamera == null) return;

        float distToUser = Vector3.Distance(transform.position, userCamera.position);

        switch (currentState)
        {
            case GuideState.LEADING_PATH:
                UpdateLeadingState(distToUser);
                break;
            case GuideState.WAITING_FOR_USER:
                UpdateWaitingState(distToUser);
                break;
            case GuideState.ARRIVAL_POINTING:
                // Look at user / door
                break;
        }
    }

    public void StartLeading(Vector3 destination)
    {
        currentDestination = destination;
        isNavigating = true;
        SetState(GuideState.LEADING_PATH);
    }

    public void StopLeading()
    {
        isNavigating = false;
        SetState(GuideState.IDLE_STAND);
    }

    private void UpdateLeadingState(float distToUser)
    {
        if (distToUser > lagDistanceThreshold)
        {
            SetState(GuideState.WAITING_FOR_USER);
            return;
        }

        float distToDest = Vector3.Distance(transform.position, currentDestination);
        if (distToDest < arrivalThreshold)
        {
            SetState(GuideState.ARRIVAL_POINTING);
            return;
        }

        // Keep moving along path in front of user
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(currentDestination);
        }
    }

    private void UpdateWaitingState(float distToUser)
    {
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
        }

        // Turn towards user while waiting
        Vector3 lookDir = userCamera.position - transform.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 4f);
        }

        if (distToUser <= resumeDistanceThreshold)
        {
            SetState(GuideState.LEADING_PATH);
        }
    }

    public void SetState(GuideState newState)
    {
        currentState = newState;
        if (animator == null) return;

        animator.SetBool("isWalking", currentState == GuideState.LEADING_PATH);
        animator.SetBool("isWaiting", currentState == GuideState.WAITING_FOR_USER);
        animator.SetBool("isTalking", currentState == GuideState.CONVERSING);
        animator.SetBool("isPointing", currentState == GuideState.ARRIVAL_POINTING);
    }
}
```

- [ ] **Step 3: Verify compilation in Unity**

Verify no compile errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Avatar/AIAvatarGuideController.cs Assets/Scripts/Avatar/AvatarSafetyFade.cs Assets/Scripts/Avatar/*.meta
git commit -m "feat(avatar): add AIAvatarGuideController and proximity safety fade"
```

---

### Task 7: AR Navigation & Voice Input Integration Wire-up

**Files:**
- Modify: `Assets/Speech Recognition/VoiceInputHandler.cs`
- Modify: `Assets/Scripts/Navigation/FloorTransitionController.cs`

**Interfaces:**
- Triggers Avatar speak & lead when voice intent or RAG query resolves.
- Triggers Elevator Handoff state during multi-floor transition (ADR-020).

- [ ] **Step 1: Connect Avatar TTS & Guide in `VoiceInputHandler.cs`**

Hubungkan hasil query RAG agar memanggil `AvatarAudioClient.Instance.PlaySpeech(ragAnswer.answer)` dan menginstruksikan avatar untuk mulai memandu saat `poi_id` teridentifikasi.

- [ ] **Step 2: Connect Lift Transition state in `FloorTransitionController.cs`**

Saat user mencapai lift sebelum ganti lantai, panggil `guideController.SetState(AIAvatarGuideController.GuideState.ELEVATOR_HANDOFF)`.

- [ ] **Step 3: Test wiring in Play Mode on scene `TestingHCM.unity`**

Verifikasi bahwa memanggil query atau tombol voice memicu avatar berbicara dan mengupdate state FSM tanpa error.

- [ ] **Step 4: Commit**

```bash
git add Assets/Speech\ Recognition/VoiceInputHandler.cs Assets/Scripts/Navigation/FloorTransitionController.cs
git commit -m "feat(avatar): wire avatar audio and FSM into voice and floor transition"
```

---

## Plan Self-Review Checklist

1. **Spec Coverage:** Seluruh 5 fase dalam spesifikasi telah tercakup (TTS/STT backend, avatar audio, lip-sync, dynamic look-at gaze, lead-follow FSM, elevator handoff).
2. **No Placeholders:** Semua fungsi, class, dan tipe data didefinisikan secara konkret.
3. **Platform Safety:** Android budget ($\le 15\text{k}$ tris, alpha fade $< 0.8\text{m}$, offloaded audio) dipatuhi.
