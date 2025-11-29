using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using XiloAdventures.Engine;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Ui;

namespace XiloAdventures.Wpf.Windows;

public partial class MainWindow : Window
{
    private readonly WorldModel _world;
    private readonly GameEngine _engine;
    private readonly SoundManager _sound;
    private readonly UiSettings _uiSettings;

    private static readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://localhost:11434/")
    };

    private readonly List<string> _commandHistory = new();
    private int _commandHistoryIndex = -1;

    public MainWindow(WorldModel world, GameState state, SoundManager soundManager, UiSettings uiSettings)
    {
        _world = world;
        _sound = soundManager;
        _uiSettings = uiSettings;
        _engine = new GameEngine(world, state, _sound);
        _engine.RoomChanged += Engine_RoomChanged;

        InitializeComponent();
        ApplyUiSettings();

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Title = $"Xilo Adventures - {_world.Game.Title}";
        InputTextBox.Focus();
        AppendText(_engine.DescribeCurrentRoom());
        UpdateStatusPanel();
        UpdateRoomVisuals();
    }

    private void ApplyUiSettings()
    {
        var size = _uiSettings.FontSize;
        OutputTextBox.FontSize = size;
        InputTextBox.FontSize = size;
        RoomTitleText.FontSize = size + 2;
    }

    private void AppendText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var paragraph = new Paragraph(new Run(text))
        {
            Margin = new Thickness(0, 0, 0, 8)
        };
        OutputTextBox.Document.Blocks.Add(paragraph);
        OutputTextBox.ScrollToEnd();
    }


    private async void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Historial de comandos con flechas arriba/abajo
        if (e.Key == Key.Enter)
        {
            var cmd = InputTextBox.Text.Trim();

            // Limpiar siempre el textbox justo después de pulsar ENTER,
            // tanto si el comando es válido como si no.
            InputTextBox.Clear();

            if (string.IsNullOrEmpty(cmd))
            {
                e.Handled = true;
                return;
            }

            // Comando de limpiar la salida a nivel de UI
            var lower = cmd.ToLowerInvariant();
            if (lower is "limpiar" or "cls" or "clear")
            {
                OutputTextBox.Document.Blocks.Clear();
                InputTextBox.Clear();
                e.Handled = true;
                return;
            }

            AppendText($"> {cmd}");

            // Guardar en historial
            _commandHistory.Add(cmd);
            _commandHistoryIndex = _commandHistory.Count;

            // Enviar al motor
            var result = _engine.ProcessCommand(cmd);

            if (_uiSettings.UseLlmForUnknownCommands)
            {
                var trimmed = (result ?? string.Empty).Trim();
                if (string.Equals(trimmed, "No entiendo ese comando.", StringComparison.OrdinalIgnoreCase))
                {
                    string? suggestion = null;
                    SetLlmBusy(true);
                    try
                    {
                        suggestion = await TryAskLlmForUnknownCommandAsync(cmd);
                    }
                    finally
                    {
                        SetLlmBusy(false);
                    }

                    if (!string.IsNullOrWhiteSpace(suggestion))
                    {
                        var trimmedSuggestion = suggestion.Trim();
                        const string cmdPrefix = "CMD:";
                        const string helpPrefix = "AYUDA:";

                        if (trimmedSuggestion.StartsWith(cmdPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var suggestedCommand = trimmedSuggestion.Substring(cmdPrefix.Length).Trim();
                            if (!string.IsNullOrWhiteSpace(suggestedCommand) &&
                                !string.Equals(suggestedCommand, cmd, StringComparison.OrdinalIgnoreCase))
                            {
                                // Mostramos el comando reinterpretado por la IA
                                AppendText($"> {suggestedCommand}");
                                result = _engine.ProcessCommand(suggestedCommand);
                            }
                        }
                        else if (trimmedSuggestion.StartsWith(helpPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var helpText = trimmedSuggestion.Substring(helpPrefix.Length).Trim();
                            if (!string.IsNullOrWhiteSpace(helpText))
                            {
                                // Mostramos solo la ayuda de la IA y omitimos el mensaje genérico de error.
                                AppendText(helpText);
                                result = null;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(result))
                AppendText(result);

            UpdateStatusPanel();
            UpdateRoomVisuals();

            try
            {
                SaveManager.AutoSave(_engine.State, AppPaths.SavesFolder);
            }
            catch
            {
                // Ignoramos errores de autosave
            }

            InputTextBox.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (_commandHistory.Count == 0)
            {
                e.Handled = true;
                return;
            }

            _commandHistoryIndex--;
            if (_commandHistoryIndex < 0)
                _commandHistoryIndex = 0;

            InputTextBox.Text = _commandHistory[_commandHistoryIndex];
            InputTextBox.CaretIndex = InputTextBox.Text.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (_commandHistory.Count == 0)
            {
                e.Handled = true;
                return;
            }

            _commandHistoryIndex++;
            if (_commandHistoryIndex >= _commandHistory.Count)
            {
                _commandHistoryIndex = _commandHistory.Count;
                InputTextBox.Clear();
            }
            else
            {
                InputTextBox.Text = _commandHistory[_commandHistoryIndex];
                InputTextBox.CaretIndex = InputTextBox.Text.Length;
            }

            e.Handled = true;
        }
        else if (e.Key == Key.PageUp || e.Key == Key.PageDown)
        {
            // Reenviamos PageUp/PageDown al RichTextBox para permitir scroll desde el input
            OutputTextBox.Focus();
            var routed = new KeyEventArgs(e.KeyboardDevice, e.InputSource, e.Timestamp, e.Key)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            InputManager.Current.ProcessInput(routed);
            e.Handled = true;
        }
    }





    private void SetLlmBusy(bool isBusy)
    {
        if (LlmSpinner is null)
            return;

        LlmSpinner.Visibility = isBusy
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
    }

    private async System.Threading.Tasks.Task<string?> TryAskLlmForUnknownCommandAsync(string originalCommand)
    {
        try
        {
            var prompt = BuildLlmPrompt(originalCommand);

            var payload = new
            {
                model = "llama3",
                prompt,
                stream = false
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("api/generate", content);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (doc.RootElement.TryGetProperty("response", out var responseProp))
            {
                var answer = responseProp.GetString();
                if (!string.IsNullOrWhiteSpace(answer))
                {
                    // Tomamos solo la primera línea no vacía como comando sugerido.
                    var firstLine = answer
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault();
                    return firstLine?.Trim();
                }
            }

            // Si no hay respuesta útil, no sugerimos nada.
            return null;
        }
        catch (HttpRequestException)
        {
            HandleLlmConnectionError();
            return null;
        }
        catch (Exception ex)
        {
            new AlertWindow($"Se produjo un error al consultar el modelo IA:\n{ex.Message}", "Error IA")
            {
                Owner = this
            }.ShowDialog();
            return null;
        }
    }

    private string BuildLlmPrompt(string originalCommand)
    {
        // Le damos contexto al modelo y le pedimos que devuelva un comando o un mensaje de ayuda.
        var roomDescription = _engine.DescribeCurrentRoom();

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Eres un asistente que traduce el texto libre del jugador en comandos válidos de un juego de aventuras de texto en español.");
        promptBuilder.AppendLine("El parser interno del juego no ha entendido el comando del jugador.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Contexto del lugar donde está el jugador:");
        promptBuilder.AppendLine(roomDescription);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"Comando original del jugador: \"{originalCommand}\"");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Si puedes convertir razonablemente el texto del jugador en un comando del juego, responde en UNA SOLA línea con el formato:");
        promptBuilder.AppendLine("CMD: <comando>");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Si no puedes convertirlo razonablemente (porque es ruido, no tiene sentido o no está claro a qué se refiere), responde en UNA SOLA línea y con el formato:");
        promptBuilder.AppendLine("AYUDA: <una respuesta ocurrente si ves que no tiene nada que ver con el contexto, o una pregunta o consejo muy corto sobre qué podría escribir el jugador>");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Cuando el jugador use pronombres como \"lo\", \"la\", \"los\", \"las\", \"él\", \"ella\" o similares, intenta deducir a qué objeto o personaje se refiere usando el contexto. Es preferible elegir el objeto o personaje más relevante o mencionado en la descripción actual.");
        promptBuilder.AppendLine("Si el comando del jugador es ambiguo y podría referirse a varios objetos (por ejemplo, dos llaves distintas), usa AYUDA: para hacer una pregunta de clarificación, como por ejemplo: AYUDA: ¿Te refieres a la llave de hierro o a la llave dorada?");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Usa solo comandos típicos de aventuras de texto, por ejemplo: mirar, mirar alrededor, examinar <objeto>, ir norte/sur/este/oeste, abrir <algo>, usar <objeto> con <otro>, coger <objeto>, hablar con <personaje>, inventario.");
        promptBuilder.AppendLine("No inventes mecánicas nuevas ni comandos que no sean verbos habituales de aventuras de texto.");
        promptBuilder.AppendLine("No añadas explicaciones fuera de los formatos CMD: o AYUDA:; no devuelvas varias líneas ni listas de comandos.");
        return promptBuilder.ToString();
    }

    private void HandleLlmConnectionError()
    {
        var composePath = System.IO.Path.Combine(AppContext.BaseDirectory, "docker-compose.yml");
        bool composeStarted = false;

        if (System.IO.File.Exists(composePath))
        {
            composeStarted = TryStartDockerCompose(composePath);
        }

        string message;
        if (composeStarted)
        {
            message =
                "No se ha podido contactar con el modelo IA en http://localhost:11434.\n\n" +
                "He intentado arrancar el servicio usando 'docker compose up' con el fichero docker-compose.yml.\n" +
                "Asegúrate de que Docker Desktop está instalado y ejecutándose y vuelve a probar el comando.";
        }
        else
        {
            message =
                "No se ha podido contactar con el modelo IA en http://localhost:11434.\n\n" +
                "Debes tener Docker Desktop instalado y en ejecución, y el comando 'docker compose' disponible, " +
                "para poder usar esta opción.";
        }

        new AlertWindow(message, "IA no disponible")
        {
            Owner = this
        }.ShowDialog();
    }

    private bool TryStartDockerCompose(string composeFilePath)
    {
        try
        {
            var candidates = new (string FileName, string Arguments)[]
            {
                ("docker", $"compose -f \"{composeFilePath}\" up -d"),
                ("docker-compose", $"-f \"{composeFilePath}\" up -d")
            };

            foreach (var candidate in candidates)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate.FileName,
                    Arguments = candidate.Arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    continue;

                if (!process.WaitForExit(10000))
                {
                    try { process.Kill(); } catch { }
                    continue;
                }

                if (process.ExitCode == 0)
                    return true;
            }
        }
        catch
        {
            // Ignoramos errores aquí; el método devolverá false.
        }

        return false;
    }

    private static bool _IsSaveOrLoadCommand(string cmd)
        => cmd.StartsWith("guardar") || cmd.StartsWith("cargar");

    private void UpdateStatusPanel()
    {
        StatsLabel.Text = _engine.DescribePlayerStats();
        InventoryLabel.Text = _engine.DescribeInventory();

        try
        {
            var gameTime = _engine.State.GameTime;
            var tod = gameTime.TimeOfDay;
            string periodo = (tod.Hours >= 21 || tod.Hours < 7) ? "Noche" : "Día";
            var weather = _engine.State.Weather.ToString().ToLowerInvariant();
            TimeLabel.Text = $"{gameTime:HH:mm} ({periodo}, {weather})";
        }
        catch
        {
            TimeLabel.Text = string.Empty;
        }
    }

    private void Engine_RoomChanged(Room obj)
    {
        UpdateRoomVisuals();
    }


    private void UpdateRoomVisuals()
    {
        var room = _engine.CurrentRoom;
        if (room == null)
            return;

        RoomTitleText.Text = room.Name;

        // Imagen de la sala desde el propio mundo (Base64)
        if (!string.IsNullOrWhiteSpace(room.ImageBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(room.ImageBase64);
                using var ms = new MemoryStream(bytes);

                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                RoomImage.Source = bmp;
            }
            catch
            {
                RoomImage.Source = null;
            }
        }
        else
        {
            RoomImage.Source = null;
        }
    }

    private void SaveMenu_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Guardar partida",
            Filter = "Partidas guardadas (*.xas)|*.xas|Todos los archivos (*.*)|*.*",
            InitialDirectory = AppPaths.SavesFolder,
            FileName = $"{_engine.State.WorldId}_partida.xas"
        };

        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                SaveManager.SaveToPath(_engine.State, dlg.FileName);
            }
            catch (Exception ex)
            {
                new AlertWindow($"Error al guardar partida:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
            }
        }
    }

    private void LoadMenu_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Cargar partida",
            Filter = "Partidas guardadas (*.xas)|*.xas|Todos los archivos (*.*)|*.*",
            InitialDirectory = AppPaths.SavesFolder
        };

        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                var newState = SaveManager.LoadFromPath(dlg.FileName, _world);
                var newWindow = new MainWindow(_world, newState, _sound, _uiSettings);
                newWindow.Owner = Owner;
                Close();
                newWindow.Show();
            }
            catch (Exception ex)
            {
                new AlertWindow($"Error al cargar partida:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
            }
        }
    }

    private void OptionsMenu_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OptionsWindow(_uiSettings, OnOptionsChanged, _world.Game.Id);
        dlg.Owner = this;
        dlg.ShowDialog();
    }

    private void OnOptionsChanged(UiSettings settings)
    {
        // Aplicar cambios en vivo
        _uiSettings.SoundEnabled = settings.SoundEnabled;
        _uiSettings.FontSize = settings.FontSize;
        _uiSettings.UseLlmForUnknownCommands = settings.UseLlmForUnknownCommands;

        _sound.SoundEnabled = settings.SoundEnabled;
        ApplyUiSettings();

        UiSettingsManager.SaveForWorld(_engine.State.WorldId, _uiSettings);
    }

    private void ExitMenu_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }


    private void AboutMenu_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow
        {
            Owner = this
        };
        about.ShowDialog();
    }


    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        var dlg = new ConfirmWindow(
            "¿Quieres salir de la partida? Puedes guardar antes de salir desde Archivo -> Guardar partida...",
            "Salir")
        {
            Owner = this
        };

        var result = dlg.ShowDialog() == true;

        if (!result)
        {
            e.Cancel = true;
            return;
        }

        // Al cerrar la ventana de partida detenemos toda la música.
        try
        {
            _sound.StopMusic();
            _sound.Dispose();
        }
        catch
        {
            // Ignorar errores al cerrar el sonido.
        }

        base.OnClosing(e);
    }
}