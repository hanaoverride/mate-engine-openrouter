using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using LLMUnity;
using System.Linq;

[System.Serializable]
public class OpenRouterSettings
{
    [Header("API Configuration")]
    public string apiKey = "";
    public string model = "deepseek/deepseek-chat-v3.1";
    
    [Header("Model Parameters")]
    [Range(0f, 2f)]
    public float temperature = 0.7f;
    [Range(1, 8192)]
    public int maxTokens = 1000;
    [Range(0f, 1f)]
    public float topP = 1.0f;
    [Range(0f, 2f)]
    public float frequencyPenalty = 0.0f;
    [Range(0f, 2f)]
    public float presencePenalty = 0.0f;
    
    [Header("Features")]
    public bool enableStreaming = true;
    public bool debugRequests = false;
}

public class OpenRouterCharacter : MonoBehaviour
{
    [Header("OpenRouter Configuration")]
    public OpenRouterSettings settings = new OpenRouterSettings();
    
    [Header("Chat Settings")]
    [TextArea(3, 8)]
    public string systemPrompt = "You are a helpful AI assistant for a desktop pet application. Be friendly, concise, and engaging.";
    public string playerName = "User";
    public string aiName = "Assistant";
    public bool saveHistory = true;
    public string saveFileName = "OpenRouter_Chat";
    
    [Header("UI References")]
    public Transform chatContainer;
    
    // UI feedback components
    [Header("Error Display")]
    public UnityEngine.UI.Text statusText; // For showing connection status
    public bool showErrorsInChat = true; // Show errors as chat messages
    
    private OpenRouterClient client;
    private List<OpenRouterMessage> chatHistory = new List<OpenRouterMessage>();
    private System.Threading.SemaphoreSlim chatLock = new System.Threading.SemaphoreSlim(1, 1);
    private bool isProcessing = false;

