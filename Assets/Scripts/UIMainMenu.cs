using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIMainMenu : MonoBehaviour
{
    [SerializeField] private NetworkSetup networkSetup;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private string gameSceneName = "Game";

    private async void Start()
    {
        hostButton.interactable = false;
        joinButton.interactable = false;

        await networkSetup.InitializeUnityServices();

        hostButton.interactable = true;
        joinButton.interactable = true;
    }

    public async void OnHostClicked()
    {
        statusText.text = "Creating lobby...";
        string joinCode = await networkSetup.StartHostWithRelay(2, gameSceneName);
        if (!string.IsNullOrEmpty(joinCode))
        {
            statusText.text = $"Host started! Join code: {joinCode}";
        }
        else
        {
            statusText.text = "Failed to host.";
        }
    }

    public async void OnJoinClicked()
    {
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            statusText.text = "Please enter a join code.";
            return;
        }

        statusText.text = "Joining...";
        await networkSetup.StartClientWithRelay(code);
        statusText.text = "Connecting...";
    }
}