# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**MultiSetNav** is a Unity 6 (6000.3.14f1) AR navigation project that integrates the MultiSet SDK with on-device AI (Ollama) for voice-controlled indoor navigation in a campus building. The project uses Photon PUN 2 for multiplayer synchronization, ARCore/ARFoundation for spatial tracking, and NavMesh for pathfinding around dynamic obstacles.

### Core Tech Stack
- **Engine**: Unity 6000.3.14f1
- **Networking**: Photon PUN 2 (multiplayer player tracking)
- **AR**: Unity XR Foundation (ARCore on Android)
- **Navigation**: MultiSet Unity SDK v1.11.5 (POI system, routing), Unity NavMesh AI
- **Voice AI**: Ollama with qwen3:8b (local LLM for POI extraction), Android SpeechRecognizer
- **UI**: TextMesh Pro, Unity UI

## Architecture & System Design

### High-Level Data Flow

`
Android Voice Input (SpeechRecognizer)
    ↓
VoiceInputHandler (transcription capture)
    ↓
OllamaConnector (local LLM inference, POI extraction)
    ↓
POIManager (fuzzy matching: exact → contains → Levenshtein → word overlap)
    ↓
NavigationAdapter (event dispatcher)
    ↓
MultiSet NavigationController (route computation via SDK)
    ↓
NavMesh pathfinding + NavMeshObstacleHelper (dynamic crowd avoidance)
    ↓
PlayerSync (AR player position sync to other users via Photon)
`

### System Components

#### 1. **Voice Input Pipeline** (Assets/Speech Recognition/)
- **VoiceInputHandler.cs**: Android Java interop for speech-to-text. Requests microphone permission, initializes SpeechRecognizer, captures recognition results in RecognitionListenerProxy (Android callback bridge). Integrates with VoiceUIController for state feedback.
- **OllamaConnector.cs**: HTTP client to local Ollama server. Sends speech text + system prompt to extract POI name. Implements retry logic (2 attempts with 2s delay). Returns JSON response {"poi": "location_name"}. Fires onConnectionFailed UnityEvent if server unreachable (useful for UI error display).

**Key Detail**: Ollama runs on laptop (DHCP IP varies by WiFi). Configure IP in Inspector field ollamaHost or hardcode before each session. Port is 11434 (default).

#### 2. **POI System** (Assets/VoiceInput/)
- **POIData.cs**: Data component attached to each POI GameObject. Fields: poiName (display name, falls back to GameObject.name), kategori (e.g., "ruangan", "toilet"), sinonim[] (aliases for fuzzy matching).
- **POIManager.cs**: Scene-wide POI registry. Scans all POIData children under poiRoot transform at Awake. Builds two-tier lookup:
  - Direct registration of poiName and sinonim from inspector
  - Static sinonimMap hardcoded aliases (e.g., "BAAK" → ["administrasi", "tata usaha", "akademik", ...])
  - Tracks whether a lookup key is from a synonym (boosts name-only matches with 1.0x vs 0.8x for synonyms)

  **Matching Algorithm** (priority order):
  1. Exact string match (normalized)
  2. Contains match (longest substring wins)
  3. Levenshtein distance <= 2 (typo tolerance)
  4. Word overlap scoring with synonym weighting
  5. Category tiebreaker (if multiple POIs score identically, prefer category match from query words)

- **AutoAttachPOIData.cs** (Editor): Menu tool Tools/POI/Auto Attach POIData that scans GameObject "POIs" and adds missing POIData components to direct children.

#### 3. **Navigation Adapter** (Assets/VoiceInput/NavigationAdapter.cs)
Bridges voice input to MultiSet SDK navigation. Receives POIData from VoiceInputHandler's onPoiMatched event. Dispatches to:
- **SendMessage path** (default): Calls SetPOIForNavigation(POI) on NavigationController component
- **Event path** (alternative): Fires UnityEvents onNavigateToTransform, onNavigateToPosition, onNavigateToName for custom handlers
- **Validation**: Uses reflection (ValidateMethodExists) to check methods exist before SendMessage (prevents silent failures)
- **Wiring menu**: Right-click component → "Validate Wiring" performs edit-time checks

**Inspector Setup**:
| Field | Expected | Notes |
|-------|----------|-------|
| Navigation Controller | NavigationController component (from MultiSet SDK) | Required for SDK routing |
| Set Poi Method Name | SetPOIForNavigation | Name of method taking POI parameter |
| Navigation UI Controller | NavigationUIController (from SDK) | For progress slider display |
| Start Navigation UI Method Name | ClickedStartNavigation | Invoked when navigation starts |
| Destination Select UI | Panel GameObject (optional) | Auto-hidden after navigation begins |
| On Navigate To Transform/Position/Name | Event handlers (optional) | Alternative to SendMessage |
| On Navigation Failed | Error handler event (optional) | Fired if no POI component or handler found |

#### 4. **UI Layer** (Assets/UI/Voice/)
- **VoiceUIConfig.cs**: ScriptableObject holding visual config (colors, waveform bar count/spacing, mic pulse speed/scale)
- **VoiceUIController.cs**: Animates voice UI state machine (Idle → Listening → Processing → Error). Features:
  - Waveform bar animation (sine + perlin noise combined)
  - Mic button pulse animation (scale + alpha)
  - Auto-hide transcript after configurable delay
  - State-driven color changes on status pill

