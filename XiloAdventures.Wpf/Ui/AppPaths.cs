using System;
using System.IO;

namespace XiloAdventures.Wpf;

public static class AppPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;
    public static string WorldsFolder => Path.Combine(BaseDirectory, "worlds");
    public static string SavesFolder => Path.Combine(BaseDirectory, "saves");
    public static string SoundFolder => Path.Combine(BaseDirectory, "sound");
    public static string ImagesFolder => Path.Combine(BaseDirectory, "images");
    public static string GlobalConfigPath => Path.Combine(BaseDirectory, "config.xac");
    public static string WorldConfigPath(string worldId) => Path.Combine(BaseDirectory, $"config_{worldId}.xac");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(WorldsFolder);
        Directory.CreateDirectory(SavesFolder);
        Directory.CreateDirectory(SoundFolder);
        Directory.CreateDirectory(ImagesFolder);
    }
}
