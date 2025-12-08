using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using XiloAdventures.Engine;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Common.Services;
using XiloAdventures.Wpf.Common.Ui;
using XiloAdventures.Wpf.Common.Windows;

namespace XiloAdventures.Wpf.Player;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Capturar excepciones no manejadas
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            LogError($"Excepción no manejada en AppDomain: {ex}");
            MessageBox.Show(
                $"Error crítico no manejado:\n{ex?.Message}\n\nStack:\n{ex?.StackTrace}\n\nVer log en el escritorio.",
                "Error crítico",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            LogError($"Excepción no manejada en Dispatcher: {args.Exception}");
            MessageBox.Show(
                $"Error en la interfaz:\n{args.Exception.Message}\n\nStack:\n{args.Exception.StackTrace}\n\nVer log en el escritorio.",
                "Error en UI",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            LogError($"Excepción no observada en Task: {args.Exception}");
            args.SetObserved();
        };

        try
        {
            // Cargar el mundo embebido
            var world = LoadEmbeddedWorld();

            if (world == null)
            {
                LogError("El mundo es null después de LoadEmbeddedWorld");
                MessageBox.Show(
                    "No se pudo cargar el mundo del juego.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // Iniciar el juego
            LogError("Creando ventana de juego...");

            // Configurar el parser
            Parser.SetWorldDictionary(world.Game.ParserDictionaryJson);

            // Crear el estado inicial del juego
            var state = WorldLoader.CreateInitialState(world);

            // Crear el SoundManager (deshabilitado para ejecutables standalone)
            var soundManager = new SoundManager
            {
                SoundEnabled = false
            };

            // Configuración de UI (sin IA para ejecutables standalone)
            var uiSettings = new UiSettings
            {
                FontSize = 18,
                UseLlmForUnknownCommands = false
            };

            // Crear y mostrar la ventana de juego (isRunningFromEditor = false)
            var window = new MainWindow(world, state, soundManager, uiSettings, isRunningFromEditor: false);
            LogError("Ventana creada, mostrando...");
            window.Show();
            LogError("Ventana mostrada exitosamente");
        }
        catch (Exception ex)
        {
            LogError($"Error en OnStartup: {ex}");
            MessageBox.Show(
                $"Error al iniciar el juego: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nSe ha guardado un log en el escritorio.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void LogError(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "XiloAdventures_Error.log");
            File.AppendAllText(logPath, $"{DateTime.Now}: {message}\n\n");
        }
        catch
        {
            // Si no se puede escribir el log, ignorar
        }
    }

    private WorldModel? LoadEmbeddedWorld()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "XiloAdventures.Wpf.Player.world.xaw";

            LogError($"Buscando recurso: {resourceName}");
            LogError($"Recursos disponibles: {string.Join(", ", assembly.GetManifestResourceNames())}");

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                LogError("Stream del recurso es null");
                MessageBox.Show(
                    "No se encontró el mundo embebido en el ejecutable.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return null;
            }

            LogError($"Recurso encontrado, tamaño del stream: {stream.Length}");

            // Extraer el recurso a un archivo temporal para poder usar WorldLoader
            var tempPath = Path.Combine(Path.GetTempPath(), "temp_world.xaw");
            LogError($"Extrayendo a: {tempPath}");

            using (var fileStream = File.Create(tempPath))
            {
                stream.CopyTo(fileStream);
            }

            LogError($"Archivo temporal creado, tamaño: {new FileInfo(tempPath).Length}");

            try
            {
                // Usar WorldLoader para cargar correctamente el archivo (maneja Base64/ZIP)
                LogError("Llamando a WorldLoader.LoadWorldModel...");
                var world = WorldLoader.LoadWorldModel(tempPath);
                LogError($"Mundo cargado exitosamente. Título: {world?.Game?.Title}");
                return world;
            }
            finally
            {
                // Limpiar el archivo temporal
                try { File.Delete(tempPath); } catch { }
            }
        }
        catch (Exception ex)
        {
            LogError($"Error en LoadEmbeddedWorld: {ex}");
            MessageBox.Show(
                $"Error al cargar el mundo: {ex.Message}\n\nDetalles: {ex.GetType().Name}\n\nSe ha guardado un log en el escritorio.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return null;
        }
    }
}
