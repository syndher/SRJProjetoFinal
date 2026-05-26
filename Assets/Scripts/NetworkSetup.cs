using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System;
using System.IO;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.Build.Reporting;
using UnityEditor;
#endif

using System.Linq;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using Unity.Services.Relay.Models;
using UnityEngine.UnityConsent;

#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
using System.Diagnostics;
#endif

using Debug = UnityEngine.Debug;

public class NetworkSetup : MonoBehaviour
{
    [SerializeField] private List<Transform>        playerSpawnLocations;
    [SerializeField] private List<PlayerController> playerPrefabs;
    [SerializeField] private TextMeshProUGUI        textJoinCode;
    [SerializeField] private int                    maxPlayers = 2;
    [SerializeField] private string                 joinCode = "";
    [SerializeField] private bool                   enableAnalytics;

    private HashSet<ulong> spawnedClients = new HashSet<ulong>();
    private int playerPrefabIndex = 0;

    public class RelayHostData
    {
        public string JoinCode;
        public string IPv4Address;
        public ushort Port;
        public Guid AllocationID;
        public byte[] AllocationIDBytes;
        public byte[] ConnectionData;
        public byte[] HostConnectionData;
        public byte[] Key;
    }
    private RelayHostData relayData;

    private bool isServer = false;
    private UnityTransport transport;
    private NetworkManager networkManager;
    private bool isRelay = false;
    private static bool unityServicesInitialized = false;

    private string pendingGameSceneName;
    private Queue<ulong> pendingClientSpawns = new Queue<ulong>();

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        transport = GetComponent<UnityTransport>();
        networkManager = GetComponent<NetworkManager>();
        if (transport == null) Debug.LogError("No UnityTransport found!");
        if (networkManager == null) Debug.LogError("No NetworkManager found!");

