using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

[Serializable]
public class OpenRouterMessage
{
    public string role;
    public string content;
}

[Serializable]
public class OpenRouterRequest
{
    public string model;
    public List<OpenRouterMessage> messages;
    public float temperature = 0.7f;
    public int max_tokens = 1000;
    public bool stream = false;
    public float top_p = 1.0f;
    public float frequency_penalty = 0.0f;
    public float presence_penalty = 0.0f;
}

[Serializable]
public class OpenRouterChoice
{
    public int index;
    public OpenRouterMessage message;
    public OpenRouterDelta delta; // For streaming responses
    public string finish_reason;
}

[Serializable]
public class OpenRouterDelta
{
    public string content;
}

[Serializable]
public class OpenRouterUsage
{
    public int prompt_tokens;
    public int completion_tokens;
    public int total_tokens;
}

[Serializable]
public class OpenRouterResponse
{
    public string id;
    public string @object;
    public long created;
    public string model;
    public List<OpenRouterChoice> choices;
    public OpenRouterUsage usage;
}

[Serializable]
public class OpenRouterError
{
    public string message;
    public string type;
    public string code;
}

[Serializable]
public class OpenRouterErrorResponse
{
    public OpenRouterError error;
}

[Serializable]
public class OpenRouterStreamDelta
{
    public string content;
    public string role;
}

[Serializable]
public class OpenRouterStreamChoice
{
    public int index;
    public OpenRouterStreamDelta delta;
    public string finish_reason;
}

[Serializable]
public class OpenRouterStreamResponse
{
    public string id;
    public string @object;
    public long created;
    public string model;
    public List<OpenRouterStreamChoice> choices;
}

public class OpenRouterClient : MonoBehaviour
{
    [Header("OpenRouter Configuration")]
    [SerializeField] private string apiKey = "";
    [SerializeField] private string baseUrl = "https://openrouter.ai/api/v1";
    [SerializeField] private string defaultModel = "deepseek/deepseek-chat-v3.1";
    
    [Header("Request Settings")]
    public float temperature = 0.7f;
    public int maxTokens = 1000;
    public float topP = 1.0f;
    public float frequencyPenalty = 0.0f;
    public float presencePenalty = 0.0f;
    public bool enableStreaming = false; // Disabled by default due to SSE parsing complexity
    
    [Header("Retry Settings")]
    public int maxRetries = 3;
    public float baseRetryDelay = 1.0f;
    
    [Header("Debug")]
    public bool debugRequests = false;

    private List<UnityWebRequest> activeRequests = new List<UnityWebRequest>();

    void Start()
    {
        // API 키를 PlayerPrefs 또는 파일에서 로드
        LoadApiKey();
    }

