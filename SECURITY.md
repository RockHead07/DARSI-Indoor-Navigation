# Security & Safety Policy — DARSI Indoor Navigation (Unity AR Client)

**Status:** Mandatory  
**Scope:** Unity 6 C# Client, ARCore / MultiSet VPS, Android UaaL Bridge, VRM 3D Avatar, AI Voice Integration, Photon Networking, Assets & CI/CD  
**Target Environment:** RS Islam A. Yani Surabaya (Hospital Wayfinding System)  
**Audience:** Developers, AI Coding Agents, Security Reviewers, Clinical & Hospital IT Stakeholders  

---

## 1. Purpose & Core Philosophy

This document defines the mandatory security, patient privacy, and spatial safety standards for the **DARSI Indoor Navigation Unity AR Client** codebase.

Because DARSI is deployed in a hospital environment (RS Islam A. Yani), the threat model goes beyond traditional software vulnerabilities. A bug or malicious manipulation in DARSI can cause **physical hazards** (such as a patient tripping or colliding in a corridor because an AR object obstructed their view) or **privacy violations** (such as tracking vulnerable patients or recording private clinical conversations).

All systems and AI coding agents operating in this repository MUST enforce **defense-in-depth**:

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                       DARSI SECURITY & SAFETY MODEL                     │
├─────────────────────────────────────────────────────────────────────────┤
│ 1. SPATIAL SAFETY   : Zero corridor obstruction, safety fade, nav limits│
│ 2. SENSOR PRIVACY   : Ephemeral camera/mic data, no storage, no leaks   │
│ 3. ANTI-STALKING    : Mutual-consent friend finding, AR-session scope   │
│ 4. ASSET INTEGRITY  : No secrets in YAML/Prefabs, clean .meta tracking │
│ 5. BRIDGE DEFENSE   : Sanitized Flutter UaaL payloads, robust JSON      │
│ 6. AI RESILIENCE    : IGD triage priority, safe string parsing, fallbacks│
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Enforcement Categories

Every requirement in this document is tagged with an enforcement category:

* **`[SPATIAL_SAFETY]`** — Must be enforced in AR rendering, camera view safety, and navigation kinematics to prevent physical accidents in hospital corridors.
* **`[CODE]`** — Must be enforced in C# logic and validated through automated EditMode/PlayMode tests or runtime assertions.
* **`[ASSET]`** — Must be enforced in Unity scene serialization (`.unity`), prefab configurations (`.prefab`), `.meta` files, and `.gitignore`.
* **`[INFRA]`** — Must be enforced in CI/CD workflows, build scripts, or network endpoints.
* **`[HUMAN]`** — Requires explicit human judgment, ethics approval, or coordination with Hospital IT (RS Islam A. Yani).

---

## 3. Core Principles for Developers & AI Agents

### 3.1 Inspect Before Modifying `[CODE]` `[ASSET]`
AI agents and developers MUST inspect and understand existing components, GUIDs, and serialized relationships before altering scenes, prefabs, or scripts. Never blind-overwrite Unity assets or delete `.meta` files, as this destroys asset references and breaks scene hierarchy.

### 3.2 Least Privilege in Android Permissions `[CODE]` `[INFRA]`
The application must request only the absolute minimum Android permissions necessary:
* `CAMERA` (Required by ARCore / MultiSet VPS for visual localization).
* `RECORD_AUDIO` (Required for voice interaction).
* `INTERNET` (Required for RAG query and Photon sync).
* **FORBIDDEN:** `ACCESS_FINE_LOCATION`, `READ_EXTERNAL_STORAGE`, `WRITE_EXTERNAL_STORAGE`, `READ_CONTACTS`, or background tracking services.

### 3.3 Fail-Closed & Graceful Degradation `[CODE]`
* If the backend RAG service or internet connection fails, the app must gracefully fall back to local UI notifications without freezing coroutines or crashing.
* If VPS localization is lost, navigation kinematics and avatar locomotion MUST pause immediately (`Fail-Closed`).

### 3.4 Never Disable Spatial Safety Gates `[SPATIAL_SAFETY]` `[CODE]`
Never bypass, disable, or mock spatial safety gates (such as `AvatarSafetyFade` distance checks or `LocalizationSuccess` requirements) in production builds. Editor bypasses must strictly be enclosed in `#if UNITY_EDITOR`.

---

## 4. Trust Boundaries & Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       ANDROID HOST ENVIRONMENT                          │
│  ┌────────────────────────┐             ┌────────────────────────────┐  │
│  │   Flutter App (MyRSIy) │             │  Hardware (Camera / Mic)   │  │
│  └───────────┬────────────┘             └─────────────┬──────────────┘  │
└──────────────┼────────────────────────────────────────┼─────────────────┘
               │ (Boundary 1: UaaL Bridge)              │ (Boundary 2: Native OS)
