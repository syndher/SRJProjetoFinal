using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections;

public class InGameUI : MonoBehaviour
{
    [Header("Player UI")]
    public Slider healthBar1;
    public TextMeshProUGUI scoreText1;
    public Slider healthBar2;
    public TextMeshProUGUI scoreText2;

    [Header("Join Code Display")]
    public TextMeshProUGUI codeText;

    [Header("Victory Panel")]
    public GameObject victoryPanel;
    public TextMeshProUGUI victoryText;

    private ulong player1ClientId = ulong.MaxValue;
    private ulong player2ClientId = ulong.MaxValue;

    private void Start()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);

        var networkSetup = FindFirstObjectByType<NetworkSetup>();
        if (networkSetup != null && NetworkManager.Singleton.IsServer)
        {
            string joinCode = networkSetup.GetJoinCode();
            if (!string.IsNullOrEmpty(joinCode))
            {
                codeText.text = $"{joinCode}";
                codeText.gameObject.SetActive(true);
            }
            else codeText.gameObject.SetActive(false);
        }
        else codeText.gameObject.SetActive(false);

        if (healthBar1 != null) healthBar1.gameObject.SetActive(false);
        if (scoreText1 != null) scoreText1.gameObject.SetActive(false);
        if (healthBar2 != null) healthBar2.gameObject.SetActive(false);
        if (scoreText2 != null) scoreText2.gameObject.SetActive(false);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        PlayerController.OnAnyPlayerHealthChanged += OnHealthChanged;
        GameMatchManager.OnMatchWinsChanged += OnMatchWinsChanged;

        StartCoroutine(RetryUIAssignment());
    }

    public void ShowVictoryScreen(ulong winnerId)
    {
        if (victoryPanel == null)
        {
            Debug.LogError("Victory panel not assigned in InGameUI!");
            return;
        }
        if (victoryText != null)
        {
            if (NetworkManager.Singleton.LocalClientId == winnerId)
                victoryText.text = "YOU WIN THE MATCH!";
            else
                victoryText.text = $"Player {winnerId} WINS THE MATCH!";
        }
        victoryPanel.SetActive(true);
        Debug.Log($"Victory screen shown for winner {winnerId}");
    }

    public void CopyCode()
    {
        if (codeText != null)
            GUIUtility.systemCopyBuffer = codeText.text;
    }

    private IEnumerator RetryUIAssignment()
    {
        while (player1ClientId == ulong.MaxValue || player2ClientId == ulong.MaxValue)
        {
            RefreshUIAssignments();
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        PlayerController.OnAnyPlayerHealthChanged -= OnHealthChanged;
        GameMatchManager.OnMatchWinsChanged -= OnMatchWinsChanged;
    }

    private void OnClientConnected(ulong clientId)
    {
        Invoke(nameof(RefreshUIAssignments), 0.2f);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (player1ClientId == clientId)
        {
            player1ClientId = ulong.MaxValue;
            if (healthBar1 != null) healthBar1.gameObject.SetActive(false);
            if (scoreText1 != null) scoreText1.gameObject.SetActive(false);
        }
        if (player2ClientId == clientId)
        {
            player2ClientId = ulong.MaxValue;
            if (healthBar2 != null) healthBar2.gameObject.SetActive(false);
            if (scoreText2 != null) scoreText2.gameObject.SetActive(false);
        }
    }

    private void RefreshUIAssignments()
    {
        player1ClientId = ulong.MaxValue;
        player2ClientId = ulong.MaxValue;

        int slot = 0;
        foreach (var player in NetworkSetup.AllPlayers)
        {
            if (player == null) continue;

            if (slot == 0)
            {
                player1ClientId = player.OwnerClientId;
                UpdateHealthBar(healthBar1, player);
                UpdateScoreText(scoreText1, player);
            }
            else if (slot == 1)
            {
                player2ClientId = player.OwnerClientId;
                UpdateHealthBar(healthBar2, player);
                UpdateScoreText(scoreText2, player);
            }
            slot++;
            if (slot >= 2) break;
        }
    }

    private void UpdateHealthBar(Slider bar, PlayerController player)
    {
        if (bar == null) return;
        bar.gameObject.SetActive(true);
        bar.maxValue = player.maxHealth;
        bar.value = player.currentHealth.Value;
    }

    private void UpdateScoreText(TextMeshProUGUI text, PlayerController player)
    {
        if (text == null) return;
        text.gameObject.SetActive(true);
        int wins = GetMatchWinsForClient(player.OwnerClientId);
        text.text = $"{wins}";
    }

    private void OnHealthChanged(ulong clientId, int newHealth)
    {
        if (clientId == player1ClientId && healthBar1 != null)
            healthBar1.value = newHealth;
        else if (clientId == player2ClientId && healthBar2 != null)
            healthBar2.value = newHealth;
    }

    private void OnMatchWinsChanged(ulong clientId, int wins)
    {
        if (clientId == player1ClientId && scoreText1 != null)
            scoreText1.text = $"{wins}";
        else if (clientId == player2ClientId && scoreText2 != null)
            scoreText2.text = $"{wins}";
    }

    private int GetMatchWinsForClient(ulong clientId)
    {
        var matchManager = FindFirstObjectByType<GameMatchManager>();
        if (matchManager != null)
            return matchManager.GetWinsForClient(clientId);
        return 0;
    }
}