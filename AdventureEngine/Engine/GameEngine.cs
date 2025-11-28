using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Engine;

public class GameEngine
{
    private readonly WorldModel _world;
    private readonly SoundManager _sound;
    private readonly GameState _state;
    private readonly DoorService _doorService;
    private DateTime _lastRealTime;

    public GameState State => _state;

    public event Action<Room>? RoomChanged;

    public GameEngine(WorldModel world, GameState state, SoundManager soundManager)
    {
        _world = world;
        _sound = soundManager;
        _state = state;
        _doorService = new DoorService(_state.Doors, _state.Keys);

        // Inicializar hora de juego al comenzar la partida si no viene informada.
        if (_state.GameTime == default)
        {
            _state.GameTime = DateTime.Now;
        }
        _lastRealTime = DateTime.Now;

        // Asegurar índices consistentes
        WorldLoader.RebuildRoomIndexes(_state);
        EnsurePlayerRoom();
        OnRoomChanged();
    }

    public Room? CurrentRoom =>
        _state.Rooms.FirstOrDefault(r => r.Id.Equals(_state.CurrentRoomId, StringComparison.OrdinalIgnoreCase));


    private void UpdateGameTimeFromReal()
    {
        var now = DateTime.Now;
        var realDelta = now - _lastRealTime;
        if (realDelta < TimeSpan.Zero)
            realDelta = TimeSpan.Zero;

        // 10 minutos reales -> 3 horas en el juego (factor 18x)
        double factor = 18.0;
        var scaledTicks = (long)(realDelta.Ticks * factor);
        if (scaledTicks != 0)
        {
            _state.GameTime = _state.GameTime.Add(TimeSpan.FromTicks(scaledTicks));
            _lastRealTime = now;
        }
    }

    public string ProcessCommand(string input)
    {
        _state.TurnCounter++;
        UpdateGameTimeFromReal();

        var parsed = Parser.Parse(input);
        if (string.IsNullOrEmpty(parsed.Verb))
            return string.Empty;

        var sb = new StringBuilder();

        switch (parsed.Verb)
        {
            case "look":
                sb.AppendLine(DescribeCurrentRoom());
                break;

            case "go":
                sb.AppendLine(HandleGo(parsed));
                break;

            case "open":
                sb.AppendLine(HandleOpen(parsed));
                break;

            case "close":
                sb.AppendLine(HandleClose(parsed));
                break;

            case "inventory":
                sb.AppendLine(DescribeInventory());
                break;

            case "take":
                sb.AppendLine(HandleTake(parsed));
                break;

            case "drop":
                sb.AppendLine(HandleDrop(parsed));
                break;

            case "talk":
            case "say":
            case "option":
                sb.AppendLine(HandleTalk(parsed));
                break;

            case "use":
                sb.AppendLine(HandleUse(parsed));
                break;

            case "give":
                sb.AppendLine(HandleGive(parsed));
                break;

            case "quests":
                sb.AppendLine(DescribeQuests());
                break;

            case "save":
                sb.AppendLine("Usa el menú Archivo -> Guardar partida... para guardar.");
                break;

            case "load":
                sb.AppendLine("Usa el menú Archivo -> Cargar partida... para cargar.");
                break;

            case "help":
                sb.AppendLine(GetHelpText());
                break;

            default:
                sb.AppendLine("No entiendo ese comando.");
                break;
        }

        return sb.ToString().TrimEnd();
    }


    private bool IsRoomLit(Room room)
    {
        var timeOfDay = _state.GameTime.TimeOfDay;
        bool isNight = timeOfDay.Hours >= 21 || timeOfDay.Hours < 7;

        if (room.IsInterior)
        {
            // En interiores la iluminación depende del propio flag de la sala.
            return room.IsIlluminated;
        }
        else
        {
            // En exteriores depende de si es de día o de noche.
            return !isNight;
        }
    }

