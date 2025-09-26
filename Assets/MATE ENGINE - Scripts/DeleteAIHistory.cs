using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class DeleteAIHistory : MonoBehaviour
{
    [Header("UI Button to delete AI history")]
    public Button deleteButton;

    [Tooltip("Base filename for AI history. Default is 'ZomeAI'.")]
    public string fileName = "ZomeAI";
    
    [Header("OpenRouter Support")]
    [Tooltip("Also delete OpenRouter history files")]
    public bool deleteOpenRouterHistory = true;
    public string openRouterFileName = "OpenRouter_Chat";

    void Start()
    {
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(DeleteHistoryFiles);
        }
        else
        {
            Debug.LogWarning("[DeleteAIHistory] Delete Button is not assigned.");
        }
    }

    public void DeleteHistoryFiles()
    {
        bool deletedSomething = false;
        
        // Delete original ZomeAI files
        deletedSomething |= DeleteLegacyFiles();
        
        // Delete OpenRouter files
        if (deleteOpenRouterHistory)
        {
            deletedSomething |= DeleteOpenRouterFiles();
        }
        
        // Clear OpenRouter character history if present
        var openRouterCharacter = FindFirstObjectByType<OpenRouterCharacter>();
        if (openRouterCharacter != null)
        {
            openRouterCharacter.ClearHistory();
            Debug.Log("[DeleteAIHistory] Cleared OpenRouter character history");
            deletedSomething = true;
        }

        if (!deletedSomething)
        {
            Debug.LogWarning("[DeleteAIHistory] No AI history files found to delete");
        }
        else
        {
            Debug.Log("[DeleteAIHistory] AI history deletion completed");
        }
    }

    private bool DeleteLegacyFiles()
    {
        bool deleted = false;
        string jsonPath = Path.Combine(Application.persistentDataPath, fileName + ".json");
        string cachePath = Path.Combine(Application.persistentDataPath, fileName + ".cache");

        if (File.Exists(jsonPath))
        {
            File.Delete(jsonPath);
            Debug.Log("[DeleteAIHistory] Deleted: " + jsonPath);
            deleted = true;
        }

        if (File.Exists(cachePath))
        {
            File.Delete(cachePath);
            Debug.Log("[DeleteAIHistory] Deleted: " + cachePath);
            deleted = true;
        }
        
        return deleted;
    }
    
    private bool DeleteOpenRouterFiles()
    {
        bool deleted = false;
        string openRouterJsonPath = Path.Combine(Application.persistentDataPath, openRouterFileName + ".json");
        string openRouterPromptPath = Path.Combine(Application.persistentDataPath, "OpenRouter_prompt.txt");

        if (File.Exists(openRouterJsonPath))
        {
            File.Delete(openRouterJsonPath);
            Debug.Log("[DeleteAIHistory] Deleted OpenRouter history: " + openRouterJsonPath);
            deleted = true;
        }

        if (File.Exists(openRouterPromptPath))
        {
            File.Delete(openRouterPromptPath);
            Debug.Log("[DeleteAIHistory] Deleted OpenRouter prompt: " + openRouterPromptPath);
            deleted = true;
        }
        
        return deleted;
    }
}
