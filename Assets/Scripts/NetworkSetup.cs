using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System;
using UnityEditor;
using System.IO;

#if UNITY_EDITOR
using UnityEditor.Build.Reporting;
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
    private int playerPrefabIndex = 0;
    private UnityTransport transport;
    private bool isRelay = false;

    void Start()
    {
        // Parse command line arguments
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--server")
            {
                // --server found, this should be a server application
                isServer = true;
            }
            else if (args[i] == "--code")
            {
                joinCode = ((i + 1) < args.Length) ? (args[i + 1]) : ("");
            }
        }

        transport = GetComponent<UnityTransport>();
        if (transport.Protocol == UnityTransport.ProtocolType.RelayUnityTransport)
        {
            isRelay = true;
        }
        else
        {
            textJoinCode.gameObject.SetActive(false);
        }

        if (isServer)
            StartCoroutine(StartAsServerCR());
        else
            StartCoroutine(StartAsClientCR());
    }

    IEnumerator StartAsServerCR()
    {
        var networkManager = GetComponent<NetworkManager>();
        networkManager.enabled = true;
        var transport = GetComponent<UnityTransport>();
        transport.enabled = true;
        SetWindowTitle("Starting as server...");

        // Wait a frame for setups to be done
        yield return null;

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        if (isRelay)
        {
            var loginTask = Login();
            yield return new WaitUntil(() => loginTask.IsCompleted);
            if (loginTask.Exception != null)
            {
                Debug.LogError("Login failed: " + loginTask.Exception);
                yield break;
            }
            Debug.Log("Login successfull!");

            var allocationTask = CreateAllocationAsync(maxPlayers);
            yield return new WaitUntil(() => allocationTask.IsCompleted);
            if (allocationTask.Exception != null)
            {
                Debug.LogError("Allocation failed: " + allocationTask.Exception);
                yield break;
            }
            else
            {
                Debug.Log("Allocation successfull!");
                // Fetch result of the task
                Allocation allocation = allocationTask.Result;

                relayData = new RelayHostData();
                // Find the appropriate endpoint, just select the first one and use it
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

                    transport.SetRelayServerData(relayData.IPv4Address, relayData.Port, relayData.AllocationIDBytes, relayData.Key, relayData.ConnectionData);

                }
            }
        }

        InitAnalytics();

        if (networkManager.StartServer())
        {
            SetWindowTitle("MPWyzards - Server");
            Debug.Log($"Serving on port {transport.ConnectionData.Port}...");

            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
        else
        {
            SetWindowTitle("Fail to start as server");
            Debug.LogError($"Failed to serve on port {transport.ConnectionData.Port}...");
        }
    }

    private async Task Login()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error login: " + e);
            throw e;
        }
    }

    private async Task<Allocation> CreateAllocationAsync(int maxPlayers)
    {
        try
        {
            // This requests space for maxPlayers + 1 connections (the +1 is for the server itself)
            Allocation allocation = await Unity.Services.Relay.RelayService.Instance.CreateAllocationAsync(maxPlayers);
            return allocation;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error creating allocation: " + e);
            throw;
        }
    }

    private async Task<string> GetJoinCodeAsync(Guid allocationID)
    {
        try
        {
            string code = await Unity.Services.Relay.RelayService.Instance.GetJoinCodeAsync(allocationID);
            return code;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error retrieving join code: " + e);
            throw;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        UnityEngine.Debug.Log($"Player {clientId} connected, prefab index = {playerPrefabIndex}!");

        // Check a free spot for this player
        var spawnPos = Vector3.zero;
        var currentPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var playerSpawnLocation in playerSpawnLocations)
        {
            var closestDist = float.MaxValue;
            foreach (var player in currentPlayers)
            {
                float d = Vector3.Distance(player.transform.position, playerSpawnLocation.position);
                closestDist = Mathf.Min(closestDist, d);
            }
            if (closestDist > 20)
            {
                spawnPos = playerSpawnLocation.position;
                break;
            }
        }
        // Spawn player object
        var spawnedObject = Instantiate(playerPrefabs[playerPrefabIndex], spawnPos, Quaternion.identity);
        var prefabNetworkObject = spawnedObject.GetComponent<NetworkObject>();
        // It is a player object, Unity needs to know this
        prefabNetworkObject.SpawnAsPlayerObject(clientId, true);
        // Now the ownership of this object is no longer the server, but the client that just connected
        prefabNetworkObject.ChangeOwnership(clientId);
        playerPrefabIndex = (playerPrefabIndex + 1) % playerPrefabs.Count;
    }
    private void OnClientDisconnected(ulong clientId)
    {
        UnityEngine.Debug.Log($"Player {clientId} disconnected!");
    }

    IEnumerator StartAsClientCR()
    {
        var networkManager = GetComponent<NetworkManager>();
        networkManager.enabled = true;
        var transport = GetComponent<UnityTransport>();
        transport.enabled = true;
        SetWindowTitle("Starting as client...");
        // Wait a frame for setups to be done
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
            Debug.Log("Login successfull!");
            //Ask Unity Services for allocation data based on a join code
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

                relayData = new RelayHostData();
                var allocation = joinAllocationTask.Result;
                // Find the appropriate endpoint, just select the first one and use it
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
            SetWindowTitle("MPWyzards - Client...");
            UnityEngine.Debug.Log($"Connecting on port {transport.ConnectionData.Port}...");
        }
        else
        {
            SetWindowTitle("Fail to start as client");
            UnityEngine.Debug.LogError($"Failed to connect on port {transport.ConnectionData.Port}...");
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
            var allocation = await Unity.Services.Relay.RelayService.Instance.JoinAllocationAsync(joinCode);

            return allocation;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error joining allocation: " + e);
            throw;
        }
    }

