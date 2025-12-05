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
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (_world == null)
            return;

        EnsureLayout();
        UpdateRoomRects();

        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        dc.DrawRectangle(
            new SolidColorBrush(Color.FromRgb(20, 20, 20)),
            null,
            new Rect(0, 0, width, height));

        DrawConnections(dc);
        DrawRooms(dc);
        DrawSelectionRectangle(dc);
        DrawPendingConnection(dc);
    }

    private void EnsureLayout()
    {
        if (_world == null)
            return;

        // Distribución sencilla en rejilla para salas que no tengan posición
        const double padding = 40;
        int index = 0;

        int roomsPerRow;
        if (double.IsNaN(ActualWidth) || ActualWidth <= 0)
        {
            roomsPerRow = 6;
        }
        else
        {
            roomsPerRow = Math.Max(1, (int)((ActualWidth - padding) / (RoomBoxWidth + padding)));
        }

        foreach (var room in _world.Rooms)
        {
            if (!_roomPositions.ContainsKey(room.Id))
            {
                int row = index / roomsPerRow;
                int col = index % roomsPerRow;

                double x = padding + RoomBoxWidth / 2 + col * (RoomBoxWidth + padding);
                double y = padding + RoomBoxHeight / 2 + row * (RoomBoxHeight + padding);

                _roomPositions[room.Id] = new Point(x, y);
            }

            index++;
        }
    }

    private void UpdateRoomRects()
    {
        _roomRects.Clear();
        _portRects.Clear();
        _roomObjectIconRects.Clear();
        _roomNpcIconRects.Clear();
        _roomStartIconRects.Clear();
        _doorIconRects.Clear();
        _keyIconRects.Clear();
        _keyIconKeyDefs.Clear();

        if (_world == null)
            return;

        foreach (var room in _world.Rooms)
        {
            if (!_roomPositions.TryGetValue(room.Id, out var logicalCenter))
                continue;

            Point topLeftLogical = new(
                logicalCenter.X - RoomBoxWidth / 2.0,
                logicalCenter.Y - RoomBoxHeight / 2.0);
            Point bottomRightLogical = new(
                logicalCenter.X + RoomBoxWidth / 2.0,
                logicalCenter.Y + RoomBoxHeight / 2.0);

            Point topLeftScreen = LogicalToScreen(topLeftLogical);
            Point bottomRightScreen = LogicalToScreen(bottomRightLogical);

            Rect rect = new Rect(topLeftScreen, bottomRightScreen);
            _roomRects[room.Id] = rect;
        }
    }

    private void DrawRooms(DrawingContext dc)
    {
        if (_world == null)
            return;

        Typeface typeface = new("Segoe UI");

        foreach (var room in _world.Rooms)
        {
            if (!_roomPositions.TryGetValue(room.Id, out _))
                continue;

            if (!_roomRects.TryGetValue(room.Id, out var rect))
                continue;

            bool isSelected = _selectedRoomIds.Contains(room.Id);
            bool isLit = room.IsIlluminated;

            // Colores:
            //  - Salas seleccionadas: verde oscuro.
            //  - Salas iluminadas (IsLit = true) no seleccionadas: azul (como el antiguo color de selección).
            //  - Salas no iluminadas y no seleccionadas: gris oscuro original.
            SolidColorBrush selectedBrush = new(Color.FromRgb(40, 100, 40));
            SolidColorBrush litBrush = new(Color.FromRgb(70, 110, 170));
            SolidColorBrush normalBrush = new(Color.FromRgb(45, 45, 45));

            Brush fill = isSelected
                ? selectedBrush
                : (isLit ? litBrush : normalBrush);

            double borderThickness = room.IsInterior ? 2.0 : 1.0;

            Pen borderPen = isSelected
                ? new Pen(new SolidColorBrush(Color.FromRgb(200, 220, 255)), borderThickness)
                : new Pen(new SolidColorBrush(Color.FromRgb(120, 120, 120)), borderThickness);

            dc.DrawRoundedRectangle(fill, borderPen, rect, 6, 6);

            string text = string.IsNullOrWhiteSpace(room.Name)
                ? room.Id
                : room.Name;

            var formatted = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                12,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Si el nombre no cabe en una sola línea, permitimos que se divida en varias líneas
            // ajustándolo al ancho de la caja de la sala.
            const double textPadding = 10.0;
            formatted.MaxTextWidth = rect.Width - textPadding * 2.0;

            Point textPos = new(
                rect.X + (rect.Width - formatted.Width) / 2.0,
                rect.Y + (rect.Height - formatted.Height) / 2.0);

            // Dibujamos iconos de objetos y NPCs si la sala contiene alguno.
            bool hasObjects = room.ObjectIds != null && room.ObjectIds.Count > 0;

            // Además, comprobamos si hay objetos cuyo RoomId apunta a esta sala,
            // por si el JSON sólo ha rellenado RoomId y no la lista Room.ObjectIds.
            if (!hasObjects && _world?.Objects != null)
            {
                hasObjects = _world.Objects.Any(o =>
                    !string.IsNullOrWhiteSpace(o.RoomId) &&
                    string.Equals(o.RoomId, room.Id, StringComparison.OrdinalIgnoreCase));
            }

            bool hasNpcs = room.NpcIds != null && room.NpcIds.Count > 0;
            bool isStartRoom = _world?.Game != null &&
                               string.Equals(_world.Game.StartRoomId, room.Id, StringComparison.OrdinalIgnoreCase);

            const double iconSize = 14.0;
            const double iconMargin = 4.0;

            if (hasObjects)
            {
                Rect objRect = new Rect(
                    rect.X + iconMargin,
                    rect.Y + iconMargin,
                    iconSize,
                    iconSize);

                _roomObjectIconRects[room.Id] = objRect;

                SolidColorBrush objBg = new SolidColorBrush(Color.FromRgb(90, 140, 90));
                Pen objPen = new Pen(new SolidColorBrush(Color.FromRgb(230, 230, 230)), 0.8);
                dc.DrawRoundedRectangle(objBg, objPen, objRect, 3, 3);

                var objText = new FormattedText(
                    "O",
                    System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    10,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                Point objTextPos = new(
                    objRect.X + (objRect.Width - objText.Width) / 2.0,
                    objRect.Y + (objRect.Height - objText.Height) / 2.0);
                dc.DrawText(objText, objTextPos);
            }

            if (hasNpcs)
            {
                Rect npcRect = new Rect(
                    rect.X + rect.Width - iconMargin - iconSize,
                    rect.Y + iconMargin,
                    iconSize,
                    iconSize);

                _roomNpcIconRects[room.Id] = npcRect;

                SolidColorBrush npcBg = new SolidColorBrush(Color.FromRgb(140, 90, 90));
                Pen npcPen = new Pen(new SolidColorBrush(Color.FromRgb(230, 230, 230)), 0.8);
                dc.DrawRoundedRectangle(npcBg, npcPen, npcRect, 3, 3);

                var npcText = new FormattedText(
                    "N",
                    System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    10,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                Point npcTextPos = new(
                    npcRect.X + (npcRect.Width - npcText.Width) / 2.0,
                    npcRect.Y + (npcRect.Height - npcText.Height) / 2.0);
                dc.DrawText(npcText, npcTextPos);
            }

            if (isStartRoom)
            {
                Rect startRect = new Rect(
                    rect.X + iconMargin,
                    rect.Bottom - iconMargin - iconSize,
                    iconSize,
                    iconSize);

                _roomStartIconRects[room.Id] = startRect;

                SolidColorBrush startBg = new SolidColorBrush(Color.FromRgb(90, 90, 140));
                Pen startPen = new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 255)), 1.0);
                dc.DrawRoundedRectangle(startBg, startPen, startRect, 3, 3);

                var startText = new FormattedText(
                    "S",
                    System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    10,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                Point startTextPos = new(
                    startRect.X + (startRect.Width - startText.Width) / 2.0,
                    startRect.Y + (startRect.Height - startText.Height) / 2.0);
                dc.DrawText(startText, startTextPos);
            }


            // Dibujamos los puntos de salida (puertos) alrededor de la sala.
            const double portSize = 12.0;
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

            // Puntos "arriba" y "abajo" algo separados de los centros para no
            // solaparse tanto con el texto, pero situados sobre el borde superior/inferior.
            Point upPoint = new Point((centerScreen.X + topRight.X) / 2.0, rect.Y);
            Point downPoint = new Point((centerScreen.X + bottomRight.X) / 2.0, rect.Y + rect.Height);

            var ports = new (string direction, Point point)[]
            {
                ("norte", topCenter),
                ("sur", bottomCenter),
                ("oeste", leftCenter),
                ("este", rightCenter),
                ("noroeste", topLeft),
                ("noreste", topRight),
                ("suroeste", bottomLeft),
                ("sureste", bottomRight),
                ("arriba", upPoint),
                ("abajo", downPoint)
            };

            foreach (var port in ports)
            {
                Rect portRect = new Rect(
                    port.point.X - portSize / 2.0,
                    port.point.Y - portSize / 2.0,
                    portSize,
                    portSize);

                _portRects[(room.Id, port.direction)] = portRect;

                SolidColorBrush portFill = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                Pen portPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 160, 160)), 1);

                dc.DrawRectangle(portFill, portPen, portRect);
            }

            dc.DrawText(formatted, textPos);
        }
    }

    

