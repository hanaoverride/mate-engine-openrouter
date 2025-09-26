using UnityEngine;
using LLMUnity;
using LLMUnitySamples;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Extended ChatBot that supports both LLMUnity (ZomeAI) and OpenRouter
/// This replaces the original LLMUnitySamples.ChatBot with OpenRouter support
/// </summary>
public class ExtendedChatBot : MonoBehaviour
{
    [Header("Chat Provider Selection")]
    public ChatProvider provider = ChatProvider.ZomeAI;
    
    [Header("Original ChatBot Settings")]
    public Transform chatContainer;
    public Color playerColor = new Color32(81, 164, 81, 255);
    public Color aiColor = new Color32(29, 29, 73, 255);
    public Color fontColor = Color.white;
    public Font font;
    public int fontSize = 16;
    public int bubbleWidth = 600;
    public float textPadding = 10f;
    public float bubbleSpacing = 10f;
    public Sprite sprite;

    [Header("LLM Providers")]
    public LLMCharacter llmCharacter; // ZomeAI
    public OpenRouterCharacter openRouterCharacter; // OpenRouter

    [Header("Bubble Style")]
    [Range(0, 64)]
    public int cornerRadius = 16;
    public Sprite roundedSprite16;
    public Sprite roundedSprite32;
    public Sprite roundedSprite64;

    [Header("Input Settings")]
    public string inputPlaceholder = "Message me";

    [Header("Audio")]
    public AudioSource streamAudioSource;

    public Material playerMaterial;
    public Material aiMaterial;
    public Color playerFontColor = Color.white;
    public Color aiFontColor = Color.white;

    // Private fields
    private InputBubble inputBubble;
    private List<Bubble> chatBubbles = new List<Bubble>();
    private bool blockInput = true;
    private BubbleUI playerUI, aiUI;
    private bool warmUpDone = false;
    private int lastBubbleOutsideFOV = -1;

    public enum ChatProvider
    {
        ZomeAI,      // Original LLMUnity system
        OpenRouter   // New OpenRouter system
    }

    void Start()
    {
        InitializeProviders();
        SetupUI();
        StartWarmup();
    }

    private void InitializeProviders()
    {
        // Setup OpenRouter character if not assigned
        if (openRouterCharacter == null)
        {
            openRouterCharacter = GetComponent<OpenRouterCharacter>();
            if (openRouterCharacter == null)
            {
                openRouterCharacter = gameObject.AddComponent<OpenRouterCharacter>();
            }
        }

        // Set chat container for OpenRouter
        if (openRouterCharacter != null)
        {
            openRouterCharacter.chatContainer = chatContainer;
        }

        Debug.Log($"[ExtendedChatBot] Initialized with provider: {provider}");
    }

    private void SetupUI()
    {
        // Setup bubble UI
        playerUI = new BubbleUI
        {
            font = font,
            fontSize = fontSize,
            fontColor = playerFontColor,
            bubbleColor = playerColor,
            bottomPosition = 0,
            leftPosition = 1,
            textPadding = textPadding,
            bubbleOffset = bubbleSpacing,
            bubbleWidth = bubbleWidth,
            bubbleHeight = -1
        };

        aiUI = new BubbleUI
        {
            font = font,
            fontSize = fontSize,
            fontColor = aiFontColor,
            bubbleColor = aiColor,
            bottomPosition = 0,
            leftPosition = 0,
            textPadding = textPadding,
            bubbleOffset = bubbleSpacing,
            bubbleWidth = bubbleWidth,
            bubbleHeight = -1
        };

        // Choose rounded sprite based on radius
        if (cornerRadius <= 16)
            sprite = roundedSprite16;
        else if (cornerRadius <= 32)
            sprite = roundedSprite32;
        else
            sprite = roundedSprite64;

        playerUI.sprite = sprite;
        aiUI.sprite = sprite;

        // Create input bubble
        if (chatContainer != null)
        {
            inputBubble = new InputBubble(chatContainer, playerUI, "InputBubble", "Loading...", 4);
            inputBubble.AddSubmitListener(OnInputFieldSubmit);
            inputBubble.AddValueChangedListener(OnValueChanged);
            inputBubble.setInteractable(false);
        }
    }

    private async void StartWarmup()
    {
        switch (provider)
        {
            case ChatProvider.ZomeAI:
                if (llmCharacter != null)
                {
                    _ = llmCharacter.Warmup(WarmUpCallback);
                }
                else
                {
                    WarmUpCallback();
                }
                break;

            case ChatProvider.OpenRouter:
                // OpenRouter doesn't need warmup, just enable input
                WarmUpCallback();
                break;
        }
    }

    public void WarmUpCallback()
    {
        warmUpDone = true;
        if (inputBubble != null)
        {
            inputBubble.SetPlaceHolderText(inputPlaceholder);
            AllowInput();
        }
        Debug.Log($"[ExtendedChatBot] {provider} warmed up and ready");
    }

