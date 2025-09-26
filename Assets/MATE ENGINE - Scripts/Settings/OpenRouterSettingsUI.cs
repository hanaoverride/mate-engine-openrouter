using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class OpenRouterSettingsUI : MonoBehaviour
{
    [Header("UI References")]
    public InputField apiKeyField;
    public Dropdown modelDropdown;
    public Slider temperatureSlider;
    public Text temperatureValueText;
    public Slider maxTokensSlider;
    public Text maxTokensValueText;
    public Toggle streamingToggle;
    public Toggle debugToggle;
    public Button saveButton;
    public Button testConnectionButton;
    public Text statusText;
    
    [Header("System Prompt")]
    public InputField systemPromptField;
    public Button resetPromptButton;

    [Header("Available Models")]
    private readonly Dictionary<string, string> availableModels = new Dictionary<string, string>
    {
        // OpenAI Models
        {"gpt-4o", "OpenAI GPT-4o"},
        {"gpt-4o-mini", "OpenAI GPT-4o Mini"},
        {"gpt-4-turbo", "OpenAI GPT-4 Turbo"},
        {"gpt-3.5-turbo", "OpenAI GPT-3.5 Turbo"},
        
        // Anthropic Models
        {"anthropic/claude-3.5-sonnet", "Anthropic Claude 3.5 Sonnet"},
        {"anthropic/claude-3-haiku", "Anthropic Claude 3 Haiku"},
        {"anthropic/claude-3-opus", "Anthropic Claude 3 Opus"},
        
        // Google Models
        {"google/gemini-pro", "Google Gemini Pro"},
        {"google/gemini-pro-vision", "Google Gemini Pro Vision"},
        
        // Other Popular Models
        {"meta-llama/llama-3.2-90b-vision-instruct", "Meta Llama 3.2 90B Vision"},
        {"meta-llama/llama-3.1-405b-instruct", "Meta Llama 3.1 405B"},
        {"mistralai/mistral-large", "Mistral Large"},
        {"mistralai/codestral-mamba", "Codestral Mamba"},
        
        // Free Models
        {"microsoft/phi-3-mini-128k-instruct:free", "Phi-3 Mini (Free)"},
        {"meta-llama/llama-3.1-8b-instruct:free", "Llama 3.1 8B (Free)"},
        {"google/gemma-2-9b-it:free", "Gemma 2 9B (Free)"}
    };

    private OpenRouterCharacter openRouterCharacter;

    void Start()
    {
        InitializeUI();
        LoadSettings();
        SetupEventListeners();
    }

    private void InitializeUI()
    {
        // Find OpenRouter character in scene
        openRouterCharacter = FindFirstObjectByType<OpenRouterCharacter>();
        
        // Populate model dropdown
        PopulateModelDropdown();
        
        // Set initial status
        UpdateStatus("Settings loaded");
    }

    private void PopulateModelDropdown()
    {
        if (modelDropdown == null) return;

        modelDropdown.ClearOptions();
        List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
        
        foreach (var model in availableModels)
        {
            options.Add(new Dropdown.OptionData(model.Value));
        }
        
        modelDropdown.AddOptions(options);
    }

    private void SetupEventListeners()
    {
        if (temperatureSlider != null)
            temperatureSlider.onValueChanged.AddListener(OnTemperatureChanged);
            
        if (maxTokensSlider != null)
            maxTokensSlider.onValueChanged.AddListener(OnMaxTokensChanged);
            
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveSettings);
            
        if (testConnectionButton != null)
            testConnectionButton.onClick.AddListener(TestConnection);
            
        if (resetPromptButton != null)
            resetPromptButton.onClick.AddListener(ResetSystemPrompt);
    }

    private void LoadSettings()
    {
        // Load API Key
        if (apiKeyField != null)
        {
            string savedApiKey = PlayerPrefs.GetString("OpenRouter_API_Key", "");
            apiKeyField.text = string.IsNullOrEmpty(savedApiKey) ? "" : "••••••••••••••••"; // Masked display
            apiKeyField.contentType = InputField.ContentType.Password;
        }

        // Load Model Selection
        if (modelDropdown != null)
        {
            string savedModel = PlayerPrefs.GetString("OpenRouter_Model", "anthropic/claude-3.5-sonnet");
            int modelIndex = 0;
            var modelKeys = new List<string>(availableModels.Keys);
            for (int i = 0; i < modelKeys.Count; i++)
            {
                if (modelKeys[i] == savedModel)
                {
                    modelIndex = i;
                    break;
                }
            }
            modelDropdown.value = modelIndex;
        }

        // Load Temperature
        if (temperatureSlider != null)
        {
            float temperature = PlayerPrefs.GetFloat("OpenRouter_Temperature", 0.7f);
            temperatureSlider.value = temperature;
            OnTemperatureChanged(temperature);
        }

        // Load Max Tokens
        if (maxTokensSlider != null)
        {
            float maxTokens = PlayerPrefs.GetFloat("OpenRouter_MaxTokens", 1000f);
            maxTokensSlider.value = maxTokens;
            OnMaxTokensChanged(maxTokens);
        }

        // Load Streaming Toggle
        if (streamingToggle != null)
        {
            bool streaming = PlayerPrefs.GetInt("OpenRouter_Streaming", 1) == 1;
            streamingToggle.isOn = streaming;
        }

        // Load Debug Toggle
        if (debugToggle != null)
        {
            bool debug = PlayerPrefs.GetInt("OpenRouter_Debug", 0) == 1;
            debugToggle.isOn = debug;
        }

        // Load System Prompt
        LoadSystemPrompt();
    }

    private void LoadSystemPrompt()
    {
        if (systemPromptField != null && openRouterCharacter != null)
        {
            systemPromptField.text = openRouterCharacter.systemPrompt;
        }
    }

    private void OnTemperatureChanged(float value)
    {
        if (temperatureValueText != null)
        {
            temperatureValueText.text = value.ToString("F2");
        }
    }

    private void OnMaxTokensChanged(float value)
    {
        if (maxTokensValueText != null)
        {
            maxTokensValueText.text = ((int)value).ToString();
        }
    }

    public void SaveSettings()
    {
        try
        {
            // Save API Key (only if it's not the masked version)
            if (apiKeyField != null && !apiKeyField.text.Contains("••"))
            {
                PlayerPrefs.SetString("OpenRouter_API_Key", apiKeyField.text);
            }

            // Save Model
            if (modelDropdown != null)
            {
                var modelKeys = new List<string>(availableModels.Keys);
                if (modelDropdown.value < modelKeys.Count)
                {
                    PlayerPrefs.SetString("OpenRouter_Model", modelKeys[modelDropdown.value]);
                }
            }

            // Save Temperature
            if (temperatureSlider != null)
            {
                PlayerPrefs.SetFloat("OpenRouter_Temperature", temperatureSlider.value);
            }

            // Save Max Tokens
            if (maxTokensSlider != null)
            {
                PlayerPrefs.SetFloat("OpenRouter_MaxTokens", maxTokensSlider.value);
            }

            // Save Streaming
            if (streamingToggle != null)
            {
                PlayerPrefs.SetInt("OpenRouter_Streaming", streamingToggle.isOn ? 1 : 0);
            }

            // Save Debug
            if (debugToggle != null)
            {
                PlayerPrefs.SetInt("OpenRouter_Debug", debugToggle.isOn ? 1 : 0);
            }

            PlayerPrefs.Save();

            // Apply settings to OpenRouter character
            ApplySettingsToCharacter();

            // Save system prompt
            if (systemPromptField != null && openRouterCharacter != null)
            {
                openRouterCharacter.SetPrompt(systemPromptField.text);
            }

            UpdateStatus("Settings saved successfully!");
            
            Debug.Log("[OpenRouterSettings] All settings saved");
        }
        catch (System.Exception e)
        {
            UpdateStatus($"Error saving settings: {e.Message}");
            Debug.LogError($"[OpenRouterSettings] Save error: {e.Message}");
        }
    }

    private void ApplySettingsToCharacter()
    {
        if (openRouterCharacter == null) return;

        var modelKeys = new List<string>(availableModels.Keys);
        
        // Update settings
        if (apiKeyField != null && !apiKeyField.text.Contains("••"))
        {
            openRouterCharacter.settings.apiKey = apiKeyField.text;
        }
        
        if (modelDropdown != null && modelDropdown.value < modelKeys.Count)
        {
            openRouterCharacter.settings.model = modelKeys[modelDropdown.value];
        }
        
        if (temperatureSlider != null)
        {
            openRouterCharacter.settings.temperature = temperatureSlider.value;
        }
        
        if (maxTokensSlider != null)
        {
            openRouterCharacter.settings.maxTokens = (int)maxTokensSlider.value;
        }
        
        if (streamingToggle != null)
        {
            openRouterCharacter.settings.enableStreaming = streamingToggle.isOn;
        }
        
        if (debugToggle != null)
        {
            openRouterCharacter.settings.debugRequests = debugToggle.isOn;
        }
    }

    private async void TestConnection()
    {
        if (openRouterCharacter == null)
        {
            UpdateStatus("OpenRouter character not found!");
            return;
        }

        UpdateStatus("Testing connection...");
        testConnectionButton.interactable = false;

        try
        {
            // Apply current settings temporarily
            ApplySettingsToCharacter();

            // Send a simple test message
            string testResponse = await openRouterCharacter.Chat("Hello, please respond with 'Connection test successful!'");

            if (!string.IsNullOrEmpty(testResponse))
            {
                UpdateStatus("Connection test successful!");
            }
            else
            {
                UpdateStatus("Connection test failed - no response received");
            }
        }
        catch (System.Exception e)
        {
            UpdateStatus($"Connection test failed: {e.Message}");
        }
        finally
        {
            testConnectionButton.interactable = true;
        }
    }

    private void ResetSystemPrompt()
    {
        if (systemPromptField != null)
        {
            string defaultPrompt = "You are a helpful AI assistant for a desktop pet application. Be friendly, concise, and engaging.";
            systemPromptField.text = defaultPrompt;
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[OpenRouterSettings] {message}");
    }

    // Called when the API key field is focused (to clear the masked text)
    public void OnApiKeyFieldFocus()
    {
        if (apiKeyField != null && apiKeyField.text.Contains("••"))
        {
            apiKeyField.text = PlayerPrefs.GetString("OpenRouter_API_Key", "");
        }
    }

    // Called when the API key field loses focus (to mask the text)
    public void OnApiKeyFieldEndEdit()
    {
        if (apiKeyField != null && !string.IsNullOrEmpty(apiKeyField.text) && !apiKeyField.text.Contains("••"))
        {
            // Store the actual key
            string actualKey = apiKeyField.text;
            // Show masked version
            apiKeyField.text = "••••••••••••••••";
            // Store in PlayerPrefs
            PlayerPrefs.SetString("OpenRouter_API_Key", actualKey);
        }
    }
}