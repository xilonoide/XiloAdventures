using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using XiloAdventures.Engine;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Common.Services;
using XiloAdventures.Wpf.Common.Ui;
using XiloAdventures.Wpf.Common.Windows;
using XiloAdventures.Wpf.Controls;

namespace XiloAdventures.Wpf.Windows;

public partial class WorldEditorWindow : Window
{

    private WorldModel _world = new();
    private string? _currentPath;
    private List<Room>? _roomsClipboard;
    private bool _roomsClipboardIsCut;
    private IReadOnlyDictionary<string, Point>? _roomsClipboardPositions;
    private Dictionary<string, string>? _lastClipboardIdMap;

    private readonly UndoRedoManager _undoRedo = new();

    private bool _isPlayRunning;
    private bool _isDirty;
    private readonly string _baseTitle;
    private TreeViewItem? _gameTreeNode;
    private string? _initialWorldPath;
    public bool IsCanceled { get; private set; }

    public WorldEditorWindow()
    {
        InitializeComponent();
        _baseTitle = Title ?? "Editor de mundos";
        PropertyEditor.PropertyEdited += PropertyEditor_PropertyEdited;
        PropertyEditor.GetRooms = () => _world.Rooms;
        MapPanel.RoomClicked += MapPanel_RoomClicked;
        MapPanel.MapEdited += MapPanel_MapEdited;
        MapPanel.DoorCreated += MapPanel_DoorCreated;
        MapPanel.DoorDoubleClicked += MapPanel_DoorDoubleClicked;
        MapPanel.KeyDoubleClicked += MapPanel_KeyDoubleClicked;
        MapPanel.DoorClicked += MapPanel_DoorClicked;
        MapPanel.SelectionCleared += MapPanel_SelectionCleared;

        MapPanel.AddObjectToRoomRequested += MapPanel_AddObjectToRoomRequested;
        MapPanel.AddNpcToRoomRequested += MapPanel_AddNpcToRoomRequested;
        MapPanel.EmptyMapDoubleClicked += MapPanel_EmptyMapDoubleClicked;

        Loaded += Window_Loaded;
    }

    public WorldEditorWindow(string? worldPath) : this()
    {
        _initialWorldPath = worldPath;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        try
        {
            if (!string.IsNullOrWhiteSpace(_initialWorldPath) && System.IO.File.Exists(_initialWorldPath))
            {
                TryLoadWorldWithPrompt(_initialWorldPath);
                if (IsCanceled)
                {
                    Close();
                    return;
                }
            }
            else
            {
                _world = new WorldModel();
                _world.Game.Id = "nuevo_mundo";
                _world.Game.Title = "Nuevo mundo";
                _world.Game.StartRoomId = "sala_inicio";

                _world.Rooms.Clear();
                var startRoom = new Room
                {
                    Id = "sala_inicio",
                    Name = "Sala inicial",
                    Description = "Esta es la sala inicial de tu mundo."
                };
                _world.Rooms.Add(startRoom);
                _currentPath = null;
            }

            MapPanel.SetWorld(_world);
            BuildTree();
            ResetUndoRedo();
            SetDirty(false);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void TryLoadWorldWithPrompt(string worldPath)
    {
        try
        {
            _world = WorldLoader.LoadWorldModel(worldPath);
            _currentPath = worldPath;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error al cargar el mundo:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            IsCanceled = true;
        }
    }

    private void PropertyEditor_PropertyEdited(object? obj, string propertyName)
    {
        if (obj is null) return;

        // Si se ha editado el nombre, actualizamos el nodo correspondiente en el ├âãÆ├é┬írbol
        if (propertyName == "Name")
        {
            foreach (TreeViewItem root in WorldTree.Items)
            {
                UpdateTreeHeaderRecursive(root, obj);
            }
        }

        PushUndoSnapshot();
        SetDirty(true);
    }

    private void UpdateTreeHeaderRecursive(TreeViewItem item, object target)
    {
        if (item.Tag == target)
        {
            string headerText = item.Header?.ToString() ?? string.Empty;

            switch (target)
            {
                case Room r:
                    headerText = r.Name;
                    break;
                case GameObject o:
                    headerText = o.Name;
                    break;
                case Npc n:
                    headerText = n.Name;
                    break;
                case QuestDefinition q:
                    headerText = q.Name;
                    break;
                case Door d:
                    headerText = d.Name;
                    break;
                case KeyDefinition k:
                    headerText = string.IsNullOrWhiteSpace(k.ObjectId) ? k.Id : $"Llave ({k.ObjectId})";
                    break;
            }

            item.Header = headerText;
        }

        foreach (var child in item.Items.OfType<TreeViewItem>())
        {
            UpdateTreeHeaderRecursive(child, target);
        }
    }

    private void MapPanel_RoomClicked(Room room)
    {
        // seleccionar en el árbol la sala correspondiente
        SelectRoomInTree(room);
        MapPanel.SetSelectedRoom(room);
    }

    private void MapPanel_SelectionCleared()
    {
        ClearTreeSelection();
        PropertyEditor.SetObject(null);
    }

    private void MapPanel_MapEdited()
    {
        PushUndoSnapshot();
        SetDirty(true);
    }

    private void MapPanel_DoorCreated(Door door, KeyDefinition? createdKey, GameObject? createdObject)
    {
        AddDoorToTreeNode(door);

        if (createdObject != null)
            AddObjectToTreeNode(createdObject);

        if (createdKey != null)
            AddKeyToTreeNode(createdKey);

        SelectDoorInTree(door);
        PropertyEditor.SetObject(door);
        SetDirty(true);
    }

    private void MapPanel_DoorDoubleClicked(Door door)
    {
        SelectDoorInTree(door);
        PropertyEditor.SetObject(door);
    }

    private void MapPanel_KeyDoubleClicked(KeyDefinition key)
    {
        SelectKeyInTree(key);
        PropertyEditor.SetObject(key);
    }

    private void MapPanel_DoorClicked(Door door)
    {
        SelectDoorInTree(door);
        PropertyEditor.SetObject(door);
    }

    private void BuildTree()
    {
        WorldTree.Items.Clear();
        _gameTreeNode = null;

        _world.Doors ??= new List<Door>();
        _world.Keys ??= new List<KeyDefinition>();

        var gameNode = new TreeViewItem { Header = "Juego", Tag = _world.Game, Foreground = Brushes.White };
        _gameTreeNode = gameNode;
        WorldTree.Items.Add(gameNode);

        var roomsRoot = new TreeViewItem { Header = "Salas", Foreground = Brushes.White };
        foreach (var room in _world.Rooms)
        {
            roomsRoot.Items.Add(new TreeViewItem { Header = room.Name, Tag = room, Foreground = Brushes.White });
        }
        WorldTree.Items.Add(roomsRoot);

        var doorsRoot = new TreeViewItem { Header = "Puertas", Foreground = Brushes.White };
        foreach (var door in _world.Doors)
        {
            doorsRoot.Items.Add(new TreeViewItem { Header = door.Name, Tag = door, Foreground = Brushes.White });
        }
        WorldTree.Items.Add(doorsRoot);

        var objsRoot = new TreeViewItem { Header = "Objetos", Foreground = Brushes.White };
        foreach (var obj in _world.Objects)
        {
            objsRoot.Items.Add(new TreeViewItem { Header = obj.Name, Tag = obj, Foreground = Brushes.White });
        }
        WorldTree.Items.Add(objsRoot);

        var npcsRoot = new TreeViewItem { Header = "NPCs", Foreground = Brushes.White };
        foreach (var npc in _world.Npcs)
        {
            npcsRoot.Items.Add(new TreeViewItem { Header = npc.Name, Tag = npc, Foreground = Brushes.White });
        }
        WorldTree.Items.Add(npcsRoot);

        var questsRoot = new TreeViewItem { Header = "Misiones", Foreground = Brushes.White };
        foreach (var q in _world.Quests)
        {
            questsRoot.Items.Add(new TreeViewItem { Header = q.Name, Tag = q, Foreground = Brushes.White });
        }
        WorldTree.Items.Add(questsRoot);

        var keysRoot = new TreeViewItem { Header = "Llaves", Foreground = Brushes.White };
        foreach (var k in _world.Keys)
        {
            // Mostramos el nombre del objeto asociado o el Id de la llave
            var header = string.IsNullOrWhiteSpace(k.ObjectId) ? k.Id : $"Llave ({k.ObjectId})";
            keysRoot.Items.Add(new TreeViewItem { Header = header, Tag = k, Foreground = Brushes.White });
        }
        WorldTree.Items.Add(keysRoot);

        // Seleccionar por defecto el nodo Juego al (re)construir el ├âãÆ├é┬írbol
        SelectGameTreeNode();
    }

    private void SelectGameTreeNode()
    {
        if (_gameTreeNode == null)
            return;

        Dispatcher.InvokeAsync(() =>
        {
            WorldTree.UpdateLayout();
            _gameTreeNode.IsSelected = true;
            _gameTreeNode.Focus();
            _gameTreeNode.BringIntoView();
        }, DispatcherPriority.Loaded);
    }

    private void ClearTreeSelection()
    {
        if (WorldTree.SelectedItem is TreeViewItem selectedItem)
        {
            selectedItem.IsSelected = false;
        }
    }

    private void WorldTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (WorldTree.SelectedItem is not TreeViewItem item)
        {
            PropertyEditor.SetObject(null);
            MapPanel.SetSelectedRoom(null);
            return;
        }

        PropertyEditor.SetObject(item.Tag);

        if (item.Tag is Room room)
        {
            MapPanel.SetSelectedRoom(room);
        }
        else
        {
            MapPanel.SetSelectedRoom(null);
        }
    }

    private void WorldTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WorldTree.SelectedItem is not TreeViewItem item)
            return;

