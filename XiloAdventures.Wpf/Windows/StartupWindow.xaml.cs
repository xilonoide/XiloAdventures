using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Navigation;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using XiloAdventures.Engine;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Ui;

namespace XiloAdventures.Wpf.Windows;

public partial class StartupWindow : Window
{
    private bool _isStartingNewGame;
    private bool _isLoadingVisible;

    public StartupWindow()
    {
        InitializeComponent();
        Loaded += StartupWindow_Loaded;
    }

    private void StartupWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ReloadWorlds();

        // Seleccionar automÃƒÂ¡ticamente el primer mundo disponible al iniciar,
        // si hay alguno y nada estÃƒÂ¡ seleccionado todavÃƒÂ­a.
        if (WorldsList.Items.Count > 0 && WorldsList.SelectedIndex < 0)
        {
            WorldsList.SelectedIndex = 0;
        }

        SoundCheckBox.IsChecked = UiSettingsManager.GlobalSettings.SoundEnabled;
        SoundCheckBox.Checked += SoundCheckBox_Changed;
        SoundCheckBox.Unchecked += SoundCheckBox_Changed;

        LlmCheckBox.IsChecked = UiSettingsManager.GlobalSettings.UseLlmForUnknownCommands;
        LlmCheckBox.Checked += LlmCheckBox_Changed;
        LlmCheckBox.Unchecked += LlmCheckBox_Changed;