    private void LoadApiKey()
    {
        // 먼저 PlayerPrefs에서 확인
        if (PlayerPrefs.HasKey("OpenRouter_API_Key"))
        {
            apiKey = PlayerPrefs.GetString("OpenRouter_API_Key");
            Debug.Log("[OpenRouter] API Key loaded from PlayerPrefs");
        }
        else if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[OpenRouter] No API Key found. Please set it in Inspector or PlayerPrefs.");
        }
    }

    public void SetApiKey(string key)
    {
        apiKey = key;
        PlayerPrefs.SetString("OpenRouter_API_Key", key);
        PlayerPrefs.Save();
        Debug.Log("[OpenRouter] API Key saved");
    }

    public async Task<string> SendChatRequest(List<OpenRouterMessage> messages, string model = null, Action<string> onChunk = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[OpenRouter] API Key is not set!");
            return "Error: API Key not configured. Please set your OpenRouter API key.";
        }

        // Validate API key format (OpenRouter keys typically start with "sk-or-")
        if (!apiKey.StartsWith("sk-or-") && !apiKey.StartsWith("sk-"))
        {
            Debug.LogWarning("[OpenRouter] API key format seems unusual. OpenRouter keys usually start with 'sk-or-'");
        }

        // Retry logic with exponential backoff
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var result = await SendSingleRequest(messages, model, onChunk, attempt);
                
                // If we get a rate limit error, don't retry immediately
                if (result.Contains("Rate limited"))
                {
                    if (attempt < maxRetries - 1)
                    {
                        float delay = CalculateRetryDelay(attempt, isRateLimit: true);
                        Debug.Log($"[OpenRouter] Rate limited, retrying in {delay} seconds... (Attempt {attempt + 1}/{maxRetries})");
                        await Task.Delay((int)(delay * 1000));
                        continue;
                    }
                }
                
                return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OpenRouter] Attempt {attempt + 1} failed: {e.Message}");
                
                if (attempt < maxRetries - 1)
                {
                    float delay = CalculateRetryDelay(attempt, isRateLimit: false);
                    await Task.Delay((int)(delay * 1000));
                }
                else
                {
                    return $"Error: All {maxRetries} attempts failed. Last error: {e.Message}";
                }
            }
        }
        
        return "Error: Maximum retry attempts exceeded.";
    }

    private float CalculateRetryDelay(int attempt, bool isRateLimit)
    {
        if (isRateLimit)
        {
            // For rate limits, use longer delays
            return baseRetryDelay * (float)Math.Pow(2, attempt) + UnityEngine.Random.Range(1f, 5f);
        }
        else
        {
            // For other errors, use shorter exponential backoff
            return baseRetryDelay * (float)Math.Pow(1.5, attempt) + UnityEngine.Random.Range(0.1f, 1f);
        }
    }

    private async Task<string> SendSingleRequest(List<OpenRouterMessage> messages, string model, Action<string> onChunk, int attemptNumber)
    {
        var request = new OpenRouterRequest
        {
            model = model ?? defaultModel,
            messages = messages,
            temperature = temperature,
            max_tokens = maxTokens,
            stream = enableStreaming && onChunk != null,
            top_p = topP,
            frequency_penalty = frequencyPenalty,
            presence_penalty = presencePenalty
        };

        string jsonRequest = JsonUtility.ToJson(request);
        if (debugRequests)
        {
            Debug.Log($"[OpenRouter] Request (Attempt {attemptNumber + 1}): {jsonRequest}");
        }

        using (UnityWebRequest webRequest = new UnityWebRequest($"{baseUrl}/chat/completions", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequest);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            
            // Set headers
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            webRequest.SetRequestHeader("HTTP-Referer", "https://github.com/hanaoverride/Mate-Engine-OpenRouter");
            webRequest.SetRequestHeader("X-Title", "Mate Engine Desktop Pet");

            activeRequests.Add(webRequest);
            
            try
            {
                if (request.stream && onChunk != null)
                {
                    return await ProcessStreamingResponse(webRequest, onChunk);
                }
                else
                {
                    return await ProcessNonStreamingResponse(webRequest);
                }
            }
            finally
            {
                activeRequests.Remove(webRequest);
            }
        }
    }

    private async Task<string> ProcessNonStreamingResponse(UnityWebRequest webRequest)
    {
        var operation = webRequest.SendWebRequest();
        
        while (!operation.isDone)
        {
            await Task.Yield();
        }

        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            string responseText = webRequest.downloadHandler.text;
            
            // Always log the raw response for debugging when there are issues
            if (debugRequests || string.IsNullOrEmpty(responseText) || !responseText.TrimStart().StartsWith("{"))
            {
                Debug.Log($"[OpenRouter] Raw Response (Status: {webRequest.responseCode}): {responseText}");
            }

            // Check if response is Server-Sent Events (streaming format)
            if (responseText.StartsWith("data:") || responseText.Contains("data: {"))
            {
                Debug.LogWarning("[OpenRouter] Received streaming response, but streaming is disabled. Parsing as SSE...");
                return ParseSSEResponse(responseText);
            }

            // Check if response is HTML instead of JSON
            if (responseText.TrimStart().StartsWith("<") || responseText.Contains("<html"))
            {
                Debug.LogError($"[OpenRouter] Received HTML response instead of JSON. Status: {webRequest.responseCode}");
                Debug.LogError($"[OpenRouter] HTML Response: {responseText.Substring(0, System.Math.Min(500, responseText.Length))}...");
                return "Error: Received HTML response from API. This usually indicates an authentication or server issue.";
            }

            // Check if response is empty
            if (string.IsNullOrWhiteSpace(responseText))
            {
                Debug.LogError("[OpenRouter] Received empty response from API");
                return "Error: Empty response from API";
            }

            try
            {
                var response = JsonUtility.FromJson<OpenRouterResponse>(responseText);
                if (response?.choices != null && response.choices.Count > 0)
                {
                    string content = response.choices[0].message.content;
                    if (debugRequests)
                    {
                        Debug.Log($"[OpenRouter] Parsed content: {content.Substring(0, System.Math.Min(100, content.Length))}...");
                    }
                    return content;
                }
                else
                {
                    Debug.LogError("[OpenRouter] Response missing choices or message content");
                    Debug.LogError($"[OpenRouter] Response structure: choices={response?.choices?.Count}, model={response?.model}");
                    return "Error: Invalid response format from API - missing message content";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenRouter] Failed to parse response: {e.Message}");
                Debug.LogError($"[OpenRouter] Response Text: {responseText}");
                return $"Error: Failed to parse API response - {e.Message}\nResponse: {responseText}";
            }
        }
        else
        {
            return HandleHttpError(webRequest);
        }
    }

            private string ParseSSEResponse(string sseData)
        {
            try
            {
                var lines = sseData.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                var contentBuilder = new System.Text.StringBuilder();
                
                Debug.Log($"[OpenRouter] Parsing SSE response with {lines.Length} lines");
                
                foreach (var line in lines)
                {
                    if (line.StartsWith("data: "))
                    {
                        var jsonData = line.Substring(6); // Remove "data: "
                        
                        // Skip if it's the end marker
                        if (jsonData.Trim() == "[DONE]")
                        {
                            Debug.Log("[OpenRouter] Found [DONE] marker, stopping parsing");
                            continue;
                        }
                            
                        try
                        {
                            var response = JsonUtility.FromJson<OpenRouterResponse>(jsonData);
                            if (response?.choices != null && response.choices.Count > 0)
                            {
                                var deltaContent = response.choices[0].delta?.content;
                                if (!string.IsNullOrEmpty(deltaContent))
                                {
                                    contentBuilder.Append(deltaContent);
                                    if (debugRequests)
                                    {
                                        Debug.Log($"[OpenRouter] SSE chunk: '{deltaContent}'");
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[OpenRouter] Failed to parse SSE chunk: {e.Message}");
                            Debug.LogWarning($"[OpenRouter] Chunk data: {jsonData}");
                        }
                    }
                }
                
                var finalContent = contentBuilder.ToString();
                if (string.IsNullOrWhiteSpace(finalContent))
                {
                    Debug.LogWarning("[OpenRouter] No content extracted from SSE response");
                    return "Error: No content received from streaming response";
                }
                
                Debug.Log($"[OpenRouter] Extracted content from SSE ({finalContent.Length} chars): {finalContent.Substring(0, System.Math.Min(200, finalContent.Length))}...");
                return finalContent;
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenRouter] Failed to parse SSE response: {e.Message}");
                return $"Error: Failed to parse streaming response - {e.Message}";
            }
        }

        private string HandleHttpError(UnityWebRequest webRequest)
    {
        string responseBody = webRequest.downloadHandler?.text ?? "";
        
        // Always log error responses for debugging
        Debug.LogError($"[OpenRouter] HTTP Error {webRequest.responseCode}: {webRequest.error}");
        
        if (!string.IsNullOrEmpty(responseBody))
        {
            // Log raw response body for debugging
            Debug.LogError($"[OpenRouter] Error response body: {responseBody.Substring(0, System.Math.Min(1000, responseBody.Length))}...");
            
            // Check if error response is HTML
            if (responseBody.TrimStart().StartsWith("<") || responseBody.Contains("<html"))
            {
                Debug.LogError("[OpenRouter] Received HTML error page instead of JSON error");
                // Try to extract meaningful info from HTML
                if (responseBody.Contains("403") || responseBody.Contains("Forbidden"))
                {
                    return "Error: Access forbidden. Please check your API key and permissions.";
                }
                else if (responseBody.Contains("401") || responseBody.Contains("Unauthorized"))
                {
                    return "Error: Invalid API key. Please check your OpenRouter API key.";
                }
                else
                {
                    return $"Error: Server returned HTML error page (Status {webRequest.responseCode})";
                }
            }

            // Try to parse JSON error response
            try
            {
                var errorResponse = JsonUtility.FromJson<OpenRouterErrorResponse>(responseBody);
                if (errorResponse?.error != null)
                {
                    Debug.LogError($"[OpenRouter] API Error: {errorResponse.error.message}");
                    return $"API Error: {errorResponse.error.message}";
                }
            }
            catch
            {
                Debug.LogWarning("[OpenRouter] Could not parse error response as JSON");
            }
        }

        // Handle specific error codes
        switch (webRequest.responseCode)
        {
            case 429: // Too Many Requests
                Debug.LogWarning("[OpenRouter] Rate limited. Will retry automatically...");
                return "Rate limited. Please wait a moment and try again.";
                
            case 401: // Unauthorized
                Debug.LogError("[OpenRouter] Invalid API key!");
                return "Error: Invalid API key. Please check your OpenRouter API key in settings.";
                
            case 403: // Forbidden
                Debug.LogError("[OpenRouter] Access forbidden. Check your API key permissions.");
                return "Error: Access forbidden. Please verify your API key has the necessary permissions.";
                
            case 400: // Bad Request
                Debug.LogError("[OpenRouter] Bad request. Check your message format.");
                return "Error: Bad request. Please try rephrasing your message or check your settings.";
                
            case 404: // Not Found
                Debug.LogError("[OpenRouter] Model not found or endpoint incorrect.");
                return "Error: The selected model was not found. Please check your model selection.";
                
            case 500: // Internal Server Error
            case 502: // Bad Gateway
            case 503: // Service Unavailable
            case 504: // Gateway Timeout
                Debug.LogWarning($"[OpenRouter] Server error {webRequest.responseCode}. Will retry automatically...");
                throw new Exception($"Server error {webRequest.responseCode}: {webRequest.error}");
                
            default:
                string errorMessage = $"HTTP {webRequest.responseCode}: {webRequest.error}";
                Debug.LogError($"[OpenRouter] {errorMessage}");
                return $"Error {webRequest.responseCode}: {webRequest.error}";
        }
    }

    private async Task<string> ProcessStreamingResponse(UnityWebRequest webRequest, Action<string> onChunk)
    {
        // Implementation for streaming would go here
        // For now, fallback to non-streaming
        return await ProcessNonStreamingResponse(webRequest);
    }

    public void CancelAllRequests()
    {
        foreach (var request in activeRequests.ToArray())
        {
            request?.Abort();
        }
        activeRequests.Clear();
        Debug.Log("[OpenRouter] All requests cancelled");
    }

    void OnDestroy()
    {
        CancelAllRequests();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            CancelAllRequests();
        }
    }
}