        switch (item.Tag)
        {
            case Room room:
                MapPanel.CenterOnRoom(room);
                break;
            case GameObject obj:
                CenterOnObject(obj);
                break;
            case Npc npc:
                CenterOnNpc(npc);
                break;
        }
    }

    private void CenterOnObject(GameObject obj)
    {
        if (_world == null)
            return;

        Room? room = null;

        if (!string.IsNullOrWhiteSpace(obj.RoomId))
        {
            room = _world.Rooms.FirstOrDefault(r => r.Id == obj.RoomId);
        }

        if (room == null)
        {
            var dlg = new SelectRoomWindow(_world.Rooms, $"Selecciona la sala para el objeto '{obj.Name}'")
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.SelectedRoom != null)
            {
                room = dlg.SelectedRoom;
                obj.RoomId = room.Id;
            }
            else
            {
                return;
            }
        }

        if (room != null)
        {
            MapPanel.CenterOnRoom(room);
            SelectRoomInTree(room);
        }
    }

    private void CenterOnNpc(Npc npc)
    {
        if (_world == null)
            return;

        Room? room = null;

        if (!string.IsNullOrWhiteSpace(npc.RoomId))
        {
            room = _world.Rooms.FirstOrDefault(r => r.Id == npc.RoomId);
        }

        if (room == null)
        {
            var dlg = new SelectRoomWindow(_world.Rooms, $"Selecciona la sala para el NPC '{npc.Name}'")
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.SelectedRoom != null)
            {
                room = dlg.SelectedRoom;
                npc.RoomId = room.Id;
            }
            else
            {
                return;
            }
        }

        if (room != null)
        {
            MapPanel.CenterOnRoom(room);
            SelectRoomInTree(room);
        }
    }

    private void ExpandAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in WorldTree.Items.OfType<TreeViewItem>())
        {
            SetExpandedRecursive(item, true);
        }
    }

    private void CollapseAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in WorldTree.Items.OfType<TreeViewItem>())
        {
            SetExpandedRecursive(item, false);
        }
    }

    private void SetExpandedRecursive(TreeViewItem item, bool expanded)
    {
        item.IsExpanded = expanded;
        foreach (var child in item.Items.OfType<TreeViewItem>())
        {
            SetExpandedRecursive(child, expanded);
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        PerformSearch(SearchTextBox.Text);
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        PerformSearch(SearchTextBox.Text);
    }

    private TreeViewItem? FindInTree(TreeViewItem node, string text)
    {
        if (node.Header is string s && s.ToLowerInvariant().Contains(text))
            return node;

        foreach (TreeViewItem child in node.Items.OfType<TreeViewItem>())
        {
            var found = FindInTree(child, text);
            if (found != null)
                return found;
        }

        return null;
    }

    private void PerformSearch(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var normalized = text.ToLowerInvariant();

        foreach (TreeViewItem root in WorldTree.Items)
        {
            var found = FindInTree(root, normalized);
            if (found != null)
            {
                found.IsSelected = true;
                found.BringIntoView();
                break;
            }
        }
    }

    private void RoomImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (WorldTree.SelectedItem is not TreeViewItem item || item.Tag is not Room room)
            return;

        var dlg = new OpenFileDialog
        {
            Title = "Seleccionar imagen de sala",
            Filter = "Imágenes (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos los archivos (*.*)|*.*"
        };

        if (dlg.ShowDialog(this) == true)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
            room.ImageId = fileName;
            PropertyEditor.SetObject(room);
            MapPanel.InvalidateVisual();
            PushUndoSnapshot();
        }
    }

    private void RoomMusicButton_Click(object sender, RoutedEventArgs e)
    {
        if (WorldTree.SelectedItem is not TreeViewItem item || item.Tag is not Room room)
            return;

        var dlg = new OpenFileDialog
        {
            Title = "Seleccionar música de sala",
            Filter = "Audio (*.mp3;*.wav)|*.mp3;*.wav|Todos los archivos (*.*)|*.*"
        };

        if (dlg.ShowDialog(this) == true)
        {
            var fileName = System.IO.Path.GetFileName(dlg.FileName);
            room.MusicId = fileName;
            PropertyEditor.SetObject(room);
            PushUndoSnapshot();
        }
    }

    private void NewMenu_Click(object sender, RoutedEventArgs e)
    {
        _world = new WorldModel();
        _world.Game.Id = "nuevo_mundo";
        _world.Game.Title = "Nuevo mundo";
        _world.Game.StartRoomId = "sala_inicio";

        var startRoom = new Room
        {
            Id = "sala_inicio",
            Name = "Sala inicial",
            Description = "La sala inicial de tu mundo."
        };
        _world.Rooms.Add(startRoom);

        _currentPath = null;
        MapPanel.SetWorld(_world);
        BuildTree();
        ResetUndoRedo();
    }

    private void OpenMenu_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Abrir mundo",
            Filter = "Mundos (*.xaw)|*.xaw|Todos los archivos (*.*)|*.*",
            InitialDirectory = AppPaths.WorldsFolder
        };

        if (dlg.ShowDialog(this) == true)
        {
            TryLoadWorldWithPrompt(dlg.FileName);
            if (IsCanceled)
                return;

            MapPanel.SetWorld(_world);
            BuildTree();
            ResetUndoRedo();
        }
    }




    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlayRunning)
            return;

        if (_world == null)
            return;

        ShowPlayLoading("Preparando partida...");
        await Dispatcher.Yield();

        // Guardar antes de lanzar la partida de prueba (y validar clave de encriptación)
        if (!PerformSave())
        {
            // Si el save falla (por clave incorrecta u otro error), no continuar
            HidePlayLoading();
            return;
        }

        _isPlayRunning = true;
        if (PlayButton != null)
            PlayButton.IsEnabled = false;

        try
        {
            // Sincronizamos posiciones del mapa con el modelo y guardamos el mundo actual.
            try
            {
                if (_world != null)
                {
                    var roomIds = _world.Rooms.Select(r => r.Id);
                    var positions = MapPanel.GetRoomPositions(roomIds);

                    _world.RoomPositions ??= new Dictionary<string, MapPosition>();
                    _world.RoomPositions.Clear();

                    foreach (var kv in positions)
                    {
                        _world.RoomPositions[kv.Key] = new MapPosition
                        {
                            X = kv.Value.X,
                            Y = kv.Value.Y
                        };
                    }
                }

                Directory.CreateDirectory(AppPaths.WorldsFolder);

                if (string.IsNullOrEmpty(_currentPath))
                {
                    var baseName = string.IsNullOrWhiteSpace(_world!.Game.Id)
                        ? "mundo_desde_editor"
                        : _world.Game.Id;
                    _currentPath = Path.Combine(AppPaths.WorldsFolder, baseName + ".xaw");
                }

                WorldLoader.SaveWorldModel(_world!, _currentPath);
            }
            catch (Exception ex)
            {
                new AlertWindow($"Error al guardar el mundo para probarlo:\n{ex.Message}", "Error")
                {
                    Owner = this
                }.ShowDialog();
                return;
            }

            WorldModel world;
            GameState state;
            try
            {
                world = WorldLoader.LoadWorldModel(_currentPath);
                Parser.SetWorldDictionary(world.Game.ParserDictionaryJson);
                state = WorldLoader.CreateInitialState(world);
            }
            catch (Exception ex)
            {
                new AlertWindow($"Error al preparar la partida de prueba:\n{ex.Message}", "Error")
                {
                    Owner = this
                }.ShowDialog();
                return;
            }

            var uiSettings = UiSettingsManager.LoadForWorld(world.Game.Id);
            // Respetar la configuración global de sonido e IA
            uiSettings.SoundEnabled = UiSettingsManager.GlobalSettings.SoundEnabled;
            uiSettings.UseLlmForUnknownCommands = UiSettingsManager.GlobalSettings.UseLlmForUnknownCommands;

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
                        "Comprueba que Docker Desktop está instalado.",
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
                    // Si algo falla al precargar la voz, continuamos sin interrumpir la partida de prueba.
                }
            }

            var main = new Common.Windows.MainWindow(world, state, soundManager, uiSettings, isRunningFromEditor: true)
            {
                Owner = this
            };

            // Mostramos la ventana de juego como diálogo modal para no abrir varios tests a la vez.
            main.ShowDialog();
        }
        finally
        {
            HidePlayLoading();
            _isPlayRunning = false;
            if (PlayButton != null)
                PlayButton.IsEnabled = true;
        }
    }

    private void ShowPlayLoading(string message)
    {
        PlayLoadingText.Text = message;
        PlayLoadingOverlay.Visibility = Visibility.Visible;
    }

    private void HidePlayLoading()
    {
        PlayLoadingOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Valida que la clave de encriptación tenga el formato correcto.
    /// Retorna true si es válida, false si no lo es.
    /// </summary>
    private bool ValidateEncryptionKey()
    {
        if (_world == null)
            return true;

        // Forzar la actualización del valor de la clave de encriptación antes de validar
        PropertyEditor.UpdateEncryptionKey(_world.Game);

        var key = _world.Game.EncryptionKey;
        if (!string.IsNullOrWhiteSpace(key) && key.Trim().Length != 8)
        {
            new AlertWindow("La 'Clave de cifrado' debe tener exactamente 8 caracteres o dejarse vacía para usar la clave por defecto.",
                            "Clave incorrecta")
            {
                Owner = this
            }.ShowDialog();

            // Intentar seleccionar el nodo juego para facilitar al usuario corregirlo
            SelectGameTreeNode();
            return false;
        }

        return true;
    }

    private void SaveMenu_Click(object sender, RoutedEventArgs e)
    {
        PerformSave();
    }

    private bool PerformSave()
    {
        // Simulamos sender/e para reaprovechar lógica existente si fuera necesario, 
        // aunque idealmente SaveAsMenu_Click debería refactorizarse también.
        // Por ahora mantenemos la compatibilidad con el resto del código.
        var sender = this;
        var e = new RoutedEventArgs();

        if (string.IsNullOrEmpty(_currentPath))
        {
            SaveAsMenu_Click(sender, e);
            return false; // No sabemos si el usuario guardó o canceló
        }

        // Validar clave de encriptación
        if (!ValidateEncryptionKey())
            return false;

        try
        {
            // Antes de guardar, sincronizamos las posiciones actuales del mapa con el modelo.
            if (_world != null)
            {
                var roomIds = _world.Rooms.Select(r => r.Id);
                var positions = MapPanel.GetRoomPositions(roomIds);

                _world.RoomPositions ??= new Dictionary<string, MapPosition>();
                _world.RoomPositions.Clear();

                foreach (var kv in positions)
                {
                    _world.RoomPositions[kv.Key] = new MapPosition
                    {
                        X = kv.Value.X,
                        Y = kv.Value.Y
                    };
                }
            }

            Directory.CreateDirectory(AppPaths.WorldsFolder);
            WorldLoader.SaveWorldModel(_world!, _currentPath);
            SetDirty(false);
            return true;
        }
        catch (Exception ex)
        {
            new AlertWindow($"Error al guardar mundo:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
            return false;
        }
    }

    private void SaveAsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateEncryptionKey())
            return;

        var dlg = new SaveFileDialog
        {
            Title = "Guardar mundo",
            Filter = "Mundos (*.xaw)|*.xaw|Todos los archivos (*.*)|*.*",
            InitialDirectory = AppPaths.WorldsFolder,
            FileName = string.IsNullOrEmpty(_world.Game.Id) ? "nuevo_mundo.json" : _world.Game.Id + ".xaw"
        };

        if (dlg.ShowDialog(this) == true)
        {
            _currentPath = dlg.FileName;
            SaveMenu_Click(sender, e);
        }
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SaveMenu_Click(sender, e);
    }

    private void CloseMenu_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AddRoom_Click(object sender, RoutedEventArgs e)
    {
        var index = _world.Rooms.Count + 1;
        var room = new Room
        {
            Id = $"sala_{index}",
            Name = $"Sala {index}",
            Description = "Nueva sala."
        };
        _world.Rooms.Add(room);
        MapPanel.SetWorld(_world);
        BuildTree();
        SelectRoomInTree(room);

        PushUndoSnapshot();
        SetDirty(true);
    }

    private void AddExit_Click(object sender, RoutedEventArgs e)
    {
        if (WorldTree.SelectedItem is TreeViewItem item && item.Tag is Room room)
        {
            var dlg = new AddExitWindow(_world.Rooms, room)
            {
                Owner = this
            };

            var result = dlg.ShowDialog();
            if (result == true)
            {
                var direction = dlg.SelectedDirection;

                if (RoomHasExitInDirection(room, direction))
                {
                    new AlertWindow(
                        $"La sala '{room.Name}' ya tiene una salida en dirección '{NormalizeDirectionForRoom(direction)}'.",
                        "Xilo Adventures")
                    {
                        Owner = this
                    }.ShowDialog();
                }
                else
                {
                    room.Exits.Add(new Exit
                    {
                        Direction = direction,
                        TargetRoomId = dlg.SelectedTargetRoomId
                    });

                    MapPanel.InvalidateVisual();
                    PropertyEditor.SetObject(room);
                    PushUndoSnapshot();
                    SetDirty(true);
                }
            }
        }
        else
        {
            new AlertWindow("Selecciona primero una sala en el árbol.", "Xilo Adventures")
            {
                Owner = this
            }.ShowDialog();
        }
    }

    private void AddDoor_Click(object sender, RoutedEventArgs e)
    {
        _world.Doors ??= new List<Door>();

        var index = _world.Doors.Count + 1;
        var door = new Door
        {
            Id = $"door_{index}",
            Name = $"Puerta {index}",
            Description = "Nueva puerta.",
            IsOpen = false,
            HasLock = false,
            LockId = string.Empty,
            OpenFromSide = DoorOpenSide.Both
        };

        // Si hay una sala seleccionada, la usamos como RoomIdA por defecto
        if (WorldTree.SelectedItem is TreeViewItem item && item.Tag is Room room)
        {
            door.RoomIdA = room.Id;
        }

        _world.Doors.Add(door);
        BuildTree();
        PropertyEditor.SetObject(door);
        PushUndoSnapshot();
        SetDirty(true);
    }

    private void AddKey_Click(object sender, RoutedEventArgs e)
    {
        _world.Keys ??= new List<KeyDefinition>();

        var index = _world.Keys.Count + 1;
        var key = new KeyDefinition
        {
            Id = $"key_{index}",
            ObjectId = string.Empty
        };

        // Si hay un objeto seleccionado, lo usamos como objeto asociado a la llave
        if (WorldTree.SelectedItem is TreeViewItem item && item.Tag is GameObject obj)
        {
            key.ObjectId = obj.Id;
        }

        _world.Keys.Add(key);
        BuildTree();
        PropertyEditor.SetObject(key);
        PushUndoSnapshot();
        SetDirty(true);
    }


    private void AddObject_Click(object sender, RoutedEventArgs e)
    {
        var index = _world.Objects.Count + 1;
        var obj = new GameObject
        {
            Id = $"obj_{index}",
            Name = $"Objeto {index}",
            Description = "Nuevo objeto.",
            CanTake = true,
            Visible = true
        };

        if (WorldTree.SelectedItem is TreeViewItem item && item.Tag is Room room)
        {
            obj.RoomId = room.Id;
        }

        _world.Objects.Add(obj);
        BuildTree();
        PushUndoSnapshot();
        SetDirty(true);
    }

    private void AddNpc_Click(object sender, RoutedEventArgs e)
    {
        var index = _world.Npcs.Count + 1;
        var npc = new Npc
        {
            Id = $"npc_{index}",
            Name = $"PNJ {index}",
            Description = "Nuevo personaje.",
            Visible = true
        };

        if (WorldTree.SelectedItem is TreeViewItem item && item.Tag is Room room)
        {
            npc.RoomId = room.Id;
        }

        _world.Npcs.Add(npc);
        BuildTree();
        PushUndoSnapshot();
        SetDirty(true);
    }

    private void AddQuest_Click(object sender, RoutedEventArgs e)
    {
        var index = _world.Quests.Count + 1;
        var q = new QuestDefinition
        {
            Id = $"quest_{index}",
            Name = $"Misión {index}",
            Description = "Nueva misión."
        };
        _world.Quests.Add(q);
        BuildTree();
        PushUndoSnapshot();
        SetDirty(true);
    }

    private static string NormalizeDirectionForRoom(string direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
            return string.Empty;

        var key = direction.Trim().ToLowerInvariant();
        return key switch
        {
            "n" or "norte" => "norte",
            "s" or "sur" => "sur",
            "e" or "este" => "este",
            "o" or "oeste" => "oeste",
            "ne" or "noreste" => "noreste",
            "no" or "noroeste" => "noroeste",
            "se" or "sureste" => "sureste",
            "so" or "suroeste" => "suroeste",
            "arriba" or "subir" => "arriba",
            "abajo" or "bajar" => "abajo",
            _ => direction.Trim()
        };
    }

    private static bool RoomHasExitInDirection(Room room, string direction)
    {
        var norm = NormalizeDirectionForRoom(direction);
        return room.Exits.Any(ex =>
            string.Equals(NormalizeDirectionForRoom(ex.Direction), norm, StringComparison.OrdinalIgnoreCase));
    }

    private void WorldTree_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            if (WorldTree.SelectedItem is TreeViewItem item)
            {
                HandleDeleteTreeItem(item);
                e.Handled = true;
            }
        }
    }

    private void HandleDeleteTreeItem(TreeViewItem item)
    {
        if (item?.Tag is null)
            return;

        switch (item.Tag)
        {
            case Room room:
                DeleteRoom(room);
                break;
            case GameObject obj:
                DeleteObject(obj);
                break;
            case Npc npc:
                DeleteNpc(npc);
                break;
            case QuestDefinition quest:
                DeleteQuest(quest);
                break;
            case Door door:
                DeleteDoor(door);
                break;
            case KeyDefinition key:
                DeleteKey(key);
                break;
        }
    }

    private void DeleteRoom(Room room)
    {
        if (room is null) return;

        var dlg = new ConfirmWindow($"¿Eliminar la sala '{room.Name}'?", "Confirmar eliminación")
        {
            Owner = this
        };

        if (dlg.ShowDialog() != true)
            return;

        // Quitar salidas que apunten a esta sala
        foreach (var r in _world.Rooms)
        {
            r.Exits.RemoveAll(ex => string.Equals(ex.TargetRoomId, room.Id, StringComparison.OrdinalIgnoreCase));
        }

        // Quitar puertas que conecten con esta sala
        if (_world.Doors != null)
        {
            _world.Doors.RemoveAll(d =>
                string.Equals(d.RoomIdA, room.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.RoomIdB, room.Id, StringComparison.OrdinalIgnoreCase));
        }

        // Desasociar objetos y NPCs de esta sala
        foreach (var obj in _world.Objects.Where(o => string.Equals(o.RoomId, room.Id, StringComparison.OrdinalIgnoreCase)))
        {
            obj.RoomId = null;
        }

        foreach (var npc in _world.Npcs.Where(n => string.Equals(n.RoomId, room.Id, StringComparison.OrdinalIgnoreCase)))
        {
            npc.RoomId = null;
        }

        // Limpiar sala de inicio si era esta
        if (string.Equals(_world.Game.StartRoomId, room.Id, StringComparison.OrdinalIgnoreCase))
        {
            _world.Game.StartRoomId = string.Empty;
        }

        _world.Rooms.Remove(room);

        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
        PushUndoSnapshot();
        SetDirty(true);

    }

    private void DeleteObject(GameObject obj)
    {
        if (obj is null) return;

        var dlg = new ConfirmWindow($"¿Eliminar el objeto '{obj.Name}'?", "Confirmar eliminación")
        {
            Owner = this
        };

        if (dlg.ShowDialog() != true)
            return;

        WorldEditorHelpers.DeleteObject(_world, obj);

        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
        PushUndoSnapshot();
        SetDirty(true);

    }

    private void DeleteDoor(Door door)
    {
        if (door is null) return;

        var dlg = new ConfirmWindow($"¿Eliminar la puerta '{door.Name}'?", "Confirmar eliminación")
        {
            Owner = this
        };

        if (dlg.ShowDialog() != true)
            return;

        WorldEditorHelpers.DeleteDoor(_world, door);

        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
        PushUndoSnapshot();
        SetDirty(true);

    }

    private void DeleteKey(KeyDefinition key)
    {
        if (key is null) return;

        var dlg = new ConfirmWindow($"¿Eliminar la definición de llave '{key.Id}'?", "Confirmar eliminación")
        {
            Owner = this
        };

        if (dlg.ShowDialog() != true)
            return;

        _world.Keys?.Remove(key);

        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
        PushUndoSnapshot();
        SetDirty(true);

    }


    private void DeleteNpc(Npc npc)
    {
        if (npc is null) return;

        var dlg = new ConfirmWindow($"¿Eliminar el NPC '{npc.Name}'?", "Confirmar eliminación")
        {
            Owner = this
        };

        if (dlg.ShowDialog() != true)
            return;

        _world.Npcs.Remove(npc);

        foreach (var room in _world.Rooms)
        {
            room.NpcIds.Remove(npc.Id);
        }

        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
        PushUndoSnapshot();
        SetDirty(true);

    }

    private void DeleteQuest(QuestDefinition quest)
    {
        if (quest is null) return;

        var dlg = new ConfirmWindow($"¿Eliminar la misión '{quest.Name}'?", "Confirmar eliminación")
        {
            Owner = this
        };

        if (dlg.ShowDialog() != true)
            return;

        _world.Quests.Remove(quest);

        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
        PushUndoSnapshot();
        SetDirty(true);

    }

    private void AddDoorToTreeNode(Door door)
    {
        var root = WorldTree.Items.OfType<TreeViewItem>().FirstOrDefault(i => i.Header?.ToString() == "Puertas");
        if (root == null)
        {
            BuildTree();
            return;
        }

        if (!root.Items.OfType<TreeViewItem>().Any(i => ReferenceEquals(i.Tag, door)))
        {
            root.Items.Add(new TreeViewItem { Header = door.Name, Tag = door, Foreground = Brushes.White });
        }
    }

    private void AddObjectToTreeNode(GameObject obj)
    {
        var root = WorldTree.Items.OfType<TreeViewItem>().FirstOrDefault(i => i.Header?.ToString() == "Objetos");
        if (root == null)
        {
            BuildTree();
            return;
        }

        if (!root.Items.OfType<TreeViewItem>().Any(i => ReferenceEquals(i.Tag, obj)))
        {
            root.Items.Add(new TreeViewItem { Header = obj.Name, Tag = obj, Foreground = Brushes.White });
        }
    }

    private void AddKeyToTreeNode(KeyDefinition key)
    {
        var root = WorldTree.Items.OfType<TreeViewItem>().FirstOrDefault(i => i.Header?.ToString() == "Llaves");
        if (root == null)
        {
            BuildTree();
            return;
        }

        var header = string.IsNullOrWhiteSpace(key.ObjectId) ? key.Id : $"Llave ({key.ObjectId})";

        if (!root.Items.OfType<TreeViewItem>().Any(i => ReferenceEquals(i.Tag, key)))
        {
            root.Items.Add(new TreeViewItem { Header = header, Tag = key, Foreground = Brushes.White });
        }
    }

    private void SelectDoorInTree(Door door)
    {
        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.Header?.ToString() == "Puertas")
            {
                foreach (TreeViewItem child in root.Items.OfType<TreeViewItem>())
                {
                    if (child.Tag == door)
                    {
                        WorldTree.Focus();
                        child.IsSelected = true;
                        child.BringIntoView();
                        child.Focus();
                        return;
                    }
                }
            }
        }
    }

    private void SelectKeyInTree(KeyDefinition key)
    {
        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.Header?.ToString() == "Llaves")
            {
                foreach (TreeViewItem child in root.Items.OfType<TreeViewItem>())
                {
                    if (child.Tag == key)
                    {
                        WorldTree.Focus();
                        child.IsSelected = true;
                        child.BringIntoView();
                        child.Focus();
                        return;
                    }
                }
            }
        }
    }

    private void SelectRoomInTree(Room room)
    {
        if (room is null) return;

        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.Header?.ToString() == "Salas")
            {
                foreach (TreeViewItem child in root.Items.OfType<TreeViewItem>())
                {
                    if (child.Tag == room)
                    {
                        // Seleccionamos la sala en el árbol exactamente igual
                        // que si el usuario hubiese hecho clic en el TreeView.
                        WorldTree.Focus();
                        child.IsSelected = true;
                        child.BringIntoView();
                        child.Focus();
                        return;
                    }
                }
            }
        }
    }

    private void CutCopyCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Cut && e.Command != ApplicationCommands.Copy)
            return;

        if (MapPanel == null)
        {
            e.CanExecute = false;
            e.Handled = true;
            return;
        }

        var selected = MapPanel.GetSelectedRooms();
        e.CanExecute = selected != null && selected.Count > 0;
        e.Handled = true;
    }

    private void PasteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _roomsClipboard != null && _roomsClipboard.Count > 0;
        e.Handled = true;
    }

    private void CutCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selected = MapPanel.GetSelectedRooms();
        if (selected == null || selected.Count == 0 || _world == null)
            return;

        _roomsClipboard = CloneRoomsForClipboard(selected);
        _roomsClipboardIsCut = true;

        // Guardamos también las posiciones actuales de las salas copiadas
        _roomsClipboardPositions = MapPanel.GetRoomPositions(selected.Select(r => r.Id));
        _lastClipboardIdMap = null;

        foreach (var room in selected)
        {
            DeleteRoomWithoutConfirmation(room);
        }

        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
        PushUndoSnapshot();
        SetDirty(true);

        MapPanel.ClearSelection();
        PushUndoSnapshot();
    }

    private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selected = MapPanel.GetSelectedRooms();
        if (selected == null || selected.Count == 0)
            return;

        _roomsClipboard = CloneRoomsForClipboard(selected);
        _roomsClipboardIsCut = false;

        // Guardamos también las posiciones actuales de las salas copiadas
        _roomsClipboardPositions = MapPanel.GetRoomPositions(selected.Select(r => r.Id));
        _lastClipboardIdMap = null;
    }

    private void PasteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_world == null || _roomsClipboard == null || _roomsClipboard.Count == 0)
            return;

        var newRooms = CreateRoomsFromClipboard(_roomsClipboard);

        foreach (var room in newRooms)
        {
            _world.Rooms.Add(room);
        }

        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
        PushUndoSnapshot();
        SetDirty(true);

        // Si tenemos posiciones de las salas originales y un mapa de Ids,
        // recolocamos las salas nuevas en las mismas coordenadas pero
        // desplazadas ligeramente a la derecha y hacia abajo.
        if (_roomsClipboardPositions != null && _lastClipboardIdMap != null)
        {
            var newPositions = new Dictionary<string, Point>();
            const double offsetX = 40;
            const double offsetY = 40;

            foreach (var kv in _roomsClipboardPositions)
            {
                var originalId = kv.Key;
                var originalPos = kv.Value;

                if (_lastClipboardIdMap.TryGetValue(originalId, out var newId))
                {
                    newPositions[newId] = new Point(originalPos.X + offsetX, originalPos.Y + offsetY);
                }
            }

            if (newPositions.Count > 0)
            {
                MapPanel.SetRoomsPositions(newPositions);
            }
            else
            {
                MapPanel.PlaceRoomsAtTopLeft(newRooms);
            }
        }
        else
        {
            MapPanel.PlaceRoomsAtTopLeft(newRooms);
        }

        MapPanel.SetSelectedRooms(newRooms);

        if (_roomsClipboardIsCut)
        {
            _roomsClipboard = null;
            _roomsClipboardIsCut = false;
            _roomsClipboardPositions = null;
            _lastClipboardIdMap = null;
        }

        PushUndoSnapshot();
    }

    private static List<Room> CloneRoomsForClipboard(IEnumerable<Room> rooms)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(rooms, options);
        return JsonSerializer.Deserialize<List<Room>>(json, options) ?? new List<Room>();
    }

    private List<Room> CreateRoomsFromClipboard(List<Room> sourceRooms)
    {
        var result = new List<Room>();
        if (_world == null || sourceRooms == null || sourceRooms.Count == 0)
            return result;

        var existingIds = new HashSet<string>(_world.Rooms.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
        var idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var options = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        foreach (var room in sourceRooms)
        {
            var originalId = room.Id;
            var newId = GenerateUniqueRoomId(originalId, existingIds);
            existingIds.Add(newId);
            idMap[originalId] = newId;

            // Clonamos la sala para no modificar el contenido del portapapeles
            var json = JsonSerializer.Serialize(room, options);
            var cloned = JsonSerializer.Deserialize<Room>(json, options);
            if (cloned == null)
                continue;

            cloned.Id = newId;
            result.Add(cloned);
        }

        _lastClipboardIdMap = idMap;

        // Ajustar las salidas internas que apunten entre las salas pegadas
        foreach (var room in result)
        {
            foreach (var ex in room.Exits)
            {
                if (!string.IsNullOrEmpty(ex.TargetRoomId) && idMap.TryGetValue(ex.TargetRoomId, out var mapped))
                {
                    ex.TargetRoomId = mapped;
                }
            }
        }

        return result;
    }

    private static string GenerateUniqueRoomId(string baseId, HashSet<string> existingIds)
    {
        if (!existingIds.Contains(baseId))
            return baseId;

        int i = 1;
        while (true)
        {
            var candidate = $"{baseId}_{i}";
            if (!existingIds.Contains(candidate))
                return candidate;
            i++;
        }
    }

    private void DeleteRoomWithoutConfirmation(Room room)
    {
        if (_world == null || room is null)
            return;

        // Quitar salidas que apunten a esta sala
        foreach (var r in _world.Rooms)
        {
            r.Exits.RemoveAll(ex => string.Equals(ex.TargetRoomId, room.Id, StringComparison.OrdinalIgnoreCase));
        }

        // Desasociar objetos y NPCs de esta sala
        foreach (var obj in _world.Objects.Where(o => string.Equals(o.RoomId, room.Id, StringComparison.OrdinalIgnoreCase)))
        {
            obj.RoomId = null;
        }

        foreach (var npc in _world.Npcs.Where(n => string.Equals(n.RoomId, room.Id, StringComparison.OrdinalIgnoreCase)))
        {
            npc.RoomId = null;
        }

        // Si era la sala inicial, limpiamos ese valor
        if (!string.IsNullOrEmpty(_world.Game.StartRoomId) &&
            string.Equals(_world.Game.StartRoomId, room.Id, StringComparison.OrdinalIgnoreCase))
        {
            _world.Game.StartRoomId = string.Empty;
        }

        _world.Rooms.Remove(room);
    }




    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private sealed class EditorSnapshot
    {
        public string WorldJson { get; set; } = string.Empty;
    }

    private sealed class UndoRedoManager
    {
        private readonly Stack<EditorSnapshot> _undoStack = new();
        private readonly Stack<EditorSnapshot> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 1;
        public bool CanRedo => _redoStack.Count > 0;

        public void Reset(EditorSnapshot initialState)
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _undoStack.Push(initialState);
        }

        public void Push(EditorSnapshot snapshot)
        {
            _undoStack.Push(snapshot);
            _redoStack.Clear();
        }

        public EditorSnapshot? Undo()
        {
            if (!CanUndo)
                return null;

            var current = _undoStack.Pop();
            _redoStack.Push(current);

            return _undoStack.Peek();
        }

        public EditorSnapshot? Redo()
        {
            if (!CanRedo)
                return null;

            var snapshot = _redoStack.Pop();
            _undoStack.Push(snapshot);
            return snapshot;
        }
    }

    private EditorSnapshot CreateSnapshot()
    {
        // Sincronizamos las posiciones del mapa con el modelo antes de capturar el estado.
        if (_world != null)
        {
            var roomIds = _world.Rooms.Select(r => r.Id);
            var positions = MapPanel.GetRoomPositions(roomIds);

            _world.RoomPositions ??= new Dictionary<string, MapPosition>();
            _world.RoomPositions.Clear();

            foreach (var kv in positions)
            {
                _world.RoomPositions[kv.Key] = new MapPosition
                {
                    X = kv.Value.X,
                    Y = kv.Value.Y
                };
            }
        }

        var json = JsonSerializer.Serialize(_world, SnapshotJsonOptions);
        return new EditorSnapshot { WorldJson = json };
    }

    private void ResetUndoRedo()
    {
        var initial = CreateSnapshot();
        _undoRedo.Reset(initial);
        CommandManager.InvalidateRequerySuggested();
        SetDirty(false);
    }

    private void PushUndoSnapshot()
    {
        var snapshot = CreateSnapshot();
        _undoRedo.Push(snapshot);
        CommandManager.InvalidateRequerySuggested();
        SetDirty(true);
    }

    private void SetDirty(bool dirty)
    {
        _isDirty = dirty;
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        var worldLabel = string.IsNullOrEmpty(_currentPath) ? "Nuevo mundo" : Path.GetFileName(_currentPath);
        var dirtySuffix = _isDirty ? " *" : string.Empty;
        Title = $"{_baseTitle} - {worldLabel}{dirtySuffix}";
    }

    private Style ResolveCommandButtonStyle()
    {
        var style = TryFindResource("CommandButtonStyle") as Style
                    ?? Application.Current?.TryFindResource("CommandButtonStyle") as Style;
        return style ?? new Style(typeof(Button));
    }

    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        var world = JsonSerializer.Deserialize<WorldModel>(snapshot.WorldJson, SnapshotJsonOptions);
        if (world == null)
            return;

        _world = world;
        MapPanel.SetWorld(_world);
        BuildTree();
        CommandManager.InvalidateRequerySuggested();
    }

    private void Undo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _undoRedo.CanUndo;
        e.Handled = true;
    }

    private void Undo_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var snapshot = _undoRedo.Undo();
        if (snapshot != null)
        {
            RestoreSnapshot(snapshot);
        }
    }

    private void Redo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _undoRedo.CanRedo;
        e.Handled = true;
    }

    private void Redo_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var snapshot = _undoRedo.Redo();
        if (snapshot != null)
        {
            RestoreSnapshot(snapshot);
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
            && e.Key == Key.S)
        {
            // Guardar mundo con Ctrl+S
            SaveMenu_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isDirty)
        {
            var dlg = new SaveChangesWindow("Hay cambios sin guardar. ¿Quieres guardarlos antes de cerrar el editor?")
            {
                Owner = this
            };
            dlg.ShowDialog();

            if (dlg.Result == SaveChangesResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (dlg.Result == SaveChangesResult.Save)
            {
                if (!ValidateEncryptionKey())
                {
                    e.Cancel = true;
                    return;
                }

                SaveMenu_Click(this, new RoutedEventArgs());
                if (_isDirty)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        // Al cerrar el editor intentamos también cerrar Docker Desktop.
        try
        {
            DockerShutdownHelper.TryShutdownDockerDesktop();
        }
        catch
        {
            // Ignoramos errores al cerrar Docker; no deben bloquear el cierre del editor.
        }

        base.OnClosing(e);
    }





    private void MapPanel_AddObjectToRoomRequested(Room room)
    {
        var index = _world.Objects.Count + 1;
        var obj = new GameObject
        {
            Id = $"obj_{index}",
            Name = $"Objeto {index}",
            Description = "Nuevo objeto.",
            CanTake = true,
            Visible = true,
            RoomId = room.Id
        };

        _world.Objects.Add(obj);
        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
    }

    private void MapPanel_AddNpcToRoomRequested(Room room)
    {
        var index = _world.Npcs.Count + 1;
        var npc = new Npc
        {
            Id = $"npc_{index}",
            Name = $"PNJ {index}",
            Description = "Nuevo personaje.",
            Visible = true,
            RoomId = room.Id
        };

        _world.Npcs.Add(npc);
        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
    }

    private void MapPanel_EmptyMapDoubleClicked(Point logicalPos)
    {
        var index = _world.Rooms.Count + 1;
        var room = new Room
        {
            Id = $"sala_{index}",
            Name = $"Sala {index}",
            Description = "Nueva sala."
        };
        _world.Rooms.Add(room);

        // Establecer la posición de la sala en el mapa
        MapPanel.SetRoomPosition(room.Id, logicalPos);

        MapPanel.SetWorld(_world);
        BuildTree();
        SelectRoomInTree(room);

        PushUndoSnapshot();
        SetDirty(true);
    }

    private async void ExportMenu_Click(object sender, RoutedEventArgs e)
    {
        // Verificar que el mundo esté guardado
        if (_isDirty)
        {
            var result = MessageBox.Show(
                "Debes guardar el mundo antes de exportar. ¿Quieres guardarlo ahora?",
                "Guardar antes de exportar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SaveMenu_Click(sender, e);
                if (_isDirty) // Si sigue dirty, es que el usuario canceló o hubo error
                    return;
            }
            else
            {
                return;
            }
        }

        if (string.IsNullOrEmpty(_currentPath))
        {
            MessageBox.Show(
                "Debes guardar el mundo en un archivo antes de exportar.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Seleccionar ubicación de salida
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Ejecutable (*.exe)|*.exe",
            DefaultExt = ".exe",
            FileName = $"{_world.Game.Title}.exe"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        var outputPath = saveDialog.FileName;

        // Mostrar indicador de progreso
        ShowPlayLoading("Exportando ejecutable...");

        try
        {
            await System.Threading.Tasks.Task.Run(() => ExportStandaloneExecutable(_currentPath, outputPath));

            HidePlayLoading();

            new AlertWindow(
                $"Ejecutable creado exitosamente en:\n{outputPath}\n\nTamaño aproximado: ~80-100 MB\n\nEl jugador no necesitará .NET instalado para ejecutarlo.",
                "Exportación completada")
            {
                Owner = this
            }.ShowDialog();

            // Preguntar si quiere abrir la carpeta
            var confirmDlg = new ConfirmWindow(
                "¿Deseas abrir la carpeta donde se guardó el ejecutable?",
                "Abrir carpeta")
            {
                Owner = this
            };

            if (confirmDlg.ShowDialog() == true)
            {
                var folderPath = System.IO.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
            }
        }
        catch (Exception ex)
        {
            HidePlayLoading();
            MessageBox.Show(
                $"Error al exportar el ejecutable:\n\n{ex.Message}",
                "Error de exportación",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ExportStandaloneExecutable(string worldPath, string outputPath)
    {
        // Buscar el proyecto player desde la carpeta de la solución
        var baseDir = AppContext.BaseDirectory;

        // Navegar hacia arriba para encontrar la raíz de la solución
        // Desde bin\Debug\net8.0-windows -> volver 3 niveles arriba, luego entrar a XiloAdventures.Wpf.Player
        var currentDir = new System.IO.DirectoryInfo(baseDir);

        // Subir hasta encontrar la carpeta que contiene el .sln
        while (currentDir != null && !System.IO.File.Exists(System.IO.Path.Combine(currentDir.FullName, "XiloAdventures.sln")))
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir == null)
        {
            throw new InvalidOperationException(
                "No se pudo encontrar la raíz del proyecto (carpeta con XiloAdventures.sln).");
        }

        var playerProjectPath = System.IO.Path.Combine(currentDir.FullName, "XiloAdventures.Wpf.Player");

        // Verificar que existe el proyecto player
        var playerCsproj = System.IO.Path.Combine(playerProjectPath, "XiloAdventures.Wpf.Player.csproj");
        if (!System.IO.File.Exists(playerCsproj))
        {
            throw new InvalidOperationException(
                $"No se encontró el proyecto del player en:\n{playerProjectPath}\n\n" +
                "Asegúrate de que el proyecto XiloAdventures.Wpf.Player existe en la solución.");
        }

        // Copiar el mundo al proyecto player
        var worldDestPath = System.IO.Path.Combine(playerProjectPath, "world.xaw");
        System.IO.File.Copy(worldPath, worldDestPath, true);

        try
        {
            // Compilar con dotnet publish
            var publishDir = System.IO.Path.Combine(playerProjectPath, "bin", "publish");
            if (System.IO.Directory.Exists(publishDir))
            {
                System.IO.Directory.Delete(publishDir, true);
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{playerCsproj}\" -c Release -r win-x64 --self-contained true -o \"{publishDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("No se pudo iniciar el proceso de compilación.");
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException(
                    $"Error al compilar el ejecutable (código {process.ExitCode}):\n\n{error}");
            }

            // Copiar el ejecutable resultante a la ubicación final
            var compiledExePath = System.IO.Path.Combine(publishDir, "XiloAdventures.Wpf.Player.exe");
            if (!System.IO.File.Exists(compiledExePath))
            {
                throw new InvalidOperationException(
                    $"No se encontró el ejecutable compilado en:\n{compiledExePath}");
            }

            System.IO.File.Copy(compiledExePath, outputPath, true);
        }
        finally
        {
            // Limpiar el archivo temporal del mundo
            if (System.IO.File.Exists(worldDestPath))
            {
                try { System.IO.File.Delete(worldDestPath); } catch { }
            }
        }
    }

    public class SelectRoomWindow : Window
    {
        private readonly ComboBox _combo;
        public Room? SelectedRoom { get; private set; }

        public SelectRoomWindow(IEnumerable<Room> rooms, string title)
        {
            Title = title;
            Width = 420;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(34, 34, 34));
            Foreground = Brushes.White;
            WindowStyle = WindowStyle.ToolWindow;

            var panel = new StackPanel { Margin = new Thickness(10) };

            var text = new TextBlock
            {
                Text = "Elige la sala:",
                Margin = new Thickness(0, 0, 0, 8)
            };

            _combo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                DisplayMemberPath = "Name",
                ItemsSource = rooms.ToList()
            };
            if (_combo.Items.Count > 0)
                _combo.SelectedIndex = 0;

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button
            {
                Content = "Aceptar",
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0)
            };
            okButton.Click += (s, e) =>
            {
                SelectedRoom = _combo.SelectedItem as Room;
                DialogResult = true;
            };

            var cancelButton = new Button
            {
                Content = "Cancelar",
                Width = 80
            };
            cancelButton.Click += (s, e) =>
            {
                DialogResult = false;
            };

            buttonsPanel.Children.Add(okButton);
            buttonsPanel.Children.Add(cancelButton);

            panel.Children.Add(text);
            panel.Children.Add(_combo);
            panel.Children.Add(buttonsPanel);

            Content = panel;
        }
    }

}














