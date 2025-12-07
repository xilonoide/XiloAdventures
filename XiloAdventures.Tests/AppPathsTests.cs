using System;
using System.IO;
using XiloAdventures.Wpf.Common.Ui;
using Xunit;

public class AppPathsTests
{
    [Fact]
    public void EnsureDirectories_CreatesWorldsAndSaves()
    {
        var worlds = Path.Combine(AppPaths.BaseDirectory, "worlds");
        var saves = Path.Combine(AppPaths.BaseDirectory, "saves");

        if (Directory.Exists(worlds))
            Directory.Delete(worlds, recursive: true);
        if (Directory.Exists(saves))
            Directory.Delete(saves, recursive: true);

        AppPaths.EnsureDirectories();

        Assert.True(Directory.Exists(worlds));
        Assert.True(Directory.Exists(saves));
    }

    [Fact]
    public void WorldConfigPath_IncludesWorldId()
    {
        var id = Guid.NewGuid().ToString("N");
        var path = AppPaths.WorldConfigPath(id);

        Assert.Contains(id, path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".xac", path, StringComparison.OrdinalIgnoreCase);
    }
}