private void DrawConnections(DrawingContext dc)
{
    if (_world == null)
        return;

    // Los rectángulos de hit test de salidas se recalculan en cada render
    _exitHitRects.Clear();

    // Para evitar solapamiento de textos cuando hay conexiones bidireccionales
    // entre las mismas dos salas, sólo dibujamos una etiqueta por par de salas.
    var labeledConnections = new HashSet<(string a, string b)>();

    // Mapa rápido de puertas por Id para consultar su estado (abierta/cerrada).
    Dictionary<string, Door>? doorsById = null;
    if (_world.Doors != null && _world.Doors.Count > 0)
    {
        doorsById = _world.Doors
            .Where(d => !string.IsNullOrWhiteSpace(d.Id))
            .ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);
    }

    // Conjunto de LockIds que tienen al menos una llave asociada.
    HashSet<string> locksWithKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var firstKeyByLockId = new Dictionary<string, KeyDefinition>(StringComparer.OrdinalIgnoreCase);
    if (_world.Keys != null)
    {
        foreach (var key in _world.Keys)
        {
            if (key.LockIds == null)
                continue;

            foreach (var lockId in key.LockIds)
            {
                if (!string.IsNullOrWhiteSpace(lockId))
                {
                    locksWithKeys.Add(lockId);
                    if (!firstKeyByLockId.ContainsKey(lockId))
                        firstKeyByLockId[lockId] = key;
                }
            }
        }
    }

    Pen normalPen = new(new SolidColorBrush(Color.FromRgb(160, 160, 160)), 1.2);
    Pen selectedPen = new(new SolidColorBrush(Color.FromRgb(255, 220, 80)), 2.0);

    Typeface typeface = new("Segoe UI");
    double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

    // Si hay exactamente una sala seleccionada, los textos de las salidas
    // sólo se mostrarán para las conexiones que involucren a esa sala.
    string? singleSelectedRoomId = null;
    bool hasSingleSelectedRoom = _selectedRoomIds.Count == 1;
    if (hasSingleSelectedRoom)
    {
        singleSelectedRoomId = _selectedRoomIds.First();
    }

    foreach (var room in _world.Rooms)
    {
        if (room.Exits == null)
            continue;

        if (!_roomRects.TryGetValue(room.Id, out var fromRect))
            continue;

        for (int i = 0; i < room.Exits.Count; i++)
        {
            var exit = room.Exits[i];
            if (exit == null || string.IsNullOrEmpty(exit.TargetRoomId))
                continue;

            var target = _world.Rooms.FirstOrDefault(r => r.Id == exit.TargetRoomId);
            if (target == null)
                continue;

            if (!_roomRects.TryGetValue(target.Id, out var toRect))
                continue;

            string rawDirection = exit.Direction ?? string.Empty;
            string normDir = NormalizeDirectionLabel(rawDirection);

            Point fromPoint = GetPortPointForDirection(fromRect, normDir);
            string oppositeDir = GetOppositeDirection(normDir);
            Point toPoint = GetPortPointForDirection(toRect, oppositeDir);

            var exitKey = (room.Id, i);
            bool isSelected = _selectedExits.Contains(exitKey);
            Pen linePen = isSelected ? selectedPen : normalPen;

            dc.DrawLine(linePen, fromPoint, toPoint);

            // Rectángulo de hit test que cubre toda la línea de la salida,
            // con un pequeño margen para facilitar el click.
            Point mid = Midpoint(fromPoint, toPoint);

            // Rectángulo de hit-test estrecho alrededor de la línea de la salida,
            // para evitar que se seleccione haciendo click demasiado lejos.
            Rect baseRect = new Rect(fromPoint, toPoint);
            double dx = Math.Abs(fromPoint.X - toPoint.X);
            double dy = Math.Abs(fromPoint.Y - toPoint.Y);
            const double halfThickness = 4.0;

            Rect lineHitRect;
            if (dx >= dy)
            {
                // Conexión principalmente horizontal: corredor fino en vertical.
                double centerY = (fromPoint.Y + toPoint.Y) / 2.0;
                lineHitRect = new Rect(
                    baseRect.X,
                    centerY - halfThickness,
                    baseRect.Width,
                    halfThickness * 2.0);
            }
            else
            {
                // Conexión principalmente vertical: corredor fino en horizontal.
                double centerX = (fromPoint.X + toPoint.X) / 2.0;
                lineHitRect = new Rect(
                    centerX - halfThickness,
                    baseRect.Y,
                    halfThickness * 2.0,
                    baseRect.Height);
            }

            // Inicialmente, el hit test de la salida es el de la línea.
            Rect hitRect = lineHitRect;

            // Etiqueta con la dirección en el punto medio.
            // Si hay exactamente una sala seleccionada, el texto de la salida
            // se muestra desde el punto de vista de esa sala.
            string labelDirectionRaw = exit.Direction ?? string.Empty;

            if (hasSingleSelectedRoom && singleSelectedRoomId != null)
            {
                if (singleSelectedRoomId == room.Id)
                {
                    // Vista desde la sala origen: usamos la dirección tal cual está definida.
                    labelDirectionRaw = rawDirection;
                }
                else if (singleSelectedRoomId == target.Id)
                {
                    // Vista desde la sala destino: usamos la dirección opuesta.
                    string labelNorm = NormalizeDirectionLabel(rawDirection);
                    string oppositeForLabel = GetOppositeDirection(labelNorm);
                    labelDirectionRaw = oppositeForLabel;
                }
            }

            string label = GetSingleDirectionLabel(labelDirectionRaw);

            // Clave de conexión independiente del sentido (A-B == B-A)
            var key = string.CompareOrdinal(room.Id, target.Id) <= 0
                ? (room.Id, target.Id)
                : (target.Id, room.Id);

            bool canDrawLabel =
                !string.IsNullOrWhiteSpace(label) &&
                (!hasSingleSelectedRoom ||
                 (singleSelectedRoomId == room.Id || singleSelectedRoomId == target.Id)) &&
                !labeledConnections.Contains(key);

            if (canDrawLabel)
            {
                labeledConnections.Add(key);

                var formatted = new FormattedText(
                    label,
                    System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    11,
                    isSelected ? Brushes.LightYellow : Brushes.White,
                    dpi);

                Point textPos = new(
                    mid.X - formatted.Width / 2.0,
                    mid.Y - formatted.Height - 4);

                // Rectángulo específico para el texto de la etiqueta
                Rect textRect = new Rect(
                    textPos.X - 4,
                    textPos.Y - 2,
                    formatted.Width + 8,
                    formatted.Height + 4);

                // Combinamos la zona de la línea con la del texto.
                hitRect = Rect.Union(hitRect, textRect);

                dc.DrawText(formatted, textPos);
            }

            // Iconos de puerta y llave: siempre visibles aunque sólo se muestren
            // etiquetas de texto para algunas conexiones.
            Door? door = null;
            if (doorsById != null && !string.IsNullOrEmpty(exit.DoorId))
            {
                doorsById.TryGetValue(exit.DoorId, out door);
            }

            if (door == null && _world.Doors != null && _world.Doors.Count > 0)
            {
                door = _world.Doors.FirstOrDefault(d =>
                    !string.IsNullOrEmpty(d.RoomIdA) &&
                    !string.IsNullOrEmpty(d.RoomIdB) &&
                    ((string.Equals(d.RoomIdA, room.Id, StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(d.RoomIdB, target.Id, StringComparison.OrdinalIgnoreCase)) ||
                     (string.Equals(d.RoomIdB, room.Id, StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(d.RoomIdA, target.Id, StringComparison.OrdinalIgnoreCase))));
            }

            if (door != null)
            {
                const double doorIconWidth = 14.0;
                const double doorIconHeight = 14.0;
                const double doorIconMargin = 4.0;

                Point doorTopLeft = new(
                    mid.X - doorIconWidth / 2.0,
                    mid.Y + doorIconMargin);

                Rect doorRect = new(
                    doorTopLeft.X,
                    doorTopLeft.Y,
                    doorIconWidth,
                    doorIconHeight);

                // Guardamos el rectángulo del icono de puerta para hit-test.
                _doorIconRects[door.Id] = doorRect;

                SolidColorBrush doorFill = door.IsOpen
                    ? new SolidColorBrush(Color.FromRgb(60, 170, 60))
                    : new SolidColorBrush(Color.FromRgb(200, 60, 60));

                Pen doorPen = new Pen(new SolidColorBrush(Color.FromRgb(240, 240, 240)), 0.8);

                dc.DrawRoundedRectangle(doorFill, doorPen, doorRect, 3, 3);

                // Pomo de la puerta
                Point knobCenter = new(
                    doorRect.Right - 4,
                    doorRect.Y + doorRect.Height / 2.0);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(240, 240, 240)), null, knobCenter, 1.5, 1.5);

                // Icono de llave, si la puerta tiene cerradura y al menos una llave asociada.
                if (door.HasLock && !string.IsNullOrWhiteSpace(door.LockId) &&
                    locksWithKeys.Contains(door.LockId))
                {
                    const double keyIconSize = 12.0;
                    const double keyIconMargin = 2.0;

                    Rect keyRect = new(
                        doorRect.Right + keyIconMargin,
                        doorRect.Y + (doorRect.Height - keyIconSize) / 2.0,
                        keyIconSize,
                        keyIconSize);

                    SolidColorBrush keyFill = doorFill; // mismo color que la puerta
                    Pen keyPen = new Pen(new SolidColorBrush(Color.FromRgb(240, 240, 240)), 0.8);

                dc.DrawRoundedRectangle(keyFill, keyPen, keyRect, 3, 3);

                if (firstKeyByLockId.TryGetValue(door.LockId, out var keyDef))
                {
                    _keyIconRects[door.LockId] = keyRect;
                    _keyIconKeyDefs[door.LockId] = keyDef;
                }

                // Un pequeño "diente" de llave dentro
                    Point toothStart = new(keyRect.X + 3, keyRect.Y + keyRect.Height / 2.0);
                    Point toothEnd = new(keyRect.Right - 3, keyRect.Y + keyRect.Height / 2.0);
                    dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(240, 240, 240)), 1.2), toothStart, toothEnd);

                    // Ampliamos el hit-test para cubrir también el icono de llave.
                    hitRect = Rect.Union(hitRect, keyRect);
                }

                // Ampliamos el hit-test para cubrir también el icono de puerta.
                hitRect = Rect.Union(hitRect, doorRect);
            }

            _exitHitRects[exitKey] = hitRect;
        }
    }
}

