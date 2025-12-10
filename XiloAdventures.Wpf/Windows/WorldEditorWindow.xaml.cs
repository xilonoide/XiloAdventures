using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    public static RoutedCommand ToggleGridCommand = new RoutedCommand();
    public static RoutedCommand ToggleSnapCommand = new RoutedCommand();
    public static RoutedCommand PlayCommand = new RoutedCommand();

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
    private bool _useLlmForGenders;
    private bool _isInitializingCheckbox;
    public bool IsCanceled { get; private set; }

    private static readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://localhost:11434/"),
        Timeout = TimeSpan.FromSeconds(60)
    };

    public WorldEditorWindow()
    {
        InitializeComponent();
        _baseTitle = Title ?? "Editor de mundos";
        PropertyEditor.PropertyEdited += PropertyEditor_PropertyEdited;
        PropertyEditor.RequestDeleteObject += PropertyEditor_RequestDeleteObject;
        PropertyEditor.GetRooms = () => _world.Rooms;
        PropertyEditor.GetMusics = () => _world.Musics;
        PropertyEditor.GetObjects = () => _world.Objects;
        MapPanel.RoomClicked += MapPanel_RoomClicked;
        MapPanel.MapEdited += MapPanel_MapEdited;
        MapPanel.DoorCreated += MapPanel_DoorCreated;
        MapPanel.DoorDoubleClicked += MapPanel_DoorDoubleClicked;
        MapPanel.DoorClicked += MapPanel_DoorClicked;
        MapPanel.SelectionCleared += MapPanel_SelectionCleared;

        MapPanel.AddObjectToRoomRequested += MapPanel_AddObjectToRoomRequested;
        MapPanel.AddNpcToRoomRequested += MapPanel_AddNpcToRoomRequested;
        MapPanel.EmptyMapDoubleClicked += MapPanel_EmptyMapDoubleClicked;
        MapPanel.RoomsDeleteRequested += MapPanel_RoomsDeleteRequested;

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

        bool isNewWorld = false;
        Room? startRoom = null;

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
                _world.ShowGrid = true; // Grid activado por defecto en mundos nuevos

                _world.Rooms.Clear();
                startRoom = new Room
                {
                    Id = "sala_inicio",
                    Name = "Sala inicial",
                    Description = "Esta es la sala inicial de tu mundo."
                };
                _world.Rooms.Add(startRoom);

                // Posición ajustada al grid (centro de la primera celda: 80, 45)
                _world.RoomPositions["sala_inicio"] = new MapPosition { X = 80, Y = 45 };

                _currentPath = null;
                isNewWorld = true;
            }

            MapPanel.SetWorld(_world);
            BuildTree();
            ResetUndoRedo();
            SetDirty(false);

            // Restaurar estado del grid y snap-to-grid desde el mundo
            MapPanel.SetGridVisibility(_world.ShowGrid);
            MapPanel.SetSnapToGrid(_world.SnapToGrid);

            // Sincronizar estado visual de los ToggleButtons
            ToggleGridButton.IsChecked = _world.ShowGrid;
            ToggleSnapButton.IsChecked = _world.SnapToGrid;

            // Inicializar checkbox de IA sin disparar el evento
            _isInitializingCheckbox = true;
            _useLlmForGenders = _world.UseLlmForGenders;
            UseLlmCheckBox.IsChecked = _useLlmForGenders;
            PropertyEditor.IsAiEnabled = _useLlmForGenders;
            _isInitializingCheckbox = false;

            // Si la IA estaba activada en el mundo, iniciar Docker silenciosamente
            if (_useLlmForGenders)
            {
                await EnsureDockerStartedForAiAsync();
            }

            // Centrar en la sala inicial si es un mundo nuevo
            if (isNewWorld && startRoom != null)
            {
                MapPanel.CenterOnRoom(startRoom);
            }

            // Centrar en la sala inicial si se cargó un mundo existente
            if (!string.IsNullOrWhiteSpace(_initialWorldPath) && System.IO.File.Exists(_initialWorldPath))
            {
                startRoom = _world.Rooms.FirstOrDefault(r => r.Id == _world.Game.StartRoomId);
                if (startRoom != null)
                {
                    MapPanel.CenterOnRoom(startRoom);
                }
            }
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

        // Si se ha editado el nombre o IsContainer, actualizamos el nodo correspondiente en el árbol
        if (propertyName == "Name" || propertyName == "IsContainer")
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
                    // Incluir icono 📦 si es contenedor
                    headerText = o.IsContainer ? $"📦 {o.Name}" : o.Name;
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

    private void MapPanel_DoorCreated(Door door, GameObject? createdObject)
    {
        AddDoorToTreeNode(door);

        if (createdObject != null)
            AddObjectToTreeNode(createdObject);

        SelectDoorInTree(door);
        PropertyEditor.SetObject(door);
        SetDirty(true);
    }

    private void MapPanel_DoorDoubleClicked(Door door)
    {
        SelectDoorInTree(door);
        PropertyEditor.SetObject(door);
    }

    private void MapPanel_DoorClicked(Door door)
    {
        SelectDoorInTree(door);
        PropertyEditor.SetObject(door);
    }

    private void BuildTree()
    {
        // Guardar el estado expandido de los nodos antes de reconstruir
        var expandedNodes = new HashSet<string>();
        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.IsExpanded && root.Header is string header)
            {
                expandedNodes.Add(header);
            }
        }

        WorldTree.Items.Clear();
        _gameTreeNode = null;

        _world.Doors ??= new List<Door>();

        var gameNode = new TreeViewItem { Header = "Juego", Tag = _world.Game, Foreground = Brushes.White };
        _gameTreeNode = gameNode;

        // Nodo Jugador como hijo de Juego
        _world.Player ??= new PlayerDefinition();
        var playerNode = new TreeViewItem { Header = "Jugador", Tag = _world.Player, Foreground = Brushes.White };
        gameNode.Items.Add(playerNode);

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
            // Solo añadir objetos que NO están contenidos en otros
            if (!IsObjectContainedInAnother(obj))
            {
                BuildObjectTreeRecursive(objsRoot, obj);
            }
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

        // Restaurar el estado expandido de los nodos
        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.Header is string header && expandedNodes.Contains(header))
            {
                root.IsExpanded = true;
            }
        }

        // Seleccionar por defecto el nodo Juego al (re)construir el árbol solo si no hay nodos expandidos
        if (expandedNodes.Count == 0)
        {
            SelectGameTreeNode();
        }
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

    private Point? _dragStartPoint;

    private void WorldTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void WorldTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _dragStartPoint.HasValue)
        {
            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint.Value - currentPosition;

            // Solo iniciar drag si el movimiento es significativo (más de 5 píxeles)
            if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
            {
                if (WorldTree.SelectedItem is TreeViewItem item)
                {
                    // Solo permitir drag de objetos y NPCs
                    if (item.Tag is GameObject gameObj)
                    {
                        var data = new DataObject();
                        data.SetData("GameObject", gameObj);
                        DragDrop.DoDragDrop(WorldTree, data, DragDropEffects.Move);
                        _dragStartPoint = null;
                    }
                    else if (item.Tag is Npc npc)
                    {
                        var data = new DataObject();
                        data.SetData("Npc", npc);
                        DragDrop.DoDragDrop(WorldTree, data, DragDropEffects.Move);
                        _dragStartPoint = null;
                    }
                }
            }
        }
    }

    private void WorldTree_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("GameObject") || e.Data.GetDataPresent("Npc"))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void WorldTree_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("GameObject") || e.Data.GetDataPresent("Npc"))
        {
            // Obtener el TreeViewItem sobre el que se está arrastrando
            var targetItem = GetTreeViewItemAtPoint(WorldTree, e.GetPosition(WorldTree));

            if (targetItem != null)
            {
                // Validar si es una operación válida
                bool isValid = false;

                if (e.Data.GetDataPresent("GameObject"))
                {
                    var draggedObj = e.Data.GetData("GameObject") as GameObject;
                    if (draggedObj != null)
                    {
                        // Objeto → Contenedor (debe ser diferente y no crear referencia circular)
                        if (targetItem.Tag is GameObject targetObj && targetObj.IsContainer && targetObj.Id != draggedObj.Id)
                        {
                            // Verificar que no se cree una referencia circular
                            if (!WouldCreateCircularReference(draggedObj, targetObj))
                            {
                                // Verificar capacidad del contenedor (si tiene límite)
                                if (targetObj.MaxCapacity > 0)
                                {
                                    double currentVolume = CalculateContainerUsedVolume(targetObj);
                                    double newVolume = currentVolume + draggedObj.Volume;

                                    // Solo permitir si no se excede la capacidad
                                    isValid = newVolume <= targetObj.MaxCapacity;
                                }
                                else
                                {
                                    // Sin límite de capacidad
                                    isValid = true;
                                }
                            }
                        }
                        // Objeto → Sala
                        else if (targetItem.Tag is Room)
                        {
                            isValid = true;
                        }
                    }
                }
                else if (e.Data.GetDataPresent("Npc"))
                {
                    // NPC → Sala
                    if (targetItem.Tag is Room)
                    {
                        isValid = true;
                    }
                }

                e.Effects = isValid ? DragDropEffects.Move : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void WorldTree_Drop(object sender, DragEventArgs e)
    {
        try
        {
            var targetItem = GetTreeViewItemAtPoint(WorldTree, e.GetPosition(WorldTree));
            if (targetItem == null) return;

            bool changed = false;

            if (e.Data.GetDataPresent("GameObject"))
            {
                var draggedObj = e.Data.GetData("GameObject") as GameObject;
                if (draggedObj != null)
                {
                    // Objeto → Contenedor
                    if (targetItem.Tag is GameObject targetObj && targetObj.IsContainer && targetObj.Id != draggedObj.Id)
                    {
                        // Verificar capacidad del contenedor (si tiene límite)
                        if (targetObj.MaxCapacity > 0)
                        {
                            double currentVolume = CalculateContainerUsedVolume(targetObj);
                            double newVolume = currentVolume + draggedObj.Volume;

                            if (newVolume > targetObj.MaxCapacity)
                            {
                                var dlg = new AlertWindow(
                                    $"No hay espacio suficiente en '{targetObj.Name}'.\n\n" +
                                    $"Capacidad máxima: {targetObj.MaxCapacity:F3} m³\n" +
                                    $"Volumen usado: {currentVolume:F3} m³\n" +
                                    $"Espacio disponible: {(targetObj.MaxCapacity - currentVolume):F3} m³\n" +
                                    $"Volumen del objeto: {draggedObj.Volume:F3} m³",
                                    "Capacidad excedida")
                                {
                                    Owner = this
                                };
                                dlg.ShowDialog();
                                return;
                            }
                        }

                        // Quitar de contenedor anterior si estaba en uno
                        foreach (var container in _world.Objects.Where(o => o.IsContainer))
                        {
                            if (container.ContainedObjectIds.Contains(draggedObj.Id, StringComparer.OrdinalIgnoreCase))
                            {
                                container.ContainedObjectIds.Remove(draggedObj.Id);
                            }
                        }

                        // Añadir al nuevo contenedor
                        if (!targetObj.ContainedObjectIds.Contains(draggedObj.Id, StringComparer.OrdinalIgnoreCase))
                        {
                            targetObj.ContainedObjectIds.Add(draggedObj.Id);
                        }

                        // Sincronizar la sala del objeto con la del contenedor
                        draggedObj.RoomId = targetObj.RoomId;

                        changed = true;
                    }
                    // Objeto → Sala
                    else if (targetItem.Tag is Room targetRoom)
                    {
                        // Quitar del contenedor si estaba en uno
                        foreach (var container in _world.Objects.Where(o => o.IsContainer))
                        {
                            if (container.ContainedObjectIds.Contains(draggedObj.Id, StringComparer.OrdinalIgnoreCase))
                            {
                                container.ContainedObjectIds.Remove(draggedObj.Id);
                            }
                        }

                        // Cambiar la sala del objeto
                        draggedObj.RoomId = targetRoom.Id;
                        changed = true;
                    }
                }
            }
            else if (e.Data.GetDataPresent("Npc"))
            {
                var draggedNpc = e.Data.GetData("Npc") as Npc;
                if (draggedNpc != null && targetItem.Tag is Room targetRoom)
                {
                    // NPC → Sala
                    draggedNpc.RoomId = targetRoom.Id;
                    changed = true;
                }
            }

            if (changed)
            {
                BuildTree();
                MapPanel.InvalidateVisual();
                PropertyEditor.SetObject(WorldTree.SelectedItem is TreeViewItem item ? item.Tag : null);
                PushUndoSnapshot();
                SetDirty(true);
            }

            e.Handled = true;
        }
        catch
        {
            // Ignorar errores
        }
    }

    private TreeViewItem? GetTreeViewItemAtPoint(TreeView treeView, Point point)
    {
        var hitTestResult = VisualTreeHelper.HitTest(treeView, point);
        if (hitTestResult == null) return null;

        var element = hitTestResult.VisualHit;
        while (element != null && element != treeView)
        {
            if (element is TreeViewItem item)
            {
                return item;
            }
            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    /// <summary>
    /// Verifica si poner draggedObj dentro de targetContainer crearía una referencia circular.
    /// Por ejemplo, si draggedObj contiene targetContainer (directa o indirectamente).
    /// </summary>
    private bool WouldCreateCircularReference(GameObject draggedObj, GameObject targetContainer)
    {
        if (!draggedObj.IsContainer)
        {
            // Si draggedObj no es contenedor, no puede crear referencias circulares
            return false;
        }

        // Verificar si targetContainer está contenido (directa o indirectamente) en draggedObj
        return IsObjectContainedIn(targetContainer, draggedObj);
    }

    /// <summary>
    /// Verifica si obj está contenido (directa o indirectamente) en container.
    /// </summary>
    private bool IsObjectContainedIn(GameObject obj, GameObject container)
    {
        if (!container.IsContainer)
            return false;

        // Verificación directa
        if (container.ContainedObjectIds.Contains(obj.Id, StringComparer.OrdinalIgnoreCase))
            return true;

        // Verificación indirecta (recursiva)
        foreach (var containedId in container.ContainedObjectIds)
        {
            var containedObj = _world.Objects.FirstOrDefault(o =>
                string.Equals(o.Id, containedId, StringComparison.OrdinalIgnoreCase));

            if (containedObj != null && containedObj.IsContainer)
            {
                if (IsObjectContainedIn(obj, containedObj))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Verifica si un objeto está contenido dentro de algún otro objeto.
    /// </summary>
    private bool IsObjectContainedInAnother(GameObject obj)
    {
        return _world.Objects.Any(container =>
            container.IsContainer &&
            container.ContainedObjectIds.Contains(obj.Id, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Construye recursivamente el árbol de objetos, incluyendo objetos contenidos como hijos.
    /// </summary>
    private void BuildObjectTreeRecursive(TreeViewItem parentNode, GameObject obj)
    {
        var header = obj.IsContainer ? $"📦 {obj.Name}" : obj.Name;
        var objNode = new TreeViewItem { Header = header, Tag = obj, Foreground = Brushes.White };

        // Si es un contenedor, añadir sus objetos contenidos como hijos
        if (obj.IsContainer && obj.ContainedObjectIds.Count > 0)
        {
            foreach (var containedId in obj.ContainedObjectIds)
            {
                var containedObj = _world.Objects.FirstOrDefault(o =>
                    string.Equals(o.Id, containedId, StringComparison.OrdinalIgnoreCase));

                if (containedObj != null)
                {
                    BuildObjectTreeRecursive(objNode, containedObj);
                }
            }
        }

        parentNode.Items.Add(objNode);
    }

    /// <summary>
    /// Calcula el volumen total de los objetos contenidos en un contenedor.
    /// </summary>
    private double CalculateContainerUsedVolume(GameObject container)
    {
        if (!container.IsContainer)
            return 0;

        double totalVolume = 0;

        foreach (var containedId in container.ContainedObjectIds)
        {
            var containedObj = _world.Objects.FirstOrDefault(o =>
                string.Equals(o.Id, containedId, StringComparison.OrdinalIgnoreCase));

            if (containedObj != null)
            {
                totalVolume += containedObj.Volume;
            }
        }

        return totalVolume;
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
        _world.ShowGrid = true; // Grid activado por defecto en mundos nuevos

        var startRoom = new Room
        {
            Id = "sala_inicio",
            Name = "Sala inicial",
            Description = "La sala inicial de tu mundo."
        };
        _world.Rooms.Add(startRoom);

        // Posición ajustada al grid (centro de la primera celda: 80, 45)
        _world.RoomPositions["sala_inicio"] = new MapPosition { X = 80, Y = 45 };

        _currentPath = null;
        MapPanel.SetWorld(_world);
        BuildTree();
        ResetUndoRedo();

        // Sincronizar estado visual del grid
        MapPanel.SetGridVisibility(_world.ShowGrid);
        MapPanel.SetSnapToGrid(_world.SnapToGrid);
        ToggleGridButton.IsChecked = _world.ShowGrid;
        ToggleSnapButton.IsChecked = _world.SnapToGrid;

        // Centrar el mapa en la sala inicial
        MapPanel.CenterOnRoom(startRoom);
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

        ShowPlayLoading("Guardando mundo...");
        await Dispatcher.Yield();

        // Guardar antes de lanzar la partida de prueba (y validar clave de encriptación)
        // No ocultamos el loading al terminar de guardar para mantener el overlay visible
        if (!await PerformSaveAsync(hideLoadingOnComplete: false))
        {
            // Si el save falla (por clave incorrecta u otro error), no continuar
            HidePlayLoading();
            return;
        }

        // Cambiar mensaje a "Preparando todo..." después de guardar
        ShowPlayLoading("Preparando todo...");
        await Dispatcher.Yield();

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

            // Ocultar el loading antes de mostrar la ventana del jugador
            HidePlayLoading();

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

    /// <summary>
    /// Valida que las características del jugador sumen exactamente 100 puntos.
    /// Retorna true si es válido, false si no lo es.
    /// </summary>
    private bool ValidatePlayerAttributes()
    {
        if (_world?.Player == null)
            return true;

        var total = _world.Player.TotalAttributePoints;
        if (total != 100)
        {
            new AlertWindow(
                $"Las características del jugador deben sumar exactamente 100 puntos.\n\n" +
                $"Suma actual: {total} puntos\n" +
                $"Diferencia: {(total > 100 ? "+" : "")}{total - 100} puntos",
                "Características incorrectas")
            {
                Owner = this
            }.ShowDialog();

            // Seleccionar el nodo Jugador para facilitar la corrección
            SelectPlayerInTree();
            return false;
        }

        // Validar y corregir dinero inicial si es negativo
        if (_world.Player.InitialGold < 0)
        {
            _world.Player.InitialGold = 0;
        }

        return true;
    }

    /// <summary>
    /// Selecciona el nodo Jugador en el árbol.
    /// </summary>
    private void SelectPlayerInTree()
    {
        if (_world?.Player == null) return;

        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.Header?.ToString() == "Juego")
            {
                root.IsExpanded = true;
                foreach (TreeViewItem child in root.Items.OfType<TreeViewItem>())
                {
                    if (child.Tag == _world.Player)
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

    /// <summary>
    /// Valida que todas las puertas con cerradura tengan una llave asignada.
    /// Retorna true si es válido, false si no lo es.
    /// </summary>
    private bool ValidateDoorKeys()
    {
        if (_world?.Doors == null)
            return true;

        var doorsWithoutKey = _world.Doors
            .Where(d => d.IsLocked && string.IsNullOrWhiteSpace(d.KeyObjectId))
            .ToList();

        if (doorsWithoutKey.Count > 0)
        {
            var doorNames = string.Join("\n• ", doorsWithoutKey.Select(d =>
                string.IsNullOrWhiteSpace(d.Name) ? d.Id : d.Name));

            new AlertWindow(
                $"Las siguientes puertas tienen cerradura pero no tienen llave asignada:\n\n• {doorNames}\n\nAsigna una llave o desactiva la cerradura.",
                "Puertas sin llave")
            {
                Owner = this
            }.ShowDialog();

            // Seleccionar la primera puerta sin llave
            SelectDoorInTree(doorsWithoutKey[0]);
            PropertyEditor.SetObject(doorsWithoutKey[0]);
            return false;
        }

        return true;
    }

    private async void SaveMenu_Click(object sender, RoutedEventArgs e)
    {
        await PerformSaveAsync();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await PerformSaveAsync();
    }

    private async Task<bool> PerformSaveAsync(bool hideLoadingOnComplete = true)
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

        // Aplicar validaciones pendientes del PropertyEditor antes de guardar
        PropertyEditor.ApplyPendingValidations();

        // Validar clave de encriptación
        if (!ValidateEncryptionKey())
            return false;

        // Validar características del jugador
        if (!ValidatePlayerAttributes())
            return false;

        // Validar puertas con cerradura tengan llave asignada
        if (!ValidateDoorKeys())
            return false;

        // Si la IA está activada, determinar géneros gramaticales antes de guardar
        if (_useLlmForGenders)
        {
            await ApplyLlmGendersAsync();
            // Refrescar el PropertyEditor para mostrar los cambios del LLM
            RefreshPropertyEditor();
        }

        ShowPlayLoading("Guardando mundo...");

        try
        {
            await Task.Run(() =>
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

                    // Guardar estado del grid y snap-to-grid
                    _world.ShowGrid = MapPanel.IsGridVisible;
                    _world.SnapToGrid = MapPanel.IsSnapToGridEnabled;

                    // Guardar configuración de IA
                    _world.UseLlmForGenders = _useLlmForGenders;
                }

                Directory.CreateDirectory(AppPaths.WorldsFolder);
                WorldLoader.SaveWorldModel(_world!, _currentPath);
            });

            SetDirty(false);
            if (hideLoadingOnComplete)
                HidePlayLoading();
            return true;
        }
        catch (Exception ex)
        {
            HidePlayLoading();
            new AlertWindow($"Error al guardar mundo:\n{ex.Message}", "Error") { Owner = this }.ShowDialog();
            return false;
        }
    }

    private bool PerformSave()
    {
        return PerformSaveAsync().GetAwaiter().GetResult();
    }

    private void SaveAsMenu_Click(object sender, RoutedEventArgs e)
    {
        // Aplicar validaciones pendientes del PropertyEditor antes de guardar
        PropertyEditor.ApplyPendingValidations();

        if (!ValidateEncryptionKey())
            return;

        if (!ValidatePlayerAttributes())
            return;

        if (!ValidateDoorKeys())
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

    private void PlayCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        PlayButton_Click(sender, e);
    }

    private void CloseMenu_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MusicMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_world == null)
        {
            new AlertWindow("No hay ningún mundo abierto.", "Gestión de Música")
            {
                Owner = this
            }.ShowDialog();
            return;
        }

        // Guardar el objeto actualmente seleccionado
        var currentObject = PropertyEditor.GetCurrentObject();

        var musicWindow = new MusicManagerWindow(_world)
        {
            Owner = this
        };
        musicWindow.ShowDialog();

        // Marcar como modificado si se han hecho cambios
        SetDirty(true);

        // Recargar el PropertyEditor para actualizar los combos de música
        if (currentObject != null)
        {
            PropertyEditor.SetObject(currentObject);
        }
    }

    private void FxMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_world == null)
        {
            new AlertWindow("No hay ningún mundo abierto.", "Gestión de FX")
            {
                Owner = this
            }.ShowDialog();
            return;
        }

        var fxWindow = new FxManagerWindow(_world)
        {
            Owner = this
        };
        fxWindow.ShowDialog();

        SetDirty(true);
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
            IsLocked = false,
            KeyObjectId = null,
            OpenFromSide = DoorOpenSide.Both
        };

        // Si hay una sala seleccionada, la usamos como RoomIdA por defecto
        if (WorldTree.SelectedItem is TreeViewItem item && item.Tag is Room room)
        {
            door.RoomIdA = room.Id;
        }

        _world.Doors.Add(door);
        BuildTree();
        SelectDoorInTree(door);
        PropertyEditor.SetObject(door);
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
        SelectObjectInTree(obj);
        PropertyEditor.SetObject(obj);
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
        SelectNpcInTree(npc);
        PropertyEditor.SetObject(npc);
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
        SelectQuestInTree(q);
        PropertyEditor.SetObject(q);
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

    /// <summary>
    /// Elimina un objeto por su ID sin pedir confirmación (usada desde PropertyEditor).
    /// </summary>
    private void PropertyEditor_RequestDeleteObject(string objectId)
    {
        if (string.IsNullOrEmpty(objectId)) return;

        var obj = _world.Objects.FirstOrDefault(o => o.Id == objectId);
        if (obj == null) return;

        WorldEditorHelpers.DeleteObject(_world, obj);

        BuildTree();
        MapPanel.SetWorld(_world);
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

    private void SelectDoorInTree(Door door)
    {
        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.Header?.ToString() == "Puertas")
            {
                root.IsExpanded = true;
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

    private void SelectRoomInTree(Room room)
    {
        if (room is null) return;

        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.Header?.ToString() == "Salas")
            {
                root.IsExpanded = true;
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

    /// <summary>
    /// Busca recursivamente un objeto en el árbol y lo selecciona, expandiendo todos los nodos padre.
    /// </summary>
    private bool FindAndSelectInTree(TreeViewItem node, object target)
    {
        // Verificar si este nodo contiene el objeto buscado
        if (node.Tag == target)
        {
            WorldTree.Focus();
            node.IsSelected = true;
            node.BringIntoView();
            node.Focus();
            return true;
        }

        // Buscar recursivamente en los hijos
        foreach (TreeViewItem child in node.Items.OfType<TreeViewItem>())
        {
            if (FindAndSelectInTree(child, target))
            {
                // Expandir este nodo ya que contiene el objeto buscado
                node.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    private void SelectObjectInTree(GameObject obj)
    {
        if (obj is null) return;

        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.Header?.ToString() == "Objetos")
            {
                root.IsExpanded = true;
                FindAndSelectInTree(root, obj);
                return;
            }
        }
    }

    private void SelectNpcInTree(Npc npc)
    {
        if (npc is null) return;

        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.Header?.ToString() == "NPCs")
            {
                root.IsExpanded = true;
                foreach (TreeViewItem child in root.Items.OfType<TreeViewItem>())
                {
                    if (child.Tag == npc)
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

    private void SelectQuestInTree(QuestDefinition quest)
    {
        if (quest is null) return;

        foreach (TreeViewItem root in WorldTree.Items)
        {
            if (root.Header?.ToString() == "Misiones")
            {
                root.IsExpanded = true;
                foreach (TreeViewItem child in root.Items.OfType<TreeViewItem>())
                {
                    if (child.Tag == quest)
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
        UpdateSaveButtonAppearance();
    }

    private void UpdateSaveButtonAppearance()
    {
        // Cuando NO hay cambios: botón se ve "pulsado" (más oscuro)
        // Cuando HAY cambios: botón se ve normal (más claro)
        if (_isDirty)
        {
            // Hay cambios: estilo normal
            SaveButton.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3A3A3A"));
            SaveButton.BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A4A4A"));
        }
        else
        {
            // No hay cambios: estilo "pulsado" (más oscuro/hundido)
            SaveButton.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A2A"));
            SaveButton.BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A1A1A"));
        }
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

                PerformSave();
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

        // Encontrar la posición libre más cercana, considerando snap-to-grid si está activado
        Point freePosition = MapPanel.FindNearestFreePosition(logicalPos);

        // Establecer la posición de la sala en el mapa
        MapPanel.SetRoomPosition(room.Id, freePosition);

        MapPanel.SetWorld(_world);
        BuildTree();
        SelectRoomInTree(room);

        PushUndoSnapshot();
        SetDirty(true);
    }

    private void MapPanel_RoomsDeleteRequested(List<string> roomIds)
    {
        if (roomIds == null || roomIds.Count == 0)
            return;

        // Buscar las salas correspondientes
        var roomsToDelete = new List<Room>();
        foreach (var roomId in roomIds)
        {
            var room = _world.Rooms.FirstOrDefault(r => string.Equals(r.Id, roomId, StringComparison.OrdinalIgnoreCase));
            if (room != null)
                roomsToDelete.Add(room);
        }

        if (roomsToDelete.Count == 0)
            return;

        // Mostrar confirmación
        string message = roomsToDelete.Count == 1
            ? $"¿Eliminar la sala '{roomsToDelete[0].Name}'?"
            : $"¿Eliminar {roomsToDelete.Count} salas seleccionadas?";

        var dlg = new ConfirmWindow(message, "Confirmar eliminación")
        {
            Owner = this
        };

        if (dlg.ShowDialog() != true)
            return;

        // Eliminar cada sala
        foreach (var room in roomsToDelete)
        {
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
        }

        BuildTree();
        MapPanel.SetWorld(_world);
        PushUndoSnapshot();
        SetDirty(true);
    }

    private void ToggleGridButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggleButton)
        {
            bool isChecked = toggleButton.IsChecked ?? false;
            MapPanel.SetGridVisibility(isChecked);
        }
    }

    private void ToggleSnapButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggleButton)
        {
            bool isChecked = toggleButton.IsChecked ?? false;
            MapPanel.SetSnapToGrid(isChecked);
        }
    }

    private void ToggleGrid_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ToggleGridButton.IsChecked = !ToggleGridButton.IsChecked;
        MapPanel.SetGridVisibility(ToggleGridButton.IsChecked ?? false);
    }

    private void ToggleSnap_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ToggleSnapButton.IsChecked = !ToggleSnapButton.IsChecked;
        MapPanel.SetSnapToGrid(ToggleSnapButton.IsChecked ?? false);
    }

    private bool IsDotNet8SdkInstalled()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-sdks",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                return false;

            process.WaitForExit(5000);
            var output = process.StandardOutput.ReadToEnd();

            // Buscar SDK 8.x o superior
            return output.Split('\n').Any(line =>
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    return false;

                var parts = trimmed.Split('.');
                if (parts.Length > 0 && int.TryParse(parts[0], out var majorVersion))
                {
                    return majorVersion >= 8;
                }
                return false;
            });
        }
        catch
        {
            return false;
        }
    }

    private async void ExportMenu_Click(object sender, RoutedEventArgs e)
    {
        // Verificar que el SDK de .NET 8 esté instalado
        if (!IsDotNet8SdkInstalled())
        {
            var alert = new AlertWindow(
                "Para exportar a ejecutable necesitas tener instalado el SDK de .NET 8.",
                "SDK de .NET 8 requerido")
            {
                Owner = this
            };

            // Crear un panel con el link de descarga
            var panel = new System.Windows.Controls.StackPanel();
            var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Descarga el SDK"))
            {
                NavigateUri = new Uri("https://dotnet.microsoft.com/es-es/download/dotnet/thank-you/sdk-8.0.416-windows-x64-installer")
            };
            link.RequestNavigate += (s, args) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = args.Uri.ToString(),
                    UseShellExecute = true
                });
            };

            var linkTextBlock = new System.Windows.Controls.TextBlock
            {
                FontSize = 16,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 180, 255))
            };
            linkTextBlock.Inlines.Add(link);
            panel.Children.Add(linkTextBlock);

            alert.SetCustomContent(panel);
            alert.ShowDialog();
            return;
        }

        // Verificar que el mundo esté guardado
        if (_isDirty)
        {
            var confirmDlg = new ConfirmWindow(
                "Debes guardar el mundo antes de exportar. ¿Quieres guardarlo ahora?",
                "Guardar antes de exportar")
            {
                Owner = this
            };

            if (confirmDlg.ShowDialog() == true)
            {
                await PerformSaveAsync();
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
            new AlertWindow(
                "Debes guardar el mundo en un archivo antes de exportar.",
                "Error")
            {
                Owner = this
            }.ShowDialog();
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
            new AlertWindow(
                $"Error al exportar el ejecutable:\n\n{ex.Message}",
                "Error de exportación")
            {
                Owner = this
            }.ShowDialog();
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

    #region LLM/IA para géneros gramaticales

    /// <summary>
    /// Llama al LLM para determinar el género gramatical y número (singular/plural) de objetos y puertas que no tienen valores manuales.
    /// </summary>
    private async Task ApplyLlmGendersAsync()
    {
        if (_world == null) return;

        // Recopilar nombres de objetos y puertas que NO tienen género/plural manual
        var itemsToAnalyze = new List<(string id, string name, string type)>();

        foreach (var obj in _world.Objects.Where(o => !o.GenderAndPluralSetManually))
        {
            itemsToAnalyze.Add((obj.Id, obj.Name, "objeto"));
        }

        foreach (var door in _world.Doors.Where(d => !d.GenderAndPluralSetManually))
        {
            itemsToAnalyze.Add((door.Id, door.Name, "puerta"));
        }

        if (itemsToAnalyze.Count == 0)
            return;

        ShowPlayLoading("Analizando géneros con IA...");

        try
        {
            // Construir prompt
            var namesListBuilder = new StringBuilder();
            for (int i = 0; i < itemsToAnalyze.Count; i++)
            {
                namesListBuilder.AppendLine($"{i + 1}. {itemsToAnalyze[i].name}");
            }

            var prompt = $@"Eres un experto en gramática española. Analiza los siguientes nombres de objetos/elementos de un juego de aventuras y determina su género gramatical (masculino o femenino) y si son singulares o plurales.

NOMBRES A ANALIZAR:
{namesListBuilder}

INSTRUCCIONES:
1. Para cada nombre, determina:
   - Género: masculino (M) o femenino (F)
   - Número: singular (S) o plural (P)
2. Responde SOLO con una lista numerada con género y número separados por espacio, ejemplo:
1. M S
2. F S
3. M P
4. F P
...

NO añadas explicaciones, solo el número, género (M/F) y número gramatical (S/P).";

            var requestBody = new
            {
                model = "llama3",
                prompt = prompt,
                stream = false,
                options = new { temperature = 0.1 }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/generate", content);
            if (!response.IsSuccessStatusCode)
            {
                // Si falla, simplemente no actualizamos los géneros
                return;
            }

            var responseText = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseText);
            var llmResponse = doc.RootElement.GetProperty("response").GetString() ?? "";

            // Parsear respuesta
            var lines = llmResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Formato esperado: "1. M S" o "1. F P"
                var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                if (!int.TryParse(parts[0].Trim(), out var index)) continue;
                if (index < 1 || index > itemsToAnalyze.Count) continue;

                var valuePart = parts[1].Trim().ToUpperInvariant();
                var tokens = valuePart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // Extraer género (M/F)
                var gender = tokens.Length > 0 && tokens[0].StartsWith("M")
                    ? GrammaticalGender.Masculine
                    : GrammaticalGender.Feminine;

                // Extraer número (S/P) - por defecto singular
                var isPlural = tokens.Length > 1 && tokens[1].StartsWith("P");

                var item = itemsToAnalyze[index - 1];

                // Aplicar género y plural
                if (item.type == "objeto")
                {
                    var obj = _world.Objects.FirstOrDefault(o => o.Id == item.id);
                    if (obj != null && !obj.GenderAndPluralSetManually)
                    {
                        obj.Gender = gender;
                        obj.IsPlural = isPlural;
                    }
                }
                else if (item.type == "puerta")
                {
                    var door = _world.Doors.FirstOrDefault(d => d.Id == item.id);
                    if (door != null && !door.GenderAndPluralSetManually)
                    {
                        door.Gender = gender;
                        door.IsPlural = isPlural;
                    }
                }
            }
        }
        catch
        {
            // Si hay error, simplemente no actualizamos los géneros
        }
        finally
        {
            HidePlayLoading();
        }
    }

    private async void UseLlmCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializingCheckbox)
            return;

        if (UseLlmCheckBox.IsChecked == true)
        {
            var confirmDlg = new ConfirmWindow(
                "Al activar la IA se iniciará Docker Desktop automáticamente.\n\n" +
                "Si es la primera vez que la usas, se descargarán los modelos necesarios (puede tardar varios minutos dependiendo de tu conexión).\n\n" +
                "Al guardar el mundo, la IA determinará el género gramatical (masculino/femenino) y número (singular/plural) de objetos y puertas que no hayan sido configurados manualmente.\n\n" +
                "¿Deseas continuar?",
                "Activar IA")
            {
                Owner = this
            };

            if (confirmDlg.ShowDialog() != true)
            {
                UseLlmCheckBox.IsChecked = false;
                return;
            }

            var progressWindow = new DockerProgressWindow
            {
                Owner = this,
                IncludeTts = false // En el editor solo necesitamos Ollama, no Coqui TTS
            };

            var result = await progressWindow.RunAsync().ConfigureAwait(true);

            if (result.Canceled || !result.Success)
            {
                UseLlmCheckBox.IsChecked = false;
                _useLlmForGenders = false;

                if (!result.Canceled)
                {
                    new AlertWindow(
                        "No se han podido iniciar los servicios de IA. Comprueba que Docker Desktop está instalado y en ejecución.",
                        "Error")
                    {
                        Owner = this
                    }.ShowDialog();
                }
                return;
            }

            _useLlmForGenders = true;
            PropertyEditor.IsAiEnabled = true;
        }
        else
        {
            _useLlmForGenders = false;
            PropertyEditor.IsAiEnabled = false;
        }

        // Refrescar el PropertyEditor para actualizar visibilidad del checkbox de género manual
        RefreshPropertyEditor();
    }

    /// <summary>
    /// Inicia Docker silenciosamente si la IA estaba activada en el mundo.
    /// Si falla, desactiva la IA y muestra un mensaje.
    /// </summary>
    private async Task EnsureDockerStartedForAiAsync()
    {
        var progressWindow = new DockerProgressWindow
        {
            Owner = this,
            IncludeTts = false // En el editor solo necesitamos Ollama, no Coqui TTS
        };

        var result = await progressWindow.RunAsync().ConfigureAwait(true);

        if (result.Canceled || !result.Success)
        {
            // Desactivar la IA si Docker no se pudo iniciar
            _isInitializingCheckbox = true;
            UseLlmCheckBox.IsChecked = false;
            _isInitializingCheckbox = false;
            _useLlmForGenders = false;
            PropertyEditor.IsAiEnabled = false;

            if (!result.Canceled)
            {
                new AlertWindow(
                    "No se han podido iniciar los servicios de IA. La IA ha sido desactivada.\n\n" +
                    "Comprueba que Docker Desktop está instalado y en ejecución.",
                    "Error")
                {
                    Owner = this
                }.ShowDialog();
            }
        }
    }

    private void RefreshPropertyEditor()
    {
        // Guardar el objeto actual y refrescarlo para actualizar la visibilidad del checkbox
        var currentObject = PropertyEditor.GetCurrentObject();
        if (currentObject != null)
        {
            PropertyEditor.SetObject(currentObject);
        }
    }

    private void LlmHelpIcon_Click(object sender, MouseButtonEventArgs e)
    {
        new AlertWindow(
            "IA para géneros gramaticales\n\n" +
            "Cuando está activada, al guardar el mundo la IA determinará automáticamente el género gramatical (masculino/femenino) de:\n" +
            "• Objetos (ej: \"la espada\", \"el cofre\")\n" +
            "• Puertas (ej: \"la puerta\", \"el portón\")\n\n" +
            "Esto permite que los mensajes del juego usen los artículos correctos.\n\n" +
            "IMPORTANTE: Si configuras manualmente el género de un objeto o puerta, la IA no lo sobrescribirá.",
            "Ayuda")
        {
            Owner = this
        }.ShowDialog();
    }

    #endregion
}














