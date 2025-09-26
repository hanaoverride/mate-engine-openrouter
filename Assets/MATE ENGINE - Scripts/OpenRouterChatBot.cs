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
    public bool useOpenRouter = true;  // Default to true for easier testing
    public string openRouterApiKey = "";
    public string openRouterModel = "deepseek/deepseek-chat-v3.1";
    
    [Header("Provider Status")]
    public UnityEngine.UI.Text statusText;
    
    private OpenRouterCharacter openRouterCharacter;
    private bool isOpenRouterInitialized = false;

    protected override void Start()
    {
        // Load settings first and override Inspector values
        LoadOpenRouterSettings();
        
        Debug.Log($"[OpenRouterChatBot] After loading settings: useOpenRouter = {useOpenRouter}");
        
        // Initialize OpenRouter if enabled
        if (useOpenRouter)
        {
            InitializeOpenRouter();
        }
        
        // Initialize UI components (from original ChatBot.Start)
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        
        playerUI = new BubbleUI
        {
            sprite = sprite,
            font = font,
            fontSize = fontSize,
            fontColor = playerFontColor,
            bubbleColor = playerColor,
            bottomPosition = 0,
            leftPosition = 0,
            textPadding = textPadding,
            bubbleOffset = bubbleSpacing,
            bubbleWidth = bubbleWidth,
            bubbleHeight = -1
        };

        aiUI = new BubbleUI
        {
            sprite = sprite,
            font = font,
            fontSize = fontSize,
            fontColor = aiFontColor,
            bubbleColor = aiColor,
            bottomPosition = 0,
            leftPosition = 1,
            textPadding = textPadding,
            bubbleOffset = bubbleSpacing,
            bubbleWidth = bubbleWidth,
            bubbleHeight = -1
        };

        inputBubble = new InputBubble(chatContainer, playerUI, "InputBubble", "Loading...", 4);
        inputBubble.AddSubmitListener(onInputFieldSubmit);
        inputBubble.AddValueChangedListener(onValueChanged);
        inputBubble.setInteractable(false);

        // Choose rounded sprite based on radius
        if (cornerRadius <= 16)
            sprite = roundedSprite16;
        else if (cornerRadius <= 32)
            sprite = roundedSprite32;
        else
            sprite = roundedSprite64;

        playerUI.sprite = sprite;
        aiUI.sprite = sprite;

        // Conditional initialization based on provider
        if (useOpenRouter && isOpenRouterInitialized)
        {
            // Skip LLM warmup, go directly to ready state
            WarmUpCallback();
        }
        else
        {
            // Use original LLM warmup
            ShowLoadedMessages();
            if (llmCharacter != null)
            {
                _ = llmCharacter.Warmup(WarmUpCallback);
            }
            else
            {
                Debug.LogWarning("[OpenRouterChatBot] LLMCharacter not assigned, going to ready state");
                WarmUpCallback();
            }
        }
        
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
        
        // Apply loaded settings
        if (openRouterCharacter != null)
        {
            openRouterCharacter.SetApiKey(openRouterApiKey);
            openRouterCharacter.SetModel(openRouterModel);
        }
        
        isOpenRouterInitialized = true;
        Debug.Log("[OpenRouterChatBot] OpenRouter initialized successfully");
    }

    private void LoadOpenRouterSettings()
    {
        // Log current Inspector values first
        Debug.Log($"[OpenRouterChatBot] Before loading - Inspector useOpenRouter: {useOpenRouter}");

        bool loadedFromFile = false;
        var saveData = SaveLoadHandler.Instance?.data;
        if (saveData != null)
        {
            useOpenRouter = saveData.openRouterEnabled;
            openRouterApiKey = saveData.openRouterApiKey ?? string.Empty;
            string modelFromFile = string.IsNullOrWhiteSpace(saveData.openRouterModel) ? null : saveData.openRouterModel;
            openRouterModel = modelFromFile ?? openRouterModel;

            // Keep legacy PlayerPrefs in sync for other components that still rely on them
            PlayerPrefs.SetString("OpenRouter_API_Key", openRouterApiKey);
            PlayerPrefs.SetString("OpenRouter_Model", openRouterModel);
            PlayerPrefs.SetInt("OpenRouter_Enabled", useOpenRouter ? 1 : 0);
            PlayerPrefs.SetFloat("OpenRouter_Temperature", saveData.openRouterTemperature);
            PlayerPrefs.SetFloat("OpenRouter_MaxTokens", saveData.openRouterMaxTokens);
            PlayerPrefs.SetInt("OpenRouter_Streaming", saveData.openRouterStreaming ? 1 : 0);
            PlayerPrefs.SetInt("OpenRouter_Debug", saveData.openRouterDebug ? 1 : 0);
            PlayerPrefs.Save();

            loadedFromFile = true;
        }

        if (!loadedFromFile)
        {
            // Load from PlayerPrefs, defaulting to legacy values if file isn't available
            openRouterApiKey = PlayerPrefs.GetString("OpenRouter_API_Key", "");
            openRouterModel = PlayerPrefs.GetString("OpenRouter_Model", "deepseek/deepseek-chat-v3.1");
            int enabledValue = PlayerPrefs.GetInt("OpenRouter_Enabled", 1);
            useOpenRouter = enabledValue == 1;
            Debug.Log($"[OpenRouterChatBot] Loaded OpenRouter settings from PlayerPrefs");
        }

        Debug.Log($"[OpenRouterChatBot] Settings loaded - UseOpenRouter: {useOpenRouter}, Model: {openRouterModel}, ApiKey: {(string.IsNullOrEmpty(openRouterApiKey) ? "EMPTY" : "SET")}");
    }

    private void SaveOpenRouterSettings()
    {
        PlayerPrefs.SetString("OpenRouter_API_Key", openRouterApiKey);
        PlayerPrefs.SetString("OpenRouter_Model", openRouterModel);
        PlayerPrefs.SetInt("OpenRouter_Enabled", useOpenRouter ? 1 : 0);
        PlayerPrefs.Save();

        if (SaveLoadHandler.Instance != null)
        {
            var data = SaveLoadHandler.Instance.data;
            data.openRouterEnabled = useOpenRouter;
            data.openRouterApiKey = openRouterApiKey;
            data.openRouterModel = openRouterModel;
            SaveLoadHandler.Instance.SaveToDisk();
        }
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

    // Public method to enable/disable OpenRouter (called from UI)
    public void SetOpenRouterEnabled(bool enabled)
    {
        useOpenRouter = enabled;
        SaveOpenRouterSettings();
        UpdateStatusText();
        
        if (enabled && !isOpenRouterInitialized)
        {
            InitializeOpenRouter();
        }
        
        Debug.Log($"[OpenRouterChatBot] OpenRouter {(enabled ? "enabled" : "disabled")}");
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
    public void ClearChatHistory()
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

    [ContextMenu("Reset PlayerPrefs")]
    public void ResetPlayerPrefs()
    {
        PlayerPrefs.DeleteKey("OpenRouter_API_Key");
        PlayerPrefs.DeleteKey("OpenRouter_Model");
        PlayerPrefs.DeleteKey("OpenRouter_Enabled");
        PlayerPrefs.Save();
        Debug.Log("[OpenRouterChatBot] PlayerPrefs reset! Reload the scene to apply default values.");
    }

    [ContextMenu("Log Current Settings")]
    public void LogCurrentSettings()
    {
        Debug.Log($"[OpenRouterChatBot] Current useOpenRouter: {useOpenRouter}");
        Debug.Log($"[OpenRouterChatBot] PlayerPrefs 'OpenRouter_Enabled': {PlayerPrefs.GetInt("OpenRouter_Enabled", -999)}");
        Debug.Log($"[OpenRouterChatBot] PlayerPrefs 'OpenRouter_API_Key': {PlayerPrefs.GetString("OpenRouter_API_Key", "NOT_SET")}");
    }
}