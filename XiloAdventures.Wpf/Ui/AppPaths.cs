using System;
using System.IO;

namespace XiloAdventures.Wpf;

public static class AppPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;
    public static string WorldsFolder => Path.Combine(BaseDirectory, "worlds");
    public static string SavesFolder => Path.Combine(BaseDirectory, "saves");
    public static string SoundFolder => Path.Combine(BaseDirectory, "Sound");
    public static string GlobalConfigPath => Path.Combine(BaseDirectory, "config.json");
    public static string WorldConfigPath(string worldId) => Path.Combine(BaseDirectory, $"config_{worldId}.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(WorldsFolder);
        Directory.CreateDirectory(SavesFolder);
        Directory.CreateDirectory(SoundFolder);
    }
}
