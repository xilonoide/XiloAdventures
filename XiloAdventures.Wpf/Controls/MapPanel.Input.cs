using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Windows;

namespace XiloAdventures.Wpf.Controls;

public partial class MapPanel : Control
{
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        Focus();

        if (_world == null)
            return;

        Point pos = e.GetPosition(this);

        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
        {
            var doubleClickRoom = HitTestRoom(pos);
            if (doubleClickRoom != null)
            {
                RoomDoubleClicked?.Invoke(doubleClickRoom);
                e.Handled = true;
                return;
            }

            // Doble click en una zona libre del mapa: crear una nueva sala.
            var portHit = HitTestPort(pos);
            var exitHit = HitTestExit(pos);

            bool hasPort = portHit.HasValue;
            bool hasExit = exitHit.HasValue;

            if (!hasPort && !hasExit)
            {
                Point logicalPos = ScreenToLogical(pos);
                EmptyMapDoubleClicked?.Invoke(logicalPos);
                e.Handled = true;
                return;
            }
        }

        if (e.ChangedButton == MouseButton.Middle)
        {
            _isPanning = true;
            _lastMiddleDown = pos;
            CaptureMouse();
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            _mouseDownScreen = pos;
            _mouseDownRoom = HitTestRoom(pos);

            // Click sobre un punto de salida (puerto) sin modificadores: usamos conexión basada en dirección fija.
            var portHit = HitTestPort(pos);
            if (Keyboard.Modifiers == ModifierKeys.None && portHit.HasValue)
            {
                var (portRoom, portDirection) = portHit.Value;

                if (_connectionStart == null || string.IsNullOrEmpty(_pendingPortDirection))
                {
                    // Empezamos una conexión desde este puerto.
                    _connectionStart = portRoom;
                    _pendingPortDirection = portDirection;
                    _connectionCurrentMouseScreen = pos;
                    CaptureMouse();
                    InvalidateVisual();
                }
                else if (ReferenceEquals(_connectionStart, portRoom) &&
                         string.Equals(_pendingPortDirection, portDirection, StringComparison.OrdinalIgnoreCase))
                {
                    // Click sobre el mismo puerto de origen: cancelar conexión.
                    _connectionStart = null;
                    _pendingPortDirection = null;
                    InvalidateVisual();
                }
                else
                {
                    // Click sobre un puerto de otra sala: creamos la salida con la dirección del puerto de origen.
                    CreateExitFromPendingPort(portRoom);
                }

                return;
            }


        // Selección de salidas (exits) haciendo click sobre la línea/etiqueta
        // Solo si no hemos hecho hit sobre una sala: la sala tiene prioridad.
        if (_mouseDownRoom == null)
        {
            var exitHit = HitTestExit(pos);
            if (exitHit.HasValue)
            {
                bool ctrlForSelection = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var (exitRoom, exitIndex) = exitHit.Value;
                var exitKey = (exitRoom.Id, exitIndex);

                if (ctrlForSelection)
                {
                    if (_selectedExits.Contains(exitKey))
                    {
                        _selectedExits.Remove(exitKey);
                    }
                    else
                    {
                        _selectedExits.Add(exitKey);
                    }
                }
                else
                {
                    _selectedRoomIds.Clear();
                    _selectedExits.Clear();
                    _selectedExits.Add(exitKey);
                }

                InvalidateVisual();
                return;
            }
        }


            // Inicio de conexión si se hace Alt + click sobre una sala
            if (_mouseDownRoom != null &&
                (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                _connectionStart = _mouseDownRoom;
                _connectionCurrentMouseScreen = pos;
                CaptureMouse();
                return;
            }

            // Si hacemos click sobre una sala, podemos o bien alternar su selección con Ctrl
            // o bien iniciar un posible arrastre (sin Ctrl).
            if (_mouseDownRoom != null)
            {
                bool ctrlForSelection = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                bool shiftForSelection = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                if (ctrlForSelection || shiftForSelection)
                {
                    // CTRL+click: alterna la selección de la sala sin iniciar arrastre.
                    // SHIFT+click: añade la sala a la selección (sin deseleccionar las ya seleccionadas).
                    if (shiftForSelection)
                    {
                        if (!_selectedRoomIds.Contains(_mouseDownRoom.Id))
                        {
                            _selectedRoomIds.Add(_mouseDownRoom.Id);
                        }
                    }
                    else
                    {
                        if (_selectedRoomIds.Contains(_mouseDownRoom.Id))
                        {
                            _selectedRoomIds.Remove(_mouseDownRoom.Id);
                        }
                        else
                        {
                            _selectedRoomIds.Add(_mouseDownRoom.Id);
                        }
                    }

                    InvalidateVisual();
                    return;
                }

                _selectedExits.Clear();
                _isDraggingRooms = true;
                _dragStartMouseScreen = pos;
                _dragStartLogicalPositions.Clear();

                // Si no hay multi-selección que ya incluya esta sala,
                // seleccionamos solo esta.
                if (!_selectedRoomIds.Contains(_mouseDownRoom.Id))
                {
                    _selectedRoomIds.Clear();
                    _selectedRoomIds.Add(_mouseDownRoom.Id);
                }

                foreach (var id in _selectedRoomIds)
                {
                    if (_roomPositions.TryGetValue(id, out var logical))
                    {
                        _dragStartLogicalPositions[id] = logical;
                    }
                }

                CaptureMouse();
                InvalidateVisual();
            }
            else
            {
                // Arrastre de selección en área vacía
                _isDragSelecting = true;
                _selectionStartScreen = pos;
                _selectionEndScreen = pos;
                CaptureMouse();
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_world == null)
            return;

        Point pos = e.GetPosition(this);

        if (_isPanning && e.MiddleButton == MouseButtonState.Pressed)
        {
            Vector delta = pos - _lastMiddleDown;
            _offset = new Point(
                _offset.X + delta.X / _zoom,
                _offset.Y + delta.Y / _zoom);
            _lastMiddleDown = pos;
            InvalidateVisual();
            return;
        }

        if (_connectionStart != null && e.LeftButton == MouseButtonState.Pressed)
        {
            _connectionCurrentMouseScreen = pos;
            InvalidateVisual();
            return;
        }

        if (_isDraggingRooms && e.LeftButton == MouseButtonState.Pressed)
        {
            Vector deltaScreen = pos - _dragStartMouseScreen;
            Vector deltaLogical = new(deltaScreen.X / _zoom, deltaScreen.Y / _zoom);

            foreach (var pair in _dragStartLogicalPositions)
            {
                string id = pair.Key;
                Point startLogical = pair.Value;

                // Posición candidata aplicando el delta del ratón.
                Point candidate = new(
                    startLogical.X + deltaLogical.X,
                    startLogical.Y + deltaLogical.Y);

                // Mantener la sala dentro del área visible del mapa durante el arrastre.
                candidate = ClampRoomCenterToMap(candidate);

                // No comprobamos colisiones aquí; las resolveremos al soltar el ratón
                // para poder acercarnos todo lo posible a la posición deseada.
                _roomPositions[id] = candidate;
            }

            InvalidateVisual();
            return;
        }

        if (_isDragSelecting && e.LeftButton == MouseButtonState.Pressed)
        {
            _selectionEndScreen = pos;
            InvalidateVisual();
            return;
        }

        // Actualizamos el tooltip de iconos (objetos / NPCs) según la posición del ratón
        UpdateIconTooltip(pos);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (_world == null)
            return;

        Point pos = e.GetPosition(this);

        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
        {
            var doubleClickRoom = HitTestRoom(pos);
            if (doubleClickRoom != null)
            {
                RoomDoubleClicked?.Invoke(doubleClickRoom);
                e.Handled = true;
                return;
            }

            // Doble click en una zona libre del mapa: crear una nueva sala.
            var portHit = HitTestPort(pos);
            var exitHit = HitTestExit(pos);

            bool hasPort = portHit.HasValue;
            bool hasExit = exitHit.HasValue;

            if (!hasPort && !hasExit)
            {
                Point logicalPos = ScreenToLogical(pos);
                EmptyMapDoubleClicked?.Invoke(logicalPos);
                e.Handled = true;
                return;
            }
        }

        if (e.ChangedButton == MouseButton.Middle)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            // Finalizar conexión si estaba activa
            if (_connectionStart != null)
            {
                Room? targetRoom = HitTestRoom(pos);

                if (targetRoom != null && targetRoom != _connectionStart)
                {
                    if (!string.IsNullOrEmpty(_pendingPortDirection))
                    {
                        // Conexión iniciada desde un puerto: usar dirección fija del puerto.
                        CreateExitFromPendingPort(targetRoom);
                    }
                    else
                    {
                        // Conexión iniciada desde una sala (Alt+click): preguntar dirección.
                        var owner = Window.GetWindow(this);
                        var dlg = new DirectionPickerWindow
                        {
                            Owner = owner
                        };

                        bool? result = dlg.ShowDialog();
                        if (result == true && !string.IsNullOrWhiteSpace(dlg.SelectedDirection))
                        {
                            string dir = dlg.SelectedDirection!;

                            bool exists = _connectionStart.Exits.Any(ex =>
                                string.Equals(ex.Direction, dir, StringComparison.OrdinalIgnoreCase));

                            if (!exists)
                            {
                                _connectionStart.Exits.Add(new Exit
                                {
                                    Direction = dir,
                                    TargetRoomId = targetRoom.Id
                                });
                                MapEdited?.Invoke();
                            }
                        }

                        _connectionStart = null;
                        InvalidateVisual();
                    }
                }
                else
                {
                    // Si no hay sala destino, simplemente cancelamos la conexión pendiente.
                    _connectionStart = null;
                    _pendingPortDirection = null;
                    InvalidateVisual();
                }

                ReleaseMouseCapture();
                return;
            }

            // Fin de arrastre de salas
            if (_isDraggingRooms)
            {
                _isDraggingRooms = false;

                // Salas que han sido arrastradas en este gesto.
                var movedRoomIds = new HashSet<string>(_dragStartLogicalPositions.Keys);

                if (movedRoomIds.Count > 0)
                {
                    ResolveCollisionsAfterDrag(movedRoomIds);
                    MapEdited?.Invoke();
                }

                _dragStartLogicalPositions.Clear();
                ReleaseMouseCapture();

                // Si el ratón apenas se ha movido, consideramos que es un click.
                if ((_mouseDownScreen - pos).Length < 4.0 && _mouseDownRoom != null)
                {
                    // Seleccionar sala clicada y disparar evento
                    _selectedRoomIds.Clear();
                    _selectedRoomIds.Add(_mouseDownRoom.Id);
                    InvalidateVisual();

                    RoomClicked?.Invoke(_mouseDownRoom);
                }

                return;
            }

            if (_isDragSelecting)
            {
                _isDragSelecting = false;
                ReleaseMouseCapture();

                Rect rect = new(_selectionStartScreen, _selectionEndScreen);
                if (rect.Width < 0)
                {
                    rect = new Rect(
                        rect.X + rect.Width,
                        rect.Y,
                        -rect.Width,
                        rect.Height);
                }
                if (rect.Height < 0)
                {
                    rect = new Rect(
                        rect.X,
                        rect.Y + rect.Height,
                        rect.Width,
                        -rect.Height);
                }

                bool shiftForSelection = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                // Calculamos qué salas quedan dentro del rectángulo de selección.
                var roomsInRect = new List<string>();
                foreach (var pair in _roomRects)
                {
                    if (rect.IntersectsWith(pair.Value))
                    {
                        roomsInRect.Add(pair.Key);
                    }
                }

                // Si no se mantiene Shift, reemplazamos la selección de salas.
                if (!shiftForSelection)
                {
                    _selectedRoomIds.Clear();
                }

                // En cualquier caso, al usar el cuadro de selección, las salidas se vuelven a calcular.
                _selectedExits.Clear();

                foreach (var id in roomsInRect)
                {
                    _selectedRoomIds.Add(id);
                }

                foreach (var pair in _exitHitRects)
                {
                    if (rect.IntersectsWith(pair.Value))
                    {
                        _selectedExits.Add(pair.Key);
                    }
                }

                InvalidateVisual();
                return;
            }
        }
        UpdateIconTooltip(pos);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HideIconTooltip();
    }



