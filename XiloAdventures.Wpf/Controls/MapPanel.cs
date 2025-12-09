using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Windows;
using XiloAdventures.Wpf.Common.Windows;

namespace XiloAdventures.Wpf.Controls;

public partial class MapPanel : Control
{
    private const double RoomBoxWidth = 160;
    private const double RoomBoxHeight = 90;
    private const double RoomSpacingMargin = 50.0;

    private WorldModel? _world;

    // Estado del grid y snap-to-grid
    private bool _showGrid = false;
    private bool _snapToGrid = true;

    // Posiciones lógicas (coordenadas de mapa, no píxeles) del centro de cada sala
    private readonly Dictionary<string, Point> _roomPositions = new();
    // Rectángulos en pantalla (tras zoom + pan) para hit testing
    private readonly Dictionary<string, Rect> _roomRects = new();
    // Salas seleccionadas (por Id)
    private readonly HashSet<string> _selectedRoomIds = new();
    // Rectángulos de los puertos de salida (roomId, dirección)
    private readonly Dictionary<(string roomId, string direction), Rect> _portRects = new();
    // Rectángulos de hit testing para las salidas (exits), indexadas por sala + índice de salida
    private readonly Dictionary<(string roomId, int exitIndex), Rect> _exitHitRects = new();
    // Salidas seleccionadas (por sala + índice)
    private readonly HashSet<(string roomId, int exitIndex)> _selectedExits = new();
    // Rectángulos de iconos de objetos y NPC por sala
    private readonly Dictionary<string, Rect> _roomObjectIconRects = new();
    private readonly Dictionary<string, Rect> _roomNpcIconRects = new();
    private readonly Dictionary<string, Rect> _roomStartIconRects = new();
    private readonly Dictionary<string, Rect> _doorIconRects = new();
    private readonly Dictionary<string, Rect> _keyIconRects = new();
    private readonly Dictionary<string, KeyDefinition> _keyIconKeyDefs = new();


    // Tooltip para iconos de objetos/NPCs
    private ToolTip? _iconToolTip;
    private string? _iconTooltipRoomId;
    private bool _iconTooltipIsObjectIcon;

    // Tooltip para imagen de salas
    private ToolTip? _roomImageToolTip;
    private string? _roomImageTooltipRoomId;

    private string? _pendingPortDirection;

    // Transformación de vista
    private double _zoom = 1.0;
    private Point _offset = new(0, 0); // offset lógico

    // Pan con botón central
    private bool _isPanning;
    private Point _lastMiddleDown;

    // Arrastre de salas
    private bool _isDraggingRooms;
    private Point _dragStartMouseScreen;
    private readonly Dictionary<string, Point> _dragStartLogicalPositions = new();

    // Selección por rectángulo
    private bool _isDragSelecting;
    private Point _selectionStartScreen;
    private Point _selectionEndScreen;

    // Creación de conexiones (Ctrl + arrastrar de una sala a otra)
    private Room? _connectionStart;
    private Point _connectionCurrentMouseScreen;

    // Para distinguir click de arrastre
    private Point _mouseDownScreen;
    private Room? _mouseDownRoom;

    public event Action<Room>? RoomClicked;
    public event Action<Door>? DoorClicked;
    public event Action<Door>? DoorKeyRequested;
    public event Action<Door, KeyDefinition?, GameObject?>? DoorCreated;
    public event Action<Door>? DoorDoubleClicked;
    public event Action<KeyDefinition>? KeyDoubleClicked;
    public event Action<Room>? RoomDoubleClicked;
    public event Action<Room, int>? ExitDoubleClicked;
    public event Action<Point>? EmptyMapDoubleClicked;
    public event Action? SelectionCleared;
    public event Action? MapEdited;

    public event Action<Room>? AddObjectToRoomRequested;
    public event Action<Room>? AddNpcToRoomRequested;

