using UnityEngine;
using LLMUnitySamples;

/// <summary>
/// Helper script to quickly set up a ChatBridge system in the scene
/// This script can be added to any GameObject to automatically configure chat functionality
/// </summary>
public class ChatSystemSetup : MonoBehaviour
{
    [Header("Auto Setup Options")]
    public bool setupOnStart = true;
    public bool createChatContainer = true;
    public bool findExistingComponents = true;
    
    [Header("Chat Container Settings")]
    public Transform existingChatContainer;
    public string chatContainerName = "ChatContainer";
    
    [Header("Debug")]
    public bool debugMode = true;

    void Start()
    {
        if (setupOnStart)
        {
            SetupChatSystem();
        }
    }

    [ContextMenu("Setup Chat System")]
    public void SetupChatSystem()
    {
        if (debugMode)
            Debug.Log("[ChatSystemSetup] Starting chat system setup...");

        // Find or create chat container
        Transform chatContainer = SetupChatContainer();
        
        // Setup ChatBridge component
        ChatBridge chatBridge = SetupChatBridge(chatContainer);
        
        // Setup OpenRouter character
        OpenRouterCharacter openRouterChar = SetupOpenRouterCharacter();
        
        // Connect components
        ConnectComponents(chatBridge, openRouterChar, chatContainer);
        
        if (debugMode)
            Debug.Log("[ChatSystemSetup] Chat system setup completed!");
    }

    private Transform SetupChatContainer()
    {
        if (existingChatContainer != null)
        {
            if (debugMode)
                Debug.Log($"[ChatSystemSetup] Using existing chat container: {existingChatContainer.name}");
            return existingChatContainer;
        }

        if (createChatContainer)
        {
            // Look for existing chat container first
            GameObject existingContainer = GameObject.Find(chatContainerName);
            if (existingContainer != null)
            {
                if (debugMode)
                    Debug.Log($"[ChatSystemSetup] Found existing chat container: {existingContainer.name}");
                return existingContainer.transform;
            }

            // Create new chat container
            GameObject container = new GameObject(chatContainerName);
            
            // Add RectTransform for UI
            RectTransform rectTransform = container.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(800, 600);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            
            if (debugMode)
                Debug.Log($"[ChatSystemSetup] Created new chat container: {container.name}");
            
            return container.transform;
        }

        return null;
    }

    private ChatBridge SetupChatBridge(Transform chatContainer)
    {
        ChatBridge chatBridge = GetComponent<ChatBridge>();
        
        if (chatBridge == null)
        {
            chatBridge = gameObject.AddComponent<ChatBridge>();
            if (debugMode)
                Debug.Log("[ChatSystemSetup] Added ChatBridge component");
        }

        // Set chat container
        chatBridge.chatContainer = chatContainer;
        
        // Configure default settings
        chatBridge.provider = ChatBridge.ChatProvider.OpenRouter;

        return chatBridge;
    }

    private OpenRouterCharacter SetupOpenRouterCharacter()
    {
        OpenRouterCharacter openRouterChar = GetComponent<OpenRouterCharacter>();
        
        if (openRouterChar == null)
        {
            openRouterChar = gameObject.AddComponent<OpenRouterCharacter>();
            if (debugMode)
                Debug.Log("[ChatSystemSetup] Added OpenRouterCharacter component");
        }

        // Configure default settings
        openRouterChar.systemPrompt = "You are a helpful and friendly AI assistant for a desktop pet application called Mate Engine. Be conversational, engaging, and concise in your responses.";
        openRouterChar.playerName = "User";
        openRouterChar.aiName = "Assistant";
        openRouterChar.saveHistory = true;
        openRouterChar.saveFileName = "OpenRouter_Chat";

        return openRouterChar;
    }

    private void ConnectComponents(ChatBridge chatBridge, OpenRouterCharacter openRouterChar, Transform chatContainer)
    {
        // Connect ChatBridge to OpenRouterCharacter
        chatBridge.openRouterCharacter = openRouterChar;
        
        // Set chat container for OpenRouter character
        openRouterChar.chatContainer = chatContainer;
        
        if (debugMode)
            Debug.Log("[ChatSystemSetup] Components connected successfully");
    }

    [ContextMenu("Find Existing Chat Components")]
    public void FindExistingChatComponents()
    {
        Debug.Log("=== Existing Chat Components ===");
        
        // Find LLMCharacter components
        var llmCharacters = FindObjectsByType<LLMUnity.LLMCharacter>(FindObjectsSortMode.None);
        Debug.Log($"Found {llmCharacters.Length} LLMCharacter(s)");
        foreach (var llm in llmCharacters)
        {
            Debug.Log($"  - {llm.gameObject.name} (save: {llm.save})");
        }
        
        // Find existing chat containers
        var containers = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var container in containers)
        {
            if (container.name.ToLower().Contains("chat"))
            {
                Debug.Log($"Found potential chat container: {container.name}");
            }
        }
        
        // Find delete buttons
        var deleteButtons = FindObjectsByType<DeleteAIHistory>(FindObjectsSortMode.None);
        Debug.Log($"Found {deleteButtons.Length} DeleteAIHistory component(s)");
        
        Debug.Log("=== End Chat Components ===");
    }

    void OnValidate()
    {
        // Ensure we have valid settings
        if (string.IsNullOrEmpty(chatContainerName))
        {
            chatContainerName = "ChatContainer";
        }
    }
}