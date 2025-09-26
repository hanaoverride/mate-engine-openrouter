using UnityEngine;
using LLMUnity;
using System.Threading.Tasks;

/// <summary>
/// Intercepts calls to LLMCharacter and redirects them to ChatBridge/OpenRouter
/// Add this component to the same GameObject as LLMCharacter to override its behavior
/// </summary>
[RequireComponent(typeof(LLMCharacter))]
public class LLMCharacterInterceptor : MonoBehaviour
{
    [Header("Interception Settings")]
    public bool enableInterception = true;
    public bool debugInterception = true;
    
    [Header("Target System")]
    public ChatBridge chatBridge;
    public OpenRouterCharacter openRouterCharacter;

    private LLMCharacter originalLLMCharacter;

    void Awake()
    {
        originalLLMCharacter = GetComponent<LLMCharacter>();
        
        if (enableInterception)
        {
            // Find ChatBridge if not assigned
            if (chatBridge == null)
            {
                chatBridge = FindFirstObjectByType<ChatBridge>();
            }
            
            // Find OpenRouterCharacter if not assigned
            if (openRouterCharacter == null)
            {
                openRouterCharacter = FindFirstObjectByType<OpenRouterCharacter>();
            }
            
            if (debugInterception)
            {
                Debug.Log($"[LLMCharacterInterceptor] Intercepting LLMCharacter calls on {gameObject.name}");
                Debug.Log($"[LLMCharacterInterceptor] ChatBridge: {(chatBridge != null ? "Found" : "Not Found")}");
                Debug.Log($"[LLMCharacterInterceptor] OpenRouterCharacter: {(openRouterCharacter != null ? "Found" : "Not Found")}");
            }
        }
    }

    // This method can be called by reflection or direct access to override LLMCharacter.Chat
    public async Task<string> Chat(string query, LLMUnity.Callback<string> callback = null, LLMUnity.EmptyCallback completionCallback = null, bool addToHistory = true)
    {
        if (!enableInterception)
        {
            // Fall back to original LLMCharacter
            if (debugInterception)
                Debug.Log("[LLMCharacterInterceptor] Interception disabled, using original LLMCharacter");
            return await originalLLMCharacter.Chat(query, callback, completionCallback, addToHistory);
        }

        if (debugInterception)
            Debug.Log($"[LLMCharacterInterceptor] Intercepted chat request: '{query.Substring(0, Mathf.Min(50, query.Length))}...'");

        // Try ChatBridge first
        if (chatBridge != null)
        {
            try
            {
                string response = await chatBridge.Chat(query);
                
                // Call the callback with the response (simulate streaming)
                callback?.Invoke(response);
                completionCallback?.Invoke();
                
                return response;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LLMCharacterInterceptor] ChatBridge failed: {e.Message}");
            }
        }

        // Try OpenRouterCharacter directly
        if (openRouterCharacter != null)
        {
            try
            {
                string response = await openRouterCharacter.Chat(
                    query,
                    onPartialResponse: (partial) => callback?.Invoke(partial),
                    onComplete: () => completionCallback?.Invoke()
                );
                
                return response;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LLMCharacterInterceptor] OpenRouterCharacter failed: {e.Message}");
            }
        }

        // Last resort: use original LLMCharacter
        Debug.LogWarning("[LLMCharacterInterceptor] No OpenRouter system found, falling back to original LLMCharacter");
        return await originalLLMCharacter.Chat(query, callback, completionCallback, addToHistory);
    }

    // Override other LLMCharacter methods if needed
    public void CancelRequests()
    {
        if (enableInterception)
        {
            chatBridge?.CancelCurrentRequest();
            openRouterCharacter?.CancelRequests();
        }
        else
        {
            originalLLMCharacter.CancelRequests();
        }
    }

    // Public method to enable/disable interception at runtime
    public void SetInterception(bool enabled)
    {
        enableInterception = enabled;
        if (debugInterception)
            Debug.Log($"[LLMCharacterInterceptor] Interception {(enabled ? "enabled" : "disabled")}");
    }

    // Public method to switch back to original LLMCharacter
    public void UseOriginalLLMCharacter()
    {
        enableInterception = false;
        if (debugInterception)
            Debug.Log("[LLMCharacterInterceptor] Switched back to original LLMCharacter");
    }

    // Public method to switch to OpenRouter
    public void UseOpenRouter()
    {
        enableInterception = true;
        if (debugInterception)
            Debug.Log("[LLMCharacterInterceptor] Switched to OpenRouter");
    }
}