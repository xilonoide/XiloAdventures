using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XiloAdventures.Wpf.Common.Services;

public static class DockerService
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private const string OllamaContainerName = "xilo-ollama";
    private const string TtsContainerName = "xilo-tts";

    public static async Task EnsureAllAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report("Comprobando Docker Desktop...");
            await EnsureDockerAvailableAsync(progress, cancellationToken).ConfigureAwait(false);

            progress?.Report("Preparando contenedor de IA (Ollama)...");
            await EnsureOllamaAsync(progress, cancellationToken).ConfigureAwait(false);

            progress?.Report("Descargando modelo llama3 (si es necesario)...");
            await EnsureLlamaModelAsync(progress, cancellationToken).ConfigureAwait(false);

            progress?.Report("Preparando servidor de voz (Coqui TTS)...");
            await EnsureTtsAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public static async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await StopContainerIfExistsAsync(OllamaContainerName, cancellationToken).ConfigureAwait(false);
            await StopContainerIfExistsAsync(TtsContainerName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task StopContainerIfExistsAsync(string name, CancellationToken cancellationToken)
    {
        if (await ContainerExistsAsync(name, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await RunDockerCheckedAsync($"stop {name}", cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Si no podemos parar un contenedor, no bloqueamos la cancelación.
            }
        }
    }

    private static async Task EnsureDockerAvailableAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Primero intentamos hablar con Docker normalmente.
        try
        {
            await RunDockerCheckedAsync("info", cancellationToken).ConfigureAwait(false);
            return;
        }
        catch
        {
            // Si falla, intentamos arrancar Docker Desktop nosotros mismos (si existe).
        }

        if (await TryStartDockerDesktopAsync(progress, TimeSpan.FromMinutes(2), cancellationToken).ConfigureAwait(false))
        {
            // Si hemos conseguido arrancar Docker Desktop y 'docker info' responde, todo OK.
            return;
        }

        // Si llegamos aquí, o Docker no está instalado o no hemos sido capaces de arrancarlo.
        throw new InvalidOperationException(
            "No se ha podido contactar con Docker. Asegúrate de que Docker Desktop está instalado y en ejecución.");
    }

    private static bool TryGetDockerDesktopPath(out string path)
    {
        // Rutas típicas de instalación de Docker Desktop en Windows.
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        string[] candidates =
        {
            Path.Combine(programFiles, "Docker", "Docker", "Docker Desktop.exe"),
            Path.Combine(programFilesX86, "Docker", "Docker", "Docker Desktop.exe")
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    private static async Task<bool> TryStartDockerDesktopAsync(IProgress<string>? progress, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!TryGetDockerDesktopPath(out var exePath))
        {
            // No hemos encontrado Docker Desktop instalado.
            return false;
        }

        try
        {
            progress?.Report("Arrancando Docker Desktop...");
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                }
            };
            process.Start();
        }
        catch
        {
            // Si no podemos arrancarlo, devolvemos false y dejaremos que arriba se muestre el mensaje clásico.
            return false;
        }

        // Esperamos a que el daemon de Docker responda a 'docker info'.
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);

            try
            {
                await RunDockerCheckedAsync("info", cancellationToken).ConfigureAwait(false);
                // Si llegamos aquí sin excepción, Docker ya está operativo.
                return true;
            }
            catch
            {
                // Todavía no ha arrancado, seguimos esperando hasta agotar el timeout.
            }

            progress?.Report("Esperando a que Docker termine de arrancar...");
        }

        return false;
    }

    private static async Task EnsureOllamaAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (await IsContainerRunningAsync(OllamaContainerName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (await ContainerExistsAsync(OllamaContainerName, cancellationToken).ConfigureAwait(false))
        {
            progress?.Report("Arrancando contenedor existente de Ollama...");
            await RunDockerCheckedAsync($"start {OllamaContainerName}", cancellationToken).ConfigureAwait(false);
            return;
        }

        progress?.Report("Descargando imagen de Ollama (la primera vez tarda hasta 15')...");
        await RunDockerCheckedAsync("pull ollama/ollama:latest", cancellationToken).ConfigureAwait(false);

        progress?.Report("Creando contenedor de Ollama...");
        // Creamos el contenedor con volumen persistente para los modelos
        await RunDockerCheckedAsync(
            $"run -d --name {OllamaContainerName} -p 11434:11434 -v {OllamaContainerName}:/root/.ollama ollama/ollama:latest",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureLlamaModelAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        // Siempre intentamos hacer pull; si ya está descargado será rápido.
        progress?.Report("Descargando modelo llama3 dentro del contenedor de Ollama (esto tarda hasta 15' o más)...");
        await RunDockerCheckedAsync($"exec {OllamaContainerName} ollama pull llama3", cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureTtsAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (await IsContainerRunningAsync(TtsContainerName, cancellationToken).ConfigureAwait(false))
        {
            progress?.Report("Coqui TTS ya está en ejecución.");
            return;
        }

        if (await ContainerExistsAsync(TtsContainerName, cancellationToken).ConfigureAwait(false))
        {
            progress?.Report("Arrancando contenedor existente de Coqui TTS...");
            await RunDockerCheckedAsync($"start {TtsContainerName}", cancellationToken).ConfigureAwait(false);

            progress?.Report("Esperando a que Coqui TTS esté listo...");
            await WaitForTtsReadyAsync(progress, cancellationToken).ConfigureAwait(false);
            return;
        }

        progress?.Report("Descargando imagen de Coqui TTS (la primera vez tarda hasta 15')...");
        await RunDockerCheckedAsync("pull ghcr.io/idiap/coqui-tts-cpu:latest", cancellationToken).ConfigureAwait(false);

        progress?.Report("Creando contenedor de Coqui TTS...");
        // Arrancamos el servidor HTTP de TTS en el puerto 5002 con un modelo de un solo hablante
        await RunDockerCheckedAsync(
            $"run -d --name {TtsContainerName} -p 5002:5002 --entrypoint python3 ghcr.io/idiap/coqui-tts-cpu TTS/server/server.py --model_name tts_models/es/css10/vits",
            cancellationToken).ConfigureAwait(false);

        progress?.Report("Esperando a que Coqui TTS esté listo...");
        await WaitForTtsReadyAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForTtsReadyAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        const int maxAttempts = 100;
        var maxDuration = TimeSpan.FromMinutes(2);
        var stopwatch = Stopwatch.StartNew();
        int attempt = 0;

        while (attempt < maxAttempts && stopwatch.Elapsed < maxDuration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;
            try
            {
                progress?.Report($"Esperando disponibilidad de Coqui TTS... (intento {attempt}/{maxAttempts})");

                using var client = new HttpClient
                {
                    BaseAddress = new Uri("http://localhost:5002"),
                    Timeout = TimeSpan.FromSeconds(3)
                };

                var response = await client.GetAsync("api/tts?text=ok", cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                    if (bytes.Length > 0)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout interno de la petición HTTP, reintentamos.
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                // El servidor aún no está listo, reintentamos.
            }

            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Coqui TTS no ha estado disponible tras 100 reintentos o 2 minutos de espera.");
    }

    private static async Task<bool> IsContainerRunningAsync(string name, CancellationToken cancellationToken)
    {
        var output = await RunDockerGetOutputAsync(
            $"ps --filter name={name} --format {{{{.ID}}}}", cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(output);
    }

    private static async Task<bool> ContainerExistsAsync(string name, CancellationToken cancellationToken)
    {
        var output = await RunDockerGetOutputAsync(
            $"ps -a --filter name={name} --format {{{{.ID}}}}", cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(output);
    }

    private static async Task RunDockerCheckedAsync(string args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
        {
            throw new InvalidOperationException("No se ha podido iniciar el comando 'docker'.");
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
                // Ignoramos errores al matar el proceso en cancelación.
            }
        });

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdoutTask, stderrTask).ConfigureAwait(false);

        string stdout = stdoutTask.Result;
        string stderr = stderrTask.Result;

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"Error al ejecutar 'docker {args}': {message}");
        }
    }

    private static async Task<string> RunDockerGetOutputAsync(string args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
        {
            throw new InvalidOperationException("No se ha podido iniciar el comando 'docker'.");
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
                // Ignoramos errores al matar el proceso en cancelación.
            }
        });

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdoutTask, stderrTask).ConfigureAwait(false);

        string stdout = stdoutTask.Result;
        string stderr = stderrTask.Result;

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"Error al ejecutar 'docker {args}': {message}");
        }

        return stdout.Trim();
    }
}
