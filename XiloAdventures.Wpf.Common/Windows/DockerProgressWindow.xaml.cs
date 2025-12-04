using System;
using System.Threading.Tasks;
using System.Windows;
using XiloAdventures.Wpf.Common.Services;

namespace XiloAdventures.Wpf.Common.Windows;

public partial class DockerProgressWindow : Window
{
    public DockerProgressWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Muestra la ventana de progreso y ejecuta el trabajo indicado mientras está abierta.
    /// Devuelve true si todo ha ido bien, false si ha fallado.
    /// </summary>
    public async Task<bool> RunAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        Loaded += async (_, _) =>
        {
            var progress = new Progress<string>(msg =>
            {
                DetailText.Text = msg;
            });

            try
            {
                await DockerService.EnsureAllAsync(progress).ConfigureAwait(true);
                tcs.TrySetResult(true);
                DialogResult = true;
                Close();
            }
            catch
            {
                tcs.TrySetResult(false);
                DialogResult = false;
                Close();
            }
        };

        ShowDialog();

        return await tcs.Task.ConfigureAwait(true);
    }
}