    public string DescribeCurrentRoom()
    {
        var room = CurrentRoom;
        if (room == null)
            return "Te encuentras en un lugar desconocido.";

        var sb = new StringBuilder();
        sb.AppendLine(room.Name);
        sb.AppendLine();

        if (!IsRoomLit(room))
        {
            sb.AppendLine("Está demasiado oscuro para ver nada.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine(room.Description);

        // Objetos visibles
        var visibleObjects = _state.Objects
            .Where(o => o.Visible && _state.InventoryObjectIds.All(id => !id.Equals(o.Id, StringComparison.OrdinalIgnoreCase)))
            .Where(o => room.ObjectIds.Contains(o.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (visibleObjects.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Ves aquí:");
            foreach (var obj in visibleObjects)
                sb.AppendLine($" - {obj.Name}");
        }

        // NPCs visibles
        var visibleNpcs = _state.Npcs
            .Where(n => n.Visible && room.NpcIds.Contains(n.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (visibleNpcs.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Personajes presentes:");
            foreach (var npc in visibleNpcs)
                sb.AppendLine($" - {npc.Name}");
        }

        // Salidas
        if (room.Exits.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Salidas:");
            foreach (var exit in room.Exits)
            {
                var dir = exit.Direction;
                if (exit.IsLocked)
                    sb.AppendLine($" - {dir} (bloqueada)");
                else
                    sb.AppendLine($" - {dir}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public string DescribeInventory()
    {
        var sb = new StringBuilder();

        if (!_state.InventoryObjectIds.Any())
        {
            sb.AppendLine("No llevas nada.");
        }
        else
        {
            foreach (var id in _state.InventoryObjectIds)
            {
                var obj = _state.Objects.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (obj != null)
                    sb.AppendLine($" - {obj.Name}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public string DescribePlayerStats()
    {
        var p = _state.Player;
        var sb = new StringBuilder();
        sb.AppendLine($"Clase: {p.ClassName}");
        sb.AppendLine($"Nivel: {p.Level}  Exp: {p.Experience}");
        sb.AppendLine($"STR: {p.Strength}  DEX: {p.Dexterity}  INT: {p.Intelligence}");
        sb.AppendLine($"Vida: {p.CurrentHealth}/{p.MaxHealth}");
        sb.AppendLine($"Oro: {p.Gold}");
        sb.AppendLine($"Turno: {_state.TurnCounter}");
        sb.AppendLine($"Hora del día: {_state.TimeOfDay}");
        sb.AppendLine($"Clima: {_state.Weather}");
        return sb.ToString().TrimEnd();
    }

    private string HandleGo(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return "Estás perdido.";

        var dir = parsed.DirectObject ?? string.Empty;
        dir = dir.ToLowerInvariant();

        if (string.IsNullOrEmpty(dir))
            return "¿Hacia dónde quieres ir?";

        var exit = room.Exits.FirstOrDefault(e =>
            string.Equals(NormalizeDirection(e.Direction), NormalizeDirection(dir), StringComparison.OrdinalIgnoreCase));

        if (exit == null)
            return "No puedes ir en esa dirección.";

        // Si la salida está asociada a una puerta, usamos el estado de la puerta.
        if (!string.IsNullOrEmpty(exit.DoorId))
        {
            var door = _state.Doors.FirstOrDefault(d => d.Id.Equals(exit.DoorId, StringComparison.OrdinalIgnoreCase));
            if (door != null && !door.IsOpen)
                return "La puerta está cerrada.";
        }
        else if (exit.IsLocked)
        {
            return "La salida está bloqueada.";
        }

        var target = _state.Rooms.FirstOrDefault(r => r.Id.Equals(exit.TargetRoomId, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return "Algo falla: esa salida no lleva a ningún sitio.";

        _state.CurrentRoomId = target.Id;
        WorldLoader.RebuildRoomIndexes(_state); // por si algún script ha cambiado cosas
        OnRoomChanged();
        return DescribeCurrentRoom();
    }

    

    private Door? FindDoorInCurrentRoomByName(Room room, string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return null;

        return _state.Doors.FirstOrDefault(d =>
            (string.Equals(d.RoomIdA, room.Id, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(d.RoomIdB, room.Id, StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrEmpty(d.Name) &&
            d.Name.Contains(arg, StringComparison.OrdinalIgnoreCase));
    }

    private string HandleOpen(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return "Estás perdido.";

        var arg = (parsed.DirectObject ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(arg))
            return "¿Qué quieres abrir?";

        // 1) Buscar puerta por nombre en la sala actual.
        var door = FindDoorInCurrentRoomByName(room, arg);

        // 2) Si no encontramos por nombre, probamos si es una dirección
        //    (por ejemplo: "abrir norte").
        if (door == null)
        {
            var exit = room.Exits.FirstOrDefault(e =>
                string.Equals(NormalizeDirection(e.Direction), NormalizeDirection(arg), StringComparison.OrdinalIgnoreCase));

            if (exit != null && !string.IsNullOrEmpty(exit.DoorId))
            {
                door = _state.Doors.FirstOrDefault(d => d.Id.Equals(exit.DoorId, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (door == null)
            return "Aquí no hay ninguna puerta así.";

        // Objetos disponibles: inventario + objetos de la sala.
        var availableObjectIds = new List<string>(_state.InventoryObjectIds);
        availableObjectIds.AddRange(room.ObjectIds);

        var result = _doorService.TryOpenDoor(door.Id, room.Id, availableObjectIds);

        switch (result.MessageKey)
        {
            case "door_wrong_side":
                return "No puedes abrir la puerta desde este lado.";
            case "door_requires_key":
                return "La puerta está cerrada con llave.";
            case "door_already_open":
                return "La puerta ya está abierta.";
            case "door_opened":
                return $"Abres {door.Name}.";
            case "door_not_found":
            default:
                return "Aquí no hay ninguna puerta así.";
        }
    }

    private string HandleClose(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return "Estás perdido.";

        var arg = (parsed.DirectObject ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(arg))
            return "¿Qué quieres cerrar?";

        var door = FindDoorInCurrentRoomByName(room, arg);

        if (door == null)
        {
            var exit = room.Exits.FirstOrDefault(e =>
                string.Equals(NormalizeDirection(e.Direction), NormalizeDirection(arg), StringComparison.OrdinalIgnoreCase));

            if (exit != null && !string.IsNullOrEmpty(exit.DoorId))
            {
                door = _state.Doors.FirstOrDefault(d => d.Id.Equals(exit.DoorId, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (door == null)
            return "Aquí no hay ninguna puerta así.";

        var availableObjectIds = new List<string>(_state.InventoryObjectIds);
        availableObjectIds.AddRange(room.ObjectIds);

        var result = _doorService.TryCloseDoor(door.Id, room.Id, availableObjectIds);

        switch (result.MessageKey)
        {
            case "door_wrong_side":
                return "No puedes cerrar la puerta desde este lado.";
            case "door_requires_key":
                return "La puerta está cerrada con llave y no tienes la llave adecuada.";
            case "door_already_closed":
                return "La puerta ya está cerrada.";
            case "door_closed":
                return $"Cierras {door.Name}.";
            case "door_not_found":
            default:
                return "Aquí no hay ninguna puerta así.";
        }
    }
private string HandleTake(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return "No estás en ninguna parte.";

        var arg = parsed.DirectObject ?? string.Empty;
        arg = arg.ToLowerInvariant();

        if (string.IsNullOrEmpty(arg))
            return "¿Qué quieres coger?";

        if (arg.StartsWith("todo"))
        {
            return HandleTakeAll(arg, room);
        }

        var obj = FindVisibleObjectInRoom(room, arg);
        if (obj == null)
            return "No ves eso aquí.";

        if (!obj.CanTake)
            return "No puedes coger eso.";

        if (!_state.InventoryObjectIds.Contains(obj.Id))
            _state.InventoryObjectIds.Add(obj.Id);

        room.ObjectIds.RemoveAll(id => id.Equals(obj.Id, StringComparison.OrdinalIgnoreCase));

        return $"Coges {obj.Name}.";
    }

    private string HandleTakeAll(string arg, Room room)
    {
        var exceptName = string.Empty;

        if (arg.StartsWith("todo menos"))
            exceptName = arg.Substring("todo menos".Length).Trim();
        else if (arg == "todo")
            exceptName = string.Empty;

        var visibleObjs = _state.Objects
            .Where(o => o.Visible && room.ObjectIds.Contains(o.Id, StringComparer.OrdinalIgnoreCase) && o.CanTake)
            .ToList();

        if (!visibleObjs.Any())
            return "No hay nada que puedas coger.";

        var sb = new StringBuilder();

        foreach (var obj in visibleObjs)
        {
            if (!string.IsNullOrEmpty(exceptName) &&
                obj.Name.Contains(exceptName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!_state.InventoryObjectIds.Contains(obj.Id))
                _state.InventoryObjectIds.Add(obj.Id);

            room.ObjectIds.RemoveAll(id => id.Equals(obj.Id, StringComparison.OrdinalIgnoreCase));
            sb.AppendLine($"Coges {obj.Name}.");
        }

        if (sb.Length == 0)
            sb.AppendLine("No coges nada.");

        return sb.ToString().TrimEnd();
    }

    private string HandleDrop(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return "No estás en ninguna parte.";

        var arg = parsed.DirectObject ?? string.Empty;
        arg = arg.ToLowerInvariant();

        if (string.IsNullOrEmpty(arg))
            return "¿Qué quieres soltar?";

        var obj = _state.InventoryObjectIds
            .Select(id => _state.Objects.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(o => o != null && o.Name.Contains(arg, StringComparison.OrdinalIgnoreCase));

        if (obj == null)
            return "No llevas eso.";

        _state.InventoryObjectIds.RemoveAll(id => id.Equals(obj.Id, StringComparison.OrdinalIgnoreCase));
        if (!room.ObjectIds.Contains(obj.Id))
            room.ObjectIds.Add(obj.Id);

        obj.RoomId = room.Id;

        return $"Sueltes {obj.Name}.";
    }

    private string HandleTalk(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return "No estás en ninguna parte.";

        var arg = parsed.DirectObject ?? string.Empty;

        if (string.IsNullOrEmpty(arg))
            return "¿Con quién quieres hablar?";

        var npc = _state.Npcs
            .Where(n => n.Visible && room.NpcIds.Contains(n.Id, StringComparer.OrdinalIgnoreCase))
            .FirstOrDefault(n => n.Name.Contains(arg, StringComparison.OrdinalIgnoreCase));

        if (npc == null)
            return "No ves a esa persona aquí.";

        if (npc.Dialogue == null || npc.Dialogue.Count == 0)
            return $"{npc.Name} no tiene nada que decir.";

        var sb = new StringBuilder();
        sb.AppendLine($"Hablas con {npc.Name}:");
        foreach (var line in npc.Dialogue.OrderBy(d => d.Index))
        {
            sb.AppendLine($" [{line.Index}] {line.Text}");
        }
        sb.AppendLine("Puedes usar 'decir <n>' u 'opcion <n>' para elegir.");

        return sb.ToString().TrimEnd();
    }

    private string HandleUse(ParsedCommand parsed)
    {
        var objName = parsed.DirectObject ?? string.Empty;
        if (string.IsNullOrWhiteSpace(objName))
            return "¿Qué quieres usar?";

        // Uso básico: sólo mostramos un texto.
        return $"Intentas usar {objName}, pero aún no hay reglas específicas definidas.";
    }

    private string HandleGive(ParsedCommand parsed)
    {
        var objName = parsed.DirectObject ?? string.Empty;
        if (string.IsNullOrWhiteSpace(objName))
            return "¿Qué quieres dar?";

        return "El sistema de comercio está definido a nivel de datos, pero aquí sólo mostramos un mensaje básico.";
    }

    private string DescribeQuests()
    {
        if (_state.Quests.Count == 0)
            return "No tienes misiones activas.";

        var sb = new StringBuilder();
        sb.AppendLine("Misiones:");

        foreach (var kvp in _state.Quests)
        {
            var def = _world.Quests.FirstOrDefault(q => q.Id == kvp.Key);
            var st = kvp.Value;

            var name = def?.Name ?? kvp.Key;
            sb.Append($" - {name} [{st.Status}]");

            if (st.Status == QuestStatus.InProgress && def != null && st.CurrentObjectiveIndex < def.Objectives.Count)
            {
                sb.Append($": {def.Objectives[st.CurrentObjectiveIndex]}");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetHelpText()
    {
        return @"Comandos básicos:
 - mirar / examinar / x
 - ir <dirección> (n, s, e, o, ne, no, se, so, subir, bajar, arriba, abajo)
 - coger <objeto>, coger todo, coger todo menos <objeto>
 - soltar <objeto>
 - inventario / i
 - hablar [npc], decir <n>, opcion <n>
 - usar <objeto> [con <objeto>]
 - dar <objeto> [a <npc>]
 - misiones
 - guardar / cargar (usa el menú Archivo)
 - limpiar / cls / clear (limpia la pantalla de texto)
";
    }

    private GameObject? FindVisibleObjectInRoom(Room room, string namePart)
    {
        return _state.Objects
            .Where(o => o.Visible && room.ObjectIds.Contains(o.Id, StringComparer.OrdinalIgnoreCase))
            .FirstOrDefault(o => o.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase));
    }

    private void OnRoomChanged()
    {
        var room = CurrentRoom;
        if (room != null)
        {
            _sound.PlayRoomMusic(room.MusicId, _state.WorldMusicId);
            RoomChanged?.Invoke(room);
        }
        else
        {
            _sound.PlayWorldMusic(_state.WorldMusicId);
        }
    }

    private void EnsurePlayerRoom()
    {
        if (_state.Rooms.All(r => !r.Id.Equals(_state.CurrentRoomId, StringComparison.OrdinalIgnoreCase)))
        {
            var first = _state.Rooms.FirstOrDefault();
            if (first != null)
                _state.CurrentRoomId = first.Id;
        }
    }

    private static string NormalizeDirection(string dir)
    {
        dir = dir.ToLowerInvariant().Trim();
        return dir switch
        {
            "norte" or "n" => "n",
            "sur" or "s" => "s",
            "este" or "e" => "e",
            "oeste" or "o" => "o",
            "noreste" or "ne" => "ne",
            "noroeste" or "no" => "no",
            "sureste" or "se" => "se",
            "suroeste" or "so" => "so",
            "arriba" or "subir" => "up",
            "abajo" or "bajar" => "down",
            _ => dir
        };
    }
}