using System.Windows;
using XiloAdventures.Wpf.Common.Ui;
using XiloAdventures.Wpf.Player.Windows;
using XiloAdventures.Wpf.Common.Services;

namespace XiloAdventures.Wpf.Player;

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

    protected override void OnExit(ExitEventArgs e)
    {
        // Al cerrar la aplicación intentamos cerrar Docker Desktop por completo.
        try
        {
            DockerShutdownHelper.TryShutdownDockerDesktop();
        }
        catch
        {
            // Ignoramos cualquier error; no queremos bloquear el cierre de la app.
        }

        base.OnExit(e);
    }
}
