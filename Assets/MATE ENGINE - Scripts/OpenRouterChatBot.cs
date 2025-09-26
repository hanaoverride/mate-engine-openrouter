using UnityEngine;
using LLMUnity;
using LLMUnitySamples;
using System.Threading.Tasks;
using System.Collections;

/// <summary>
/// Extended ChatBot that adds OpenRouter support while preserving original UI and functionality
/// This inherits from the original ChatBot and adds provider switching capability
/// </summary>
public class OpenRouterChatBot : ChatBot
{
    [Header("OpenRouter Integration")]
    public bool useOpenRouter = false;
    public string openRouterApiKey = "";
    public string openRouterModel = "deepseek/deepseek-chat-v3.1";
    
    [Header("Provider Status")]
    public UnityEngine.UI.Text statusText;
    
    private OpenRouterCharacter openRouterCharacter;
    private bool isOpenRouterInitialized = false;

    protected override void Start()
    {
        // Initialize OpenRouter if enabled
        if (useOpenRouter)
        {
            InitializeOpenRouter();
        }
        
        // Call original Start method
        base.Start();
        
        UpdateStatusText();
    }

    private void InitializeOpenRouter()
    {
        Debug.Log("[OpenRouterChatBot] Initializing OpenRouter...");
        
        // Get or create OpenRouterCharacter component
        openRouterCharacter = GetComponent<OpenRouterCharacter>();
        if (openRouterCharacter == null)
        {
            openRouterCharacter = gameObject.AddComponent<OpenRouterCharacter>();
        }
        
        // Configure OpenRouterCharacter to use the same chat container
        openRouterCharacter.chatContainer = chatContainer;
        
        // Load settings
        LoadOpenRouterSettings();
        
        isOpenRouterInitialized = true;
        Debug.Log("[OpenRouterChatBot] OpenRouter initialized successfully");
    }

    private void LoadOpenRouterSettings()
    {
        // Load from PlayerPrefs
        openRouterApiKey = PlayerPrefs.GetString("OpenRouter_ApiKey", "");
        openRouterModel = PlayerPrefs.GetString("OpenRouter_Model", "openai/gpt-3.5-turbo");
        useOpenRouter = PlayerPrefs.GetInt("OpenRouter_Enabled", 0) == 1;
        
        // Apply to OpenRouterCharacter
        if (openRouterCharacter != null)
        {
            openRouterCharacter.SetApiKey(openRouterApiKey);
            openRouterCharacter.SetModel(openRouterModel);
        }
        
        Debug.Log($"[OpenRouterChatBot] Settings loaded - UseOpenRouter: {useOpenRouter}, Model: {openRouterModel}");
    }

