using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using System.Text.Json;
using XiloAdventures.Engine;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Controls;
using XiloAdventures.Wpf.Ui;

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

    public WorldEditorWindow()
    {
        InitializeComponent();
        PropertyEditor.PropertyEdited += PropertyEditor_PropertyEdited;
        PropertyEditor.GetRooms = () => _world.Rooms;
        MapPanel.RoomClicked += MapPanel_RoomClicked;
        MapPanel.MapEdited += MapPanel_MapEdited;
        BuildTree();
        MapPanel.SetWorld(_world);
        UpdateButtonsForSelection(null);
        ResetUndoRedo();
    }

    public WorldEditorWindow(string? worldPath) : this()
    {
        // Si nos pasan una ruta válida, intentamos cargar ese mundo.
        if (!string.IsNullOrWhiteSpace(worldPath) && System.IO.File.Exists(worldPath))
        {
            try
            {
                _world = WorldLoader.LoadWorldModel(worldPath);
                _currentPath = worldPath;
            }
            catch (Exception ex)
            {
                new AlertWindow($"Error al abrir mundo:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
                _world = new WorldModel();
                _currentPath = null;
            }
        }
        else
        {
            // Si no hay mundo seleccionado, creamos uno nuevo sencillo por defecto.
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
        UpdateButtonsForSelection(null);
        ResetUndoRedo();
    }

    private void PropertyEditor_PropertyEdited(object? obj, string propertyName)
    {
        if (obj is null) return;

        // Si se ha editado el nombre, actualizamos el nodo correspondiente en el árbol
        if (propertyName == "Name")
        {
            foreach (TreeViewItem root in WorldTree.Items)
            {
                UpdateTreeHeaderRecursive(root, obj);
            }
        }

        PushUndoSnapshot();
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

    private void MapPanel_MapEdited()
    {
        PushUndoSnapshot();
    }

    private void BuildTree()
    {
        WorldTree.Items.Clear();

        _world.Doors ??= new List<Door>();
        _world.Keys ??= new List<KeyDefinition>();

        var gameNode = new TreeViewItem { Header = "Juego", Tag = _world.Game, Foreground = Brushes.White };
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

        // Seleccionar por defecto el nodo Juego al (re)construir el árbol
        gameNode.IsSelected = true;
        gameNode.Focus();
    }

    private void WorldTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (WorldTree.SelectedItem is not TreeViewItem item)
        {
            PropertyEditor.SetObject(null);
            UpdateButtonsForSelection(null);
            MapPanel.SetSelectedRoom(null);
            return;
        }

        PropertyEditor.SetObject(item.Tag);
        UpdateButtonsForSelection(item.Tag);

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

    private void UpdateButtonsForSelection(object? selected)
    {
        // Los botones de imagen/música de sala ya no se usan en esta versión.
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
        var text = SearchTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = text.ToLowerInvariant();

        foreach (TreeViewItem root in WorldTree.Items)
        {
            var found = FindInTree(root, text);
            if (found != null)
            {
                found.IsSelected = true;
                found.BringIntoView();
                break;
            }
        }
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
            Description = "Esta es la sala inicial de tu mundo."
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
            try
            {
                _world = WorldLoader.LoadWorldModel(dlg.FileName);
                _currentPath = dlg.FileName;
                MapPanel.SetWorld(_world);
                BuildTree();
                ResetUndoRedo();
            }
            catch (Exception ex)
            {
                new AlertWindow($"Error al abrir mundo:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
            }
        }
    }

    

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlayRunning)
            return;

        if (_world == null)
            return;

        _isPlayRunning = true;
        PlayButton.IsEnabled = false;

        try
        {
            // Sincronizamos posiciones del mapa con el modelo y guardamos el mundo actual.
            try
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

                Directory.CreateDirectory(AppPaths.WorldsFolder);

                if (string.IsNullOrEmpty(_currentPath))
                {
                    var baseName = string.IsNullOrWhiteSpace(_world.Game.Id)
                        ? "mundo_desde_editor"
                        : _world.Game.Id;
                    _currentPath = Path.Combine(AppPaths.WorldsFolder, baseName + ".xaw");
                }

                WorldLoader.SaveWorldModel(_world, _currentPath);
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
            // Respetar la configuración de sonido global
            uiSettings.SoundEnabled = UiSettingsManager.GlobalSettings.SoundEnabled;

            var soundManager = new SoundManager(AppPaths.SoundFolder)
            {
                SoundEnabled = uiSettings.SoundEnabled
            };

            var main = new MainWindow(world, state, soundManager, uiSettings)
            {
                Owner = this
            };

            // Mostramos la ventana de juego como diálogo modal para no abrir varios tests a la vez.
            main.ShowDialog();
        }
        finally
        {
            _isPlayRunning = false;
            if (PlayButton != null)
                PlayButton.IsEnabled = true;
        }
    }

private void SaveMenu_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentPath))
        {
            SaveAsMenu_Click(sender, e);
            return;
        }

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
            WorldLoader.SaveWorldModel(_world, _currentPath);
        }
        catch (Exception ex)
        {
            new AlertWindow($"Error al guardar mundo:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
        }
    }

    private void SaveAsMenu_Click(object sender, RoutedEventArgs e)
    {
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

        _world.Objects.Remove(obj);

        // Quitar referencias desde salas y otros objetos / NPCs
        foreach (var room in _world.Rooms)
        {
            room.ObjectIds.Remove(obj.Id);
        }

        foreach (var other in _world.Objects)
        {
            other.ContainedObjectIds.Remove(obj.Id);
        }

        foreach (var npc in _world.Npcs)
        {
            npc.InventoryObjectIds.Remove(obj.Id);
        }

        BuildTree();
        MapPanel.SetWorld(_world);
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

        // Quitar referencias a esta puerta desde las salidas
        foreach (var room in _world.Rooms)
        {
            foreach (var ex in room.Exits)
            {
                if (string.Equals(ex.DoorId, door.Id, StringComparison.OrdinalIgnoreCase))
                {
                    ex.DoorId = null;
                }
            }
        }

        _world.Doors?.Remove(door);

        BuildTree();
        MapPanel.SetWorld(_world);
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
        if (e.Command == ApplicationCommands.Cut || e.Command == ApplicationCommands.Copy)
        {
            var selected = MapPanel.GetSelectedRooms();
            e.CanExecute = selected != null && selected.Count > 0;
            e.Handled = true;
        }
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
    }

    private void PushUndoSnapshot()
    {
        var snapshot = CreateSnapshot();
        _undoRedo.Push(snapshot);
        CommandManager.InvalidateRequerySuggested();
    }

    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        var world = JsonSerializer.Deserialize<WorldModel>(snapshot.WorldJson, SnapshotJsonOptions);
        if (world == null)
            return;

        _world = world;
        MapPanel.SetWorld(_world);
        BuildTree();
        UpdateButtonsForSelection(null);
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
        var dlg = new ConfirmWindow("¿Seguro que quieres cerrar el editor de mundos?", "Cerrar editor")
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