        WorldsList.SelectionChanged += WorldsList_SelectionChanged;
        UpdateDeleteWorldButtonEnabled();
    }

    private void SoundCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UiSettingsManager.GlobalSettings.SoundEnabled = SoundCheckBox.IsChecked == true;
        UiSettingsManager.SaveGlobal();
    }

    private void LlmCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UiSettingsManager.GlobalSettings.UseLlmForUnknownCommands = LlmCheckBox.IsChecked == true;
        UiSettingsManager.SaveGlobal();
    }



        private void LlmInfoIcon_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var message = "Si activas la IA, el juego intentara entender mejor comandos complejos o mal escritos. Ademas, si subes el volumen de voz en las opciones, oiras las descripciones de las salas.\n\nPara usarla debes tener Docker Desktop instalado y funcionando. La primera vez que se use se descargaran algunas cosas y puede tardar unos minutos. Despues funcionara muy rapido.";

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
        link.Inlines.Add(new Run(""));
        link.Inlines.Add(hyperlink);

        var dlg = new AlertWindow(message, "Ayuda sobre la IA")
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
            // Ignoramos errores al abrir el navegador
        }
    }

    private void ReloadWorlds()
    {
        WorldsList.Items.Clear();

        if (!Directory.Exists(AppPaths.WorldsFolder))
        {
            UpdateDeleteWorldButtonEnabled();
            return;
        }

        var files = Directory.GetFiles(AppPaths.WorldsFolder, "*.xaw");
        foreach (var file in files.OrderBy(f => f))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            WorldsList.Items.Add(name);
        }

        if (WorldsList.Items.Count > 0 && WorldsList.SelectedIndex < 0)
        {
            WorldsList.SelectedIndex = 0;
        }

        UpdateDeleteWorldButtonEnabled();
    }

    private void WorldsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateDeleteWorldButtonEnabled();
    }

    private void UpdateDeleteWorldButtonEnabled()
    {
        if (DeleteWorldButton == null)
            return;

        DeleteWorldButton.IsEnabled = WorldsList.SelectedItem != null;
    }

    private string? GetSelectedWorldFile()
    {
        if (WorldsList.SelectedItem is string name)
        {
            return Path.Combine(AppPaths.WorldsFolder, name + ".xaw");
        }

        new AlertWindow("Selecciona un mundo primero.") { Owner = this }.ShowDialog();
        return null;
    }

    private async void NewGameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isStartingNewGame)
            return;

        _isStartingNewGame = true;
        NewGameButton.IsEnabled = false;
        ShowLoading("Iniciando partida...");

        try
        {
        var worldPath = GetSelectedWorldFile();
        if (worldPath is null)
            return;

        WorldModel world;
        GameState state;
        try
        {
            world = WorldLoader.LoadWorldModel(worldPath, null, () => PromptForEncryptionKey("Introduce la clave usada para cifrar este mundo:"));
            Parser.SetWorldDictionary(world.Game.ParserDictionaryJson);
            state = WorldLoader.CreateInitialState(world);
        }
        catch (Exception ex)
        {
            new AlertWindow($"Error al cargar el mundo:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
            return;
        }

        
        var uiSettings = UiSettingsManager.LoadForWorld(world.Game.Id);
        // Respetar el check de sonido global
        uiSettings.SoundEnabled = SoundCheckBox.IsChecked == true;

        // Respetar tambiÃƒÂ©n el check de IA global de la pantalla inicial
        if (LlmCheckBox.IsChecked == true)
        {
            uiSettings.UseLlmForUnknownCommands = true;
        }
        else
        {
            uiSettings.UseLlmForUnknownCommands = false;
        }

        var soundManager = new SoundManager()
        {
            SoundEnabled = uiSettings.SoundEnabled,
            MusicVolume = (float)(uiSettings.MusicVolume / 10.0),
            EffectsVolume = (float)(uiSettings.EffectsVolume / 10.0),
            MasterVolume = (float)(uiSettings.MasterVolume / 10.0),
            VoiceVolume = (float)(uiSettings.VoiceVolume / 10.0)
        };
        soundManager.RefreshVolumes();

        // Si la IA estÃƒÂ¡ activada para este mundo, preparar los contenedores Docker (IA + voz)
        if (uiSettings.UseLlmForUnknownCommands)
        {
            var dockerWindow = new DockerProgressWindow
            {
                Owner = this
            };

            var dockerResult = await dockerWindow.RunAsync();
            if (dockerResult.Canceled)
            {
                uiSettings.UseLlmForUnknownCommands = false;
                return;
            }

            if (!dockerResult.Success)
            {
                uiSettings.UseLlmForUnknownCommands = false;

                new AlertWindow(
                    "No se han podido iniciar los servicios de IA y voz.\n\n" +
                    "Comprueba que Docker Desktop estÃƒÂ¡ instalado y en ejecuciÃƒÂ³n.",
                    "Error")
                {
                    Owner = this
                }.ShowDialog();
            }
        }

        // Precargar la voz de la sala inicial antes de mostrar la partida,
        // para que se escuche nada mÃƒÂ¡s entrar.
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
                // Si algo falla al precargar la voz, continuamos sin interrumpir el inicio de la partida.
            }
        }

        var main = new MainWindow(world, state, soundManager, uiSettings);

        main.Owner = this;
        Hide();
        main.ShowDialog();

        // Al cerrar la partida, volvemos a mostrar el inicio
        Show();
        ReloadWorlds();
    
        }
        finally
        {
            HideLoading();
            _isStartingNewGame = false;
            NewGameButton.IsEnabled = true;
        }
    }

    private async void LoadGameButton_Click(object sender, RoutedEventArgs e)
    {
        ShowLoading("Cargando partida...");
        try
        {
            var dlg = new OpenFileDialog
            {
                Title = "Cargar partida",
                Filter = "Partidas guardadas (*.xas)|*.xas|Todos los archivos (*.*)|*.*",
                InitialDirectory = AppPaths.SavesFolder
            };

            if (dlg.ShowDialog(this) != true)
                return;

            SaveData? save;
            try
            {
                var json = CryptoUtil.DecryptFromFile(dlg.FileName);
                save = System.Text.Json.JsonSerializer.Deserialize<SaveData>(json);
            }
            catch (Exception ex)
            {
                new AlertWindow($"Error al leer la partida:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
                return;
            }

            if (save == null || string.IsNullOrWhiteSpace(save.WorldId))
            {
                new AlertWindow("La partida no contiene un identificador de mundo válido.", "Error") { Owner = this }.ShowDialog();
                return;
            }

            WorldModel? world = null;
            if (Directory.Exists(AppPaths.WorldsFolder))
            {
                foreach (var f in Directory.GetFiles(AppPaths.WorldsFolder, "*.xaw"))
                {
                    try
                    {
                        var candidate = WorldLoader.LoadWorldModel(f, null, () => PromptForEncryptionKey("Introduce la clave usada para cifrar este mundo:"));
                        if (candidate.Game.Id == save.WorldId)
                        {
                            world = candidate;
                            break;
                        }
                    }
                    catch
                    {
                        // ignorar ficheros corruptos
                    }
                }
            }

            if (world == null)
            {
                new AlertWindow("No se ha encontrado el mundo correspondiente a la partida.", "Error") { Owner = this }.ShowDialog();
                return;
            }

            GameState state;
            try
            {
                Parser.SetWorldDictionary(world.Game.ParserDictionaryJson);
                state = SaveManager.LoadFromPath(dlg.FileName, world);
            }
            catch (Exception ex)
            {
                new AlertWindow($"Error al cargar la partida:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
                return;
            }

            var uiSettings = UiSettingsManager.LoadForWorld(world.Game.Id);
            uiSettings.SoundEnabled = SoundCheckBox.IsChecked == true;
            uiSettings.UseLlmForUnknownCommands = LlmCheckBox.IsChecked == true;

            var soundManager = new SoundManager()
            {
                SoundEnabled = uiSettings.SoundEnabled,
                MusicVolume = (float)(uiSettings.MusicVolume / 10.0),
                EffectsVolume = (float)(uiSettings.EffectsVolume / 10.0),
                MasterVolume = (float)(uiSettings.MasterVolume / 10.0),
                VoiceVolume = (float)(uiSettings.VoiceVolume / 10.0)
            };
            soundManager.RefreshVolumes();

            if (uiSettings.UseLlmForUnknownCommands)
            {
                var dockerWindow = new DockerProgressWindow
                {
                    Owner = this
                };

                var dockerResult = await dockerWindow.RunAsync();
                if (dockerResult.Canceled)
                {
                    uiSettings.UseLlmForUnknownCommands = false;
                    return;
                }

                if (!dockerResult.Success)
                {
                    uiSettings.UseLlmForUnknownCommands = false;

                    new AlertWindow(
                        "No se han podido iniciar los servicios de IA y voz.\n\n" +
                        "Comprueba que Docker Desktop está instalado y en ejecución.",
                        "Error")
                    {
                        Owner = this
                    }.ShowDialog();
                }
            }

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
                    // Si algo falla al precargar la voz, continuamos sin interrumpir el inicio de la partida.
                }
            }

            var main = new MainWindow(world, state, soundManager, uiSettings);

            main.Owner = this;
            Hide();
            main.ShowDialog();
            Show();
            ReloadWorlds();
        }
        finally
        {
            HideLoading();
        }
    }
    private void EditorButton_Click(object sender, RoutedEventArgs e)
    {
        string? worldPath = null;

        // Si hay un mundo seleccionado en la lista, intentamos abrir su fichero .json
        if (WorldsList.SelectedItem is string name)
        {
            var candidate = System.IO.Path.Combine(AppPaths.WorldsFolder, name + ".xaw");
            if (System.IO.File.Exists(candidate))
            {
                worldPath = candidate;
            }
        }

        // Si worldPath es null o el fichero no existe, el editor creará un mundo nuevo
        var editor = new WorldEditorWindow(worldPath);
        if (editor.IsCanceled)
            return;
        editor.Owner = this;
        Hide();
        editor.ShowDialog();
        Show();
        ReloadWorlds();
    }

    
    private void DeleteWorldButton_Click(object sender, RoutedEventArgs e)
    {
        var worldPath = GetSelectedWorldFile();
        if (worldPath is null)
            return;

        var worldName = Path.GetFileNameWithoutExtension(worldPath);

        var dlg = new ConfirmWindow(
            $"\u00bfSeguro que quieres eliminar el mundo \"{worldName}\"?\n\nEsta acci\u00f3n no se puede deshacer.",
            "Eliminar mundo")
        {
            Owner = this
        };

        var result = dlg.ShowDialog() == true;
        if (!result)
            return;

        try
        {
            if (File.Exists(worldPath))
            {
                File.Delete(worldPath);
            }
        }
        catch (Exception ex)
        {
            new AlertWindow($"No se ha podido eliminar el mundo:\n{ex.Message}", "Error")
            {
                Owner = this
            }.ShowDialog();
            return;
        }

        ReloadWorlds();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        var dlg = new ConfirmWindow("\u00bfSeguro que quieres salir de Xilo Adventures?", "Confirmar salida")
        {
            Owner = this
        };
        var result = dlg.ShowDialog() == true;

        if (!result)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    private void ShowLoading(string message)
    {
        _isLoadingVisible = true;
        LoadingText.Text = message;
        LoadingOverlay.Visibility = Visibility.Visible;
    }

    private void HideLoading()
    {
        if (!_isLoadingVisible)
            return;

        _isLoadingVisible = false;
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private string? PromptForEncryptionKey(string message)
    {
        var dialog = new Window
        {
            Title = "Clave de cifrado",
            Owner = this,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(34, 34, 34)),
            Foreground = Brushes.White,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false
        };

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var msg = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(msg, 0);

        var pb = new PasswordBox
        {
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetRow(pb, 1);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var ok = new Button { Content = "Aceptar", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancelar", Width = 90 };
        ok.Click += (_, _) => { dialog.DialogResult = true; dialog.Close(); };
        cancel.Click += (_, _) => { dialog.DialogResult = false; dialog.Close(); };
        btnPanel.Children.Add(ok);
        btnPanel.Children.Add(cancel);
        Grid.SetRow(btnPanel, 2);

        grid.Children.Add(msg);
        grid.Children.Add(pb);
        grid.Children.Add(btnPanel);

        dialog.Content = grid;
        var result = dialog.ShowDialog();
        if (result == true)
        {
            return pb.Password.Trim();
        }

        return null;
    }

}