    private void SaveOpenRouterSettings()
    {
        PlayerPrefs.SetString("OpenRouter_ApiKey", openRouterApiKey);
        PlayerPrefs.SetString("OpenRouter_Model", openRouterModel);
        PlayerPrefs.SetInt("OpenRouter_Enabled", useOpenRouter ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void UpdateStatusText()
    {
        if (statusText != null)
        {
            string provider = useOpenRouter ? $"OpenRouter ({openRouterModel})" : "ZomeAI (Local)";
            statusText.text = $"AI Provider: {provider}";
        }
    }

    // Override the onInputFieldSubmit method to add provider switching
    protected override void onInputFieldSubmit(string newText)
    {
        inputBubble.ActivateInputField();
        if (blockInput || newText.Trim() == "" || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            StartCoroutine(BlockInteraction());
            return;
        }

        // Check for provider switch commands
        string trimmedText = newText.Trim().ToLower();
        if (trimmedText.StartsWith("/provider"))
        {
            HandleProviderCommand(trimmedText);
            inputBubble.SetText("");
            return;
        }

        // Use appropriate provider
        if (useOpenRouter && isOpenRouterInitialized && !string.IsNullOrEmpty(openRouterApiKey))
        {
            HandleOpenRouterMessage(newText);
        }
        else
        {
            // Fall back to original ZomeAI implementation
            base.onInputFieldSubmit(newText);
        }
    }

    private void HandleProviderCommand(string command)
    {
        if (command == "/provider zomeai" || command == "/provider local")
        {
            useOpenRouter = false;
            AddBubble("Switched to ZomeAI (Local LLM)", false);
        }
        else if (command == "/provider openrouter")
        {
            if (string.IsNullOrEmpty(openRouterApiKey))
            {
                AddBubble("OpenRouter API key not set. Use: /apikey YOUR_KEY", false);
                return;
            }
            useOpenRouter = true;
            AddBubble($"Switched to OpenRouter ({openRouterModel})", false);
        }
        else if (command.StartsWith("/apikey "))
        {
            string newApiKey = command.Substring(8).Trim();
            if (!string.IsNullOrEmpty(newApiKey))
            {
                openRouterApiKey = newApiKey;
                if (openRouterCharacter != null)
                {
                    openRouterCharacter.SetApiKey(openRouterApiKey);
                }
                AddBubble("OpenRouter API key updated", false);
            }
            else
            {
                AddBubble("Please provide a valid API key", false);
            }
        }
        else if (command.StartsWith("/model "))
        {
            string newModel = command.Substring(7).Trim();
            if (!string.IsNullOrEmpty(newModel))
            {
                openRouterModel = newModel;
                if (openRouterCharacter != null)
                {
                    openRouterCharacter.SetModel(openRouterModel);
                }
                AddBubble($"OpenRouter model changed to: {openRouterModel}", false);
            }
            else
            {
                AddBubble("Please provide a valid model name", false);
            }
        }
        else
        {
            AddBubble("Available commands:\n/provider zomeai - Switch to local LLM\n/provider openrouter - Switch to OpenRouter\n/apikey YOUR_KEY - Set OpenRouter API key\n/model MODEL_NAME - Set OpenRouter model", false);
        }

        SaveOpenRouterSettings();
        UpdateStatusText();
    }

    private async void HandleOpenRouterMessage(string message)
    {
        if (openRouterCharacter == null)
        {
            AddBubble("OpenRouter not properly initialized", false);
            AllowInput();
            return;
        }

        blockInput = true;

        // Add user bubble using original method
        AddBubble(message, true);
        var aiBubble = AddBubble("...", false);

        // Start audio if available
        if (streamAudioSource != null)
            streamAudioSource.Play();

        try
        {
            // Send message to OpenRouter
            await openRouterCharacter.Chat(message,
                onPartialResponse: (partialText) => aiBubble?.SetText(partialText),
                onComplete: () => {
                    // Stop audio and allow input
                    if (streamAudioSource != null && streamAudioSource.isPlaying)
                        StartCoroutine(FadeOutStreamAudio());
                    AllowInput();
                }
            );
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[OpenRouterChatBot] Error with OpenRouter: {e.Message}");
            aiBubble?.SetText("Sorry, there was an error with OpenRouter. Please try again or switch to local LLM with /provider zomeai");
            
            if (streamAudioSource != null && streamAudioSource.isPlaying)
                StartCoroutine(FadeOutStreamAudio());
            AllowInput();
        }

        inputBubble.SetText("");
    }

    // Public method to switch providers (can be called from UI buttons)
    public void SwitchToZomeAI()
    {
        useOpenRouter = false;
        SaveOpenRouterSettings();
        UpdateStatusText();
        AddBubble("Switched to ZomeAI (Local LLM)", false);
    }

    public void SwitchToOpenRouter()
    {
        if (string.IsNullOrEmpty(openRouterApiKey))
        {
            AddBubble("OpenRouter API key not set. Please set it first.", false);
            return;
        }
        
        useOpenRouter = true;
        SaveOpenRouterSettings();
        UpdateStatusText();
        AddBubble($"Switched to OpenRouter ({openRouterModel})", false);
    }

    public void SetOpenRouterApiKey(string apiKey)
    {
        openRouterApiKey = apiKey;
        if (openRouterCharacter != null)
        {
            openRouterCharacter.SetApiKey(apiKey);
        }
        SaveOpenRouterSettings();
        UpdateStatusText();
    }

    public void SetOpenRouterModel(string model)
    {
        openRouterModel = model;
        if (openRouterCharacter != null)
        {
            openRouterCharacter.SetModel(model);
        }
        SaveOpenRouterSettings();
        UpdateStatusText();
    }

    // Clear chat history for both providers
    public new void ClearChatHistory()
    {
        if (useOpenRouter && openRouterCharacter != null)
        {
            openRouterCharacter.ClearHistory();
        }
        else
        {
            llmCharacter?.ClearChat();
        }

        // Clear visual bubbles (this is handled by base class)
        Debug.Log($"[OpenRouterChatBot] Chat history cleared for {(useOpenRouter ? "OpenRouter" : "ZomeAI")}");
    }

    // Context menu for easy testing
    [ContextMenu("Test Provider Switch")]
    public void TestProviderSwitch()
    {
        useOpenRouter = !useOpenRouter;
        UpdateStatusText();
        Debug.Log($"[OpenRouterChatBot] Provider switched to: {(useOpenRouter ? "OpenRouter" : "ZomeAI")}");
    }
}