using System.Windows;
using XiloAdventures.Wpf.Ui;
using XiloAdventures.Wpf.Windows;

namespace XiloAdventures.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureDirectories();
        UiSettingsManager.LoadGlobal();

        var startup = new StartupWindow();
        startup.Show();
    }
}
