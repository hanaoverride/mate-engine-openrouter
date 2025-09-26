using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Ensures OpenRouter runtime settings can be injected through environment variables
/// before any scene scripts start executing. This is especially useful in player builds
/// where PlayerPrefs are not populated ahead of time.
///
/// Supported environment variables:
///  - OPENROUTER_API_KEY        : string API key (stored in PlayerPrefs as OpenRouter_API_Key)
///  - OPENROUTER_MODEL          : string model id (stored in PlayerPrefs as OpenRouter_Model)
///  - OPENROUTER_ENABLED        : bool (true/false/1/0) controlling OpenRouter_Enabled
///  - OPENROUTER_TEMPERATURE    : float, overrides OpenRouter_Temperature
///  - OPENROUTER_MAX_TOKENS     : int, overrides OpenRouter_MaxTokens
///  - OPENROUTER_STREAMING      : bool, overrides OpenRouter_Streaming
///  - OPENROUTER_DEBUG          : bool, overrides OpenRouter_Debug
/// </summary>
public static class OpenRouterEnvironmentBootstrapper
{
    private const string ApiKeyEnv = "OPENROUTER_API_KEY";
    private const string ModelEnv = "OPENROUTER_MODEL";
    private const string EnabledEnv = "OPENROUTER_ENABLED";
    private const string TemperatureEnv = "OPENROUTER_TEMPERATURE";
    private const string MaxTokensEnv = "OPENROUTER_MAX_TOKENS";
    private const string StreamingEnv = "OPENROUTER_STREAMING";
    private const string DebugEnv = "OPENROUTER_DEBUG";

    private static bool _hasApplied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyEnvironmentOverridesOnLoad()
    {
        ApplyEnvironmentOverrides();
    }

    /// <summary>
    /// Applies environment based overrides to PlayerPrefs so that existing systems
    /// loading through PlayerPrefs (ChatBot, UI, OpenRouterClient) receive the injected values.
    /// Safe to call multiple times (updates PlayerPrefs only when values change).
    /// </summary>
    public static void ApplyEnvironmentOverrides()
    {
        bool changed = false;

    changed |= TrySetString(ApiKeyEnv, "OpenRouter_API_Key", maskValueInLog: true);
    changed |= TrySetString(ModelEnv, "OpenRouter_Model");
    changed |= TrySetBool(EnabledEnv, "OpenRouter_Enabled");
    changed |= TrySetFloat(TemperatureEnv, "OpenRouter_Temperature");
    changed |= TrySetFloat(MaxTokensEnv, "OpenRouter_MaxTokens");
    changed |= TrySetBool(StreamingEnv, "OpenRouter_Streaming");
    changed |= TrySetBool(DebugEnv, "OpenRouter_Debug");

        if (changed)
        {
            PlayerPrefs.Save();
            Debug.Log("[OpenRouterEnv] Applied environment overrides to PlayerPrefs");
        }

        _hasApplied = true;
    }

    private static bool TrySetString(string envName, string playerPrefKey, bool maskValueInLog = false)
    {
        string envValue = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrEmpty(envValue))
        {
            return false;
        }

        string current = PlayerPrefs.GetString(playerPrefKey, "");
        if (current == envValue)
        {
            return false;
        }

        PlayerPrefs.SetString(playerPrefKey, envValue);
        Debug.Log($"[OpenRouterEnv] {playerPrefKey} overridden from environment ({envName})" +
                  (maskValueInLog ? " - value masked" : $" = {envValue}"));
        return true;
    }

    private static bool TrySetBool(string envName, string playerPrefKey)
    {
        string envValue = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrEmpty(envValue))
        {
            return false;
        }

        if (!TryParseBool(envValue, out bool result))
        {
            Debug.LogWarning($"[OpenRouterEnv] Could not parse boolean environment variable {envName} (value: '{envValue}')");
            return false;
        }

        int current = PlayerPrefs.GetInt(playerPrefKey, result ? 1 : 0);
        if (current == (result ? 1 : 0))
        {
            return false;
        }

        PlayerPrefs.SetInt(playerPrefKey, result ? 1 : 0);
        Debug.Log($"[OpenRouterEnv] {playerPrefKey} overridden from environment ({envName}) = {result}");
        return true;
    }

    private static bool TrySetFloat(string envName, string playerPrefKey)
    {
        string envValue = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrEmpty(envValue))
        {
            return false;
        }

        if (!float.TryParse(envValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
        {
            Debug.LogWarning($"[OpenRouterEnv] Could not parse float environment variable {envName} (value: '{envValue}')");
            return false;
        }

        float current = PlayerPrefs.GetFloat(playerPrefKey, result);
        if (Mathf.Approximately(current, result))
        {
            return false;
        }

        PlayerPrefs.SetFloat(playerPrefKey, result);
        Debug.Log($"[OpenRouterEnv] {playerPrefKey} overridden from environment ({envName}) = {result}");
        return true;
    }

    private static bool TrySetInt(string envName, string playerPrefKey)
    {
        string envValue = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrEmpty(envValue))
        {
            return false;
        }

        if (!int.TryParse(envValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            Debug.LogWarning($"[OpenRouterEnv] Could not parse int environment variable {envName} (value: '{envValue}')");
            return false;
        }

        int current = PlayerPrefs.GetInt(playerPrefKey, result);
        if (current == result)
        {
            return false;
        }

        PlayerPrefs.SetInt(playerPrefKey, result);
        Debug.Log($"[OpenRouterEnv] {playerPrefKey} overridden from environment ({envName}) = {result}");
        return true;
    }

    private static bool TryParseBool(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "t":
            case "yes":
            case "y":
                result = true;
                return true;
            case "0":
            case "false":
            case "f":
            case "no":
            case "n":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    /// <summary>
    /// Expose whether we have already attempted to apply overrides. Mostly useful for tests.
    /// </summary>
    public static bool HasApplied => _hasApplied;
}
