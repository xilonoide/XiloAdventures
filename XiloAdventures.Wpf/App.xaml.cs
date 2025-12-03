using System.Windows;
using XiloAdventures.Wpf.Ui;
using XiloAdventures.Wpf.Windows;
using XiloAdventures.Wpf.Services;

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
