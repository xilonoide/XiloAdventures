using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using XiloAdventures.Engine;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Common.Services;
using XiloAdventures.Wpf.Common.Ui;
using XiloAdventures.Wpf.Common.Windows;

namespace XiloAdventures.Wpf.Player;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Capturar excepciones no manejadas
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show(
                $"Error crítico no manejado:\n{ex?.Message}\n\nStack:\n{ex?.StackTrace}",
                "Error crítico",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show(
                $"Error en la interfaz:\n{args.Exception.Message}\n\nStack:\n{args.Exception.StackTrace}",
                "Error en UI",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            args.SetObserved();
        };

        // Mostrar splash screen
        var splash = new SplashWindow();
        splash.Show();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Cargar el mundo embebido en background
            WorldModel? world = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                world = LoadEmbeddedWorld();
            });

            if (world == null)
            {
                splash.Close();
                MessageBox.Show(
                    "No se pudo cargar el mundo del juego.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // Configurar el parser
            Parser.SetWorldDictionary(world.Game.ParserDictionaryJson);

            // Intentar cargar autoguardado, si no existe crear estado inicial
            GameState state;
            try
            {
                var autosaveFileName = $"autosave_{world.Game.Id}.xas";
                var autosavePath = Path.Combine(AppPaths.SavesFolder, autosaveFileName);

                if (File.Exists(autosavePath))
                {
                    state = SaveManager.LoadFromPath(autosavePath, world);

                    // Validar que el autoguardado pertenece al mundo actual
                    if (!string.Equals(state.WorldId, world.Game.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        state = WorldLoader.CreateInitialState(world);
                    }
                }
                else
                {
                    state = WorldLoader.CreateInitialState(world);
                }
            }
            catch
            {
                state = WorldLoader.CreateInitialState(world);
            }

            // Configuración de UI: intentar cargar desde archivo, sino desactivar IA
            var uiSettings = TryLoadConfigOrDefault(world.Game.Id);

            // Crear el SoundManager y aplicar configuración desde uiSettings
            var soundManager = new SoundManager
            {
                SoundEnabled = uiSettings.SoundEnabled,
                MusicVolume = (float)(uiSettings.MusicVolume / 10.0),
                EffectsVolume = (float)(uiSettings.EffectsVolume / 10.0),
                MasterVolume = (float)(uiSettings.MasterVolume / 10.0),
                VoiceVolume = (float)(uiSettings.VoiceVolume / 10.0)
            };
            soundManager.RefreshVolumes();

            // Crear la ventana de juego
            var window = new MainWindow(world, state, soundManager, uiSettings, isRunningFromEditor: false);

            // Precargar la voz de la sala inicial antes de mostrar la ventana,
            // para que se escuche nada más entrar.
            if (uiSettings.SoundEnabled && uiSettings.VoiceVolume > 0)
            {
                try
                {
                    var startRoom = state.Rooms
                        .FirstOrDefault(r => r.Id.Equals(state.CurrentRoomId, StringComparison.OrdinalIgnoreCase));
                    if (startRoom != null && !string.IsNullOrWhiteSpace(startRoom.Description))
                    {
                        await soundManager.PreloadRoomVoiceAsync(startRoom.Id, startRoom.Description);
                    }
                }
                catch
                {
                    // Si algo falla al precargar la voz, continuamos sin interrumpir el inicio del juego.
                }
            }

            // Asegurar mínimo 2 segundos de splash
            var elapsed = stopwatch.ElapsedMilliseconds;
            if (elapsed < 2000)
            {
                await System.Threading.Tasks.Task.Delay((int)(2000 - elapsed));
            }

            // Cerrar splash y mostrar ventana principal
            splash.Close();
            window.Show();
        }
        catch (Exception ex)
        {
            splash.Close();
            MessageBox.Show(
                $"Error al iniciar el juego: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private UiSettings TryLoadConfigOrDefault(string worldId)
    {
        try
        {
            // Buscar archivo de configuración en la misma carpeta del ejecutable
            var exeDir = AppContext.BaseDirectory;
            var configPath = Path.Combine(exeDir, "config.xac");
            if (File.Exists(configPath))
            {
                var json = CryptoUtil.DecryptFromFile(configPath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<UiSettings>(json) ?? new UiSettings();
                settings.FontSize = 18; // Forzar tamaño de fuente
                return settings;
            }
        }
        catch
        {
            // Si hay error al cargar config, continuar con valores por defecto
        }

        // Si no hay config, IA desactivada por defecto
        return new UiSettings { FontSize = 18, UseLlmForUnknownCommands = false };
    }

    private WorldModel? LoadEmbeddedWorld()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "XiloAdventures.Wpf.Player.world.xaw";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return null;
            }

            // Extraer el recurso a un archivo temporal para poder usar WorldLoader
            var tempPath = Path.Combine(Path.GetTempPath(), "temp_world.xaw");

            using (var fileStream = File.Create(tempPath))
            {
                stream.CopyTo(fileStream);
            }

            try
            {
                // Usar WorldLoader para cargar correctamente el archivo (maneja Base64/ZIP)
                return WorldLoader.LoadWorldModel(tempPath);
            }
            finally
            {
                // Limpiar el archivo temporal
                try { File.Delete(tempPath); } catch { }
            }
        }
        catch
        {
            return null;
        }
    }
}
