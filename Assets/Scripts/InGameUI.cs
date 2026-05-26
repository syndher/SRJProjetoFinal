using TMPro;
using UnityEngine;
using Unity.Netcode;

public class InGameUI : MonoBehaviour
{
    public TextMeshProUGUI codeText;
    void Start()
    {
        var networkSetup = FindFirstObjectByType<NetworkSetup>();
        if (networkSetup != null && NetworkManager.Singleton.IsServer)
        {
            string joinCode = networkSetup.GetJoinCode();
            if (!string.IsNullOrEmpty(joinCode))
            {
                codeText.text = $"Join Code: {joinCode}";
                return;
            }
        }
        codeText.gameObject.SetActive(false);
    }
}