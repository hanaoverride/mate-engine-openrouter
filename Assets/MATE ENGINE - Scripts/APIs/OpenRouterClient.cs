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
    public string finish_reason;
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
    [SerializeField] private string defaultModel = "anthropic/claude-3.5-sonnet";
    
    [Header("Request Settings")]
    public float temperature = 0.7f;
    public int maxTokens = 1000;
    public float topP = 1.0f;
    public float frequencyPenalty = 0.0f;
    public float presencePenalty = 0.0f;
    public bool enableStreaming = true;
    
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
            return null;
        }

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
            Debug.Log($"[OpenRouter] Request: {jsonRequest}");
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
            
            if (request.stream && onChunk != null)
            {
                return await ProcessStreamingResponse(webRequest, onChunk);
            }
            else
            {
                return await ProcessNonStreamingResponse(webRequest);
            }
        }
    }

    private async Task<string> ProcessNonStreamingResponse(UnityWebRequest webRequest)
    {
        try
        {
            var operation = webRequest.SendWebRequest();
            
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            activeRequests.Remove(webRequest);

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string responseText = webRequest.downloadHandler.text;
                
                if (debugRequests)
                {
                    Debug.Log($"[OpenRouter] Response: {responseText}");
                }

                try
                {
                    var response = JsonUtility.FromJson<OpenRouterResponse>(responseText);
                    if (response.choices != null && response.choices.Count > 0)
                    {
                        return response.choices[0].message.content;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[OpenRouter] Failed to parse response: {e.Message}");
                    Debug.LogError($"[OpenRouter] Raw response: {responseText}");
                }
            }
            else
            {
                Debug.LogError($"[OpenRouter] Request failed: {webRequest.error}");
                Debug.LogError($"[OpenRouter] Response: {webRequest.downloadHandler.text}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[OpenRouter] Exception during request: {e.Message}");
            activeRequests.Remove(webRequest);
        }

        return null;
    }

    private async Task<string> ProcessStreamingResponse(UnityWebRequest webRequest, Action<string> onChunk)
    {
        try
        {
            var operation = webRequest.SendWebRequest();
            string fullResponse = "";
            string buffer = "";
            
            while (!operation.isDone)
            {
                string currentData = webRequest.downloadHandler.text;
                if (currentData.Length > buffer.Length)
                {
                    string newData = currentData.Substring(buffer.Length);
                    buffer = currentData;
                    
                    // Process SSE chunks
                    string[] lines = newData.Split('\n');
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("data: "))
                        {
                            string data = line.Substring(6).Trim();
                            if (data == "[DONE]") break;
                            
                            try
                            {
                                var streamResponse = JsonUtility.FromJson<OpenRouterStreamResponse>(data);
                                if (streamResponse.choices != null && streamResponse.choices.Count > 0)
                                {
                                    string content = streamResponse.choices[0].delta.content;
                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        fullResponse += content;
                                        onChunk?.Invoke(fullResponse);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                if (debugRequests)
                                {
                                    Debug.LogWarning($"[OpenRouter] Failed to parse stream chunk: {e.Message}");
                                }
                            }
                        }
                    }
                }
                await Task.Yield();
            }

            activeRequests.Remove(webRequest);

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[OpenRouter] Streaming request failed: {webRequest.error}");
                return null;
            }

            return fullResponse;
        }
        catch (Exception e)
        {
            Debug.LogError($"[OpenRouter] Exception during streaming: {e.Message}");
            activeRequests.Remove(webRequest);
            return null;
        }
    }

    public void CancelAllRequests()
    {
        foreach (var request in activeRequests)
        {
            if (request != null)
            {
                request.Abort();
            }
        }
        activeRequests.Clear();
        Debug.Log("[OpenRouter] All requests cancelled");
    }

    void OnDestroy()
    {
        CancelAllRequests();
    }
}