    static MapPanel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(MapPanel),
            new FrameworkPropertyMetadata(typeof(MapPanel)));
    }

    public MapPanel()
    {
        Focusable = true;
        Background = Brushes.Transparent;
    }

    public void SetWorld(WorldModel world)
    {
        // Si la instancia de mundo cambia (abrir archivo, mundo nuevo), reseteamos totalmente
        // las posiciones. Si es el mismo objeto, conservamos las posiciones existentes y solo
        // limpiamos las de salas eliminadas.
        if (!ReferenceEquals(_world, world))
        {
            _world = world;
            _roomPositions.Clear();

            // Si el mundo trae posiciones guardadas, las aplicamos.
            if (_world.RoomPositions != null)
            {
                foreach (var kv in _world.RoomPositions)
                {
                    _roomPositions[kv.Key] = new Point(kv.Value.X, kv.Value.Y);
                }
            }
        }
        else if (_world != null)
        {
            var existingIds = new HashSet<string>(_world.Rooms.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var key in _roomPositions.Keys.ToList())
            {
                if (!existingIds.Contains(key))
                    _roomPositions.Remove(key);
            }
        }

        _roomRects.Clear();
        _portRects.Clear();
        _exitHitRects.Clear();
        _roomObjectIconRects.Clear();
        _roomNpcIconRects.Clear();
        _selectedRoomIds.Clear();
        _selectedExits.Clear();
        _connectionStart = null;
        _keyIconRects.Clear();
        _keyIconKeyDefs.Clear();
        HideIconTooltip();
        EnsureLayout();
        InvalidateVisual();
    }

    public void SetSelectedRoom(Room? room)
    {
        _selectedRoomIds.Clear();
        if (room != null)
        {
            _selectedRoomIds.Add(room.Id);
        }
        InvalidateVisual();
    }

    public void CenterOnRoom(Room room)
    {
        if (_world == null || room == null)
            return;

        if (!_roomPositions.ContainsKey(room.Id))
        {
            // Nos aseguramos de tener posiciones calculadas
            EnsureLayout();
        }

        if (!_roomPositions.TryGetValue(room.Id, out var logicalCenter))
            return;

        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        // Calculamos el offset lógico necesario para centrar la sala en pantalla
        _offset = new Point(
            width / (2.0 * _zoom) - logicalCenter.X,
            height / (2.0 * _zoom) - logicalCenter.Y);

        InvalidateVisual();
    }

    /// <summary>
    /// Devuelve las salas actualmente seleccionadas.
    /// </summary>
    public IReadOnlyList<Room> GetSelectedRooms()
    {
        if (_world == null)
            return Array.Empty<Room>();

        return _world.Rooms
            .Where(r => _selectedRoomIds.Contains(r.Id))
            .ToList();
    }

    /// <summary>
    /// Marca como seleccionadas las salas indicadas (si pertenecen al mundo actual).
    /// </summary>
    public void SetSelectedRooms(IEnumerable<Room> rooms)
    {
        _selectedRoomIds.Clear();

        if (_world == null)
        {
            InvalidateVisual();
            return;
        }

        var validIds = new HashSet<string>(_world.Rooms.Select(r => r.Id));

        foreach (var room in rooms)
        {
            if (validIds.Contains(room.Id))
            {
                _selectedRoomIds.Add(room.Id);
            }
        }

        InvalidateVisual();
    }

    public void ClearSelection()
    {
        _selectedRoomIds.Clear();
        _selectedExits.Clear();
        InvalidateVisual();
    }

    /// <summary>
    /// Devuelve las posiciones lógicas actuales de las salas indicadas.
    /// </summary>
    public IReadOnlyDictionary<string, Point> GetRoomPositions(IEnumerable<string> roomIds)
    {
        var result = new Dictionary<string, Point>();
        foreach (var id in roomIds)
        {
            if (_roomPositions.TryGetValue(id, out var p))
            {
                result[id] = p;
            }
        }
        return result;
    }

    /// <summary>
    /// Establece la posición lógica de una sala específica.
    /// </summary>
    public void SetRoomPosition(string roomId, Point position)
    {
        if (_world == null)
            return;

        _roomPositions[roomId] = position;
        UpdateRoomRects();
        InvalidateVisual();
    }

    /// <summary>
    /// Establece las posiciones lógicas de varias salas a la vez.
    /// </summary>
    public void SetRoomsPositions(IDictionary<string, Point> positions)
    {
        if (_world == null)
            return;

        foreach (var kv in positions)
        {
            _roomPositions[kv.Key] = kv.Value;
        }

        UpdateRoomRects();
        InvalidateVisual();
    }

    /// <summary>
    /// Coloca las salas indicadas en la esquina superior izquierda del mapa, en fila.
    /// Útil para "Pegar".
    /// </summary>
    public void PlaceRoomsAtTopLeft(IEnumerable<Room> rooms)
    {
        double x = RoomBoxWidth / 2 + 20;
        double y = RoomBoxHeight / 2 + 20;
        double stepX = RoomBoxWidth + RoomSpacingMargin;

        double width = ActualWidth;
        double height = ActualHeight;
        bool hasSize = width > 0 && height > 0;

        var placedCenters = new Dictionary<string, Point>();

        foreach (var room in rooms)
        {
            Point candidate = new(x, y);

            if (hasSize)
            {
                candidate = ClampRoomCenterToMap(candidate);

                int safetyCounter = 0;
                while (HasCollisionWithOtherRooms(room.Id, candidate, RoomSpacingMargin, null) ||
                       placedCenters.Values.Any(p =>
                           GetRoomRectFromCenter(candidate, RoomSpacingMargin)
                               .IntersectsWith(GetRoomRectFromCenter(p, RoomSpacingMargin))))
                {
                    // Desplazamos hacia la derecha hasta encontrar un hueco libre
                    x += stepX;
                    candidate = ClampRoomCenterToMap(new Point(x, y));

                    safetyCounter++;
                    if (safetyCounter > 50)
                        break;
                }
            }

            _roomPositions[room.Id] = candidate;
            placedCenters[room.Id] = candidate;
            x += stepX;
        }

        InvalidateVisual();
    }

    public void ToggleGridVisibility()
    {
        _showGrid = !_showGrid;
        InvalidateVisual();
    }

    public void ToggleSnapToGrid()
    {
        _snapToGrid = !_snapToGrid;
    }

    public bool IsGridVisible => _showGrid;
    public bool IsSnapToGridEnabled => _snapToGrid;

    public void SetGridVisibility(bool visible)
    {
        _showGrid = visible;
        InvalidateVisual();
    }

    public void SetSnapToGrid(bool enabled)
    {
        _snapToGrid = enabled;
    }

}
