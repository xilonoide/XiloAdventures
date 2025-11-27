using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var cmd = InputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(cmd))
                return;

            // Comando limpiar a nivel de UI
            var lower = cmd.ToLowerInvariant();
            if (lower is "limpiar" or "cls" or "clear")
            {
                OutputTextBox.Document.Blocks.Clear();
                InputTextBox.Clear();
                return;
            }

            AppendText($"> {cmd}");
            _commandHistory.Add(cmd);
            _commandHistoryIndex = _commandHistory.Count;

            var result = _engine.ProcessCommand(cmd);
            if (!string.IsNullOrWhiteSpace(result))
                AppendText(result);

            UpdateStatusPanel();
            UpdateRoomVisuals();

            // Autosave salvo para guardar/cargar explícitos (ya gestionados vía menú)
            if (!_IsSaveOrLoadCommand(lower))
            {
                try
                {
                    SaveManager.AutoSave(_engine.State, AppPaths.SavesFolder);
                }
                catch
                {
                    // ignorar errores de autosave
                }
            }

            InputTextBox.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (_commandHistory.Count == 0)
                return;

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
                return;

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
            // Dejar que el RichTextBox maneje el scroll
            OutputTextBox.Focus();
            var routed = new KeyEventArgs(e.KeyboardDevice, e.InputSource, e.Timestamp, e.Key)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            InputTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
            OutputTextBox.RaiseEvent(routed);
            InputTextBox.Focus();
            e.Handled = true;
        }
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
            TimeLabel.Text = $"{gameTime:HH:mm} ({periodo})";
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

        if (!string.IsNullOrWhiteSpace(room.ImageId))
        {
            try
            {
                var imagePathPng = System.IO.Path.Combine(AppPaths.BaseDirectory, "images", room.ImageId + ".png");
                var imagePathJpg = System.IO.Path.Combine(AppPaths.BaseDirectory, "images", room.ImageId + ".jpg");

                string? chosen = null;
                if (File.Exists(imagePathPng)) chosen = imagePathPng;
                else if (File.Exists(imagePathJpg)) chosen = imagePathJpg;

                if (chosen != null)
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(chosen);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    RoomImage.Source = bmp;
                }
                else
                {
                    RoomImage.Source = null;
                }
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
            Filter = "Partidas guardadas (*.json)|*.json|Todos los archivos (*.*)|*.*",
            InitialDirectory = AppPaths.SavesFolder,
            FileName = $"{_engine.State.WorldId}_partida.json"
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
            Filter = "Partidas guardadas (*.json)|*.json|Todos los archivos (*.*)|*.*",
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

        base.OnClosing(e);
    }

}