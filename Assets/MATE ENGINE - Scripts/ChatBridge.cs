using UnityEngine;
using UnityEngine.UI;
using LLMUnity;
using LLMUnitySamples;
using System.Threading.Tasks;

/// <summary>
/// Bridge component that allows switching between LLMUnity (ZomeAI) and OpenRouter for chat functionality.
/// This component should replace direct LLMCharacter usage in the scene.
/// </summary>
public class ChatBridge : MonoBehaviour
{
    [Header("Chat Provider Selection")]
    public ChatProvider provider = ChatProvider.OpenRouter;
    
    [Header("Legacy LLMCharacter (ZomeAI)")]
    public LLMCharacter llmCharacter;
    
    [Header("OpenRouter Character")]
    public OpenRouterCharacter openRouterCharacter;
    
    [Header("UI References")]
    public Transform chatContainer;
    public InputField inputField;
    public Text statusText;
    
    [Header("Chat Bubble Settings")]
    public Sprite bubbleSprite;
    public Color playerBubbleColor = new Color32(81, 164, 81, 255);
    public Color aiBubbleColor = new Color32(29, 29, 73, 255);
    public Color fontColor = Color.white;
    public Font font;
    public int fontSize = 16;
    public int bubbleWidth = 600;
    public float textPadding = 10f;
    public float bubbleSpacing = 10f;
    
    private bool isProcessing = false;
    private Bubble currentAiBubble;

    public enum ChatProvider
    {
        LLMUnity,
        OpenRouter
    }

    void Start()
    {
        InitializeChatSystem();
        SetupInputField();
    }

    private void InitializeChatSystem()
    {
        // Ensure we have the required components
        if (provider == ChatProvider.OpenRouter && openRouterCharacter == null)
        {
            openRouterCharacter = GetComponent<OpenRouterCharacter>();
            if (openRouterCharacter == null)
            {
                Debug.LogWarning("[ChatBridge] OpenRouter provider selected but OpenRouterCharacter not found. Adding component...");
                openRouterCharacter = gameObject.AddComponent<OpenRouterCharacter>();
            }
        }
        
        if (provider == ChatProvider.LLMUnity && llmCharacter == null)
        {
            llmCharacter = GetComponent<LLMCharacter>();
            if (llmCharacter == null)
            {
                Debug.LogWarning("[ChatBridge] LLMUnity provider selected but LLMCharacter not found.");
            }
        }

        // Set chat container for OpenRouter
        if (openRouterCharacter != null && chatContainer != null)
        {
            openRouterCharacter.chatContainer = chatContainer;
        }

        Debug.Log($"[ChatBridge] Initialized with provider: {provider}");
    }

    private void SetupInputField()
    {
        if (inputField != null)
        {
            inputField.onSubmit.RemoveAllListeners();
            inputField.onSubmit.AddListener(OnInputSubmit);
            inputField.Select();
        }
    }

    private async void OnInputSubmit(string message)
    {
        if (isProcessing || string.IsNullOrEmpty(message.Trim()))
        {
            inputField.text = "";
            inputField.Select();
            return;
        }

        isProcessing = true;
        inputField.interactable = false;
        
        // Add user bubble
        AddUserBubble(message.Trim());
        
        // Add AI bubble with loading text
        currentAiBubble = AddAiBubble("...");
        
        // Update status
        UpdateStatus("Thinking...");

        try
        {
            string response = await SendChatMessage(message.Trim());
            
            if (string.IsNullOrEmpty(response))
            {
                currentAiBubble?.SetText("Sorry, I couldn't process your message. Please try again.");
                UpdateStatus("Error occurred");
            }
            else
            {
                UpdateStatus("Response completed");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChatBridge] Chat error: {e.Message}");
            currentAiBubble?.SetText("An error occurred while processing your message.");
            UpdateStatus("Error occurred");
        }
        finally
        {
            isProcessing = false;
            inputField.interactable = true;
            inputField.text = "";
            inputField.Select();
            currentAiBubble = null;
        }
    }

    private async Task<string> SendChatMessage(string message)
    {
        switch (provider)
        {
            case ChatProvider.OpenRouter:
                return await SendOpenRouterMessage(message);
                
            case ChatProvider.LLMUnity:
                return await SendLLMUnityMessage(message);
                
            default:
                Debug.LogError($"[ChatBridge] Unknown provider: {provider}");
                return null;
        }
    }