#### 5. **Multiplayer Sync** (Assets/Scripts/Multiplayer/)
- **PhotonManager.cs**: Photon connection lifecycle. Connects on startup (if autoConnect=true), waits for localization success from MultiSet SDK, joins room keyed by buildingId_floorId. Spawns player prefab on room join. Listens for NotifyLocalizationSucceeded() callback from MultiSet LocalizationSuccessDataHandler.
- **PlayerSync.cs**: MonoBehaviourPun with IPunObservable. Syncs local AR camera position to map-space (inverse transform). Remote players display as capsule body with name tag (billboarded to face AR camera). Uses lerp smoothing for network position updates.

**Room Naming**: e.g., GedungA_Lt1 (building A, floor 1). Users in same building+floor see each other.

#### 6. **Dynamic Obstacles** (Assets/VoiceInput/NavMeshObstacleHelper.cs)
Pattern for real-time crowd avoidance. NavMeshObstacle with carving enabled ("carves" the NavMesh when active). Methods:
- SetObstacleActive(bool): Toggle obstacle
- SetObstacleSize(Vector3): Update size (for YOLO bounding box integration, future)

**Design rationale**: Carving is lightweight (no rebake) vs full NavMesh rebaking. Sufficient for bounding-box obstacles.

---

## Project Structure

Assets/ contains only custom code (~700 lines total across 10 .cs files). Third-party packages (Photon PUN 2, MultiSet SDK samples) are in subdirectories but not core to the application.

Key paths:
- Assets/Scripts/Multiplayer/ - PhotonManager, PlayerSync
- Assets/Speech Recognition/ - Voice pipeline (VoiceInputHandler, OllamaConnector)
- Assets/VoiceInput/ - POI system (POIManager, POIData, NavigationAdapter, NavMeshObstacleHelper) + Editor tools
- Assets/UI/Voice/ - Voice UI (VoiceUIConfig, VoiceUIController)
- Assets/Scenes/SampleScene.unity - Main scene (single active scene)
- Packages/manifest.json - Dependencies (MultiSet SDK git URL, Photon PUN 2, AR Foundation, NavMesh AI, TextMesh Pro, Input System, etc.)

---

## Development Workflow

### Before Running on Device

1. **Ollama Server**: Ensure Ollama (qwen3:8b model) runs on your laptop in same WiFi:
   ```bash
   ollama run qwen3:8b
   ```
   Test connectivity: curl http://localhost:11434/api/generate -d '{"model":"qwen3:8b","prompt":"test","stream":false}'

2. **Update Ollama IP**: In Inspector, find GameObject with OllamaConnector component. Set ollamaHost field to your laptop's current IPv4 (run ipconfig on Windows, check WiFi adapter).

3. **POI Setup**:
   - Create or identify "POIs" GameObject parent in Hierarchy
   - Assign it to poiRoot field on POIManager component
   - Run Tools/POI/Auto Attach POIData menu tool
   - Populate each POI's poiName, kategori, and sinonim fields

4. **Navigation Wiring** (Inspector):
   - Select GameObject with NavigationAdapter component
   - Drag MultiSet's NavigationController to navigationController field
   - Drag MultiSet's NavigationUIController to navigationUIController field
   - Wire onPoiMatched event from VoiceInputHandler to NavigationAdapter.NavigateToPOI()
   - Right-click NavigationAdapter component → "Validate Wiring" to verify

5. **Photon Config**: Ensure Assets/Resources/PhotonServerSettings.asset is configured with your Photon AppID (obtain from Photon Dashboard).

### Running & Testing

- **Editor Play Mode**: VoiceInputHandler simulates voice input with hardcoded text "saya mau ke Lab Teori 201" when not on Android device. Useful for testing POI matching logic without microphone.
- **Device Build**: Build & Run on Android. Requires mic permission grant, same WiFi as Ollama server.
- **Voice Input Flow**: Click voice button → "Mendengarkan..." → speak destination → transcription + Ollama extraction → POI match + navigation start.

### Common Unity Editor Tasks

- Open scene: Assets/Scenes/SampleScene.unity
- Build for Android: File → Build Settings → Android → Build And Run
- Inspect GameObject: Scene Hierarchy → find "PhotonManager" or "POIManager" to verify component setup
- Test voice in Editor: Press Play, click voice button, check console logs for POI matching

---

## Key Integration Points

### MultiSet SDK
- **NavigationController**: Receives POI, computes route. Method signature: public void SetPOIForNavigation(POI poi)
- **NavigationUIController**: Displays progress during navigation. Method: public void ClickedStartNavigation(POI poi)
- **LocalizationSuccessDataHandler** (in sample scenes): Calls PhotonManager.NotifyLocalizationSucceeded(building, floor) after on-device localization completes

