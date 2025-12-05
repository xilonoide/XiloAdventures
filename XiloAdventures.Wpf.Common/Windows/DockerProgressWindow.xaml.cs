using System;
using System.Threading.Tasks;
using System.Windows;
using XiloAdventures.Wpf.Common.Services;

namespace XiloAdventures.Wpf.Common.Windows;

public sealed class DockerProgressResult
{
    public bool Success { get; }
    public bool Canceled { get; }

    private DockerProgressResult(bool success, bool canceled)
    {
        Success = success;
        Canceled = canceled;
    }

    public static DockerProgressResult Ok() => new(true, false);
    public static DockerProgressResult Failed() => new(false, false);
    public static DockerProgressResult Cancelled() => new(false, true);
}

public partial class DockerProgressWindow : Window
{
    private bool _closingFromCode;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<DockerProgressResult>? _tcs;

    public DockerProgressWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Muestra la ventana de progreso y ejecuta el trabajo indicado mientras está abierta.
    /// Devuelve el estado de la ejecución, indicando si se canceló manualmente.
    /// </summary>
    public async Task<DockerProgressResult> RunAsync()
    {
        _cts = new CancellationTokenSource();
        _tcs = new TaskCompletionSource<DockerProgressResult>();

        Closing += OnClosing;

        Loaded += async (_, _) =>
        {
            var progress = new Progress<string>(msg =>
            {
                DetailText.Text = msg;
            });

            try
            {
                await DockerService.EnsureAllAsync(progress, _cts.Token).ConfigureAwait(true);
                if (_cts.IsCancellationRequested)
                {
                    return;
                }

                _closingFromCode = true;
                _tcs.TrySetResult(DockerProgressResult.Ok());
                Close();
            }
            catch (OperationCanceledException)
            {
                HandleCancellation();
            }
            catch
            {
                _closingFromCode = true;
                _tcs.TrySetResult(DockerProgressResult.Failed());
                Close();
            }
        };

        ShowDialog();

        return await _tcs.Task.ConfigureAwait(true);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closingFromCode)
        {
            return;
        }

        HandleCancellation(initiatedByClosing: true);
    }

    private void HandleCancellation(bool initiatedByClosing = false)
    {
        if (_cts is { IsCancellationRequested: false })
        {
            _cts.Cancel();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await DockerService.StopAllAsync().ConfigureAwait(false);
            }
            catch
            {
                // No bloqueamos el cierre por errores al parar contenedores.
            }
            DockerShutdownHelper.TryShutdownDockerDesktop();
        });

        _tcs?.TrySetResult(DockerProgressResult.Cancelled());

        _closingFromCode = true;
        if (!initiatedByClosing && IsVisible)
        {
            Close();
        }
    }
}
