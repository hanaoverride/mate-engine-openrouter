using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI helper to display OpenRouter connection status and basic controls
/// Can be added to any UI Text component to show real-time chat status
/// </summary>
public class ChatStatusDisplay : MonoBehaviour
{
    [Header("Status Display")]
    public Text statusText;
    public Text modelText;
    public Text historyCountText;
    
    [Header("Control Buttons")]
    public Button switchProviderButton;
    public Button clearHistoryButton;
    public Button testConnectionButton;
    
    [Header("References")]
    public ChatBridge chatBridge;
    public OpenRouterCharacter openRouterCharacter;

    [Header("Update Settings")]
    public bool autoUpdate = true;
    public float updateInterval = 1.0f;
    
    private float lastUpdateTime;

    void Start()
    {
        // Auto-find components if not assigned
        if (chatBridge == null)
            chatBridge = FindFirstObjectByType<ChatBridge>();
            
        if (openRouterCharacter == null)
            openRouterCharacter = FindFirstObjectByType<OpenRouterCharacter>();

        // Auto-find status text if not assigned
        if (statusText == null)
            statusText = GetComponent<Text>();

        // Setup button listeners
        SetupButtons();
        
        // Initial update
        UpdateStatus();
    }

    void Update()
    {
        if (autoUpdate && Time.time - lastUpdateTime > updateInterval)
        {
            UpdateStatus();
            lastUpdateTime = Time.time;
        }
    }

    private void SetupButtons()
    {
        if (switchProviderButton != null)
        {
            switchProviderButton.onClick.RemoveAllListeners();
            switchProviderButton.onClick.AddListener(SwitchProvider);
        }

        if (clearHistoryButton != null)
        {
            clearHistoryButton.onClick.RemoveAllListeners();
            clearHistoryButton.onClick.AddListener(ClearHistory);
        }

        if (testConnectionButton != null)
        {
            testConnectionButton.onClick.RemoveAllListeners();
            testConnectionButton.onClick.AddListener(() => TestConnection());
        }
    }

    private void UpdateStatus()
    {
        if (statusText != null)
        {
            string status = GetStatusString();
            statusText.text = status;
        }

        if (modelText != null && openRouterCharacter != null)
        {
            modelText.text = $"Model: {openRouterCharacter.settings.model}";
        }

        if (historyCountText != null && openRouterCharacter != null)
        {
            historyCountText.text = $"History: {openRouterCharacter.GetHistoryCount()} messages";
        }
    }

    private string GetStatusString()
    {
        if (chatBridge == null)
            return "Status: No ChatBridge found";

        string providerName = chatBridge.provider.ToString();
        bool isProcessing = chatBridge.IsProcessing();
        
        string processingStatus = isProcessing ? "Processing..." : "Ready";
        
        return $"Provider: {providerName}\nStatus: {processingStatus}";
    }

    private void SwitchProvider()
    {
        if (chatBridge == null) return;

        // Toggle between providers
        if (chatBridge.provider == ChatBridge.ChatProvider.OpenRouter)
        {
            chatBridge.SwitchProvider(ChatBridge.ChatProvider.LLMUnity);
        }
        else
        {
            chatBridge.SwitchProvider(ChatBridge.ChatProvider.OpenRouter);
        }
        
        Debug.Log($"[ChatStatusDisplay] Switched to {chatBridge.provider}");
    }

    private void ClearHistory()
    {
        if (chatBridge != null)
        {
            chatBridge.ClearChatHistory();
            Debug.Log("[ChatStatusDisplay] Chat history cleared");
        }
    }

    private async void TestConnection()
    {
        if (openRouterCharacter == null)
        {
            Debug.LogWarning("[ChatStatusDisplay] No OpenRouterCharacter found for connection test");
            return;
        }

        Debug.Log("[ChatStatusDisplay] Testing OpenRouter connection...");
        
        try
        {
            string response = await openRouterCharacter.Chat("Hello! Please respond with 'Connection test successful' to confirm the connection is working.");
            
            if (!string.IsNullOrEmpty(response))
            {
                Debug.Log($"[ChatStatusDisplay] Connection test successful! Response: {response}");
            }
            else
            {
                Debug.LogWarning("[ChatStatusDisplay] Connection test failed - no response received");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChatStatusDisplay] Connection test failed: {e.Message}");
        }
    }

    // Manual refresh method that can be called from other scripts or buttons
    public void RefreshStatus()
    {
        UpdateStatus();
    }

    // Method to set custom status text
    public void SetCustomStatus(string customStatus)
    {
        if (statusText != null)
        {
            statusText.text = customStatus;
        }
    }

    void OnValidate()
    {
        // Auto-find text component if not set
        if (statusText == null)
            statusText = GetComponent<Text>();
    }
}