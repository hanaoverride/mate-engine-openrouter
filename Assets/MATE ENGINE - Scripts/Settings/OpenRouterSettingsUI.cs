using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

public class OpenRouterSettingsUI : MonoBehaviour
{
    [Header("UI References")]
    public Toggle enableOpenRouterToggle;
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
    public Button openSettingsFileButton;
    public Text statusText;
    
    [Header("System Prompt")]
    public InputField systemPromptField;
    public Button resetPromptButton;

    [Header("Available Models")]
    private readonly Dictionary<string, string> availableModels = new Dictionary<string, string>
    {
        // OpenAI Models
        {"openai/gpt-5", "OpenAI GPT-5"},
        {"openai/gpt-5-mini", "OpenAI GPT-5 Mini"},
        {"openai/gpt-5-chat-latest", "OpenAI GPT-5 Chat Latest"},
        
        // Anthropic Models
        {"anthropic/claude-4.1-sonnet", "Anthropic Claude 4.1 Sonnet"},
        
        // Google Models
        {"google/gemini-2.5-flash", "Google: Gemini 2.5 Flash"},
        {"google/gemini-2.5-pro", "Google: Gemini 2.5 Pro"},
        {"google/gemma-3-27b-it", "Google: Gemma 3 27B"},
        
        // xAI Models
        {"x-ai/grok-4-fast", "xAI: Grok 4 Fast"},
        
        // Free Models
        {"deepseek/deepseek-chat-v3.1:free", "DeepSeek: DeepSeek V3.1 (free)"},
        {"x-ai/grok-4-fast:free", "xAI: Grok 4 Fast (free)"},
    };

    private OpenRouterCharacter openRouterCharacter;
    private OpenRouterChatBot openRouterChatBot;

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
        
        // Find OpenRouter chat bot in scene
        openRouterChatBot = FindFirstObjectByType<OpenRouterChatBot>();
        
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

        if (openSettingsFileButton != null)
            openSettingsFileButton.onClick.AddListener(OpenSettingsFile);
            
