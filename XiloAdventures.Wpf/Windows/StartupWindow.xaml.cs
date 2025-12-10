using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using XiloAdventures.Wpf.Common.Windows;

namespace XiloAdventures.Wpf.Windows;

public partial class StartupWindow : Window
{
    private const string CreateNewWorldItem = "¡Crea tu aventura!";
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

        // Seleccionar automáticamente el primer mundo disponible al iniciar,
        // si hay alguno y nada está seleccionado todavía.
        if (WorldsList.Items.Count > 0 && WorldsList.SelectedIndex < 0)
        {
            WorldsList.SelectedIndex = 0;
        }

        WorldsList.SelectionChanged += WorldsList_SelectionChanged;
        UpdateDeleteWorldButtonEnabled();
    }


    private void ReloadWorlds()
    {
        WorldsList.Items.Clear();

        // Añadir siempre el elemento especial para crear un mundo nuevo
        WorldsList.Items.Add(CreateNewWorldItem);

        if (Directory.Exists(AppPaths.WorldsFolder))
        {
            var files = Directory.GetFiles(AppPaths.WorldsFolder, "*.xaw");
            foreach (var file in files.OrderBy(f => f))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                WorldsList.Items.Add(name);
            }
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

        // No permitir eliminar el elemento especial '¡Crea tu aventura!'
        var selected = WorldsList.SelectedItem as string;
        var isCreateNewWorld = selected == CreateNewWorldItem;

        DeleteWorldButton.IsEnabled = selected != null && !isCreateNewWorld;

        // Deshabilitar botones cuando se selecciona "¡Crea tu aventura!"
        if (NewGameButton != null)
            NewGameButton.IsEnabled = !isCreateNewWorld;
        if (LoadGameButton != null)
            LoadGameButton.IsEnabled = !isCreateNewWorld;
    }

    private string? GetSelectedWorldFile()
    {
        if (WorldsList.SelectedItem is string name && name != CreateNewWorldItem)
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

        var worldPath = GetSelectedWorldFile();
        if (worldPath is null)
            return;

        // Mostrar popup de opciones antes de cargar la partida
        var optionsWindow = new GameStartOptionsWindow { Owner = this };
        if (optionsWindow.ShowDialog() != true)
            return;

        _isStartingNewGame = true;
        NewGameButton.IsEnabled = false;
        ShowLoading("Iniciando partida...");

        try
        {
            WorldModel world;
            GameState state;
            try
            {
                world = WorldLoader.LoadWorldModel(worldPath, null, () => PromptForEncryptionKey("Introduce la clave usada para cifrar este mundo:"));
                Parser.SetWorldDictionary(world.Game.ParserDictionaryJson);
                state = WorldLoader.CreateInitialState(world);
            }
            catch (Exception)
            {
                new AlertWindow("Clave incorrecta", "Error") { Owner = this }.ShowDialog();
                return;
            }


            var uiSettings = UiSettingsManager.LoadForWorld(world.Game.Id);
            // Usar opciones del popup
            uiSettings.SoundEnabled = optionsWindow.SoundEnabled == true;
            uiSettings.UseLlmForUnknownCommands = optionsWindow.LlmEnabled == true;

            var soundManager = new SoundManager()
            {
                SoundEnabled = uiSettings.SoundEnabled,
                MusicVolume = (float)(uiSettings.MusicVolume / 10.0),
                EffectsVolume = (float)(uiSettings.EffectsVolume / 10.0),
                MasterVolume = (float)(uiSettings.MasterVolume / 10.0),
                VoiceVolume = (float)(uiSettings.VoiceVolume / 10.0)
            };
            soundManager.RefreshVolumes();

            // Si la IA está activada para este mundo, preparar los contenedores Docker (IA + voz)
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

            // Precargar la voz de la sala inicial antes de mostrar la partida,
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

            // Restaurar selección correcta
            if (WorldsList.Items.Count > 0)
            {
                var worldName = Path.GetFileNameWithoutExtension(worldPath);
                var index = WorldsList.Items.IndexOf(worldName);
                WorldsList.SelectedIndex = index >= 0 ? index : 1;
            }

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
        var dlg = new OpenFileDialog
        {
            Title = "Cargar partida",
            Filter = "Partidas guardadas (*.xas)|*.xas|Todos los archivos (*.*)|*.*",
            InitialDirectory = AppPaths.SavesFolder
        };

        if (dlg.ShowDialog(this) != true)
            return;

        ShowLoading("Cargando partida...");
        try
        {

            // Primero intentamos descifrar el archivo para obtener el WorldId
            // Usamos la clave por defecto para obtener el ID del mundo
            SaveData? save = null;
            try
            {
                var json = CryptoUtil.DecryptFromFile(dlg.FileName);
                save = System.Text.Json.JsonSerializer.Deserialize<SaveData>(json);
            }
            catch
            {
                // Si falla con la clave por defecto, podría ser que el mundo
                // tenga una clave personalizada. Intentaremos encontrar el mundo
                // de todas formas iterando por todos los mundos disponibles.
            }

            WorldModel? world = null;

            // Si pudimos leer el WorldId, buscamos el mundo correspondiente
            if (save != null && !string.IsNullOrWhiteSpace(save.WorldId))
            {
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
            }
            else
            {
                // Si no pudimos leer el WorldId (partida cifrada con clave personalizada),
                // intentamos cargar con cada mundo disponible hasta que uno funcione
                if (Directory.Exists(AppPaths.WorldsFolder))
                {
                    foreach (var f in Directory.GetFiles(AppPaths.WorldsFolder, "*.xaw"))
                    {
                        try
                        {
                            var candidate = WorldLoader.LoadWorldModel(f, null, () => PromptForEncryptionKey("Introduce la clave usada para cifrar este mundo:"));

                            // Intentamos cargar la partida con este mundo
                            try
                            {
                                var testState = SaveManager.LoadFromPath(dlg.FileName, candidate);
                                if (testState.WorldId == candidate.Game.Id)
                                {
                                    world = candidate;
                                    break;
                                }
                            }
                            catch
                            {
                                // Este no es el mundo correcto, continuar
                            }
                        }
                        catch
                        {
                            // ignorar ficheros corruptos
                        }
                    }
                }
            }

            if (world == null)
            {
                new AlertWindow("No se ha encontrado el mundo correspondiente a la partida, o la clave de cifrado es incorrecta.", "Error") { Owner = this }.ShowDialog();
                return;
            }

            GameState state;
            try
            {
                Parser.SetWorldDictionary(world.Game.ParserDictionaryJson);
                state = SaveManager.LoadFromPath(dlg.FileName, world);

                // Validación adicional: asegurar que el WorldId cargado coincide
                if (!string.Equals(state.WorldId, world.Game.Id, StringComparison.OrdinalIgnoreCase))
                {
                    new AlertWindow(
                        $"La partida cargada no coincide con el mundo seleccionado.\n\n" +
                        $"Partida: '{state.WorldId}'\nMundo: '{world.Game.Id}'",
                        "Error de validación")
                    {
                        Owner = this
                    }.ShowDialog();
                    return;
                }
            }
            catch (Exception)
            {
                new AlertWindow("Error al cargar la partida. Verifica que la clave de cifrado del mundo sea correcta.", "Error") { Owner = this }.ShowDialog();
                return;
            }

            var uiSettings = UiSettingsManager.LoadForWorld(world.Game.Id);

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

            // Restaurar selección del mundo cargado
            if (WorldsList.Items.Count > 0)
            {
                var worldName = Path.GetFileNameWithoutExtension(world.Game.Id);
                var index = WorldsList.Items.IndexOf(worldName);
                WorldsList.SelectedIndex = index >= 0 ? index : 1;
            }
        }
        finally
        {
            HideLoading();
        }
    }
    private void EditorButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSelectedWorldInEditor();
    }

    private void WorldsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WorldsList.SelectedItem is null)
        {
            return;
        }

        OpenSelectedWorldInEditor();
    }

    private void OpenSelectedWorldInEditor()
    {
        string? worldPath = null;
        string? selectedWorldName = null;

        // Si hay un mundo seleccionado en la lista (y no es el elemento especial), intentamos abrir su fichero
        if (WorldsList.SelectedItem is string name && name != CreateNewWorldItem)
        {
            selectedWorldName = name;
            var candidate = System.IO.Path.Combine(AppPaths.WorldsFolder, name + ".xaw");
            if (System.IO.File.Exists(candidate))
            {
                worldPath = candidate;
            }
        }

        // Si worldPath es null (elemento especial o fichero no existe), el editor creará un mundo nuevo
        var editor = new WorldEditorWindow(worldPath);
        if (editor.IsCanceled)
            return;
        editor.Owner = this;
        Hide();
        editor.ShowDialog();
        Show();
        ReloadWorlds();

        // Restaurar selección
        if (WorldsList.Items.Count > 0)
        {
            if (selectedWorldName != null)
            {
                var index = WorldsList.Items.IndexOf(selectedWorldName);
                WorldsList.SelectedIndex = index >= 0 ? index : 1;
            }
            else
            {
                WorldsList.SelectedIndex = WorldsList.Items.Count > 1 ? 1 : 0;
            }
        }
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
        var passwordBox = new PasswordBox
        {
            Margin = new Thickness(0, 12, 0, 0),
            MinWidth = 320
        };

        var dialog = new AlertWindow(message, "Clave de cifrado")
        {
            Owner = this
        };
        dialog.SetCustomContent(passwordBox);
        dialog.ShowCancelButton();
        dialog.Loaded += (_, _) => passwordBox.Focus();

        var result = dialog.ShowDialog();
        if (result == true)
        {
            return passwordBox.Password.Trim();
        }

        return null;
    }

}








