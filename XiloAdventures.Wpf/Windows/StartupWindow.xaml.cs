using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using XiloAdventures.Engine;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Ui;

namespace XiloAdventures.Wpf.Windows;

public partial class StartupWindow : Window
{
    private bool _isStartingNewGame;

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

        SoundCheckBox.IsChecked = UiSettingsManager.GlobalSettings.SoundEnabled;
        SoundCheckBox.Checked += SoundCheckBox_Changed;
        SoundCheckBox.Unchecked += SoundCheckBox_Changed;
    }

    private void SoundCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UiSettingsManager.GlobalSettings.SoundEnabled = SoundCheckBox.IsChecked == true;
        UiSettingsManager.SaveGlobal();
    }

    private void ReloadWorlds()
    {
        WorldsList.Items.Clear();
        if (!Directory.Exists(AppPaths.WorldsFolder))
            return;

        var files = Directory.GetFiles(AppPaths.WorldsFolder, "*.xaw");
        foreach (var file in files.OrderBy(f => f))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            WorldsList.Items.Add(name);
        }
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

        try
        {
        var worldPath = GetSelectedWorldFile();
        if (worldPath is null)
            return;

        WorldModel world;
        GameState state;
        try
        {
            world = WorldLoader.LoadWorldModel(worldPath);
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

        var soundManager = new SoundManager(AppPaths.SoundFolder)
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

            var success = await dockerWindow.RunAsync();
            if (!success)
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
    
        }
        finally
        {
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

        // Buscar el mundo correspondiente
        WorldModel? world = null;
        if (Directory.Exists(AppPaths.WorldsFolder))
        {
            foreach (var f in Directory.GetFiles(AppPaths.WorldsFolder, "*.xaw"))
            {
                try
                {
                    var candidate = WorldLoader.LoadWorldModel(f);
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

        var soundManager = new SoundManager(AppPaths.SoundFolder)
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

            var success = await dockerWindow.RunAsync();
            if (!success)
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
        Show();
        ReloadWorlds();
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
        editor.Owner = this;
        Hide();
        editor.ShowDialog();
        Show();
        ReloadWorlds();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        var dlg = new ConfirmWindow("¿Seguro que quieres salir de Xilo Adventures?", "Confirmar salida")
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

}