    void Start()
    {
        // Get or create OpenRouterClient
        client = gameObject.GetComponent<OpenRouterClient>();
        if (client == null)
        {
            client = gameObject.AddComponent<OpenRouterClient>();
        }
        
        // Load system prompt and chat history
        LoadSystemPrompt();
        LoadHistory();
        
        // Initialize with system prompt
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            // Check if system message already exists
            if (chatHistory.Count == 0 || chatHistory[0].role != "system")
            {
                chatHistory.Insert(0, new OpenRouterMessage
                {
                    role = "system",
                    content = systemPrompt
                });
            }
        }
    }

    public void AddMessage(string role, string content)
    {
        var message = new OpenRouterMessage
        {
            role = role == playerName ? "user" : "assistant",
            content = content
        };
        
        chatHistory.Add(message);
        
        // Limit history size to prevent token overflow
        if (chatHistory.Count > 50)
        {
            // Keep system message and remove oldest user/assistant messages
            var systemMessages = chatHistory.Where(m => m.role == "system").ToList();
            var otherMessages = chatHistory.Where(m => m.role != "system").Skip(10).ToList();
            
            chatHistory = new List<OpenRouterMessage>();
            chatHistory.AddRange(systemMessages);
            chatHistory.AddRange(otherMessages);
        }
    }

    private void LoadSystemPrompt()
    {
        try
        {
            string promptPath = Path.Combine(Application.persistentDataPath, "OpenRouter_prompt.txt");
            if (File.Exists(promptPath))
            {
                string savedPrompt = File.ReadAllText(promptPath);
                if (!string.IsNullOrEmpty(savedPrompt))
                {
                    systemPrompt = savedPrompt;
                    Debug.Log("[OpenRouterCharacter] System prompt loaded from file");
                }
            }
            else
            {
                // Create default prompt file
                File.WriteAllText(promptPath, systemPrompt);
                Debug.Log("[OpenRouterCharacter] Created new system prompt file");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[OpenRouterCharacter] Failed to load system prompt: {e.Message}");
        }
    }

    private void LoadHistory()
    {
        if (!saveHistory) return;
        
        try
        {
            string historyPath = Path.Combine(Application.persistentDataPath, $"{saveFileName}.json");
            if (File.Exists(historyPath))
            {
                string json = File.ReadAllText(historyPath);
                var wrapper = JsonUtility.FromJson<ChatHistoryWrapper>(json);
                if (wrapper?.messages != null)
                {
                    chatHistory = wrapper.messages;
                    Debug.Log($"[OpenRouterCharacter] Loaded {chatHistory.Count} messages from history");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[OpenRouterCharacter] Failed to load chat history: {e.Message}");
            chatHistory = new List<OpenRouterMessage>();
        }
    }

    private void SaveHistory()
    {
        if (!saveHistory) return;
        
        try
        {
            string historyPath = Path.Combine(Application.persistentDataPath, $"{saveFileName}.json");
            var wrapper = new ChatHistoryWrapper { messages = chatHistory };
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(historyPath, json);
            
            if (settings.debugRequests)
            {
                Debug.Log("[OpenRouterCharacter] Chat history saved");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[OpenRouterCharacter] Failed to save chat history: {e.Message}");
        }
    }

    public async Task<string> Chat(string userMessage, System.Action<string> onPartialResponse = null, System.Action onComplete = null)
    {
        if (isProcessing)
        {
            string waitMessage = "Please wait for the current request to complete.";
            ShowUserWarning(waitMessage);
            return waitMessage;
        }

        if (string.IsNullOrEmpty(userMessage.Trim()))
        {
            string emptyMessage = "Please enter a message.";
            ShowUserWarning(emptyMessage);
            return emptyMessage;
        }

        // Show processing status
        ShowUserInfo("Sending message...");

        await chatLock.WaitAsync();
        isProcessing = true;
        string response = null;

        try
        {
            // Add user message to history
            AddMessage(playerName, userMessage);
            
            // Prepare messages for API
            var apiMessages = PrepareMessagesForAPI();
            
            // Update client settings before request
            UpdateClientSettings();
            
            // Send request to OpenRouter with improved error handling
            response = await client.SendChatRequest(apiMessages, settings.model, onPartialResponse);
            
            // Check if response indicates an error
            if (!string.IsNullOrEmpty(response))
            {
                if (response.StartsWith("Error:"))
                {
                    // Remove the user message we just added since the request failed
                    if (chatHistory.Count > 0 && chatHistory[chatHistory.Count - 1].content == userMessage)
                    {
                        chatHistory.RemoveAt(chatHistory.Count - 1);
                    }
                    
                    // Show error to user
                    ShowUserError(response.Substring(6).Trim()); // Remove "Error:" prefix for display
                    return response;
                }
                else if (response.StartsWith("Rate limited"))
                {
                    // Remove the user message we just added since the request failed
                    if (chatHistory.Count > 0 && chatHistory[chatHistory.Count - 1].content == userMessage)
                    {
                        chatHistory.RemoveAt(chatHistory.Count - 1);
                    }
                    
                    // Show rate limit warning to user
                    ShowUserWarning("Rate limited. Please wait a moment before sending another message.");
                    return response;
                }
                else
                {
                    // Successful response
                    AddMessage(aiName, response);
                    SaveHistory();
                    
                    // Update UI with the response
                    onPartialResponse?.Invoke(response);
                    
                    // Show success status
                    ShowUserInfo("Message received");
                    
                    Debug.Log($"[OpenRouterCharacter] Response received: {response.Substring(0, System.Math.Min(50, response.Length))}...");
                }
            }
            else
            {
                // Empty response - treat as error
                response = "No response received from the AI. Please try again.";
                
                // Update UI with error message
                onPartialResponse?.Invoke(response);
                
                // Remove the user message since request failed
                if (chatHistory.Count > 0 && chatHistory[chatHistory.Count - 1].content == userMessage)
                {
                    chatHistory.RemoveAt(chatHistory.Count - 1);
                }
                
                // Show error to user
                ShowUserError("No response from AI server. Please check your connection and try again.");
            }
        }
        catch (System.Exception e)
        {
            // Remove the user message from history if an exception occurred
            if (chatHistory.Count > 0 && chatHistory[chatHistory.Count - 1].content == userMessage)
            {
                chatHistory.RemoveAt(chatHistory.Count - 1);
            }
            
            response = $"An error occurred: {e.Message}";
            
            // Update UI with error message
            onPartialResponse?.Invoke(response);
            
            // Show user-friendly error
            ShowUserError("Connection failed. Please check your internet connection and API settings.");
            
            Debug.LogError($"[OpenRouterCharacter] Chat request failed: {e.Message}");
        }
        finally
        {
            isProcessing = false;
            chatLock.Release();
            
            // Always call completion callback
            onComplete?.Invoke();
        }

        return response;
    }

    private List<OpenRouterMessage> PrepareMessagesForAPI()
    {
        var messages = new List<OpenRouterMessage>();
        
        // Add system message if present
        var systemMessage = chatHistory.FirstOrDefault(m => m.role == "system");
        if (systemMessage != null)
        {
            messages.Add(systemMessage);
        }
        
        // Add recent conversation history (excluding system messages)
        var recentMessages = chatHistory.Where(m => m.role != "system").TakeLast(20).ToList();
        messages.AddRange(recentMessages);
        
        return messages;
    }

    private void UpdateClientSettings()
    {
        if (client != null)
        {
            client.temperature = settings.temperature;
            client.maxTokens = settings.maxTokens;
            client.topP = settings.topP;
            client.frequencyPenalty = settings.frequencyPenalty;
            client.presencePenalty = settings.presencePenalty;
            client.enableStreaming = settings.enableStreaming;
            client.debugRequests = settings.debugRequests;
            
            // Set API key if provided
            if (!string.IsNullOrEmpty(settings.apiKey))
            {
                client.SetApiKey(settings.apiKey);
            }
        }
    }

    public void ClearHistory()
    {
        chatHistory.Clear();
        
        // Re-add system prompt
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            chatHistory.Add(new OpenRouterMessage
            {
                role = "system",
                content = systemPrompt
            });
        }
        
        SaveHistory();
        Debug.Log("[OpenRouterCharacter] Chat history cleared");
    }

    public void CancelRequests()
    {
        if (client != null)
        {
            client.CancelAllRequests();
        }
        
        isProcessing = false;
        chatLock.Release();
        Debug.Log("[OpenRouterCharacter] All requests cancelled");
    }

    public int GetHistoryCount()
    {
        return chatHistory.Count;
    }

    public bool IsProcessing()
    {
        return isProcessing;
    }

    // UI Feedback Methods
    private void ShowUserError(string errorMessage)
    {
        // Update status text if available
        UpdateStatusText($"Error: {errorMessage}");
        
        // Show error in chat if enabled
        if (showErrorsInChat)
        {
            ShowErrorInChat(errorMessage);
        }
        
        // Still log for developers
        Debug.LogError($"[OpenRouterCharacter] User Error: {errorMessage}");
    }

    private void ShowUserWarning(string warningMessage)
    {
        // Update status text if available
        UpdateStatusText($"Warning: {warningMessage}");
        
        // Show warning in chat if enabled
        if (showErrorsInChat)
        {
            ShowWarningInChat(warningMessage);
        }
        
        // Still log for developers
        Debug.LogWarning($"[OpenRouterCharacter] User Warning: {warningMessage}");
    }

    private void ShowUserInfo(string infoMessage)
    {
        // Update status text if available
        UpdateStatusText(infoMessage);
        
        // Still log for developers
        Debug.Log($"[OpenRouterCharacter] User Info: {infoMessage}");
    }

    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            // Clear status after 5 seconds for non-error messages
            if (!message.StartsWith("Error:"))
            {
                StartCoroutine(ClearStatusAfterDelay(5f));
            }
        }
    }

    private System.Collections.IEnumerator ClearStatusAfterDelay(float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        if (statusText != null && !statusText.text.StartsWith("Error:"))
        {
            statusText.text = "Ready";
        }
    }

    private void ShowErrorInChat(string errorMessage)
    {
        if (chatContainer != null)
        {
            try
            {
                // Create a system error message that appears in chat
                var errorBubble = CreateErrorBubble(errorMessage, true); // true for error (red)
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[OpenRouterCharacter] Failed to show error in chat: {e.Message}");
            }
        }
    }

    private void ShowWarningInChat(string warningMessage)
    {
        if (chatContainer != null)
        {
            try
            {
                // Create a system warning message that appears in chat
                var warningBubble = CreateErrorBubble(warningMessage, false); // false for warning (yellow)
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[OpenRouterCharacter] Failed to show warning in chat: {e.Message}");
            }
        }
    }

    private GameObject CreateErrorBubble(string message, bool isError)
    {
        // This is a simplified version - you might want to use the same Bubble system as the main chat
        GameObject errorMessage = new GameObject($"ErrorMessage_{System.DateTime.Now.Ticks}");
        errorMessage.transform.SetParent(chatContainer, false);
        
        // Add UI Text component
        var textComponent = errorMessage.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = isError ? $"❌ {message}" : $"⚠️ {message}";
        textComponent.color = isError ? Color.red : new Color(1f, 0.8f, 0f); // Red for error, yellow for warning
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = 12;
        textComponent.alignment = TextAnchor.MiddleCenter;
        
        // Add RectTransform for positioning
        var rectTransform = errorMessage.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.offsetMin = new Vector2(10, -30);
        rectTransform.offsetMax = new Vector2(-10, -10);
        
        // Auto-remove error message after 10 seconds
        StartCoroutine(RemoveErrorMessageAfterDelay(errorMessage, 10f));
        
        return errorMessage;
    }

    private System.Collections.IEnumerator RemoveErrorMessageAfterDelay(GameObject errorMessage, float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        if (errorMessage != null)
        {
            DestroyImmediate(errorMessage);
        }
    }

    // For compatibility with existing UI code
    public void SetPrompt(string newPrompt)
    {
        systemPrompt = newPrompt;
        
        // Update system message in history
        if (chatHistory.Count > 0 && chatHistory[0].role == "system")
        {
            chatHistory[0].content = newPrompt;
        }
        else
        {
            chatHistory.Insert(0, new OpenRouterMessage 
            { 
                role = "system", 
                content = newPrompt 
            });
        }

        // Save to file
        string promptPath = Path.Combine(Application.persistentDataPath, "OpenRouter_prompt.txt");
        File.WriteAllText(promptPath, newPrompt);
        
        Debug.Log("[OpenRouterCharacter] System prompt updated");
    }

    public void SetApiKey(string newApiKey)
    {
        settings.apiKey = newApiKey;
        if (client != null)
        {
            client.SetApiKey(newApiKey);
        }
        
        // Provide user feedback
        if (string.IsNullOrEmpty(newApiKey))
        {
            ShowUserWarning("API key cleared. OpenRouter will not work without a valid API key.");
        }
        else
        {
            ShowUserInfo("API key updated successfully");
        }
        
        Debug.Log("[OpenRouterCharacter] API key updated");
    }

    public void SetModel(string newModel)
    {
        string oldModel = settings.model;
        settings.model = newModel;
        
        // Provide user feedback
        if (string.IsNullOrEmpty(newModel))
        {
            ShowUserWarning("Model name is empty. Using default model.");
            settings.model = "deepseek/deepseek-chat-v3.1"; // Fallback to default
        }
        else
        {
            ShowUserInfo($"Model changed to: {newModel}");
        }
        
        Debug.Log($"[OpenRouterCharacter] Model changed from {oldModel} to: {settings.model}");
    }

    void OnDestroy()
    {
        CancelRequests();
        SaveHistory();
    }
}

[System.Serializable]
public class ChatHistoryWrapper
{
    public List<OpenRouterMessage> messages;
}