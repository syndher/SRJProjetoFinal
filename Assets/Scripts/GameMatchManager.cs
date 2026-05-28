using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameMatchManager : NetworkBehaviour
{
    private NetworkSetup networkSetup;

    [Header("Match Settings")]
    [SerializeField] private int winsToWinMatch = 3;
    [SerializeField] private float roundRestartDelay = 3f;

    private Dictionary<ulong, int> matchWins = new Dictionary<ulong, int>();
    private bool isMatchOver = false;

    public static System.Action<ulong, int> OnMatchWinsChanged;

    // Network-synced game ready flag
    public NetworkVariable<bool> gameReady = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            PlayerController.OnAnyPlayerDied += OnRoundEnd;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientCountChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientCountChanged;
            UpdateGameReady();
        }
    }

    private void OnClientCountChanged(ulong _)
    {
        if (IsServer) UpdateGameReady();
    }

    private void UpdateGameReady()
    {
        bool ready = NetworkManager.Singleton.ConnectedClientsIds.Count >= 2;
        if (gameReady.Value != ready)
        {
            gameReady.Value = ready;
            Debug.Log($"Game ready: {ready}");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            PlayerController.OnAnyPlayerDied -= OnRoundEnd;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientCountChanged;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientCountChanged;
            }
        }
        base.OnNetworkDespawn();
    }

    private void OnRoundEnd(ulong deadPlayerId)
    {
        if (isMatchOver) return;

        ulong winnerId = 0;
        foreach (var player in NetworkSetup.AllPlayers)
        {
            if (player != null && player.IsSpawned && player.OwnerClientId != deadPlayerId)
            {
                winnerId = player.OwnerClientId;
                break;
            }
        }
        if (winnerId == 0) return;

        if (!matchWins.ContainsKey(winnerId))
            matchWins[winnerId] = 0;
        matchWins[winnerId]++;

        SyncMatchWinsClientRpc(winnerId, matchWins[winnerId]);

        if (matchWins[winnerId] >= winsToWinMatch)
        {
            isMatchOver = true;
            ShowVictoryScreenClientRpc(winnerId);
            return;
        }

        StartCoroutine(RespawnPlayersAfterDelay(roundRestartDelay));
    }

    [ClientRpc]
    private void SyncMatchWinsClientRpc(ulong clientId, int wins)
    {
        OnMatchWinsChanged?.Invoke(clientId, wins);
    }

    [ClientRpc]
    private void ShowVictoryScreenClientRpc(ulong winnerId)
    {
        var ui = FindFirstObjectByType<InGameUI>();
        if (ui != null)
            ui.ShowVictoryScreen(winnerId);
        else
            Debug.LogError("InGameUI not found");
    }

    private IEnumerator RespawnPlayersAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        RespawnAllPlayers();
    }

    private void RespawnAllPlayers()
    {
        if (!IsServer) return;
        if (networkSetup == null) networkSetup = FindFirstObjectByType<NetworkSetup>();
        var spawns = networkSetup.GetPlayerSpawnLocations();
        if (spawns == null || spawns.Count < 2) return;

        List<PlayerController> players = new List<PlayerController>(NetworkSetup.AllPlayers);
        if (players.Count < 2) return;

        List<Transform> availableSpawns = new List<Transform>(spawns);
        for (int i = 0; i < players.Count; i++)
        {
            int randomIndex = Random.Range(0, availableSpawns.Count);
            Transform spawn = availableSpawns[randomIndex];
            availableSpawns.RemoveAt(randomIndex);
            players[i].ServerRespawn(spawn.position, spawn.rotation);
        }
    }

    public int GetWinsForClient(ulong clientId)
    {
        if (matchWins.ContainsKey(clientId))
            return matchWins[clientId];
        return 0;
    }
}