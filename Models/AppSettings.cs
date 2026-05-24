using System.IO;
using System.Text.Json;

namespace WhisperPtt.Models;

public class AppSettings
{
    private static AppSettings? _instance;

    /// <summary>
    /// Singleton instance loaded at startup. Call Load() first.
    /// </summary>
    public static AppSettings Instance => _instance ?? throw new InvalidOperationException("AppSettings not loaded. Call Load() first.");

    public string SelectedModel { get; set; } = "base";
    public string SelectedLanguage { get; set; } = "ru";
    public string SelectedAudioDevice { get; set; } = "Системный по умолчанию";
    public string CustomPrompt { get; set; } = "";
    public int UnloadTimeoutMinutes { get; set; } = 10;
    public string Hotkey { get; set; } = "F2";

    // Alias properties used across the codebase
    [System.Text.Json.Serialization.JsonIgnore]
    public string Model
    {
        get => SelectedModel;
        set => SelectedModel = value;
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string Language
    {
        get => SelectedLanguage;
        set => SelectedLanguage = value;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public static string GetAppDataDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "WhisperPtt") + Path.DirectorySeparatorChar;
    }

    public static string GetModelsDir()
    {
        return Path.Combine(GetAppDataDir(), "Models") + Path.DirectorySeparatorChar;
    }

    /// <summary>Alias for GetModelsDir().</summary>
    public static string ModelsDirectory => GetModelsDir();

    public static readonly Dictionary<string, string> ModelFileNames = new()
    {
        ["tiny"] = "ggml-tiny.bin",
        ["base"] = "ggml-base.bin",
        ["small"] = "ggml-small.bin",
        ["medium"] = "ggml-medium.bin",
        ["large-v3-turbo"] = "ggml-large-v3-turbo.bin",
        ["large-v3"] = "ggml-large-v3.bin"
    };

    public static string ResolveModelFileName(string modelName)
    {
        // Fallback for legacy "large" selection to "large-v3-turbo"
        if (modelName == "large")
        {
            modelName = "large-v3-turbo";
        }

        if (ModelFileNames.TryGetValue(modelName, out var fileName))
        {
            return fileName;
        }
        return $"ggml-{modelName}.bin";
    }

    public static string GetSettingsPath()
    {
        return Path.Combine(GetAppDataDir(), "settings.json");
    }

    public static AppSettings Load()
    {
        var path = GetSettingsPath();
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                if (settings is not null)
                {
                    _instance = settings;
                    return settings;
                }
            }
        }
        catch (Exception)
        {
            // If loading fails, fall through to create defaults
        }

        var defaults = new AppSettings();
        defaults.Save();
        _instance = defaults;
        return defaults;
    }

    public void Save()
    {
        var dir = GetAppDataDir();
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, _jsonOptions);
        File.WriteAllText(GetSettingsPath(), json);
    }

    public static Dictionary<string, string> GetDefaultPrompts()
    {
        return new Dictionary<string, string>
        {
            ["ru"] = "Разговорная речь, правильная пунктуация. Текст на русском языке.",
            ["en"] = "Conversational speech, proper punctuation. Text in English."
        };
    }

    public string GetEffectivePrompt()
    {
        if (!string.IsNullOrWhiteSpace(CustomPrompt))
            return CustomPrompt;

        var defaults = GetDefaultPrompts();
        return defaults.TryGetValue(SelectedLanguage, out var prompt) ? prompt : "";
    }
}