        if (playerPrefabs != null)
        {
            foreach (var prefab in playerPrefabs)
            {
                if (prefab != null && prefab.GetComponent<NetworkObject>() != null)
                {
                    networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab.gameObject });
                }
            }
        }
    }

    void Start()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--server")
                isServer = true;
            else if (args[i] == "--code")
                joinCode = ((i + 1) < args.Length) ? args[i + 1] : "";
        }

        transport = GetComponent<UnityTransport>();
        if (transport != null && transport.Protocol == UnityTransport.ProtocolType.RelayUnityTransport)
            isRelay = true;

        if (isServer)
            StartCoroutine(StartAsServerCR());
        else if (!string.IsNullOrEmpty(joinCode))
            StartCoroutine(StartAsClientCR());
        //else
            //Debug.LogError("No valid launch arguments. Use --server or --code <joinCode>");
    }

    public void SetPlayerSpawnData(List<Transform> locations, List<PlayerController> prefabs, TextMeshProUGUI codeDisplayText = null)
    {
        playerSpawnLocations = locations;
        playerPrefabs = prefabs;
        textJoinCode = codeDisplayText;
    }

    public async Task InitializeUnityServices()
    {
        if (unityServicesInitialized) return;
        await Login();
        unityServicesInitialized = true;
    }

    public async Task<string> StartHostWithRelay(int maxPlayers, string gameSceneName = null)
    {
        await InitializeUnityServices();

        var allocationTask = CreateAllocationAsync(maxPlayers);
        await allocationTask;
        if (allocationTask.Exception != null)
        {
            Debug.LogError("Allocation failed: " + allocationTask.Exception);
            return null;
        }
        Allocation allocation = allocationTask.Result;

        relayData = new RelayHostData();
        foreach (var endpoint in allocation.ServerEndpoints)
        {
            relayData.IPv4Address = endpoint.Host;
            relayData.Port = (ushort)endpoint.Port;
            break;
        }
        relayData.AllocationID = allocation.AllocationId;
        relayData.AllocationIDBytes = allocation.AllocationIdBytes;
        relayData.ConnectionData = allocation.ConnectionData;
        relayData.Key = allocation.Key;

        var joinCodeTask = GetJoinCodeAsync(relayData.AllocationID);
        await joinCodeTask;
        if (joinCodeTask.Exception != null)
        {
            Debug.LogError("Join code failed: " + joinCodeTask.Exception);
            return null;
        }
        relayData.JoinCode = joinCodeTask.Result;
        if (textJoinCode != null)
        {
            textJoinCode.text = $"JoinCode:{relayData.JoinCode}";
            textJoinCode.gameObject.SetActive(true);
        }

        transport.SetRelayServerData(relayData.IPv4Address, relayData.Port,
            relayData.AllocationIDBytes, relayData.Key, relayData.ConnectionData);

        InitAnalytics();

        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is missing!");
            return null;
        }

        if (networkManager.StartServer())
        {
            Debug.Log($"Server started on port {transport.ConnectionData.Port}");
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;

            if (!string.IsNullOrEmpty(gameSceneName))
            {
                pendingGameSceneName = gameSceneName;
                SceneManager.sceneLoaded += OnGameSceneLoaded;
                networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
            return relayData.JoinCode;
        }
        else
        {
            Debug.LogError($"Failed to start server on port {transport.ConnectionData.Port}");
            return null;
        }
    }

    public async Task StartClientWithRelay(string joinCode)
    {
        await InitializeUnityServices();

        var joinAllocationTask = JoinAllocationAsync(joinCode);
        await joinAllocationTask;
        if (joinAllocationTask.Exception != null)
        {
            Debug.LogError("Join allocation failed: " + joinAllocationTask.Exception);
            return;
        }
        var allocation = joinAllocationTask.Result;

        relayData = new RelayHostData();
        foreach (var endpoint in allocation.ServerEndpoints)
        {
            relayData.IPv4Address = endpoint.Host;
            relayData.Port = (ushort)endpoint.Port;
            break;
        }
        relayData.AllocationID = allocation.AllocationId;
        relayData.AllocationIDBytes = allocation.AllocationIdBytes;
        relayData.ConnectionData = allocation.ConnectionData;
        relayData.HostConnectionData = allocation.HostConnectionData;
        relayData.Key = allocation.Key;
        transport.SetRelayServerData(relayData.IPv4Address, relayData.Port,
            relayData.AllocationIDBytes, relayData.Key, relayData.ConnectionData,
            relayData.HostConnectionData);

        InitAnalytics();

        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is missing!");
            return;
        }

        if (networkManager.StartClient())
        {
            Debug.Log($"Client connecting on port {transport.ConnectionData.Port}");
        }
        else
        {
            Debug.LogError($"Failed to start client on port {transport.ConnectionData.Port}");
        }
    }

    IEnumerator StartAsServerCR()
    {
        networkManager.enabled = true;
        transport.enabled = true;
        SetWindowTitle("Starting as server...");
        yield return null;

        if (isRelay)
        {
            var loginTask = Login();
            yield return new WaitUntil(() => loginTask.IsCompleted);
            if (loginTask.Exception != null)
            {
                Debug.LogError("Login failed: " + loginTask.Exception);
                yield break;
            }
            Debug.Log("Login successful!");

            var allocationTask = CreateAllocationAsync(maxPlayers);
            yield return new WaitUntil(() => allocationTask.IsCompleted);
            if (allocationTask.Exception != null)
            {
                Debug.LogError("Allocation failed: " + allocationTask.Exception);
                yield break;
            }
            else
            {
                Debug.Log("Allocation successful!");
                Allocation allocation = allocationTask.Result;

                relayData = new RelayHostData();
                foreach (var endpoint in allocation.ServerEndpoints)
                {
                    relayData.IPv4Address = endpoint.Host;
                    relayData.Port = (ushort)endpoint.Port;
                    break;
                }
                relayData.AllocationID = allocation.AllocationId;
                relayData.AllocationIDBytes = allocation.AllocationIdBytes;
                relayData.ConnectionData = allocation.ConnectionData;
                relayData.Key = allocation.Key;

                var joinCodeTask = GetJoinCodeAsync(relayData.AllocationID);
                yield return new WaitUntil(() => joinCodeTask.IsCompleted);
                if (joinCodeTask.Exception != null)
                {
                    Debug.LogError("Join code failed: " + joinCodeTask.Exception);
                    yield break;
                }
                else
                {
                    Debug.Log("Code retrieved!");
                    relayData.JoinCode = joinCodeTask.Result;
                    if (textJoinCode != null)
                    {
                        textJoinCode.text = $"JoinCode:{relayData.JoinCode}";
                        textJoinCode.gameObject.SetActive(true);
                    }
                    transport.SetRelayServerData(relayData.IPv4Address, relayData.Port,
                        relayData.AllocationIDBytes, relayData.Key, relayData.ConnectionData);
                }
            }
        }

        InitAnalytics();

        if (networkManager.StartServer())
        {
            SetWindowTitle("MPTanks - Server");
            Debug.Log($"Serving on port {transport.ConnectionData.Port}");
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
        else
        {
            SetWindowTitle("Fail to start as server");
            Debug.LogError($"Failed to serve on port {transport.ConnectionData.Port}");
        }
    }

    private async Task Login()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (Exception e)
        {
            Debug.LogError("Error login: " + e);
            throw e;
        }
    }

    private async Task<Allocation> CreateAllocationAsync(int maxPlayers)
    {
        try
        {
            return await Unity.Services.Relay.RelayService.Instance.CreateAllocationAsync(maxPlayers);
        }
        catch (Exception e)
        {
            Debug.LogError("Error creating allocation: " + e);
            throw;
        }
    }

    private async Task<string> GetJoinCodeAsync(Guid allocationID)
    {
        try
        {
            return await Unity.Services.Relay.RelayService.Instance.GetJoinCodeAsync(allocationID);
        }
        catch (Exception e)
        {
            Debug.LogError("Error retrieving join code: " + e);
            throw;
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (spawnedClients.Contains(clientId))
        {
            Debug.Log($"Client {clientId} already spawned – ignoring duplicate request.");
            return;
        }
        if (playerSpawnLocations == null || playerSpawnLocations.Count == 0)
        {
            Debug.LogError("No player spawn locations set!");
            return;
        }
        if (playerPrefabs == null || playerPrefabs.Count == 0)
        {
            Debug.LogError("No player prefabs set!");
            return;
        }

        Vector3 spawnPos = playerSpawnLocations[0].position;
        var currentPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var spawnLoc in playerSpawnLocations)
        {
            bool occupied = false;
            foreach (var player in currentPlayers)
            {
                if (Vector3.Distance(player.transform.position, spawnLoc.position) < 2f)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied)
            {
                spawnPos = spawnLoc.position;
                break;
            }
        }

        var playerPrefab = playerPrefabs[playerPrefabIndex % playerPrefabs.Count];
        var playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        var netObj = playerObj.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, true);

        Debug.Log($"Spawned player for client {clientId}, prefab index {playerPrefabIndex}");
        playerPrefabIndex++;
        spawnedClients.Add(clientId);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer) return;

        if (pendingGameSceneName != null && SceneManager.GetActiveScene().name != pendingGameSceneName)
        {
            pendingClientSpawns.Enqueue(clientId);
            Debug.Log($"Queued spawn for client {clientId} (scene not ready)");
        }
        else
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
    }

    private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == pendingGameSceneName)
        {
            Debug.Log($"Game scene '{scene.name}' loaded – spawning any queued players");
            while (pendingClientSpawns.Count > 0)
            {
                ulong clientId = pendingClientSpawns.Dequeue();
                SpawnPlayerForClient(clientId);
            }
            SceneManager.sceneLoaded -= OnGameSceneLoaded;
            pendingGameSceneName = null;
        }
    }

    IEnumerator StartAsClientCR()
    {
        networkManager.enabled = true;
        transport.enabled = true;
        SetWindowTitle("Starting as client...");
        yield return null;

        if (isRelay)
        {
            var loginTask = Login();
            yield return new WaitUntil(() => loginTask.IsCompleted);
            if (loginTask.Exception != null)
            {
                Debug.LogError("Login failed: " + loginTask.Exception);
                yield break;
            }
            Debug.Log("Login successful!");
            var joinAllocationTask = JoinAllocationAsync(joinCode);
            yield return new WaitUntil(() => joinAllocationTask.IsCompleted);
            if (joinAllocationTask.Exception != null)
            {
                Debug.LogError("Join allocation failed: " + joinAllocationTask.Exception);
                yield break;
            }
            else
            {
                Debug.Log("Allocation joined!");
                var allocation = joinAllocationTask.Result;
                relayData = new RelayHostData();
                foreach (var endpoint in allocation.ServerEndpoints)
                {
                    relayData.IPv4Address = endpoint.Host;
                    relayData.Port = (ushort)endpoint.Port;
                    break;
                }
                relayData.AllocationID = allocation.AllocationId;
                relayData.AllocationIDBytes = allocation.AllocationIdBytes;
                relayData.ConnectionData = allocation.ConnectionData;
                relayData.HostConnectionData = allocation.HostConnectionData;
                relayData.Key = allocation.Key;
                transport.SetRelayServerData(relayData.IPv4Address, relayData.Port,
                    relayData.AllocationIDBytes, relayData.Key, relayData.ConnectionData,
                    relayData.HostConnectionData);
            }
        }

        InitAnalytics();

        if (networkManager.StartClient())
        {
            SetWindowTitle("MPTanks - Client...");
            Debug.Log($"Connecting on port {transport.ConnectionData.Port}");
        }
        else
        {
            SetWindowTitle("Fail to start as client");
            Debug.LogError($"Failed to connect on port {transport.ConnectionData.Port}");
        }
    }

    void InitAnalytics()
    {
        if (enableAnalytics)
        {
            ConsentState consentState = EndUserConsent.GetConsentState();
            consentState.AnalyticsIntent = ConsentStatus.Granted;
            EndUserConsent.SetConsentState(consentState);
        }
    }

    private async Task<JoinAllocation> JoinAllocationAsync(string joinCode)
    {
        try
        {
            return await Unity.Services.Relay.RelayService.Instance.JoinAllocationAsync(joinCode);
        }
        catch (Exception e)
        {
            Debug.LogError("Error joining allocation: " + e);
            throw;
        }
    }

    public string GetJoinCode() => relayData?.JoinCode;

    // ------------------------------------------------------------------
    // Windows window title
    // ------------------------------------------------------------------