    protected override void OnKeyDown(KeyEventArgs e)
{
    base.OnKeyDown(e);

    if (_world == null)
        return;

    // Ctrl + A -> seleccionar todas las salas y todas las salidas del mapa.
    if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
    {
        _selectedRoomIds.Clear();
        _selectedExits.Clear();

        foreach (var room in _world.Rooms)
        {
            if (room == null)
                continue;

            _selectedRoomIds.Add(room.Id);

            if (room.Exits != null)
            {
                for (int i = 0; i < room.Exits.Count; i++)
                {
                    _selectedExits.Add((room.Id, i));
                }
            }
        }

        InvalidateVisual();
        e.Handled = true;
        return;
    }

    // Supr -> eliminar las salidas seleccionadas (líneas) del mapa.
    if (e.Key == Key.Delete)
    {
        if (_selectedExits.Count > 0)
        {
            // Agrupar por sala para eliminar sin desordenar índices.
            var groups = _selectedExits
                .GroupBy(se => se.roomId)
                .ToList();

            foreach (var group in groups)
            {
                var room = _world.Rooms.FirstOrDefault(r => string.Equals(r.Id, group.Key, StringComparison.OrdinalIgnoreCase));
                if (room?.Exits == null)
                    continue;

                // Eliminar empezando por los índices más altos.
                var indices = group
                    .Select(se => se.exitIndex)
                    .Distinct()
                    .OrderByDescending(i => i)
                    .ToList();

                foreach (var index in indices)
                {
                    if (index >= 0 && index < room.Exits.Count)
                    {
                        room.Exits.RemoveAt(index);
                    }
                }
            }

            _selectedExits.Clear();
            MapEdited?.Invoke();
            InvalidateVisual();
            e.Handled = true;
        }

        return;
    }
}


protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_world == null)
            return;

        Point mouseScreen = e.GetPosition(this);
        Point beforeZoomLogical = ScreenToLogical(mouseScreen);

        double factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        _zoom *= factor;
        _zoom = Math.Clamp(_zoom, 0.2, 4.0);

        Point afterZoomLogical = ScreenToLogical(mouseScreen);

        // Ajustamos el offset para que el punto bajo el ratón se mantenga
        _offset = new Point(
            _offset.X + (beforeZoomLogical.X - afterZoomLogical.X),
            _offset.Y + (beforeZoomLogical.Y - afterZoomLogical.Y));

        // Ajustamos la vista para que el contenido del mapa permanezca dentro del panel central
        ClampViewToRooms();

        InvalidateVisual();
    }



    private void HideIconTooltip()
    {
        if (_iconToolTip != null)
        {
            _iconToolTip.IsOpen = false;
        }
        _iconTooltipRoomId = null;
    }


    private void ShowStartRoomTooltip(string roomId, Point screenPoint)
    {
        if (_world == null)
        {
            HideIconTooltip();
            return;
        }

        if (_iconToolTip == null)
        {
            _iconToolTip = new ToolTip();
        }

        _iconTooltipRoomId = roomId;
        _iconTooltipIsObjectIcon = false;

        _iconToolTip.Content = "Sala inicial del jugador";
        _iconToolTip.PlacementTarget = this;
        _iconToolTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
        _iconToolTip.IsOpen = true;
    }

    private void ShowIconTooltip(string roomId, bool isObjectIcon, Point screenPoint)
    {
        if (_world == null)
        {
            HideIconTooltip();
            return;
        }

        if (_iconToolTip != null && _iconToolTip.IsOpen &&
            _iconTooltipRoomId == roomId &&
            _iconTooltipIsObjectIcon == isObjectIcon)
        {
            return;
        }

        Room? room = _world.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room == null)
        {
            HideIconTooltip();
            return;
        }

        IEnumerable<string> names;
        if (isObjectIcon)
        {
            var objectIds = room.ObjectIds ?? new List<string>();

            names = _world.Objects
                .Where(o =>
                    (objectIds.Contains(o.Id)) ||
                    (!string.IsNullOrWhiteSpace(o.RoomId) &&
                     string.Equals(o.RoomId, room.Id, StringComparison.OrdinalIgnoreCase)))
                .Select(o => o.Name);
        }
        else
        {
            var npcIds = room.NpcIds ?? new List<string>();

            names = _world.Npcs
                .Where(n =>
                    (npcIds.Contains(n.Id)) ||
                    (!string.IsNullOrWhiteSpace(n.RoomId) &&
                     string.Equals(n.RoomId, room.Id, StringComparison.OrdinalIgnoreCase)))
                .Select(n => n.Name);
        }

        var list = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        if (list.Count == 0)
        {
            HideIconTooltip();
            return;
        }

        _iconTooltipRoomId = roomId;
        _iconTooltipIsObjectIcon = isObjectIcon;

        string title = isObjectIcon ? "Objetos en la sala:" : "NPCs en la sala:";
        string text = title + "\n - " + string.Join("\n - ", list);

        if (_iconToolTip == null)
        {
            _iconToolTip = new ToolTip();
        }

        _iconToolTip.Content = text;
        _iconToolTip.PlacementTarget = this;
        _iconToolTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
        _iconToolTip.IsOpen = true;
    }

    private void UpdateIconTooltip(Point screenPoint)
    {
        if (_world == null)
        {
            HideIconTooltip();
            return;
        }

        foreach (var kvp in _roomObjectIconRects)
        {
            if (kvp.Value.Contains(screenPoint))
            {
                ShowIconTooltip(kvp.Key, true, screenPoint);
                return;
            }
        }

        foreach (var kvp in _roomNpcIconRects)
        {
            if (kvp.Value.Contains(screenPoint))
            {
                ShowIconTooltip(kvp.Key, false, screenPoint);
                return;
            }
        }

        foreach (var kvp in _roomStartIconRects)
        {
            if (kvp.Value.Contains(screenPoint))
            {
                ShowStartRoomTooltip(kvp.Key, screenPoint);
                return;
            }
        }

        HideIconTooltip();
    }

    private (Room room, string direction)? HitTestPort(Point screenPoint)
    {
        foreach (var kvp in _portRects)
        {
            Rect rect = kvp.Value;
            if (rect.Contains(screenPoint))
            {
                string roomId = kvp.Key.roomId;
                string direction = kvp.Key.direction;

                if (_world != null)
                {
                    Room room = _world.Rooms.FirstOrDefault(r => r.Id == roomId);
                    if (room != null)
                        return (room, direction);
                }
            }
        }

        return null;
    }

    private (Room room, int exitIndex)? HitTestExit(Point screenPoint)
    {
        if (_world == null)
            return null;

        // Recorremos en orden inverso para que "gane" la última salida dibujada.
        foreach (var kvp in _exitHitRects.Reverse())
        {
            Rect rect = kvp.Value;
            if (rect.Contains(screenPoint))
            {
                var key = kvp.Key;
                string roomId = key.roomId;
                int exitIndex = key.exitIndex;

                Room? room = _world.Rooms.FirstOrDefault(r => r.Id == roomId);
                if (room != null && room.Exits != null &&
                    exitIndex >= 0 && exitIndex < room.Exits.Count)
                {
                    return (room, exitIndex);
                }
            }
        }

        return null;
    }

    private void CreateExitFromPendingPort(Room targetRoom)
    {
        if (_connectionStart == null || string.IsNullOrEmpty(_pendingPortDirection))
            return;

        string newDirection = _pendingPortDirection;
        string normNew = NormalizeDirectionLabel(newDirection);

        bool alreadyExists = _connectionStart.Exits.Any(ex =>
            string.Equals(NormalizeDirectionLabel(ex.Direction), normNew, StringComparison.OrdinalIgnoreCase));

        if (!alreadyExists)
        {
            _connectionStart.Exits.Add(new Exit
            {
                Direction = newDirection,
                TargetRoomId = targetRoom.Id
            });
            MapEdited?.Invoke();
        }
        else
        {
            Window owner = Window.GetWindow(this);
            new AlertWindow(
                string.Format("La sala '{0}' ya tiene una salida en dirección '{1}'.",
                    _connectionStart.Name,
                    NormalizeDirectionLabel(newDirection)),
                "Xilo Adventures")
            {
                Owner = owner
            }.ShowDialog();
        }

        _connectionStart = null;
        _pendingPortDirection = null;
        InvalidateVisual();
    }

    
    private static string GetSingleDirectionLabel(string? rawDirection)
    {
        if (string.IsNullOrWhiteSpace(rawDirection))
            return string.Empty;

        // Separadores habituales para múltiples direcciones almacenadas en el mismo texto.
        var parts = rawDirection.Split(
            new[] { '/', '-', ',', '|', ';' },
            StringSplitOptions.RemoveEmptyEntries);

        string firstPart = parts.Length > 0 ? parts[0].Trim() : rawDirection.Trim();

        // Si aún hay varias palabras (ej. "norte sur"), nos quedamos con la primera.
        var words = firstPart.Split(
            new[] { ' ', '\t' },
            StringSplitOptions.RemoveEmptyEntries);

        return words.Length > 0 ? words[0] : firstPart;
    }

    private static string NormalizeDirectionLabel(string direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
            return string.Empty;

        string key = direction.Trim().ToLowerInvariant();

        if (key == "n" || key == "norte") return "norte";
        if (key == "s" || key == "sur") return "sur";
        if (key == "e" || key == "este") return "este";
        if (key == "o" || key == "oeste") return "oeste";
        if (key == "ne" || key == "noreste") return "noreste";
        if (key == "no" || key == "noroeste") return "noroeste";
        if (key == "se" || key == "sureste") return "sureste";
        if (key == "so" || key == "suroeste") return "suroeste";
        if (key == "arriba" || key == "subir") return "arriba";
        if (key == "abajo" || key == "bajar") return "abajo";

        return direction;
    }



    private static string GetOppositeDirection(string normalizedDirection)
    {
        switch (normalizedDirection)
        {
            case "norte": return "sur";
            case "sur": return "norte";
            case "este": return "oeste";
            case "oeste": return "este";
            case "noreste": return "suroeste";
            case "noroeste": return "sureste";
            case "sureste": return "noroeste";
            case "suroeste": return "noreste";
            case "arriba": return "abajo";
            case "abajo": return "arriba";
            default: return normalizedDirection;
        }
    }

    private static Point GetPortPointForDirection(Rect rect, string normalizedDirection)
    {
        double halfWidth = rect.Width / 2.0;
        double halfHeight = rect.Height / 2.0;

        Point centerScreen = new Point(rect.X + halfWidth, rect.Y + halfHeight);

        Point topCenter = new Point(centerScreen.X, rect.Y);
        Point bottomCenter = new Point(centerScreen.X, rect.Y + rect.Height);
        Point leftCenter = new Point(rect.X, centerScreen.Y);
        Point rightCenter = new Point(rect.X + rect.Width, centerScreen.Y);

        Point topLeft = new Point(rect.X, rect.Y);
        Point topRight = new Point(rect.X + rect.Width, rect.Y);
        Point bottomLeft = new Point(rect.X, rect.Y + rect.Height);
        Point bottomRight = new Point(rect.X + rect.Width, rect.Y + rect.Height);

        // Puntos "arriba" y "abajo" como en DrawRooms
        Point upPoint = new Point((centerScreen.X + topRight.X) / 2.0, rect.Y);
        Point downPoint = new Point((centerScreen.X + bottomRight.X) / 2.0, rect.Y + rect.Height);

        switch (normalizedDirection)
        {
            case "norte": return topCenter;
            case "sur": return bottomCenter;
            case "este": return rightCenter;
            case "oeste": return leftCenter;
            case "noreste": return topRight;
            case "noroeste": return topLeft;
            case "sureste": return bottomRight;
            case "suroeste": return bottomLeft;
            case "arriba": return upPoint;
            case "abajo": return downPoint;
            default: return centerScreen;
        }
    }
    private static double DistancePointToSegment(Point p, Point a, Point b)
    {
        double vx = b.X - a.X;
        double vy = b.Y - a.Y;
        double wx = p.X - a.X;
        double wy = p.Y - a.Y;

        double c1 = vx * wx + vy * wy;
        if (c1 <= 0)
            return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

        double c2 = vx * vx + vy * vy;
        if (c2 <= c1)
            return Math.Sqrt((p.X - b.X) * (p.X - b.X) + (p.Y - b.Y) * (p.Y - b.Y));

        double t = c1 / c2;
        Point proj = new Point(a.X + t * vx, a.Y + t * vy);

        return Math.Sqrt((p.X - proj.X) * (p.X - proj.X) + (p.Y - proj.Y) * (p.Y - proj.Y));
    }



    private Point ClampRoomCenterToMap(Point logicalCenter)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
            return logicalCenter;

        double halfW = RoomBoxWidth / 2.0;
        double halfH = RoomBoxHeight / 2.0;

        Point topLeftLogical = new(
            logicalCenter.X - halfW,
            logicalCenter.Y - halfH);
        Point bottomRightLogical = new(
            logicalCenter.X + halfW,
            logicalCenter.Y + halfH);

        Point topLeftScreen = LogicalToScreen(topLeftLogical);
        Point bottomRightScreen = LogicalToScreen(bottomRightLogical);

        double dxScreen = 0;
        double dyScreen = 0;

        if (topLeftScreen.X < 0)
            dxScreen = -topLeftScreen.X;
        else if (bottomRightScreen.X > width)
            dxScreen = width - bottomRightScreen.X;

        if (topLeftScreen.Y < 0)
            dyScreen = -topLeftScreen.Y;
        else if (bottomRightScreen.Y > height)
            dyScreen = height - bottomRightScreen.Y;

        if (Math.Abs(dxScreen) < double.Epsilon && Math.Abs(dyScreen) < double.Epsilon)
            return logicalCenter;

        return new Point(
            logicalCenter.X + dxScreen / _zoom,
            logicalCenter.Y + dyScreen / _zoom);
    }

    private static Rect GetRoomRectFromCenter(Point center, double margin)
    {
        double halfW = (RoomBoxWidth + margin) / 2.0;
        double halfH = (RoomBoxHeight + margin) / 2.0;

        return new Rect(
            center.X - halfW,
            center.Y - halfH,
            halfW * 2.0,
            halfH * 2.0);
    }

    private bool HasCollisionWithOtherRooms(string roomId, Point candidateCenter, double margin, ISet<string>? ignoreRooms = null)
    {
        var candidateRect = GetRoomRectFromCenter(candidateCenter, margin);

        foreach (var kv in _roomPositions)
        {
            string otherId = kv.Key;
            if (otherId == roomId)
                continue;
            if (ignoreRooms != null && ignoreRooms.Contains(otherId))
                continue;

            var otherRect = GetRoomRectFromCenter(kv.Value, margin);
            if (candidateRect.IntersectsWith(otherRect))
                return true;
        }

        return false;
    }

    private Room? HitTestRoom(Point screenPoint)
    {
        // Cuando hay salas superpuestas, queremos que gane la que está más arriba (pintada la última),
        // por eso recorremos los rectángulos en orden inverso.
        foreach (var pair in _roomRects.Reverse())
        {
            if (pair.Value.Contains(screenPoint))
            {
                if (_world == null)
                    return null;

                return _world.Rooms.FirstOrDefault(r => r.Id == pair.Key);
            }
        }

        return null;
    }

    /// <summary>
    /// Ajusta las salas arrastradas para que no queden solapadas al soltar el ratón,
    /// respetando el margen configurado.
    /// </summary>
    private void ResolveCollisionsAfterDrag(ISet<string> movingIds)
    {
        if (movingIds == null || movingIds.Count == 0)
            return;

        foreach (var id in movingIds)
        {
            if (!_roomPositions.TryGetValue(id, out var desired))
                continue;

            var adjusted = FindNearestNonCollidingPosition(id, desired, RoomSpacingMargin);
            _roomPositions[id] = adjusted;
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Busca la posición libre más cercana a la posición deseada para una sala,
    /// manteniéndola dentro del mapa y respetando el margen con el resto de salas.
    /// </summary>
    private Point FindNearestNonCollidingPosition(string roomId, Point desiredCenter, double margin)
    {
        // Aseguramos que el punto deseado está dentro del mapa.
        Point candidate = ClampRoomCenterToMap(desiredCenter);

        if (!HasCollisionWithOtherRooms(roomId, candidate, margin, null))
            return candidate;

        double maxRadius = Math.Max(ActualWidth, ActualHeight);
        if (double.IsNaN(maxRadius) || maxRadius <= 0)
            maxRadius = 2000;

        const double stepRadius = 10.0;
        const double angleStep = Math.PI / 12.0;

        Point best = candidate;
        double bestDistance = double.MaxValue;
        bool found = false;

        for (double radius = stepRadius; radius <= maxRadius; radius += stepRadius)
        {
            for (double angle = 0.0; angle < Math.PI * 2.0; angle += angleStep)
            {
                Point trial = new(
                    desiredCenter.X + radius * Math.Cos(angle),
                    desiredCenter.Y + radius * Math.Sin(angle));

                trial = ClampRoomCenterToMap(trial);

                if (HasCollisionWithOtherRooms(roomId, trial, margin, null))
                    continue;

                double dx = trial.X - desiredCenter.X;
                double dy = trial.Y - desiredCenter.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = trial;
                    found = true;
                }
            }

            if (found)
                break;
        }

        return found ? best : candidate;
    }

    private Point LogicalToScreen(Point logical)
    {
        // Primero aplicamos offset lógico y después zoom
        return new Point(
            (logical.X + _offset.X) * _zoom,
            (logical.Y + _offset.Y) * _zoom);
    }

    private Point ScreenToLogical(Point screen)
    {
        return new Point(
            screen.X / _zoom - _offset.X,
            screen.Y / _zoom - _offset.Y);
    }


    private void ClampViewToRooms()
    {
        if (_world == null || _world.Rooms == null || _world.Rooms.Count == 0)
            return;

        if (_roomPositions.Count == 0)
            return;

        double viewWidth = ActualWidth;
        double viewHeight = ActualHeight;

        if (viewWidth <= 0 || viewHeight <= 0)
            return;

        bool first = true;
        double minX = 0;
        double minY = 0;
        double maxX = 0;
        double maxY = 0;

        foreach (var kvp in _roomPositions)
        {
            Point p = kvp.Value;

            double left = p.X - RoomBoxWidth / 2.0;
            double right = p.X + RoomBoxWidth / 2.0;
            double top = p.Y - RoomBoxHeight / 2.0;
            double bottom = p.Y + RoomBoxHeight / 2.0;

            if (first)
            {
                minX = left;
                maxX = right;
                minY = top;
                maxY = bottom;
                first = false;
            }
            else
            {
                if (left < minX) minX = left;
                if (right > maxX) maxX = right;
                if (top < minY) minY = top;
                if (bottom > maxY) maxY = bottom;
            }
        }

        if (first)
            return;

        Point topLeftScreen = LogicalToScreen(new Point(minX, minY));
        Point bottomRightScreen = LogicalToScreen(new Point(maxX, maxY));

        double contentLeft = topLeftScreen.X;
        double contentTop = topLeftScreen.Y;
        double contentRight = bottomRightScreen.X;
        double contentBottom = bottomRightScreen.Y;

        double contentWidth = contentRight - contentLeft;
        double contentHeight = contentBottom - contentTop;

        double deltaX = 0;
        double deltaY = 0;

        // Ajuste horizontal
        if (contentWidth <= viewWidth)
        {
            double desiredLeft = (viewWidth - contentWidth) / 2.0;
            deltaX = desiredLeft - contentLeft;
        }
        else
        {
            if (contentLeft > 0)
            {
                deltaX = -contentLeft;
            }
            else if (contentRight < viewWidth)
            {
                deltaX = viewWidth - contentRight;
            }
        }

        // Ajuste vertical
        if (contentHeight <= viewHeight)
        {
            double desiredTop = (viewHeight - contentHeight) / 2.0;
            deltaY = desiredTop - contentTop;
        }
        else
        {
            if (contentTop > 0)
            {
                deltaY = -contentTop;
            }
            else if (contentBottom < viewHeight)
            {
                deltaY = viewHeight - contentBottom;
            }
        }

        if (Math.Abs(deltaX) < 0.1 && Math.Abs(deltaY) < 0.1)
            return;

        double offsetDeltaX = deltaX / _zoom;
        double offsetDeltaY = deltaY / _zoom;

        _offset = new Point(_offset.X + offsetDeltaX, _offset.Y + offsetDeltaY);
    }

    private static Point Midpoint(Point a, Point b)
    {
        return new Point(
            (a.X + b.X) / 2.0,
            (a.Y + b.Y) / 2.0);
    }

}
