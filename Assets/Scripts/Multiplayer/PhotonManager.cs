using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using Photon.Realtime;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PhotonManager : MonoBehaviourPunCallbacks
{
    [Header("Photon")]
    [SerializeField] private string gameVersion = "1.0";
    [SerializeField] private bool autoConnect = true;

    [Header("Room")]
    [SerializeField] private string buildingId = "GedungA";
    [SerializeField] private string floorId = "Lt1";
    [SerializeField] private int maxPlayers = 20;

    [Header("Scene References")]
    [SerializeField] private Transform mapSpace;
    [Tooltip("AR Camera (from XR Origin). If null, Camera.main will be used")]
    [SerializeField] private Camera arCamera;

    [Header("Player Prefab")]
    [Tooltip("Prefab under Assets/Resources/")]
    [SerializeField] private string playerPrefabName = "PlayerPrefab";

    private bool isLocalized;
    private bool isOnMasterServer;

    void Start()
    {
        // Ensure this is registered as Photon callback target
        PhotonNetwork.AddCallbackTarget(this);

        if (autoConnect)
        {
            Connect();
        }

#if UNITY_EDITOR
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        debugLocalize = new InputAction("DebugLocalize", binding: "<Keyboard>/l");
        debugLocalize.performed += _ =>
        {
            Debug.Log("[PhotonManager] DEBUG: L key detected — simulating localization.");
            // Tembak UnityEvent BERSAMA, jangan panggil OnLocalizationSuccess() sendiri.
            // Memanggil diri sendiri membuat Editor menguji jalur yang berbeda dari device:
            // di device LocalizationSuccess menyalakan semua listener (UaaLEntryPoint,
            // FloorVisibilityManager, FloorTransitionController, ...), di Editor dulu hanya
            // PhotonManager — sehingga bug wiring lolos di Editor lalu muncul di lapangan.
            //
            // ponytail: ini tambalan, bukan solusi. Yang benar adalah simulasi bawaan SDK
            // (SimulationDataManager -> Fetch/Download). Hapus blok ini begitu data simulasi
            // untuk map yang dipakai sudah direkam lewat scene SimulationDataCapture.unity.
            var lm = FindFirstObjectByType<MultiSet.SingleFrameLocalizationManager>();
            if (lm != null && lm.LocalizationSuccess != null)
                lm.LocalizationSuccess.Invoke();
            else
                OnLocalizationSuccess();
        };
        debugLocalize.Enable();
        Debug.Log("[PhotonManager] DEBUG: Press L anytime to simulate localization success.");
#endif
    }

#if UNITY_EDITOR
    private InputAction debugLocalize;

    void OnDestroy()
    {
        debugLocalize?.Disable();
        debugLocalize?.Dispose();
    }
#endif

    public void Connect()
    {
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("[PhotonManager] Already connected, skipping ConnectUsingSettings.");
            return;
        }

        Debug.Log("[PhotonManager] Connecting to Photon...");
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.NickName = GetDefaultNickname();
        PhotonNetwork.ConnectUsingSettings();
    }

    // ── Photon Callbacks ─────────────────────────────────────────

    public override void OnConnectedToMaster()
    {
        Debug.Log("[PhotonManager] OnConnectedToMaster — ready for matchmaking.");
        isOnMasterServer = true;
        TryJoinRoom();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning("[PhotonManager] Disconnected: " + cause);
        isOnMasterServer = false;
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[PhotonManager] OnJoinedRoom — room: " + PhotonNetwork.CurrentRoom.Name);
        isOnMasterServer = false; // now on GameServer
        SpawnPlayer();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[PhotonManager] OnJoinRoomFailed ({returnCode}): {message}");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[PhotonManager] OnCreateRoomFailed ({returnCode}): {message}");
    }

    // ── Localization ─────────────────────────────────────────────

    /// <summary>
    /// Called when AR localization succeeds (e.g. from Immersal).
    /// Only sets the flag, then attempts to join if already on Master.
    /// If not yet connected, starts connection — OnConnectedToMaster
    /// will pick up the join automatically.
    /// </summary>
    public void OnLocalizationSuccess()
    {
        // autoConnect=false berarti dilarang gabung room, bukan cuma "jangan gabung saat
        // Start()". Sebelum guard ini, autoConnect: 0 di scene TIDAK MENCEGAH auto-join --
        // localize sukses tetap memicu Connect() lewat jalur ini, lepas dari flag-nya sama
        // sekali (audit independen menemukan tambalan sebelumnya gagal mencapai tujuan).
        if (!autoConnect)
        {
            Debug.Log("[PhotonManager] Localization succeeded, tapi autoConnect=false -- tidak join room.");
            isLocalized = true;
            return;
        }

        Debug.Log("[PhotonManager] Localization succeeded.");
        isLocalized = true;

        if (isOnMasterServer)
        {
            TryJoinRoom();
        }
        else if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("[PhotonManager] Not connected yet — connecting now...");
            Connect();
            // JoinOrCreateRoom will be called in OnConnectedToMaster
        }
        else
        {
            Debug.Log("[PhotonManager] Connected but not on MasterServer yet — waiting for OnConnectedToMaster.");
            // JoinOrCreateRoom will be called in OnConnectedToMaster
        }
    }

    public void NotifyLocalizationSucceeded(string building, string floor)
    {
        if (!string.IsNullOrWhiteSpace(building))
        {
            buildingId = building;
        }
        if (!string.IsNullOrWhiteSpace(floor))
        {
            floorId = floor;
        }

        OnLocalizationSuccess();
    }

    // ── Private Helpers ──────────────────────────────────────────

    /// <summary>
    /// Guards JoinOrCreateRoom behind TWO conditions:
    ///   1. Client is on MasterServer (not GameServer, not disconnected)
    ///   2. AR localization has completed
    /// </summary>
    private void TryJoinRoom()
    {
        if (!isLocalized)
        {
            Debug.Log("[PhotonManager] TryJoinRoom skipped — localization not ready.");
            return;
        }

        // Use actual Photon state instead of manual flag
        var state = PhotonNetwork.NetworkClientState;
        bool readyToJoin = state == Photon.Realtime.ClientState.ConnectedToMasterServer ||
                           state == Photon.Realtime.ClientState.JoinedLobby;

        if (!readyToJoin)
        {
            Debug.Log($"[PhotonManager] TryJoinRoom skipped — Photon state: {state}. Will retry in OnConnectedToMaster.");
            return;
        }

        string roomName = GetRoomName();
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = (byte)Mathf.Clamp(maxPlayers, 1, 200)
        };

        Debug.Log($"[PhotonManager] JoinOrCreateRoom: \"{roomName}\" (max {options.MaxPlayers})");
        PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
    }

    private void SpawnPlayer()
    {
        if (mapSpace == null)
        {
            GameObject mapSpaceGo = GameObject.Find("Map Space");
            mapSpace = mapSpaceGo != null ? mapSpaceGo.transform : null;
        }

        if (arCamera == null && Camera.main != null)
        {
            arCamera = Camera.main;
        }

        Debug.Log("[PhotonManager] Spawning player...");
        GameObject playerObj = PhotonNetwork.Instantiate(playerPrefabName, Vector3.zero, Quaternion.identity);
        playerObj.transform.SetParent(mapSpace, false);
        playerObj.transform.localPosition = Vector3.zero;
        playerObj.transform.localRotation = Quaternion.identity;

        PlayerSync sync = playerObj.GetComponent<PlayerSync>();
        if (sync != null)
        {
            sync.SetReferences(mapSpace, arCamera);
        }
    }

    private string GetRoomName()
    {
        return buildingId + "_" + floorId;
    }

    private string GetDefaultNickname()
    {
        string id = SystemInfo.deviceUniqueIdentifier ?? SystemInfo.deviceName ?? "XXXXXX";
        if (id.Length >= 6)
        {
            id = id.Substring(0, 6);
        }
        return "User_" + id;
    }
}