#if UNITY_STANDALONE_WIN
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowText(IntPtr hWnd, string lpString);
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    static extern IntPtr EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    // Delegate to filter windows
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private static IntPtr FindWindowByProcessId(uint processId)
    {
        IntPtr windowHandle = IntPtr.Zero;
        EnumWindows((hWnd, lParam) =>
        {
            uint windowProcessId;
            GetWindowThreadProcessId(hWnd, out windowProcessId);
            if (windowProcessId == processId)
            {
                windowHandle = hWnd;
                return false; // Found the window, stop enumerating
            }
            return true; // Continue enumerating
        }, IntPtr.Zero);
        return windowHandle;
    }

    static void SetWindowTitle(string title)
    {
#if !UNITY_EDITOR
        uint processId = (uint)Process.GetCurrentProcess().Id;
        IntPtr hWnd = FindWindowByProcessId(processId);
        if (hWnd != IntPtr.Zero)
        {
            SetWindowText(hWnd, title);
        }
#endif
    }
#else
    static void SetWindowTitle(string title)
    {
    }
#endif


#if UNITY_EDITOR
    [MenuItem("Tools/Build Windows (x64)", priority = 0)]
    public static bool BuildGame()
    {
        // Specify build options
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
        buildPlayerOptions.locationPathName = Path.Combine("Builds", "MPWyzard.exe");
        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
        buildPlayerOptions.options = BuildOptions.None;
        // Perform the build
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        // Output the result of the build
        Debug.Log($"Build ended with status: {report.summary.result}");
        // Additional log on the build, looking at report.summary
        return report.summary.result == BuildResult.Succeeded;
    }
#endif


#if UNITY_EDITOR
    private static void Run(string path, string args)
    {
        // Start a new process
        Process process = new Process();
        // Configure the process using the StartInfo properties
        process.StartInfo.FileName = path;
        process.StartInfo.Arguments = args;
        process.StartInfo.WindowStyle = ProcessWindowStyle.Normal; // Choose the window style: Hidden, Minimized, Maximized, Normal
        process.StartInfo.RedirectStandardOutput = false; // Set to true to redirect the output (so you can read it in Unity)
        process.StartInfo.UseShellExecute = true; // Set to false if you want to redirect the output
                                                  // Run the process
        process.Start();
    }

    [MenuItem("Tools/Build and Launch (Server)", priority = 10)]
    public static void BuildAndLaunch1()
    {
        CloseAll();
        if (BuildGame())
        {
            LaunchServer();
        }
    }
    [MenuItem("Tools/Build and Launch (Client)", priority = 15)]
    public static void BuildAndLaunchClient()
    {
        CloseAll();
        if (BuildGame())
        {
            LaunchClient();
        }
    }

    [MenuItem("Tools/Build and Launch (Server + Client)", priority = 20)]
    public static void BuildAndLaunchServerAndClient()
    {
        CloseAll();
        if (BuildGame())
        {
            LaunchClientAndServer();
        }
    }
    [MenuItem("Tools/Launch (Server) _F11", priority = 30)]
    public static void LaunchServer()
    {
        Run("Builds\\MPWyzard.exe", "--server");
    }
    [MenuItem("Tools/Launch (Server + Client)", priority = 40)]
    public static void LaunchClientAndServer()
    {
        LaunchServer();
        LaunchClient();
    }
    [MenuItem("Tools/Launch (Client)", priority = 45)]
    public static void LaunchClient()
    {
        Run("Builds\\MPWyzard.exe", "");
    }

    [MenuItem("Tools/Close All", priority = 100)]
    public static void CloseAll()
    {
        // Get all processes with the specified name
        Process[] processes = Process.GetProcessesByName("MPWyzard");
        foreach (var process in processes)
        {
            try
            {
                // Close the process
                process.Kill();
                // Wait for the process to exit
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                // Handle exceptions, if any
                // This could occur if the process has already exited or you don't have permission to kill it
                Debug.LogWarning($"Error trying to kill process {process.ProcessName}: {ex.Message}");
            }
        }
    }
#endif
}