### Photon PUN 2
- **Connection**: PhotonNetwork.ConnectUsingSettings() (uses ServerSettings asset)
- **Room Joining**: PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default) triggered after localization ready
- **Player Instantiation**: PhotonNetwork.Instantiate(playerPrefabName, ...) spawns synced player object
- **Serialization**: PlayerSync.OnPhotonSerializeView() streams AR camera transform (inverse-transformed to map-space) every frame

### NavMesh & Obstacles
- **NavMesh Surface**: Baked in scene. NavMeshObstacle carving applies at runtime when crowd detected.
- **Dynamic Updates**: Call NavMeshObstacleHelper.SetObstacleSize(Vector3) and SetObstacleActive(bool) from YOLO backend integration (future).

---

## Configuration & Debugging

### OllamaConnector
- **ollamaHost** (Inspector field or hardcode): IP of Ollama server (e.g., 192.168.18.150)
- **ollamaPort**: Default 11434
- **modelName**: Default qwen3:8b
- **useHttps**: Boolean for HTTPS (false by default, local only)
- **onConnectionFailed**: UnityEvent fired after 2 retry attempts fail; wire to UI error display
- **System Prompt**: Hardcoded in script. Instructs Ollama to extract POI name only, return JSON {"poi": "..."}. Update if POI list changes.

### POIManager
- **Sinonim Hardcoding**: static sinonimMap dictionary contains fallback aliases if POI components don't have sinonim fields. Edit there for global changes, or use Inspector fields for per-POI overrides.
- **ScanPOIs()**: Called at Awake. Can call manually if POIs added/removed at runtime.
- **Normalization**: All lookups are case-insensitive, punctuation-stripped, whitespace-collapsed.

### VoiceInputHandler
- **In-Editor Fallback**: Bypass Android speech recognizer in Editor by using #if UNITY_ANDROID && !UNITY_EDITOR guards. Test POI matching without device.
- **Permission Handling**: Requests Permission.Microphone at Start and before each listen. Handles both request-time and persistent grants.

### NavigationAdapter
- **Reflection Validation**: ValidateMethodExists() uses BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy to find methods. Fails gracefully with debug logs if method not found.
- **Event Redundancy**: Fires all three onNavigateTo* events simultaneously; any subscribed handler processes the navigation. Allows multiple routing systems (SendMessage + event-based) to coexist.

### PlayerSync
- **Map Space Transform**: Critical for coordinate system. All local player logic assumes mapSpace is the AR Origin or shared coordinate root. Inverse transforms AR camera to map-space for network streaming.
- **Name Tag Billboarding**: Uses Y-axis constraint to keep upright while facing camera (prevents upside-down names).
- **Lerp Speed**: Configurable per-instance (default 12f). Lower = slower network interpolation, higher = snappier but jerkier.

---

## Common Modifications

### Adding a New POI
1. Create GameObject child under "POIs" in Hierarchy
2. Add POIData component (or use Tools/POI/Auto Attach POIData)
3. Fill poiName (display), kategori (filter), sinonim (aliases)
4. Position GameObject at target location in map
5. Ensure MultiSet POI component is also on this GameObject (or parent)
6. Call POIManager.ScanPOIs() at runtime if added dynamically, or restart for Awake scan

### Changing Ollama Model or Server
- Edit OllamaConnector.modelName field in Inspector or hardcode
- Update ollamaHost and ollamaPort fields
- Ensure model is downloaded on Ollama server: ollama pull <model-name>
- Update system prompt if model output format differs

### Integrating YOLO Crowd Detection
- YOLO backend provides bounding box coordinates (x, y, w, h) via HTTP API
- Create GameObject for each detected crowd area
- Attach NavMeshObstacleHelper component
- Call SetObstacleSize() with bounding box converted to Vector3
- Call SetObstacleActive(true/false) based on detection events
- Example HTTP client stub can be added alongside OllamaConnector

### Extending Voice UI
- Edit VoiceUIConfig ScriptableObject (or Inspector fields on VoiceUIController)
- Modify colors (idleColor, listeningColor, processingColor, errorColor)
- Adjust waveform bars (barCount, barWidth, barSpacing, barMinHeight, barMaxHeight, barColor)
- Adjust mic pulse (pulseSpeed, pulseScaleMin/Max, pulseAlphaMin)
- Adjust transcript auto-hide delay: transcriptAutoHideSeconds

---

## Testing Checklist

- Ollama server running and reachable from Android device WiFi
- OllamaConnector.ollamaHost matches laptop IP
- POIs registered in scene (verify in POIManager debug logs at Awake)
- NavigationAdapter wiring validated (right-click component menu)
- Photon AppID configured in ServerSettings
- Voice button clickable, shows "Mendengarkan..." state
- Transcript captured and displayed
- Ollama extracts POI name correctly (check logs)
- POI matched via fuzzy logic (check "best match" log)
- Navigation event fired (check NavigationAdapter logs)
- Remote players visible to other connected users (same building+floor)

---

## README Reference

See README.md for:
- Step-by-step setup (Unity 6000.3.14f1, scene open, Play)
- Ollama server startup instructions
- POI auto-attach tool usage
- NavMeshObstacleHelper pattern explanation
- Troubleshooting (reimport, package re-download)
