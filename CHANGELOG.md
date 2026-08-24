# Changelog — DARSI Indoor Navigation

Semua perubahan penting pada repositori ini didokumentasikan di file ini.
Format mengikuti [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- 3D VRM Avatar Companion MVP — `AvatarCompanionController`, `VRMRuntimeLoader`,
  sandbox scene (`Sandbox_AvatarCompanion.unity`) pada branch `feature/vrm-avatar-assistant` (ADR-030)
- CI/CD audit & perbaikan: backend tests di release pipeline, Unity EditMode tests,
  ruff config, scene build settings

### Fixed
- Packages manifest conflict resolution dan dependensi UniVRM

---

## [0.9.0] — 2026-08

### Added
- Hybrid RAG Voice Navigation — `VoiceInputHandler` dengan dual-path: RAG server
  sebagai primer, keyword fallback lokal (ADR-028)
- AI Assistant REST client (`AssistantClient`) + panel uji runtime (`AssistantTestPanel`)
- ADR-026 s.d. ADR-029 (RAG Assistant, ingress, Bifrost gateway pivot)
- Groq API key dibaca dari file lokal gitignored, bukan via Inspector (security hardening)

### Fixed
- Prioritaskan teks jawaban RAG di atas metadata `poi_id` untuk resolusi POI
- Timeout client RAG dinaikkan untuk tunnel latency

---

## [0.8.0] — 2026-07

### Added
- Admin Ground-Truth Debug HUD (`LocalizationDebugHUD`) — 5-tap gesture trigger
  untuk validasi akurasi VPS di lapangan (ADR-025)
- ADR-024 (pivot provider suara ke Groq cloud, Ollama fallback)
- ADR-025 (validasi akurasi VPS vs tape measure)
- Konteks RS Islam pada prompt LLM voice (`OllamaConnector`)

### Fixed
- `RECORD_AUDIO` permission hilang dari Android manifest
- Timeout fallback Ollama dipangkas agar UX tidak hang
- Debug HUD canvas parent salah (panel toggle tapi tidak render)

---

## [0.7.0] — 2026-07

### Added
- CI/CD pipeline modular reusable workflows (ADR di `docs/CICD.md`):
  - `pr-validation.yml` → `_backend-tests.yml` + `_unity-tests.yml`
  - `release-main.yml` → `_unity-tests.yml` → `_unity-build-uaal.yml`
- Dependabot untuk GitHub Actions & pip (Backend)
- `TestingHCM` scene dan map asset baru
- ADR-022 (WebXR field validation) & ADR-023

### Fixed
- `checks:write` permission untuk Unity Test Runner (`47a5926`)
- Dua bug build-uaal: `-executeMethod` ganda + invalid `.meta` GUID (`3a5becd`)
- Deprecated `unity-request-activation-file` action dihapus

---

## [0.6.0] — 2026-06

### Added
- Segmented multi-floor navigation (ADR-020 A/B/C):
  - `FloorTransitionController` — elevator/lift handoff antar lantai
  - Cross-floor distance display & relocalize awaiting state
- `FloorVisibilityManager` — dynamic POI floor clustering via Y-gap (ADR-018)
- ADR-021: `POIData` sebagai Single Source of Truth (derived properties, bukan salinan)
- `NavBoundaryNotifier` — out-of-bounds coverage notice dari NavMesh edges (ADR-019)
- POI sync editor ke Supabase RPC (`POISyncWindow`)

### Fixed
- Floor label tuning dan nav path settings
- POI collider heights untuk deteksi multi-lantai
- Floor transition stabilization setelah relocalize
- Ring buffer logging untuk field test tanpa tethered cable

---

## [0.5.0] — 2026-05

### Added
- `UaaLEntryPoint` — single entry point untuk payload Flutter via `UnitySendMessage`
- Cold-launch payload handling di Unity
- POI arrival detection via toast hook
- Panel exclusivity dan `NavBoundaryNotifier` UI
- DARSi scene dengan data MultiSet map baru
- `UaaLBuildScript` — automated Android UaaL export untuk Game-CI
- Backend YOLO API (`yolo_api.py`) untuk crowd detection

### Changed
- Launch logic reworked untuk UaaL integration

---

## [0.4.0] — 2026-04

### Added
- POI sync editor (`POISyncWindow`) dan stable UUID per POI (`poiId`)
- ADR-016 (POI category canonicalization)
- Comprehensive architecture documentation (`docs/`)
- GUID-based POI resolution & send `poiName` ke Flutter

---

## [0.3.0] — 2026-03

### Added
- Voice input system (`VoiceInputHandler`, `OllamaConnector`)
- `POIManager` — runtime POI registry dan search engine
- `NavigationAdapter` — bridge voice commands ke MultiSet `NavigationController`
- Responsive canvas + toast notifications diterjemahkan ke Bahasa Indonesia

### Removed
- UI Toolkit design lama (diganti uGUI, ADR-003)

---

## [0.1.0] — 2026-02

### Added
- Initial Unity project setup dengan MultiSet SDK
- Integrasi AR navigation dasar (ARCore, MultiSet VPS)
- Multiplayer framework (Photon PUN 2): `PhotonManager`, `PlayerSync`, `PlayerNavigationController`
- README dan dokumentasi awal