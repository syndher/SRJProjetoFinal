using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameSceneNetworkSetup : MonoBehaviour
{
    [SerializeField] private List<Transform> playerSpawnLocations;
    [SerializeField] private List<PlayerController> playerPrefabs;

    void Awake()
    {
        var networkSetup = FindFirstObjectByType<NetworkSetup>();
        if (networkSetup != null)
        {
            networkSetup.SetPlayerSpawnData(playerSpawnLocations, playerPrefabs);
            Debug.Log($"Spawn data set: {playerSpawnLocations.Count} locations, {playerPrefabs.Count} prefabs");
        }
        else
        {
            Debug.LogError("NetworkSetup not found!");
        }
    }
}