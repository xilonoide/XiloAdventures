using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace XiloAdventures.Wpf.Services;

public static class DockerService
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private const string OllamaContainerName = "xilo-ollama";
    private const string TtsContainerName = "xilo-tts";

    public static async Task EnsureAllAsync(IProgress<string>? progress = null)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            progress?.Report("Comprobando Docker Desktop...");
            await EnsureDockerAvailableAsync().ConfigureAwait(false);

            progress?.Report("Preparando contenedor de IA (Ollama)...");
            await EnsureOllamaAsync(progress).ConfigureAwait(false);

            progress?.Report("Descargando modelo llama3 (si es necesario)...");
            await EnsureLlamaModelAsync(progress).ConfigureAwait(false);

            progress?.Report("Preparando servidor de voz (Coqui TTS)...");
            await EnsureTtsAsync(progress).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task EnsureDockerAvailableAsync()
    {
        try
        {
            await RunDockerCheckedAsync("info").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "No se ha podido contactar con Docker. Asegúrate de que Docker Desktop está instalado y en ejecución.",
                ex);
        }
    }

    private static async Task EnsureOllamaAsync(IProgress<string>? progress)
    {
        if (await IsContainerRunningAsync(OllamaContainerName).ConfigureAwait(false))
        {
            return;
        }

        if (await ContainerExistsAsync(OllamaContainerName).ConfigureAwait(false))
        {
            progress?.Report("Arrancando contenedor existente de Ollama...");
            await RunDockerCheckedAsync($"start {OllamaContainerName}").ConfigureAwait(false);
            return;
        }

        progress?.Report("Descargando imagen de Ollama (la primera vez tarda un poco)...");
        await RunDockerCheckedAsync("pull ollama/ollama:latest").ConfigureAwait(false);

        progress?.Report("Creando contenedor de Ollama...");
        // Creamos el contenedor con volumen persistente para los modelos
        await RunDockerCheckedAsync(
            $"run -d --name {OllamaContainerName} -p 11434:11434 -v {OllamaContainerName}:/root/.ollama ollama/ollama:latest").ConfigureAwait(false);
    }

    private static async Task EnsureLlamaModelAsync(IProgress<string>? progress)
    {
        // Siempre intentamos hacer pull; si ya está descargado será rápido.
        progress?.Report("Comprobando modelo llama3 dentro del contenedor de Ollama...");
        await RunDockerCheckedAsync($"exec {OllamaContainerName} ollama pull llama3").ConfigureAwait(false);
    }

    private static async Task EnsureTtsAsync(IProgress<string>? progress)
    {
        if (await IsContainerRunningAsync(TtsContainerName).ConfigureAwait(false))
        {
            progress?.Report("Coqui TTS ya está en ejecución.");
            return;
        }

        if (await ContainerExistsAsync(TtsContainerName).ConfigureAwait(false))
        {
            progress?.Report("Arrancando contenedor existente de Coqui TTS...");
            await RunDockerCheckedAsync($"start {TtsContainerName}").ConfigureAwait(false);

            progress?.Report("Esperando a que Coqui TTS esté listo...");
            await WaitForTtsReadyAsync(progress).ConfigureAwait(false);
            return;
        }

        progress?.Report("Descargando imagen de Coqui TTS (la primera vez tarda un poco)...");
        await RunDockerCheckedAsync("pull ghcr.io/idiap/coqui-tts-cpu:latest").ConfigureAwait(false);

        progress?.Report("Creando contenedor de Coqui TTS...");
        // Arrancamos el servidor HTTP de TTS en el puerto 5002 con un modelo de un solo hablante
        await RunDockerCheckedAsync(
            $"run -d --name {TtsContainerName} -p 5002:5002 --entrypoint python3 ghcr.io/idiap/coqui-tts-cpu TTS/server/server.py --model_name tts_models/es/css10/vits").ConfigureAwait(false);

        progress?.Report("Esperando a que Coqui TTS esté listo...");
        await WaitForTtsReadyAsync(progress).ConfigureAwait(false);
    }


    private static async Task WaitForTtsReadyAsync(IProgress<string>? progress)
    {
        const int maxAttempts = 100;
        var maxDuration = TimeSpan.FromMinutes(2);
        var stopwatch = Stopwatch.StartNew();
        int attempt = 0;

        while (attempt < maxAttempts && stopwatch.Elapsed < maxDuration)
        {
            attempt++;
            try
            {
                progress?.Report($"Esperando disponibilidad de Coqui TTS... (intento {attempt}/{maxAttempts})");

                using var client = new HttpClient
                {
                    BaseAddress = new Uri("http://localhost:5002")
                };
                client.Timeout = TimeSpan.FromSeconds(3);

                var response = await client.GetAsync("api/tts?text=ok").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (bytes.Length > 0)
                    {
                        return;
                    }
                }
            }
            catch (HttpRequestException)
            {
                // El servidor aún no está listo, reintentamos.
            }
            catch (TaskCanceledException)
            {
                // Timeout de la petición HTTP, volvemos a intentar.
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Coqui TTS no ha estado disponible tras 100 reintentos o 2 minutos de espera.");
    }

private static async Task<bool> IsContainerRunningAsync(string name)
    {
        var output = await RunDockerGetOutputAsync(
            $"ps --filter name={name} --format {{{{.ID}}}}").ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(output);
    }

    private static async Task<bool> ContainerExistsAsync(string name)
    {
        var output = await RunDockerGetOutputAsync(
            $"ps -a --filter name={name} --format {{{{.ID}}}}").ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(output);
    }

    private static async Task RunDockerCheckedAsync(string args)
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

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        string stdout = stdoutTask.Result;
        string stderr = stderrTask.Result;

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"Error al ejecutar 'docker {args}': {message}");
        }
    }


    private static async Task<string> RunDockerGetOutputAsync(string args)
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

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

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