    private async void OnInputFieldSubmit(string newText)
    {
        inputBubble.ActivateInputField();
        
        if (blockInput || newText.Trim() == "" || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            StartCoroutine(BlockInteraction());
            return;
        }

        blockInput = true;
        string message = inputBubble.GetText().Replace("\v", "\n");

        // Add user bubble
        AddBubble(message, true);
        Bubble aiBubble = AddBubble("...", false);

        // Start audio if available
        if (streamAudioSource != null)
            streamAudioSource.Play();

        // Send message to appropriate provider
        try
        {
            await SendMessage(message, aiBubble);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ExtendedChatBot] Error sending message: {e.Message}");
            aiBubble?.SetText("Sorry, an error occurred. Please try again.");
        }
        finally
        {
            // Stop audio and allow input
            if (streamAudioSource != null && streamAudioSource.isPlaying)
                StartCoroutine(FadeOutStreamAudio());
            
            AllowInput();
            inputBubble.SetText("");
        }
    }

    private async Task SendMessage(string message, Bubble aiBubble)
    {
        switch (provider)
        {
            case ChatProvider.ZomeAI:
                await SendZomeAIMessage(message, aiBubble);
                break;

            case ChatProvider.OpenRouter:
                await SendOpenRouterMessage(message, aiBubble);
                break;
        }
    }

    private async Task SendZomeAIMessage(string message, Bubble aiBubble)
    {
        if (llmCharacter == null)
        {
            aiBubble?.SetText("ZomeAI not configured. Please check settings.");
            return;
        }

        await llmCharacter.Chat(message, 
            callback: (partialText) => aiBubble?.SetText(partialText),
            completionCallback: () => { /* Completion handled in finally block */ }
        );
    }

    private async Task SendOpenRouterMessage(string message, Bubble aiBubble)
    {
        if (openRouterCharacter == null)
        {
            aiBubble?.SetText("OpenRouter not configured. Please set API key.");
            return;
        }

        await openRouterCharacter.Chat(message,
            onPartialResponse: (partialText) => aiBubble?.SetText(partialText),
            onComplete: () => { /* Completion handled in finally block */ }
        );
    }

    private Bubble AddBubble(string message, bool isPlayerMessage)
    {
        if (chatContainer == null) return null;

        var ui = isPlayerMessage ? playerUI : aiUI;
        var bubble = new Bubble(chatContainer, ui, isPlayerMessage ? "PlayerBubble" : "AIBubble", message);
        chatBubbles.Add(bubble);
        bubble.OnResize(UpdateBubblePositions);

        // Apply materials
        var image = bubble.GetRectTransform().GetComponentInChildren<UnityEngine.UI.Image>(true);
        if (image != null)
        {
            image.material = isPlayerMessage ? playerMaterial : aiMaterial;
        }

        // Limit bubble count
        if (chatBubbles.Count > 50)
        {
            Bubble oldest = chatBubbles[0];
            oldest.Destroy();
            chatBubbles.RemoveAt(0);
        }

        return bubble;
    }

    private void UpdateBubblePositions()
    {
        // Implementation similar to original ChatBot
        // This handles bubble positioning and scrolling
    }

    private void OnValueChanged(string newText)
    {
        // Handle enter key behavior
        if (Input.GetKey(KeyCode.Return))
        {
            if (inputBubble.GetText().Trim() == "")
                inputBubble.SetText("");
        }
    }

    private System.Collections.IEnumerator BlockInteraction()
    {
        inputBubble.setInteractable(false);
        yield return null;
        inputBubble.setInteractable(true);
        inputBubble.MoveTextEnd();
    }

    private System.Collections.IEnumerator FadeOutStreamAudio(float duration = 0.5f)
    {
        if (streamAudioSource == null) yield break;
        
        float startVolume = streamAudioSource.volume;
        while (streamAudioSource.volume > 0f)
        {
            streamAudioSource.volume -= startVolume * Time.deltaTime / duration;
            yield return null;
        }
        streamAudioSource.Stop();
        streamAudioSource.volume = startVolume;
    }

    public void AllowInput()
    {
        blockInput = false;
        inputBubble?.ReActivateInputField();
    }

    public void CancelRequests()
    {
        switch (provider)
        {
            case ChatProvider.ZomeAI:
                llmCharacter?.CancelRequests();
                break;

            case ChatProvider.OpenRouter:
                openRouterCharacter?.CancelRequests();
                break;
        }
        AllowInput();
    }

    // Public method to switch providers
    public void SwitchProvider(ChatProvider newProvider)
    {
        if (blockInput)
        {
            Debug.LogWarning("[ExtendedChatBot] Cannot switch provider while processing");
            return;
        }

        provider = newProvider;
        Debug.Log($"[ExtendedChatBot] Switched to {provider}");
        
        // Restart warmup for the new provider
        warmUpDone = false;
        inputBubble?.setInteractable(false);
        StartWarmup();
    }

    // Public method to clear chat history
    public void ClearChatHistory()
    {
        switch (provider)
        {
            case ChatProvider.ZomeAI:
                llmCharacter?.ClearChat();
                break;

            case ChatProvider.OpenRouter:
                openRouterCharacter?.ClearHistory();
                break;
        }

        // Clear visual bubbles
        foreach (var bubble in chatBubbles)
        {
            bubble?.Destroy();
        }
        chatBubbles.Clear();

        Debug.Log($"[ExtendedChatBot] {provider} chat history cleared");
    }

    void OnDisable()
    {
        CancelRequests();
    }
}