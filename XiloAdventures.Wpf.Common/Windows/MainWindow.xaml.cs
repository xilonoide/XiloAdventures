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
using System.Windows.Navigation;
using Microsoft.Win32;
using XiloAdventures.Engine;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Common.Ui;
using XiloAdventures.Wpf.Common.Services;
using XiloAdventures.Wpf.Common.Windows;

namespace XiloAdventures.Wpf.Common.Windows;

public partial class MainWindow : Window
{
    private readonly WorldModel _world;
    private readonly GameEngine _engine;
    private readonly SoundManager _sound;
    private readonly UiSettings _uiSettings;
    private readonly bool _isRunningFromEditor;

    private static readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://localhost:11434/")
    };

    private readonly List<string> _commandHistory = new();
    private int _commandHistoryIndex = -1;

    public MainWindow(WorldModel world, GameState state, SoundManager soundManager, UiSettings uiSettings, bool isRunningFromEditor = false)
    {
        _world = world;
        _sound = soundManager;
        _uiSettings = uiSettings;
        _isRunningFromEditor = isRunningFromEditor;
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

        // Inicializar checkbox de IA
        UseLlmCheckBox.IsChecked = _uiSettings.UseLlmForUnknownCommands;
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
            InputTextBox.Clear();

            // Guardar en historial
            _commandHistory.Add(cmd);
            _commandHistoryIndex = _commandHistory.Count;

            // Enviar al motor
            var result = _engine.ProcessCommand(cmd);
            string? llmAnswer = null;

            if (_uiSettings.UseLlmForUnknownCommands)
            {
                var trimmed = (result ?? string.Empty).Trim();
                if (string.Equals(trimmed, "No entiendo ese comando.", StringComparison.OrdinalIgnoreCase))
                {
                    ShowLlmProgress(cmd);
                    try
                    {
                        llmAnswer = await TryAskLlmForUnknownCommandAsync(cmd);
                    }
                    finally
                    {
                        HideLlmProgress();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(result) && llmAnswer == null)
                AppendText(result);

            if (!string.IsNullOrWhiteSpace(llmAnswer))
                AppendText(llmAnswer);

            UpdateStatusPanel();
            UpdateRoomVisuals();

            try
            {
                SaveManager.AutoSave(_engine.State, AppPaths.SavesFolder, _world.Game.EncryptionKey);
            }
            catch
            {
                // Ignoramos errores de autosave
            }

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
                    // Devolvemos la respuesta del modelo como un párrafo aparte.
                    return answer.Trim();
                }
            }

            return "Ni la IA te entiende...";
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
        // Le damos algo de contexto al modelo, pero mantenemos todo muy ligero.
        var roomDescription = _engine.DescribeCurrentRoom();

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Eres un asistente de ayuda para un juego de aventuras de texto en español.");
        promptBuilder.AppendLine("El parser interno del juego no ha entendido el comando del jugador.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Contexto del lugar donde está el jugador:");
        promptBuilder.AppendLine(roomDescription);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"Comando que ha escrito el jugador: \"{originalCommand}\"");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Responde en UNA o DOS frases muy cortas, hablando de tú y directamente al jugador,");
        promptBuilder.AppendLine("sugiriendo qué podría escribir el jugador (por ejemplo: mirar, examinar algo, ir norte, usar objeto...).");
        promptBuilder.AppendLine("No inventes mecánicas nuevas ni resuelvas puzles enteros; solo da una pista o sugerencia de comandos válidos.");
        promptBuilder.AppendLine("No hables del jugador como una tercera persona, tú estas hablandole directamente a él");
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
                "Asegúrate de que Docker Desktop está instalado y ejecutándose y vuelve a probar el comando.";
        }
        else
        {
            message =
                "No se ha podido contactar con el modelo IA en http://localhost:11434.\n\n" +
                "Debes tener Docker Desktop instalado y en ejecución." +
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

    private void ShowLlmProgress(string command)
    {
        LastCommandText.Text = $"> {command}";
        LastCommandText.Visibility = Visibility.Visible;
        LlmProgressBar.Visibility = Visibility.Visible;
        LlmStatusText.Visibility = Visibility.Visible;
    }

    private void HideLlmProgress()
    {
        LlmProgressBar.Visibility = Visibility.Collapsed;
        LastCommandText.Visibility = Visibility.Collapsed;
        LastCommandText.Text = string.Empty;
        LlmStatusText.Visibility = Visibility.Collapsed;
    }

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

        RoomImage.Source = TryLoadRoomImage(room.ImageBase64) ?? DefaultRoomImage;
    }

    private static readonly System.Windows.Media.Imaging.BitmapImage? DefaultRoomImage = LoadDefaultRoomImage();

    private static System.Windows.Media.Imaging.BitmapImage? LoadDefaultRoomImage()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/XiloAdventures.Wpf.Common;component/Assets/default_room.png", UriKind.Absolute);
            var bmp = new System.Windows.Media.Imaging.BitmapImage(uri);
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static System.Windows.Media.Imaging.BitmapImage? TryLoadRoomImage(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;

        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);

            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = ms;
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            return bmp;
        }
        catch
        {
            return null;
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
                SaveManager.SaveToPath(_engine.State, dlg.FileName, _world.Game.EncryptionKey);
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
        var wasSoundEnabled = _sound.SoundEnabled;

        _uiSettings.SoundEnabled = settings.SoundEnabled;
        _uiSettings.FontSize = settings.FontSize;
        _uiSettings.UseLlmForUnknownCommands = settings.UseLlmForUnknownCommands;
        _uiSettings.MusicVolume = settings.MusicVolume;
        _uiSettings.EffectsVolume = settings.EffectsVolume;
        _uiSettings.MasterVolume = settings.MasterVolume;
        _uiSettings.VoiceVolume = settings.VoiceVolume;

        _sound.SoundEnabled = settings.SoundEnabled;
        _sound.MusicVolume = (float)(settings.MusicVolume / 10.0);
        _sound.EffectsVolume = (float)(settings.EffectsVolume / 10.0);
        _sound.MasterVolume = (float)(settings.MasterVolume / 10.0);
        _sound.VoiceVolume = (float)(settings.VoiceVolume / 10.0);

        _sound.RefreshVolumes();
        ApplyUiSettings();

        UiSettingsManager.SaveForWorld(_engine.State.WorldId, _uiSettings);

        // Si el sonido se acaba de activar, arrancar la música adecuada (mundo o sala actual).
        if (!wasSoundEnabled && _sound.SoundEnabled)
        {
            var room = _engine.CurrentRoom;
            if (room != null)
            {
                var hasRoomMusic =
                    !string.IsNullOrWhiteSpace(room.MusicId) ||
                    !string.IsNullOrWhiteSpace(room.MusicBase64);

                if (hasRoomMusic)
                {
                    // Sala con música especial: reproducimos la de la sala.
                    _sound.PlayRoomMusic(
                        room.MusicId,
                        room.MusicBase64,
                        null,
                        null);
                }
                else if (_world.Game != null)
                {
                    // Sala sin música especial: reproducimos la música del mundo.
                    _sound.PlayWorldMusic(_engine.WorldMusicId, _world.Game.WorldMusicBase64);
                }
            }
            else if (_world.Game != null)
            {
                // Si por lo que sea no hay sala actual, intentamos al menos arrancar la música de mundo.
                _sound.PlayWorldMusic(_engine.WorldMusicId, _world.Game.WorldMusicBase64);
            }
        }
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
        var saveConfirm = new ConfirmWindow("¿Quieres guardar la partida antes de salir?", "Salir de la partida")
        {
            Owner = this
        };

        var saveResult = saveConfirm.ShowDialog() == true;
        if (saveResult)
        {
            SaveMenu_Click(this, new RoutedEventArgs());
        }

        var exitConfirm = new ConfirmWindow("¿Seguro que quieres salir de la partida?", "Salir")
        {
            Owner = this
        };

        var exitResult = exitConfirm.ShowDialog() == true;
        if (!exitResult)
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

        // Si la partida NO se ha iniciado desde el editor (Play del editor),
        // intentamos cerrar Docker Desktop por completo.
        try
        {
            if (!_isRunningFromEditor)
            {
                DockerShutdownHelper.TryShutdownDockerDesktop();
            }
        }
        catch
        {
            // Si algo falla al intentar cerrar Docker, lo ignoramos para no
            // bloquear el cierre de la partida.
        }

        base.OnClosing(e);
    }

    private async void UseLlmCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (UseLlmCheckBox.IsChecked == true)
        {
            var progressWindow = new DockerProgressWindow
            {
                Owner = this
            };

            var result = await progressWindow.RunAsync().ConfigureAwait(true);

            if (result.Canceled)
            {
                UseLlmCheckBox.IsChecked = false;
                _uiSettings.UseLlmForUnknownCommands = false;
                UiSettingsManager.SaveForWorld(_world.Game.Id, _uiSettings);
                return;
            }

            if (!result.Success)
            {
                UseLlmCheckBox.IsChecked = false;
                _uiSettings.UseLlmForUnknownCommands = false;
                UiSettingsManager.SaveForWorld(_world.Game.Id, _uiSettings);

                new AlertWindow(
                    "No se han podido iniciar los servicios de IA y voz. Comprueba que Docker Desktop está instalado y en ejecución.",
                    "Error")
                {
                    Owner = this
                }.ShowDialog();

                return;
            }

            _uiSettings.UseLlmForUnknownCommands = true;
        }
        else
        {
            if (_uiSettings.UseLlmForUnknownCommands)
            {
                // El usuario está desactivando la IA.
                // Preguntar si quiere hacer limpieza profunda de Docker.
                var dlg = new ConfirmWindow(
                    "Estás desactivando la IA. ¿Quieres desinstalar y limpiar completamente Docker Desktop y los modelos descargados?\n\n" +
                    "Esto liberará mucho espacio en disco, pero tendrás que volver a instalar Docker si quieres usar la IA en el futuro.",
                    "Limpiar Docker Desktop")
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    try
                    {
                        var result = await XiloAdventures.Wpf.Common.Utilities.DockerDesktopCleaner.CleanDockerDesktopHardAsync(true);
                        Mouse.OverrideCursor = null;
                        var msg = "Limpieza completada con éxito.";
                        new AlertWindow(msg, "Resultado limpieza") { Owner = this }.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        Mouse.OverrideCursor = null;
                        new AlertWindow($"Error durante la limpieza:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }
                }
            }

            _uiSettings.UseLlmForUnknownCommands = false;
        }

        UiSettingsManager.SaveForWorld(_world.Game.Id, _uiSettings);
    }

    private void LlmInfoIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var message = "Si activas la IA, el juego intentará entender mejor comandos complejos o mal escritos. Además, si subes el volumen de voz en las opciones, oirás las descripciones de las salas.\n\nPara usarla debes tener Docker Desktop instalado y funcionando. La primera vez que se use se descargarán algunos componentes y puede tardar unos minutos. Después funcionará muy rápido.";

        var link = new TextBlock
        {
            Margin = new Thickness(0, 12, 0, 0)
        };
        var hyperlink = new Hyperlink
        {
            NavigateUri = new Uri("https://docs.docker.com/desktop/setup/install/windows-install/")
        };
        hyperlink.Inlines.Add("Instala Docker Desktop");
        hyperlink.RequestNavigate += LlmHelpLink_RequestNavigate;
        link.Inlines.Add(hyperlink);

        var dlg = new AlertWindow(message, "IA y voz")
        {
            Owner = this
        };
        dlg.SetCustomContent(link);
        dlg.HideOkButton();
        dlg.ShowDialog();
    }

    private void LlmHelpLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch
        {
            // Ignorar errores al abrir el navegador
        }
    }
}