        if (resetPromptButton != null)
            resetPromptButton.onClick.AddListener(ResetSystemPrompt);
    }

    private void LoadSettings()
    {
        var saveData = SaveLoadHandler.Instance?.data;

        // Load OpenRouter Enable Toggle
        if (enableOpenRouterToggle != null)
        {
            // Default to enabled (1) if no setting exists yet
            bool enabled = saveData?.openRouterEnabled ?? (PlayerPrefs.GetInt("OpenRouter_Enabled", 1) == 1);
            enableOpenRouterToggle.isOn = enabled;
            Debug.Log($"[OpenRouterSettingsUI] Loaded OpenRouter_Enabled: {enabled} (raw value: {PlayerPrefs.GetInt("OpenRouter_Enabled", -999)})");
        }

        // Load API Key
        if (apiKeyField != null)
        {
            string savedApiKey = saveData?.openRouterApiKey ?? PlayerPrefs.GetString("OpenRouter_API_Key", "");
            apiKeyField.text = string.IsNullOrEmpty(savedApiKey) ? "" : "••••••••••••••••"; // Masked display
            apiKeyField.contentType = InputField.ContentType.Password;
        }

        // Load Model Selection
        if (modelDropdown != null)
        {
            string savedModel = saveData != null && !string.IsNullOrEmpty(saveData.openRouterModel)
                ? saveData.openRouterModel
                : PlayerPrefs.GetString("OpenRouter_Model", "deepseek/deepseek-chat-v3.1");
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
            float temperature = saveData?.openRouterTemperature ?? PlayerPrefs.GetFloat("OpenRouter_Temperature", 0.7f);
            temperatureSlider.value = temperature;
            OnTemperatureChanged(temperature);
        }

        // Load Max Tokens
        if (maxTokensSlider != null)
        {
            float maxTokens = saveData?.openRouterMaxTokens ?? PlayerPrefs.GetFloat("OpenRouter_MaxTokens", 1000f);
            maxTokensSlider.value = maxTokens;
            OnMaxTokensChanged(maxTokens);
        }

        // Load Streaming Toggle
        if (streamingToggle != null)
        {
            bool streaming = saveData?.openRouterStreaming ?? (PlayerPrefs.GetInt("OpenRouter_Streaming", 1) == 1);
            streamingToggle.isOn = streaming;
        }

        // Load Debug Toggle
        if (debugToggle != null)
        {
            bool debug = saveData?.openRouterDebug ?? (PlayerPrefs.GetInt("OpenRouter_Debug", 0) == 1);
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
            var saveData = SaveLoadHandler.Instance?.data;

            // Save OpenRouter Enable Toggle
            if (enableOpenRouterToggle != null)
            {
                bool enabled = enableOpenRouterToggle.isOn;
                PlayerPrefs.SetInt("OpenRouter_Enabled", enabled ? 1 : 0);
                if (saveData != null)
                {
                    saveData.openRouterEnabled = enabled;
                }
            }

            // Save API Key (only if it's not the masked version)
            if (apiKeyField != null && !apiKeyField.text.Contains("••"))
            {
                PlayerPrefs.SetString("OpenRouter_API_Key", apiKeyField.text);
                if (saveData != null)
                {
                    saveData.openRouterApiKey = apiKeyField.text;
                }
            }

            // Save Model
            if (modelDropdown != null)
            {
                var modelKeys = new List<string>(availableModels.Keys);
                if (modelDropdown.value < modelKeys.Count)
                {
                    string selectedModel = modelKeys[modelDropdown.value];
                    PlayerPrefs.SetString("OpenRouter_Model", selectedModel);
                    if (saveData != null)
                    {
                        saveData.openRouterModel = selectedModel;
                    }
                }
            }

            // Save Temperature
            if (temperatureSlider != null)
            {
                float temperature = temperatureSlider.value;
                PlayerPrefs.SetFloat("OpenRouter_Temperature", temperature);
                if (saveData != null)
                {
                    saveData.openRouterTemperature = temperature;
                }
            }

            // Save Max Tokens
            if (maxTokensSlider != null)
            {
                float maxTokens = maxTokensSlider.value;
                PlayerPrefs.SetFloat("OpenRouter_MaxTokens", maxTokens);
                if (saveData != null)
                {
                    saveData.openRouterMaxTokens = maxTokens;
                }
            }

            // Save Streaming
            if (streamingToggle != null)
            {
                bool streaming = streamingToggle.isOn;
                PlayerPrefs.SetInt("OpenRouter_Streaming", streaming ? 1 : 0);
                if (saveData != null)
                {
                    saveData.openRouterStreaming = streaming;
                }
            }

            // Save Debug
            if (debugToggle != null)
            {
                bool debugMode = debugToggle.isOn;
                PlayerPrefs.SetInt("OpenRouter_Debug", debugMode ? 1 : 0);
                if (saveData != null)
                {
                    saveData.openRouterDebug = debugMode;
                }
            }

            PlayerPrefs.Save();

            if (SaveLoadHandler.Instance != null)
            {
                SaveLoadHandler.Instance.SaveToDisk();
            }

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
        // Apply to OpenRouterChatBot first
        if (openRouterChatBot != null)
        {
            if (enableOpenRouterToggle != null)
            {
                openRouterChatBot.SetOpenRouterEnabled(enableOpenRouterToggle.isOn);
            }
            
            if (apiKeyField != null && !apiKeyField.text.Contains("••"))
            {
                openRouterChatBot.SetOpenRouterApiKey(apiKeyField.text);
            }
            
            var modelKeys = new List<string>(availableModels.Keys);
            if (modelDropdown != null && modelDropdown.value < modelKeys.Count)
            {
                openRouterChatBot.SetOpenRouterModel(modelKeys[modelDropdown.value]);
            }
        }

        // Apply to OpenRouterCharacter
        if (openRouterCharacter == null) return;

        var modelKeys2 = new List<string>(availableModels.Keys);
        
        // Update settings
        if (apiKeyField != null && !apiKeyField.text.Contains("••"))
        {
            openRouterCharacter.settings.apiKey = apiKeyField.text;
        }
        
        if (modelDropdown != null && modelDropdown.value < modelKeys2.Count)
        {
            openRouterCharacter.settings.model = modelKeys2[modelDropdown.value];
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
            string key = PlayerPrefs.GetString("OpenRouter_API_Key", "");
            if (SaveLoadHandler.Instance != null)
            {
                key = SaveLoadHandler.Instance.data.openRouterApiKey ?? key;
            }
            apiKeyField.text = key;
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
            if (SaveLoadHandler.Instance != null)
            {
                SaveLoadHandler.Instance.data.openRouterApiKey = actualKey;
                SaveLoadHandler.Instance.SaveToDisk();
            }
        }
    }

    public void ResetToDefaults()
    {
        // Reset to default values
        if (enableOpenRouterToggle != null)
        {
            enableOpenRouterToggle.isOn = true; // Default to enabled
        }

        if (apiKeyField != null)
        {
            apiKeyField.text = ""; // Clear API key
        }

        if (modelDropdown != null)
        {
            // Find index of default model
            var modelKeys = new List<string>(availableModels.Keys);
            int defaultIndex = modelKeys.IndexOf("deepseek/deepseek-chat-v3.1");
            if (defaultIndex >= 0)
            {
                modelDropdown.value = defaultIndex;
            }
        }

        if (temperatureSlider != null)
        {
            temperatureSlider.value = 0.7f;
            OnTemperatureChanged(0.7f);
        }

        if (maxTokensSlider != null)
        {
            maxTokensSlider.value = 1000f;
            OnMaxTokensChanged(1000f);
        }

        if (streamingToggle != null)
        {
            streamingToggle.isOn = true;
        }

        if (debugToggle != null)
        {
            debugToggle.isOn = false;
        }

        // Reset system prompt
        ResetSystemPrompt();

        // Save the defaults
        SaveSettings();
    }

    private string GetSettingsFilePath()
    {
        return Path.Combine(Application.persistentDataPath, "settings.json");
    }

    public void OpenSettingsFile()
    {
        try
        {
            string path = GetSettingsFilePath();

            if (!File.Exists(path))
            {
                if (SaveLoadHandler.Instance != null)
                {
                    SaveLoadHandler.Instance.SaveToDisk();
                }
                else
                {
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.WriteAllText(path, "{}");
                }
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
#elif UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", path);
#elif UNITY_STANDALONE_LINUX
            System.Diagnostics.Process.Start("xdg-open", path);
#else
            UpdateStatus("This platform does not support opening files directly.");
            return;
#endif

            UpdateStatus("settings.json 파일을 열었습니다.");
        }
        catch (System.Exception ex)
        {
            UpdateStatus($"파일 열기에 실패했습니다: {ex.Message}");
            UnityEngine.Debug.LogError($"[OpenRouterSettingsUI] Failed to open settings file: {ex}");
        }
    }
}