┌──────────────▼────────────────────────────────────────▼─────────────────┐
│                      UNITY AR CLIENT (DARSI)                            │
│  • UaaLEntryPoint (Payload & Route Dispatcher)                          │
│  • MultiSet VPS & NavMesh Pathfinding                                  │
│  • 3D Character Avatar (AIAvatarGuideController, Look-At, SafetyFade)   │
│  • Voice Input & UI Controller                                          │
└──────────────┬────────────────────────────────────────┬─────────────────┘
               │ (Boundary 3: HTTPS / TLS)              │ (Boundary 4: Photon TLS)
┌──────────────▼────────────┐             ┌─────────────▼──────────────┐
│  FastAPI Backend (RAG)    │             │   Photon Cloud (PUN)       │
│  api-darsi.rockhead07.tech│             │   (AR Friend Synchronization│
└───────────────────────────┘             └────────────────────────────┘
```

* **Boundary 1 (Flutter ↔ Unity):** All communication passes through `UaaLEntryPoint.cs` via JSON strings. The host Flutter app is trusted, but payloads must be strictly validated before triggering navigation.
* **Boundary 2 (Native OS ↔ Unity):** Camera and microphone sensor feeds. Camera frames are processed strictly in RAM by MultiSet/ARCore for pose estimation and discarded immediately.
* **Boundary 3 (Unity ↔ FastAPI Backend):** Encrypted HTTPS traffic over Cloudflare Named Tunnel.
* **Boundary 4 (Unity ↔ Photon Cloud):** Realtime multiplayer transform synchronization for Cari Teman.

---

## 5. Hospital Spatial & Physical Safety Standards `[SPATIAL_SAFETY]`

Hospital corridors contain vulnerable individuals, wheelchairs, stretchers, IV poles, and medical staff rushing for emergencies. AR visuals must never compromise physical safety.

### 5.1 "Magic Lens" Visual Guideline
* AR serves as a transparent *magic lens* to aid navigation, not an immersive VR game.
* **Prohibited:** Spawning large static 3D characters or opaque UI panels within 1.5 meters directly in front of the camera during active walking (ADR-030 / ADR-034).

### 5.2 Mandatory Distance-Based Safety Fade
* **Implementation:** `AvatarSafetyFade.cs`
* **Rule:** The 3D avatar MUST continuously compute its horizontal distance to the AR camera.
* **Thresholds:**
  * $\text{Distance} > 0.90\text{ m}$: Normal rendering.
  * $0.50\text{ m} < \text{Distance} \le 0.90\text{ m}$: Fade transition.
  * $\text{Distance} \le 0.50\text{ m}$: **Immediate 100% cutoff** (`renderer.enabled = false` for all mesh parts). The user's field of view must be completely clear of virtual obstructions.

### 5.3 Localization Gating for Avatar Locomotion
* The avatar controller (`AIAvatarGuideController.cs`) MUST NOT start moving along the path before VPS localization succeeds (`SingleFrameLocalizationManager.LocalizationSuccess`).
* Moving on unverified or drifting coordinates can mislead patients into hazardous areas.

### 5.4 Multi-Floor Vertical Safety
* **Implementation:** `FloorTransitionController.cs` (ADR-020, ADR-034).
* Rerouting across floors must be segmented per floor. Line renderers must NEVER draw continuous lines piercing through ceilings or floors.
* When entering elevators or stairs (`AwaitingRelocalize`), all navigation agents and avatars must hide until successful relocalization on the target floor.

### 5.5 Out-of-Bounds Coverage Notifications
* **Implementation:** `NavBoundaryNotifier.cs` (ADR-019).
* If a patient wanders outside the scanned NavMesh area, display a gentle coverage notice (*"Di luar jangkauan navigasi"*). Never present virtual walls that mimic solid physical barriers.

---

## 6. Patient & Hospital Privacy Standards `[CODE]` `[HUMAN]`

### 6.1 Ephemeral Sensor Feeds (Zero Persistence)
* **Camera Frames:** Used exclusively by ARCore/MultiSet in memory for visual feature matching. Under no circumstances may camera frames, screenshots, or video buffers be written to local storage, logged, or uploaded to any backend.
* **Voice Audio:** The microphone stream is buffered solely for the duration of the speech-to-text request and immediately cleared from memory once sent.

### 6.2 Anti-Stalking in "Cari Teman" (Find Friend)
Hospital environments present extreme risks for stalking (e.g., domestic violence victims, psychiatric patients, unaccompanied minors).
* **Prohibited:** Automatic proximity discovery, Bluetooth radar scans, or open public user directories (ADR-010).
* **Mandatory:** Location sharing is strictly allowed ONLY when:
  1. Both parties have established a mutual connection via exact-identifier friend request (ADR-013).
  2. Both parties are concurrently active within a localized AR session (ADR-011).
  3. Either party can revoke or opt out at any moment.

### 6.3 No Medical PII in Unity Client
* Unity client state is completely agnostic of patient identity, medical diagnoses, BPJS numbers, or health records.
* Identity is limited to an ephemeral `userId` token injected by the Flutter host for session routing (ADR-017).

---

## 7. Secrets Management & Asset Serialization `[ASSET]` `[CODE]`

Unity YAML assets (`.unity`, `.prefab`, `.asset`) serialize public and `[SerializeField]` private fields as plaintext. Leaking credentials in Unity assets is a critical vulnerability.

### 7.1 Absolute Ban on Hardcoded Secrets
* **Rule:** Never put API keys, bearer tokens, or sync credentials directly into MonoBehaviour fields, inspector properties, or script constants.
* **Incident Prevention (ADR-024):** API keys such as `groqApiKey` or `POI_SYNC_TOKEN` must never be saved in tracked scenes.

### 7.2 Secret Loading Strategy
1. **Production:** All AI and LLM keys are held exclusively on the FastAPI backend. The Unity client communicates with `/api/assistant/query` without holding third-party AI keys.
2. **Local Development:** If standalone client testing requires direct API access, keys must be loaded at runtime from gitignored local files (e.g., `groq-api-key.local.txt`).

### 7.3 Git Tracking & Pre-Commit Verification
The following patterns MUST remain in `.gitignore`:
```gitignore
*.local.txt
groq-api-key.local.txt
POI_SYNC_TOKEN.local.txt
```

---

## 8. UaaL Bridge & Inter-Process Security `[CODE]`

The bridge between Flutter (`MyRSIy`) and Unity (`DARSI`) is managed by `UaaLEntryPoint.cs`.

### 8.1 Defensive Deserialization
* All incoming JSON strings from Flutter must be parsed inside guarded `try-catch` blocks.
* Malformed or unexpected JSON must log a warning and discard the payload without throwing uncaught exceptions or crashing the Unity runtime.

### 8.2 POI Resolution & GUID Validation
* Target POIs must be resolved against registered `POIData` instances using strict GUID matching (`poiId`), with fallback to canonical naming.
* Unrecognized or invalid POI IDs must trigger a safe UI alert (`ToastManager`) and prevent path calculation to arbitrary coordinates.

---

## 9. AI Voice & Clinical Triage Safety `[CODE]`

### 9.1 Emergency Triage Fast-Path (ADR-028)
* The AI Assistant is integrated with emergency triage rules for hospital safety.
* If user speech indicates life-threatening emergencies (e.g., trauma, severe bleeding, unconsciousness, cardiac arrest), the system prompt mandates immediate routing to the **IGD (Instalasi Gawat Darurat)**.
* Client-side response parsing prioritizes explicit medical facility names in the AI response text before falling back to chunk metadata, preventing misleading routing (e.g., routing to parking lots during an emergency).

### 9.2 Defensive String & UI Parsing
* AI response strings must be sanitized before passing to `TextMeshPro` or Text-to-Speech engines to prevent formatting errors or UI buffer overruns.

---

## 10. Supply Chain & 3D Asset Provenance `[ASSET]`

### 10.1 Asset Licensing & Provenance (KNOWN-ISSUES.md)
* All 3D models, textures, audio clips, and humanoid animations MUST have verified provenance and commercial/academic distribution rights (e.g., Mixamo Humanoid animations, official UniVRM packages).
* Demo/sample assets from proprietary SDK packages (e.g., Photon Demo assets) must NOT be distributed in production builds.

### 10.2 Package Management
* UniVRM and UniGLTF packages are pinned to stable releases (`v0.131.2` for VRM 0.x on Unity 6.3 LTS) to ensure reproducible builds and prevent upstream supply chain drift.

---

## 11. CI/CD & Build Integrity `[INFRA]`

* **Automated Testing:** All Pull Requests must pass Unity Test Runner EditMode tests (`POIDataTests.cs`) and backend pytest suites before merging (ADR-031).
* **Scene Protection:** Production scenes (`DARSi-Indoor Navigation.unity`, `WholePSDKU.unity`) must only be modified through reviewed PRs, keeping experimental avatar/voice prototyping isolated in sandbox scenes (`Sandbox_AvatarCompanion.unity`, `TestingHCM.unity`).
* **Git Author Standard:** All commits must use the verified author identity (`Bagus Insan Pradana <dana.bagus07@gmail.com>`) and clean Conventional Commits format.

---

## 12. Vulnerability Reporting & Disclosure Policy

We welcome reports of security, safety, or privacy issues from researchers, developers, and hospital staff.

### Supported Versions

| Component | Version / Branch | Supported |
|---|---|---|
| Unity Client (`DARSI-Indoor Navigation`) | `main` (Unity 6000.3.14f1 LTS) | :white_check_mark: |
| Backend API (`DARSI-Indoor-Navigation-Backend`) | `main` (FastAPI / Supabase) | :white_check_mark: |
| Feature Branches | `feature/*` | :x: (Development only) |

### Reporting a Vulnerability

Please report any security or patient safety concerns directly to the project lead:

* **Contact:** Bagus Insan Pradana
* **Email:** `dana.bagus07@gmail.com`
* **Subject Line:** `[SECURITY / SAFETY REPORT] DARSI - <Brief Summary>`

**Please include:**
1. Description of the vulnerability or spatial safety hazard.
2. Steps to reproduce (scene name, payload, or device environment).
3. Potential impact on hospital operations, patient privacy, or physical safety.

We commit to acknowledging reports within **48 hours** and providing status updates throughout the remediation process.