#if UNITY_STANDALONE_WIN
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowText(IntPtr hWnd, string lpString);
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    static extern IntPtr EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private static IntPtr FindWindowByProcessId(uint processId)
    {
        IntPtr windowHandle = IntPtr.Zero;
        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint windowProcessId);
            if (windowProcessId == processId)
            {
                windowHandle = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return windowHandle;
    }

    static void SetWindowTitle(string title)
    {
#if !UNITY_EDITOR
        uint processId = (uint)Process.GetCurrentProcess().Id;
        IntPtr hWnd = FindWindowByProcessId(processId);
        if (hWnd != IntPtr.Zero) SetWindowText(hWnd, title);
#endif
    }
#else
    static void SetWindowTitle(string title) { }
#endif

    // ------------------------------------------------------------------
    // Editor build & launch tools
    // ------------------------------------------------------------------
#if UNITY_EDITOR
    [MenuItem("Tools/Build Windows (x64)", priority = 0)]
    public static bool BuildGame()
    {
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        options.locationPathName = Path.Combine("Builds", "MPTanks.exe");
        options.target = BuildTarget.StandaloneWindows64;
        options.options = BuildOptions.None;
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Build ended with status: {report.summary.result}");
        return report.summary.result == BuildResult.Succeeded;
    }

    private static void Run(string path, string args)
    {
        Process proc = new Process();
        proc.StartInfo.FileName = path;
        proc.StartInfo.Arguments = args;
        proc.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
        proc.StartInfo.UseShellExecute = true;
        proc.Start();
    }

    [MenuItem("Tools/Launch (Server)", priority = 30)]
    public static void LaunchServer() => Run("Builds\\MPTanks.exe", "--server");

    [MenuItem("Tools/Launch (Client 1)", priority = 46)]
    public static void LaunchClient1() => Run("Builds\\MPTanks.exe", "--code YOURCODE");

    [MenuItem("Tools/Launch (Client 2)", priority = 47)]
    public static void LaunchClient2() => Run("Builds\\MPTanks.exe", "--code YOURCODE");

    [MenuItem("Tools/Launch (2 Clients)", priority = 50)]
    public static void LaunchTwoClients()
    {
        LaunchClient1();
        LaunchClient2();
    }

    [MenuItem("Tools/Close All", priority = 100)]
    public static void CloseAll()
    {
        foreach (var p in Process.GetProcessesByName("MPTanks"))
        {
            try { p.Kill(); p.WaitForExit(); }
            catch (Exception ex) { Debug.LogWarning($"Error killing process: {ex.Message}"); }
        }
    }
#endif
}