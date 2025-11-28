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

        new AlertWindow("Selecciona un mundo primero.", "Xilo Adventures") { Owner = this }.ShowDialog();
        return null;
    }

    private void NewGameButton_Click(object sender, RoutedEventArgs e)
    {
        var worldPath = GetSelectedWorldFile();
        if (worldPath is null)
            return;

        WorldModel world;
        GameState state;
        try
        {
            world = WorldLoader.LoadWorldModel(worldPath);
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
            SoundEnabled = uiSettings.SoundEnabled
        };

        var main = new MainWindow(world, state, soundManager, uiSettings);
        main.Owner = this;
        Hide();
        main.ShowDialog();

        // Al cerrar la partida, volvemos a mostrar el inicio
        Show();
        ReloadWorlds();
    }

    private void LoadGameButton_Click(object sender, RoutedEventArgs e)
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
            SoundEnabled = uiSettings.SoundEnabled
        };

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