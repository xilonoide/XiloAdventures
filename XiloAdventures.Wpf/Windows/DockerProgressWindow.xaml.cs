using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using XiloAdventures.Wpf.Services;

namespace XiloAdventures.Wpf.Windows;

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
    private const string OllamaContainerName = "xilo-ollama";
    private readonly TimeSpan _logPollingInterval = TimeSpan.FromSeconds(2);

    private bool _closingFromCode;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _logCts;
    private Task? _logTask;
    private string _lastDockerLogs = string.Empty;
    private int _logStepCounter;
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

            _logCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            _logTask = MonitorOllamaLogsAsync(_logCts.Token);

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
            finally
            {
                _logCts?.Cancel();
                if (_logTask is { } logTask)
                {
                    try
                    {
                        await logTask.ConfigureAwait(true);
                    }
                    catch (OperationCanceledException)
                    {
                        // expected when the watcher is cancelled.
                    }
                }
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
        if (_logCts is { IsCancellationRequested: false })
        {
            _logCts.Cancel();
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

    private async Task MonitorOllamaLogsAsync(CancellationToken cancellationToken)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            StepText.Visibility = Visibility.Visible;
            StepText.Text = "Paso 1";
        }).Task.ConfigureAwait(true);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? logs = null;
            try
            {
                logs = await TryReadDockerLogsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(logs) && logs != _lastDockerLogs)
            {
                _lastDockerLogs = logs;
                _logStepCounter++;
                await Dispatcher.InvokeAsync(() =>
                {
                    StepText.Text = $"Paso {_logStepCounter}";
                }).Task.ConfigureAwait(true);
            }

            try
            {
                await Task.Delay(_logPollingInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task<string?> TryReadDockerLogsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"logs --tail 20 {OllamaContainerName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            if (!process.Start())
            {
                return null;
            }

            using var reg = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch
                {
                    // ignore kill failures
                }
            });

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdoutTask, stderrTask).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                return null;
            }

            var logs = stdoutTask.Result.Trim();
            return string.IsNullOrWhiteSpace(logs) ? null : logs;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
