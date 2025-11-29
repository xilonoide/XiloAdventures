using System;
using System.Text.Json;
using XiloAdventures.Engine;

namespace XiloAdventures.Wpf.Ui;

public class UiSettings
{
    public bool SoundEnabled { get; set; } = true;
    public double FontSize { get; set; } = 14.0;
    /// <summary>
    /// Si está activo, al no entender un comando se consultará un LLM local.
    /// </summary>
    public bool UseLlmForUnknownCommands { get; set; } = false;
}

public static class UiSettingsManager
{
    public static UiSettings GlobalSettings { get; private set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void LoadGlobal()
    {
        var path = AppPaths.GlobalConfigPath;
        try
        {
            if (System.IO.File.Exists(path))
            {
                var json = CryptoUtil.DecryptFromFile(path);
                GlobalSettings = JsonSerializer.Deserialize<UiSettings>(json, Options) ?? new UiSettings();
            }
        }
        catch
        {
            GlobalSettings = new UiSettings();
        }
    }

    public static void SaveGlobal()
    {
        var path = AppPaths.GlobalConfigPath;
        var json = JsonSerializer.Serialize(GlobalSettings, Options);
        CryptoUtil.EncryptToFile(path, json, "xac");
    }

    public static UiSettings LoadForWorld(string worldId)
    {
        var path = AppPaths.WorldConfigPath(worldId);
        try
        {
            if (System.IO.File.Exists(path))
            {
                var json = CryptoUtil.DecryptFromFile(path);
                return JsonSerializer.Deserialize<UiSettings>(json, Options) ?? new UiSettings();
            }
        }
        catch
        {
            // ignorar y devolver por defecto
        }

        // Por defecto heredamos de las globales
        return new UiSettings
        {
            SoundEnabled = GlobalSettings.SoundEnabled,
            FontSize = GlobalSettings.FontSize,
            UseLlmForUnknownCommands = GlobalSettings.UseLlmForUnknownCommands
        };
    }

    public static void SaveForWorld(string worldId, UiSettings settings)
    {
        var path = AppPaths.WorldConfigPath(worldId);
        var json = JsonSerializer.Serialize(settings, Options);
        CryptoUtil.EncryptToFile(path, json, "xac");
    }
}
