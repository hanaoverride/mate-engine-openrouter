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
    
    private OpenRouterClient client;
    private List<OpenRouterMessage> chatHistory = new List<OpenRouterMessage>();
    private System.Threading.SemaphoreSlim chatLock = new System.Threading.SemaphoreSlim(1, 1);
    private bool isProcessing = false;

    void Awake()
    {
        // Initialize OpenRouter client
        client = gameObject.GetComponent<OpenRouterClient>();
        if (client == null)
        {
            client = gameObject.AddComponent<OpenRouterClient>();
        }
        
        // Setup client from settings
        UpdateClientSettings();
        
        // Initialize chat history
        InitializeHistory();
    }

    void Start()
    {
        // Load system prompt from file (similar to LLMCharacter)
        LoadSystemPromptFromFile();
        
        // Apply system prompt to history
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            chatHistory.Insert(0, new OpenRouterMessage 
            { 
                role = "system", 
                content = systemPrompt 
            });
        }
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
            
            if (!string.IsNullOrEmpty(settings.apiKey))
            {
                client.SetApiKey(settings.apiKey);
            }
        }
    }

    private void LoadSystemPromptFromFile()
    {
        string promptPath = Path.Combine(Application.persistentDataPath, "OpenRouter_prompt.txt");
        
        if (File.Exists(promptPath))
        {
            string loadedPrompt = File.ReadAllText(promptPath);
            if (!string.IsNullOrEmpty(loadedPrompt))
            {
                systemPrompt = loadedPrompt;
                Debug.Log("[OpenRouterCharacter] System prompt loaded from file");
            }
        }
        else
        {
            // Create the file with default prompt
            File.WriteAllText(promptPath, systemPrompt);
            Debug.Log("[OpenRouterCharacter] Created new system prompt file");
        }
    }

    private void InitializeHistory()
    {
        if (!saveHistory) return;
        
        string historyPath = Path.Combine(Application.persistentDataPath, $"{saveFileName}.json");
        
        try
        {
            if (File.Exists(historyPath))
            {
                string json = File.ReadAllText(historyPath);
                var wrapper = JsonUtility.FromJson<ChatHistoryWrapper>(json);
                if (wrapper != null && wrapper.messages != null)
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
            Debug.LogWarning("[OpenRouterCharacter] Already processing a request. Please wait...");
            return null;
        }

        if (string.IsNullOrEmpty(userMessage.Trim()))
        {
            return null;
        }

        await chatLock.WaitAsync();
        isProcessing = true;
        string response = null;

        try
        {
            // Add user message to history
            AddMessage(playerName, userMessage);
            
            // Prepare messages for API (excluding system message for token efficiency)
            var apiMessages = PrepareMessagesForAPI();
            
            // Update client settings before request
            UpdateClientSettings();
            
            // Send request to OpenRouter
            if (settings.enableStreaming && onPartialResponse != null)
            {
                response = await client.SendChatRequest(apiMessages, settings.model, onPartialResponse);
            }
            else
            {
                response = await client.SendChatRequest(apiMessages, settings.model);
            }

            if (!string.IsNullOrEmpty(response))
            {
                // Add AI response to history
                AddMessage(aiName, response);
                
                // Save history
                SaveHistory();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[OpenRouterCharacter] Chat error: {e.Message}");
        }
        finally
        {
            isProcessing = false;
            chatLock.Release();
            onComplete?.Invoke();
        }

        return response;
    }

    private List<OpenRouterMessage> PrepareMessagesForAPI()
    {
        var messages = new List<OpenRouterMessage>();
        
        // Always include system message first
        if (chatHistory.Count > 0 && chatHistory[0].role == "system")
        {
            messages.Add(chatHistory[0]);
        }
        else if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new OpenRouterMessage { role = "system", content = systemPrompt });
        }

        // Add recent conversation history (limit to prevent token overflow)
        var recentMessages = chatHistory.Where(m => m.role != "system").TakeLast(20).ToList();
        messages.AddRange(recentMessages);

        return messages;
    }

    public void AddMessage(string role, string content)
    {
        if (string.IsNullOrEmpty(content)) return;
        
        var message = new OpenRouterMessage
        {
            role = role.ToLower() == playerName.ToLower() ? "user" : "assistant",
            content = content
        };
        
        chatHistory.Add(message);
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
    }

    public bool IsProcessing()
    {
        return isProcessing;
    }

    public int GetHistoryCount()
    {
        return chatHistory.Count;
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
            UpdateClientSettings();
        }
        Debug.Log("[OpenRouterCharacter] API key updated");
    }

    public void SetModel(string newModel)
    {
        settings.model = newModel;
        if (client != null)
        {
            UpdateClientSettings();
        }
        Debug.Log($"[OpenRouterCharacter] Model changed to: {newModel}");
    }

    void OnValidate()
    {
        // Update client settings when inspector values change
        if (Application.isPlaying && client != null)
        {
            UpdateClientSettings();
        }
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