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
    private bool _isInitializingCheckbox;

    public MainWindow(WorldModel world, GameState state, SoundManager soundManager, UiSettings uiSettings, bool isRunningFromEditor = false)
    {
        _world = world;
        _sound = soundManager;
        _uiSettings = uiSettings;
        _isRunningFromEditor = isRunningFromEditor;
        _engine = new GameEngine(world, state, _sound);
        _engine.RoomChanged += Engine_RoomChanged;
        _engine.ScriptMessage += Engine_ScriptMessage;
        _engine.ConversationDialogue += Engine_ConversationDialogue;
        _engine.ConversationOptions += Engine_ConversationOptions;
        _engine.ConversationEnded += Engine_ConversationEnded;
        _engine.ShopOpened += Engine_ShopOpened;
        _engine.TriggerInitialScripts(); // Disparar scripts iniciales después de suscribir eventos

        InitializeComponent();

        // Establecer tamaño de ventana
        Height = SystemParameters.WorkArea.Height - 100;
        Width = 1300;

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

        // Inicializar checkbox de IA sin disparar el evento
        _isInitializingCheckbox = true;
        UseLlmCheckBox.IsChecked = _uiSettings.UseLlmForUnknownCommands;
        _isInitializingCheckbox = false;
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

    private void AppendSeparator()
    {
        // Solo añadir separador si ya hay contenido
        if (OutputTextBox.Document.Blocks.Count == 0)
            return;

        // Salto de línea antes de la línea separadora
        var emptyParagraph = new Paragraph { Margin = new Thickness(0, 0, 0, 0) };
        OutputTextBox.Document.Blocks.Add(emptyParagraph);

        var line = new System.Windows.Shapes.Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var container = new BlockUIContainer(line)
        {
            Margin = new Thickness(0, 4, 0, 12)
        };

        OutputTextBox.Document.Blocks.Add(container);
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
            AppendSeparator();
            InputTextBox.Clear();

            // Guardar en historial
            _commandHistory.Add(cmd);
            _commandHistoryIndex = _commandHistory.Count;

            // Enviar al motor
            var result = _engine.ProcessCommand(cmd);

            // Si el comando requiere limpiar la pantalla antes, hacerlo ahora
            if (result.ClearScreenBefore)
            {
                OutputTextBox.Document.Blocks.Clear();
            }

            // Si hubo error y la IA está activada, consultar a la IA
            if (_uiSettings.UseLlmForUnknownCommands && result.HasError)
            {
                ShowLlmProgress(cmd);
                try
                {
                    var llmCommand = await TryAskLlmForUnknownCommandAsync(cmd);

                    if (!string.IsNullOrWhiteSpace(llmCommand) &&
                        !llmCommand.Equals("NO_ENTIENDO", StringComparison.OrdinalIgnoreCase) &&
                        !llmCommand.Contains("NO_ENTIENDO"))
                    {
                        // La IA sugirió un comando válido, ejecutarlo
                        var llmResult = _engine.ProcessCommand(llmCommand);

                        if (!llmResult.HasError)
                        {
                            // Si el comando reinterpretado requiere limpiar la pantalla, hacerlo
                            if (llmResult.ClearScreenBefore)
                            {
                                OutputTextBox.Document.Blocks.Clear();
                            }

                            // El comando interpretado funcionó
                            AppendText($"(Interpretado como: {llmCommand})");
                            AppendText(llmResult.Message);
                            result = llmResult; // Usar este resultado para el resto del flujo
                        }
                        else
                        {
                            // El comando sugerido tampoco funcionó
                            AppendText(result.Message);
                        }
                    }
                    else
                    {
                        // La IA no pudo interpretar el comando
                        AppendText(result.Message);
                    }
                }
                catch
                {
                    // Error con la IA, mostrar el mensaje original
                    AppendText(result.Message);
                }
                finally
                {
                    HideLlmProgress();
                }
            }
            else if (!string.IsNullOrWhiteSpace(result.Message))
            {
                AppendText(result.Message);
            }

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
        var inventory = _engine.DescribeInventory();
        var doors = _engine.DescribeDoorsInCurrentRoom();

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Eres un intérprete de comandos para un juego de aventuras de texto en español.");
        promptBuilder.AppendLine("El parser interno del juego no ha entendido el comando del jugador.");
        promptBuilder.AppendLine("Tu trabajo es interpretar lo que el jugador quiso decir y devolver un comando válido.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("VERBOS VÁLIDOS que el juego entiende:");
        promptBuilder.AppendLine("- mirar (sin objeto = describe la sala)");
        promptBuilder.AppendLine("- examinar <objeto> (examina un objeto específico)");
        promptBuilder.AppendLine("- ir <dirección> (norte, sur, este, oeste, arriba, abajo)");
        promptBuilder.AppendLine("- coger <objeto>");
        promptBuilder.AppendLine("- soltar <objeto>");
        promptBuilder.AppendLine("- abrir puerta <dirección> (ej: abrir puerta norte)");
        promptBuilder.AppendLine("- cerrar puerta <dirección>");
        promptBuilder.AppendLine("- hablar <personaje>");
        promptBuilder.AppendLine("- usar <objeto>");
        promptBuilder.AppendLine("- dar <objeto> a <personaje>");
        promptBuilder.AppendLine("- meter <objeto> en <contenedor>");
        promptBuilder.AppendLine("- sacar <objeto> de <contenedor>");
        promptBuilder.AppendLine("- inventario");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("CONTEXTO DE LA SALA ACTUAL:");
        promptBuilder.AppendLine(roomDescription);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("PUERTAS EN ESTA SALA:");
        promptBuilder.AppendLine(doors);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("INVENTARIO DEL JUGADOR:");
        promptBuilder.AppendLine(inventory);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"COMANDO DEL JUGADOR: \"{originalCommand}\"");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("INSTRUCCIONES:");
        promptBuilder.AppendLine("1. Interpreta lo que el jugador quiso decir");
        promptBuilder.AppendLine("2. Responde SOLO con el comando válido que crees que quiso escribir");
        promptBuilder.AppendLine("3. Usa EXACTAMENTE los verbos de la lista de arriba");
        promptBuilder.AppendLine("4. El objeto debe existir en la sala o inventario del jugador");
        promptBuilder.AppendLine("5. Para puertas, usa 'abrir puerta <dirección>' o 'cerrar puerta <dirección>'");
        promptBuilder.AppendLine("6. NO añadas explicaciones, solo el comando");
        promptBuilder.AppendLine("7. Si no puedes interpretar el comando, responde: NO_ENTIENDO");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Respuesta (solo el comando o NO_ENTIENDO):");
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
        ExitsLabel.Text = _engine.DescribeExits();

        // Actualizar turno en la parte superior
        TurnText.Text = $"Turno: {_engine.State.TurnCounter}";

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

    private void Engine_ScriptMessage(string message)
    {
        // Asegurarse de ejecutar en el hilo de UI
        Dispatcher.Invoke(() =>
        {
            AppendText(message);
        });
    }

    private void Engine_ConversationDialogue(ConversationMessage message)
    {
        Dispatcher.Invoke(() =>
        {
            // Formato: [NPC (emoción)] "Texto del diálogo"
            var emotionStr = message.Emotion != "Neutral" ? $" ({message.Emotion})" : "";
            var speaker = message.IsNpc ? message.SpeakerName : "Tú";
            var formattedText = $"[{speaker}{emotionStr}]: \"{message.Text}\"";
            AppendText(formattedText);
        });
    }

    private void Engine_ConversationOptions(List<DialogueOption> options)
    {
        Dispatcher.Invoke(() =>
        {
            if (options == null || options.Count == 0)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("\n¿Qué dices?");
            foreach (var option in options)
            {
                var prefix = option.IsEnabled ? $"  [{option.Index + 1}]" : $"  (×)";
                var suffix = !option.IsEnabled && !string.IsNullOrEmpty(option.DisabledReason)
                    ? $" - {option.DisabledReason}"
                    : "";
                sb.AppendLine($"{prefix} {option.Text}{suffix}");
            }
            sb.AppendLine("\n(Escribe el número de tu elección o 'salir' para terminar)");
            AppendText(sb.ToString());
        });
    }

    private void Engine_ConversationEnded()
    {
        Dispatcher.Invoke(() =>
        {
            AppendText("\n[Fin de la conversación]\n");
        });
    }

    private void Engine_ShopOpened(ShopData shopData)
    {
        Dispatcher.Invoke(() =>
        {
            // Por ahora mostramos la tienda en texto
            // En el futuro se puede crear una ventana de tienda visual
            var sb = new StringBuilder();
            sb.AppendLine($"\n=== {shopData.Title} ===");
            if (!string.IsNullOrEmpty(shopData.WelcomeMessage))
                sb.AppendLine($"\"{shopData.WelcomeMessage}\"");

            sb.AppendLine("\nArtículos a la venta:");
            if (shopData.ItemsForSale.Count == 0)
            {
                sb.AppendLine("  (No hay artículos disponibles)");
            }
            else
            {
                foreach (var item in shopData.ItemsForSale)
                {
                    var stock = item.Stock < 0 ? "" : $" (x{item.Stock})";
                    sb.AppendLine($"  - {item.Name}: {item.Price} monedas{stock}");
                }
            }

            sb.AppendLine("\nUsa 'comprar <objeto>' o 'vender <objeto>'");
            sb.AppendLine("Escribe 'salir' para cerrar la tienda.\n");
            AppendText(sb.ToString());
        });
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

                // Validar que la partida pertenece al mundo actual
                if (!string.Equals(newState.WorldId, _world.Game.Id, StringComparison.OrdinalIgnoreCase))
                {
                    new AlertWindow(
                        $"Esta partida pertenece a otro mundo ('{newState.WorldId}').\n\n" +
                        $"No es compatible con el mundo actual ('{_world.Game.Id}').",
                        "Partida incompatible")
                    {
                        Owner = this
                    }.ShowDialog();
                    return;
                }

                var newWindow = new MainWindow(_world, newState, _sound, _uiSettings);
                newWindow.Owner = Owner;
                Close();
                newWindow.Show();
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                new AlertWindow("Clave incorrecta o archivo corrupto.", "Error") { Owner = this }.ShowDialog();
            }
            catch (System.Text.Json.JsonException)
            {
                new AlertWindow("El archivo de partida está corrupto o no es válido.", "Error") { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                new AlertWindow($"Error al cargar partida:\n\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
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
        var saveChangesWindow = new SaveChangesWindow(
            "¿Seguro que quieres salir?",
            saveButtonText: "Guardar y salir",
            dontSaveButtonText: "Salir sin guardar",
            cancelButtonText: "Cancelar")
        {
            Owner = this
        };

        saveChangesWindow.ShowDialog();

        if (saveChangesWindow.Result == SaveChangesResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (saveChangesWindow.Result == SaveChangesResult.Save)
        {
            SaveMenu_Click(this, new RoutedEventArgs());
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
        // Ignorar durante la inicialización
        if (_isInitializingCheckbox)
            return;

        if (UseLlmCheckBox.IsChecked == true)
        {
            // El usuario está activando la IA: pedir confirmación
            var confirmDlg = new ConfirmWindow(
                "Al activar la IA se iniciará Docker Desktop automáticamente.\n\n" +
                "Si es la primera vez que la usas, se descargarán los modelos necesarios (puede tardar varios minutos dependiendo de tu conexión).\n\n" +
                "¿Deseas continuar?",
                "Activar IA")
            {
                Owner = this
            };

            if (confirmDlg.ShowDialog() != true)
            {
                // Usuario canceló: desmarcar el checkbox
                UseLlmCheckBox.IsChecked = false;
                return;
            }

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