private void DrawSelectionRectangle(DrawingContext dc)
    {
        if (!_isDragSelecting)
            return;

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

        dc.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(40, 80, 160, 240)),
            new Pen(new SolidColorBrush(Color.FromRgb(80, 160, 240)), 1.0),
            rect);
    }

    private void DrawPendingConnection(DrawingContext dc)
    {
        if (_world == null || _connectionStart == null)
            return;

        Point fromScreen;

        if (_roomRects.TryGetValue(_connectionStart.Id, out var fromRect))
        {
            if (!string.IsNullOrEmpty(_pendingPortDirection))
            {
                string normDir = NormalizeDirectionLabel(_pendingPortDirection);
                fromScreen = GetPortPointForDirection(fromRect, normDir);
            }
            else
            {
                fromScreen = new Point(
                    fromRect.X + fromRect.Width / 2.0,
                    fromRect.Y + fromRect.Height / 2.0);
            }
        }
        else if (_roomPositions.TryGetValue(_connectionStart.Id, out var fromCenterLogical))
        {
            fromScreen = LogicalToScreen(fromCenterLogical);
        }
        else
        {
            return;
        }

        Point toScreen = _connectionCurrentMouseScreen;

        Pen pen = new(new SolidColorBrush(Color.FromRgb(0, 200, 255)), 1.5)
        {
            DashStyle = DashStyles.Dash
        };

        dc.DrawLine(pen, fromScreen, toScreen);
    }


}