    private async Task<string> SendOpenRouterMessage(string message)
    {
        if (openRouterCharacter == null)
        {
            Debug.LogError("[ChatBridge] OpenRouterCharacter is null!");
            return null;
        }

        return await openRouterCharacter.Chat(
            message,
            onPartialResponse: (partialText) =>
            {
                // Update bubble with streaming text
                currentAiBubble?.SetText(partialText);
            },
            onComplete: () =>
            {
                UpdateStatus("Ready");
            }
        );
    }

    private async Task<string> SendLLMUnityMessage(string message)
    {
        if (llmCharacter == null)
        {
            Debug.LogError("[ChatBridge] LLMCharacter is null!");
            return null;
        }

        return await llmCharacter.Chat(
            message,
            callback: (partialText) =>
            {
                // Update bubble with streaming text
                currentAiBubble?.SetText(partialText);
            },
            completionCallback: () =>
            {
                UpdateStatus("Ready");
            }
        );
    }

    private Bubble AddUserBubble(string message)
    {
        if (chatContainer == null) return null;

        var ui = CreateBubbleUI(true);
        var bubble = new Bubble(chatContainer, ui, "PlayerBubble", message);
        return bubble;
    }

    private Bubble AddAiBubble(string message)
    {
        if (chatContainer == null) return null;

        var ui = CreateBubbleUI(false);
        var bubble = new Bubble(chatContainer, ui, "AIBubble", message);
        return bubble;
    }

    private BubbleUI CreateBubbleUI(bool isPlayer)
    {
        return new BubbleUI
        {
            sprite = bubbleSprite,
            font = font,
            fontSize = fontSize,
            fontColor = fontColor,
            bubbleColor = isPlayer ? playerBubbleColor : aiBubbleColor,
            bottomPosition = 0,
            leftPosition = isPlayer ? 1 : 0,
            textPadding = textPadding,
            bubbleOffset = bubbleSpacing,
            bubbleWidth = bubbleWidth,
            bubbleHeight = -1
        };
    }

    private void UpdateStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = $"Chat: {status}";
        }
        
        Debug.Log($"[ChatBridge] Status: {status}");
    }

    public void SwitchProvider(ChatProvider newProvider)
    {
        if (isProcessing)
        {
            Debug.LogWarning("[ChatBridge] Cannot switch provider while processing a message");
            return;
        }

        provider = newProvider;
        InitializeChatSystem();
        UpdateStatus($"Switched to {provider}");
    }

    public void CancelCurrentRequest()
    {
        switch (provider)
        {
            case ChatProvider.OpenRouter:
                openRouterCharacter?.CancelRequests();
                break;
                
            case ChatProvider.LLMUnity:
                llmCharacter?.CancelRequests();
                break;
        }

        isProcessing = false;
        inputField.interactable = true;
        inputField.Select();
        currentAiBubble = null;
        UpdateStatus("Cancelled");
    }

    public void ClearChatHistory()
    {
        switch (provider)
        {
            case ChatProvider.OpenRouter:
                openRouterCharacter?.ClearHistory();
                break;
                
            case ChatProvider.LLMUnity:
                // LLMCharacter doesn't have a direct clear method, but we can trigger the delete component
                var deleteComponent = FindFirstObjectByType<DeleteAIHistory>();
                deleteComponent?.DeleteHistoryFiles();
                break;
        }

        UpdateStatus("History cleared");
    }

    // Public methods for external scripts (like AvatarBigScreenTimer)
    public async Task<string> Chat(string message)
    {
        return await SendChatMessage(message);
    }

    public bool IsProcessing()
    {
        switch (provider)
        {
            case ChatProvider.OpenRouter:
                return openRouterCharacter?.IsProcessing() ?? false;
                
            case ChatProvider.LLMUnity:
                return isProcessing; // We track this manually for LLMUnity
                
            default:
                return false;
        }
    }

    void OnValidate()
    {
        // Auto-setup components in the inspector
        if (Application.isPlaying)
        {
            InitializeChatSystem();
        }
    }
}