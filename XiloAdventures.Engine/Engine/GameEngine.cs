using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XiloAdventures.Engine.Engine;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Engine;

/// <summary>
/// Core game engine that processes player commands and manages game state.
/// Handles movement, inventory, doors, NPCs, quests, and room descriptions.
/// </summary>
/// <remarks>
/// The engine uses a command parser to interpret player input and updates
/// the game state accordingly. It also manages audio playback for room
/// transitions and integrates with the door/key system for locked passages.
/// </remarks>
public class GameEngine
{
    private readonly WorldModel _world;
    private readonly SoundManager _sound;
    private GameState _state;

    private DoorService _doorService;
    private DateTime _lastRealTime;
    private ScriptEngine? _scriptEngine;
    private ConversationEngine? _conversationEngine;
    private bool _initialScriptsReady;

    /// <summary>
    /// Loads a new game state into the engine, replacing the current state.
    /// Rebuilds room indexes and triggers room change events.
    /// </summary>
    /// <param name="newState">The new game state to load.</param>
    public void LoadState(GameState newState)
    {
        _state = newState;
        _doorService = new DoorService(_state.Doors, _state.Objects);
        _lastRealTime = DateTime.Now;

        // Reinicializar el motor de scripts con el nuevo estado
        _scriptEngine = new ScriptEngine(_world, _state);
        _scriptEngine.OnMessage += message => ScriptMessage?.Invoke(message);
        _scriptEngine.OnPlaySound += soundId =>
        {
            // TODO: Implementar reproducción de efectos de sonido cuando SoundManager lo soporte
            // var fxAsset = _world.Fxs.FirstOrDefault(f => f.Id.Equals(soundId, StringComparison.OrdinalIgnoreCase));
        };
        _scriptEngine.OnPlayerTeleported += roomId =>
        {
            WorldLoader.RebuildRoomIndexes(_state);
            OnRoomChanged();
        };
        _scriptEngine.OnStartConversation += npcId =>
        {
            _ = StartConversationWithNpcAsync(npcId);
        };

        // Reinicializar el motor de conversaciones
        InitializeConversationEngine();

        WorldLoader.RebuildRoomIndexes(_state);
        OnRoomChanged();
    }

    /// <summary>
    /// Inicializa el motor de conversaciones y conecta sus eventos.
    /// </summary>
    private void InitializeConversationEngine()
    {
        _conversationEngine = new ConversationEngine(_world, _state);
        _conversationEngine.OnDialogue += msg => ConversationDialogue?.Invoke(msg);
        _conversationEngine.OnPlayerOptions += options => ConversationOptions?.Invoke(options);
        _conversationEngine.OnShopOpen += shop => ShopOpened?.Invoke(shop);
        _conversationEngine.OnConversationEnded += () => ConversationEnded?.Invoke();
        _conversationEngine.OnSystemMessage += msg => ScriptMessage?.Invoke(msg);
    }

    /// <summary>
    /// Gets the current game state containing all runtime data.
    /// </summary>
    public GameState State => _state;

    /// <summary>
    /// Event raised when the player moves to a different room.
    /// Used by UI to update room visuals and trigger audio changes.
    /// </summary>
    public event Action<Room>? RoomChanged;

    /// <summary>
    /// Event raised when a script wants to show a message to the player.
    /// </summary>
    public event Action<string>? ScriptMessage;

    /// <summary>
    /// Evento cuando hay texto de diálogo en una conversación.
    /// </summary>
    public event Action<ConversationMessage>? ConversationDialogue;

    /// <summary>
    /// Evento cuando hay opciones de diálogo para el jugador.
    /// </summary>
    public event Action<List<DialogueOption>>? ConversationOptions;

    /// <summary>
    /// Evento cuando se abre la tienda.
    /// </summary>
    public event Action<ShopData>? ShopOpened;

    /// <summary>
    /// Evento cuando termina una conversación.
    /// </summary>
    public event Action? ConversationEnded;

    /// <summary>
    /// Indica si hay una conversación activa.
    /// </summary>
    public bool IsConversationActive => _conversationEngine?.IsConversationActive == true;

    /// <summary>
    /// Indica si la tienda está abierta.
    /// </summary>
    public bool IsInShopMode => _conversationEngine?.IsInShopMode == true;

    /// <summary>
    /// Dispara los scripts iniciales (Event_OnGameStart y Event_OnEnter de la sala inicial).
    /// Debe llamarse después de suscribir los eventos del engine.
    /// </summary>
    public void TriggerInitialScripts()
    {
        _initialScriptsReady = true;

        // Disparar Event_OnGameStart
        var gameId = _world.Game?.Id ?? "game";
        _ = TriggerEntityScriptAsync("Game", gameId, "Event_OnGameStart");

        // Disparar Event_OnEnter de la sala actual
        var room = CurrentRoom;
        if (room != null)
        {
            _ = TriggerRoomScriptsAsync(room.Id, "Event_OnEnter");
        }
    }

    /// <summary>
    /// Creates a new game engine instance.
    /// </summary>
    /// <param name="world">The world model containing static game definitions.</param>
    /// <param name="state">The initial game state (can be new or loaded from save).</param>
    /// <param name="soundManager">Sound manager for music and voice playback.</param>
    public GameEngine(WorldModel world, GameState state, SoundManager soundManager)
    {
        _world = world;
        _sound = soundManager;
        _state = state;
        _doorService = new DoorService(_state.Doors, _state.Objects);

        // Inicializar hora de juego al comenzar la partida si no viene informada.
        if (_state.GameTime == default)
        {
            // Usamos la hora inicial configurada en el juego, en lugar de la hora real.
            var startHour = _world.Game?.StartHour ?? 9;
            if (startHour < 0) startHour = 0;
            if (startHour > 23) startHour = 23;

            var today = DateTime.Today;
            _state.GameTime = new DateTime(today.Year, today.Month, today.Day, startHour, 0, 0);
        }
        _lastRealTime = DateTime.Now;

        // Asegurar índices consistentes
        WorldLoader.RebuildRoomIndexes(_state);
        EnsurePlayerRoom();

        // Inicializar el motor de scripts
        _scriptEngine = new ScriptEngine(_world, _state);
        _scriptEngine.OnMessage += message => ScriptMessage?.Invoke(message);
        _scriptEngine.OnPlaySound += soundId =>
        {
            // TODO: Implementar reproducción de efectos de sonido cuando SoundManager lo soporte
            // var fxAsset = _world.Fxs.FirstOrDefault(f => f.Id.Equals(soundId, StringComparison.OrdinalIgnoreCase));
        };
        _scriptEngine.OnPlayerTeleported += roomId =>
        {
            WorldLoader.RebuildRoomIndexes(_state);
            OnRoomChanged();
        };
        _scriptEngine.OnStartConversation += npcId =>
        {
            _ = StartConversationWithNpcAsync(npcId);
        };

        // Inicializar el motor de conversaciones
        InitializeConversationEngine();

        // Arrancar la música global del mundo (si la hay) al inicio de la partida.
        if (_world.Game != null && !string.IsNullOrWhiteSpace(_state.WorldMusicId))
        {
            var musicAsset = _world.Musics.FirstOrDefault(m => m.Id.Equals(_state.WorldMusicId, StringComparison.OrdinalIgnoreCase));
            _sound.PlayWorldMusic(_state.WorldMusicId, musicAsset?.Base64);
        }

        OnRoomChanged();
    }

    // Helper methods for common searches with case-insensitive comparison
    private GameObject? FindObjectById(string id)
        => _state.Objects.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private Room? FindRoomById(string id)
        => _state.Rooms.FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private Npc? FindNpcById(string id)
        => _state.Npcs.FirstOrDefault(n => n.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns the name with first letter capitalized (for start of sentence).
    /// </summary>
    private static string Cap(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpper(name[0]) + name[1..];
    }

    /// <summary>
    /// Returns the name with first letter lowercased (for mid-sentence use).
    /// </summary>
    private static string Low(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLower(name[0]) + name[1..];
    }

    /// <summary>Devuelve el artículo definido (el/la/los/las) según género y número.</summary>
    private static string Article(GameObject obj) => obj.Gender switch
    {
        GrammaticalGender.Feminine when obj.IsPlural => "las",
        GrammaticalGender.Feminine => "la",
        GrammaticalGender.Masculine when obj.IsPlural => "los",
        _ => "el"
    };

    /// <summary>Devuelve el artículo indefinido (un/una/unos/unas) según género y número.</summary>
    private static string IndefiniteArticle(GameObject obj) => obj.Gender switch
    {
        GrammaticalGender.Feminine when obj.IsPlural => "unas",
        GrammaticalGender.Feminine => "una",
        GrammaticalGender.Masculine when obj.IsPlural => "unos",
        _ => "un"
    };

    /// <summary>Devuelve "el/la + nombre" en minúsculas.</summary>
    private static string WithArticle(GameObject obj) => $"{Article(obj)} {Low(obj.Name)}";

    /// <summary>Devuelve "El/La + nombre" con mayúscula inicial.</summary>
    private static string WithArticleCap(GameObject obj) => Cap($"{Article(obj)} {Low(obj.Name)}");

    /// <summary>Devuelve "un/una + nombre" en minúsculas.</summary>
    private static string WithIndefiniteArticle(GameObject obj) => $"{IndefiniteArticle(obj)} {Low(obj.Name)}";

    /// <summary>Devuelve "Un/Una + nombre" con mayúscula inicial.</summary>
    private static string WithIndefiniteArticleCap(GameObject obj) => Cap($"{IndefiniteArticle(obj)} {Low(obj.Name)}");

    /// <summary>
    /// Find an object in the current room or player inventory by name
    /// </summary>
    private GameObject? FindObjectInRoomOrInventory(Room room, string name)
    {
        var allObjectIds = new List<string>(_state.InventoryObjectIds);
        allObjectIds.AddRange(room.ObjectIds);

        foreach (var objId in allObjectIds)
        {
            var obj = FindObjectById(objId);
            if (obj != null && obj.Visible && obj.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                return obj;
        }

        return null;
    }

    /// <summary>
    /// Helper methods for container objects
    /// </summary>
    private bool CanOpenContainer(GameObject container, out string message)
    {
        message = "";

        if (!container.IsContainer)
        {
            message = $"{Cap(container.Name)} no es un contenedor.";
            return false;
        }

        if (!container.IsOpenable)
        {
            message = $"{Cap(container.Name)} no se puede abrir.";
            return false;
        }

        if (container.IsOpen)
        {
            message = $"{Cap(container.Name)} ya está abierto.";
            return false;
        }

        if (container.IsLocked)
        {
            message = $"{Cap(container.Name)} está cerrado con llave.";
            return false;
        }

        return true;
    }

    private bool CanCloseContainer(GameObject container, out string message)
    {
        message = "";

        if (!container.IsContainer)
        {
            message = $"{Cap(container.Name)} no es un contenedor.";
            return false;
        }

        if (!container.IsOpenable)
        {
            message = $"{Cap(container.Name)} no se puede cerrar.";
            return false;
        }

        if (!container.IsOpen)
        {
            message = $"{Cap(container.Name)} ya está cerrado.";
            return false;
        }

        return true;
    }

    private bool CanUnlockContainer(GameObject container, string? keyId, out string message)
    {
        message = "";

        if (!container.IsLocked)
        {
            message = $"{Cap(container.Name)} no está cerrado con llave.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(container.KeyId))
        {
            message = $"{Cap(container.Name)} no tiene cerradura.";
            return false;
        }

        if (keyId != container.KeyId)
        {
            message = "La llave no encaja.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the room where the player is currently located.
    /// </summary>
    public Room? CurrentRoom => FindRoomById(_state.CurrentRoomId);


    /// <summary>
    /// Gets the ID of the world's background music track.
    /// </summary>
    public string? WorldMusicId => _state.WorldMusicId;




    /// <summary>
    /// Precarga las voces de la sala actual y de las salas conectadas
    /// hasta una cierta distancia en movimientos, y elimina de la caché
    /// las salas que queden más lejos.
    /// </summary>
    public async Task PreloadVoicesAroundCurrentRoomAsync(int maxDistance = 2)
    {
        var origin = CurrentRoom;
        if (origin == null)
            return;

        if (maxDistance < 0)
            maxDistance = 0;

        var roomsById = _state.Rooms
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string roomId, int distance)>();

        visited.Add(origin.Id);
        queue.Enqueue((origin.Id, 0));

        var toPreload = new List<Room>();

        while (queue.Count > 0)
        {
            var (roomId, distance) = queue.Dequeue();
            if (!roomsById.TryGetValue(roomId, out var room))
                continue;

            if (distance > maxDistance)
                continue;

            toPreload.Add(room);

            if (distance == maxDistance)
                continue;

            foreach (var exit in room.Exits)
            {
                if (string.IsNullOrWhiteSpace(exit.TargetRoomId))
                    continue;

                if (visited.Add(exit.TargetRoomId))
                    queue.Enqueue((exit.TargetRoomId, distance + 1));
            }
        }

        var tasks = new List<Task>();
        foreach (var room in toPreload)
        {
            tasks.Add(_sound.PreloadRoomVoiceAsync(room.Id, room.Description));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var allowedIds = new HashSet<string>(toPreload.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
        var cachedIds = _sound.GetCachedVoiceRoomIds();

        foreach (var cachedId in cachedIds)
        {
            if (!allowedIds.Contains(cachedId))
                _sound.RemoveVoiceFromCache(cachedId);
        }
    }

    private void UpdateGameTimeFromReal()
    {
        var now = DateTime.Now;
        var realDelta = now - _lastRealTime;
        if (realDelta < TimeSpan.Zero)
            realDelta = TimeSpan.Zero;

        // Escalado configurable: MinutesPerGameHour minutos reales equivalen a 60 minutos de juego.
        var minutesPerGameHour = _world.Game?.MinutesPerGameHour ?? 6;
        if (minutesPerGameHour <= 0) minutesPerGameHour = 6;
        if (minutesPerGameHour > 10) minutesPerGameHour = 10;

        double factor = 60.0 / minutesPerGameHour;
        var scaledTicks = (long)(realDelta.Ticks * factor);
        if (scaledTicks != 0)
        {
            _state.GameTime = _state.GameTime.Add(TimeSpan.FromTicks(scaledTicks));
            _lastRealTime = now;
        }
    }

    /// <summary>
    /// Processes a player command and returns the result text.
    /// </summary>
    /// <param name="input">The raw command string entered by the player.</param>
    /// <returns>CommandResult con el mensaje para mostrar al jugador y si fue exitoso.</returns>
    /// <remarks>
    /// Supported commands: look, go, open, close, take, drop, talk, use, give,
    /// quests, save, load, help, and inventory.
    /// </remarks>
    public CommandResult ProcessCommand(string input)
    {
        _state.TurnCounter++;
        UpdateGameTimeFromReal();

        // Detectar comandos compuestos (ej: "sacar espada del cofre y guardarla en mochila")
        var commands = SplitCompoundCommand(input);
        if (commands.Count > 1)
        {
            var results = new List<CommandResult>();
            foreach (var cmd in commands)
            {
                results.Add(ProcessSingleCommand(cmd));
            }
            return CommandResult.Combine(results.ToArray());
        }

        return ProcessSingleCommand(input);
    }

    private CommandResult ProcessSingleCommand(string input)
    {
        // Manejar "?" directamente antes del parser (el parser elimina puntuación)
        if (input.Trim() == "?")
            return CommandResult.Success(GetCommandsText());

        // Si la tienda está abierta, manejar comandos de compra/venta
        if (IsInShopMode)
        {
            var trimmed = input.Trim().ToLowerInvariant();

            // Comandos para salir de la tienda
            if (trimmed == "salir" || trimmed == "adios" || trimmed == "adiós" || trimmed == "cerrar")
            {
                _ = _conversationEngine?.CloseShopAsync();
                return CommandResult.Success("Cierras la tienda.");
            }

            // Comando para ver inventario de tienda
            if (trimmed == "ver" || trimmed == "tienda" || trimmed == "inventario")
            {
                var shopText = _conversationEngine?.GetShopInventoryText() ?? "No hay tienda abierta.";
                return CommandResult.Success(shopText);
            }

            // Procesar comando de compra
            var parsed = Parser.Parse(input);
            if (parsed.Verb == "take" || trimmed.StartsWith("comprar ") || trimmed.StartsWith("compra "))
            {
                var itemName = parsed.DirectObject ?? "";
                if (string.IsNullOrEmpty(itemName) && trimmed.StartsWith("comprar "))
                    itemName = trimmed.Substring("comprar ".Length).Trim();
                if (string.IsNullOrEmpty(itemName) && trimmed.StartsWith("compra "))
                    itemName = trimmed.Substring("compra ".Length).Trim();

                if (string.IsNullOrEmpty(itemName))
                    return CommandResult.Error("¿Qué quieres comprar?");

                var (success, message) = _conversationEngine?.ProcessBuyCommand(itemName) ?? (false, "Error");
                return success ? CommandResult.Success(message) : CommandResult.Error(message);
            }

            // Procesar comando de venta
            if (parsed.Verb == "drop" || trimmed.StartsWith("vender ") || trimmed.StartsWith("vende "))
            {
                var itemName = parsed.DirectObject ?? "";
                if (string.IsNullOrEmpty(itemName) && trimmed.StartsWith("vender "))
                    itemName = trimmed.Substring("vender ".Length).Trim();
                if (string.IsNullOrEmpty(itemName) && trimmed.StartsWith("vende "))
                    itemName = trimmed.Substring("vende ".Length).Trim();

                if (string.IsNullOrEmpty(itemName))
                    return CommandResult.Error("¿Qué quieres vender?");

                var (success, message) = _conversationEngine?.ProcessSellCommand(itemName) ?? (false, "Error");
                return success ? CommandResult.Success(message) : CommandResult.Error(message);
            }

            // Otros comandos en tienda: mostrar ayuda
            return CommandResult.Error("Estás en la tienda. Comandos: 'comprar <objeto>', 'vender <objeto>', 'ver', 'salir'");
        }

        // Si hay conversación activa (pero no tienda), interceptar entrada numérica para seleccionar opciones
        if (IsConversationActive)
        {
            var trimmed = input.Trim().ToLowerInvariant();

            // Comandos para salir de la conversación
            if (trimmed == "salir" || trimmed == "adios" || trimmed == "adiós")
            {
                _conversationEngine?.EndConversation();
                return CommandResult.Success("Terminas la conversación.");
            }

            // Detectar entrada numérica directa: "1", "2", "3", "4"
            if (int.TryParse(trimmed, out int optionNum) && optionNum >= 1 && optionNum <= 4)
            {
                _ = HandleConversationOptionAsync(optionNum - 1); // Convertir a 0-based
                return CommandResult.Empty; // La UI se actualiza via eventos
            }

            // También aceptar "opcion N" o "decir N"
            var parsed = Parser.Parse(input);
            if ((parsed.Verb == "option" || parsed.Verb == "say") &&
                int.TryParse(parsed.DirectObject, out int parsedOptionNum) &&
                parsedOptionNum >= 1 && parsedOptionNum <= 4)
            {
                _ = HandleConversationOptionAsync(parsedOptionNum - 1);
                return CommandResult.Empty;
            }

            // Si está en conversación pero no es un número válido, informar
            return CommandResult.Error("Escribe el número de la opción (1-4), o 'salir' para terminar.");
        }

        var parsedCmd = Parser.Parse(input);
        if (string.IsNullOrEmpty(parsedCmd.Verb))
            return CommandResult.Empty;

        return parsedCmd.Verb switch
        {
            "look" => HandleLook(),
            "examine" => HandleExamine(parsedCmd),
            "go" => HandleGo(parsedCmd),
            "open" => HandleOpen(parsedCmd),
            "close" => HandleClose(parsedCmd),
            "unlock" => HandleUnlock(parsedCmd),
            "lock" => HandleLock(parsedCmd),
            "put" => HandlePutIn(parsedCmd),
            "get_from" => HandleGetFrom(parsedCmd),
            "look_in" => HandleLookIn(parsedCmd),
            "inventory" => CommandResult.Success(DescribeInventory()),
            "take" => HandleTake(parsedCmd),
            "drop" => HandleDrop(parsedCmd),
            "talk" or "say" or "option" => HandleTalk(parsedCmd),
            "use" => HandleUse(parsedCmd),
            "give" => HandleGive(parsedCmd),
            "read" => HandleRead(parsedCmd),
            "quests" => CommandResult.Success(DescribeQuests()),
            "save" => CommandResult.Success("Usa el menú Archivo -> Guardar partida... para guardar."),
            "load" => CommandResult.Success("Usa el menú Archivo -> Cargar partida... para cargar."),
            "help" or "commands" => CommandResult.Success(GetCommandsText()),
            _ => CommandResult.Error("No entiendo ese comando.")
        };
    }

    /// <summary>
    /// Divide un comando compuesto en múltiples comandos simples.
    /// Ejemplo: "sacar espada del cofre y guardarla en mochila" -> ["sacar espada del cofre", "guardar espada en mochila"]
    /// </summary>
    private List<string> SplitCompoundCommand(string input)
    {
        var result = new List<string>();
        var lower = input.ToLowerInvariant();

        // Patrones que siempre indican separación de comandos
        var separators = new[] { ", y luego ", " y luego ", ", y después ", " y después ", ". luego ", ". después " };

        foreach (var sep in separators)
        {
            if (lower.Contains(sep))
            {
                var idx = lower.IndexOf(sep);
                var first = input.Substring(0, idx).Trim();
                var second = input.Substring(idx + sep.Length).Trim();
                if (!string.IsNullOrEmpty(first)) result.Add(first);
                if (!string.IsNullOrEmpty(second)) result.AddRange(SplitCompoundCommand(second));
                return result;
            }
        }

        // Buscar " y " seguido de verbo o pronombre con verbo
        var yIndex = lower.IndexOf(" y ");
        if (yIndex > 0 && yIndex < lower.Length - 3)
        {
            var afterY = lower.Substring(yIndex + 3).TrimStart();
            if (StartsWithActionWord(afterY))
            {
                var first = input.Substring(0, yIndex).Trim();
                var secondRaw = input.Substring(yIndex + 3).Trim();

                // Resolver pronombres (guárdala -> guardar + objeto anterior)
                var second = ResolvePronouns(secondRaw, first);

                if (!string.IsNullOrEmpty(first)) result.Add(first);
                if (!string.IsNullOrEmpty(second)) result.AddRange(SplitCompoundCommand(second));
                return result;
            }
        }

        // No es un comando compuesto
        result.Add(input);
        return result;
    }

    /// <summary>
    /// Comprueba si el texto empieza con una palabra de acción (verbo o pronombre+verbo).
    /// </summary>
    private bool StartsWithActionWord(string text)
    {
        var lower = text.ToLowerInvariant();

        // Verbos infinitivos comunes
        var verbs = new[] {
            "guardar", "meter", "poner", "sacar", "coger", "tomar", "dejar", "soltar",
            "abrir", "cerrar", "usar", "examinar", "mirar", "ir", "hablar", "dar",
            "desbloquear", "bloquear", "empujar", "tirar", "leer", "comer", "beber"
        };

        // Pronombres con verbo conjugado (guárdala, mételo, cógela, etc.)
        var pronounPatterns = new[] {
            "guárdal", "métel", "ponl", "sácal", "cógel", "tómal", "déjal", "suéltal",
            "ábrel", "ciérral", "úsal", "examínal", "míral", "dal"
        };

        foreach (var verb in verbs)
        {
            if (lower.StartsWith(verb))
                return true;
        }

        foreach (var pattern in pronounPatterns)
        {
            if (lower.StartsWith(pattern))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resuelve pronombres en el segundo comando usando el objeto del primer comando.
    /// Ejemplo: "guárdala" con primer comando "sacar espada" -> "guardar espada"
    /// </summary>
    private string ResolvePronouns(string command, string previousCommand)
    {
        var lower = command.ToLowerInvariant();

        // Mapeo de pronombres a verbos
        var pronounMappings = new Dictionary<string, string>
        {
            { "guárdala", "guardar" }, { "guárdalo", "guardar" }, { "guárdalas", "guardar" }, { "guárdalos", "guardar" },
            { "métela", "meter" }, { "mételo", "meter" }, { "mételas", "meter" }, { "mételos", "meter" },
            { "ponla", "poner" }, { "ponlo", "poner" }, { "ponlas", "poner" }, { "ponlos", "poner" },
            { "sácala", "sacar" }, { "sácalo", "sacar" }, { "sácalas", "sacar" }, { "sácalos", "sacar" },
            { "cógela", "coger" }, { "cógelo", "coger" }, { "cógelas", "coger" }, { "cógelos", "coger" },
            { "tómala", "tomar" }, { "tómalo", "tomar" }, { "tómalas", "tomar" }, { "tómalos", "tomar" },
            { "déjala", "dejar" }, { "déjalo", "dejar" }, { "déjalas", "dejar" }, { "déjalos", "dejar" },
            { "úsala", "usar" }, { "úsalo", "usar" }, { "úsalas", "usar" }, { "úsalos", "usar" },
            { "ábrela", "abrir" }, { "ábrelo", "abrir" }, { "ábrelas", "abrir" }, { "ábrelos", "abrir" },
            { "ciérrala", "cerrar" }, { "ciérralo", "cerrar" }, { "ciérralas", "cerrar" }, { "ciérralos", "cerrar" },
            { "dásela", "dar" }, { "dáselo", "dar" }, { "dáselas", "dar" }, { "dáselos", "dar" },
        };

        // Buscar el objeto del comando anterior
        var prevParsed = Parser.Parse(previousCommand);
        var objectName = prevParsed.DirectObject ?? "";

        // Buscar si el comando empieza con un pronombre mapeado
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return command;

        var firstWord = words[0].ToLowerInvariant();
        if (pronounMappings.TryGetValue(firstWord, out var verb))
        {
            // Reconstruir el comando con el verbo y el objeto
            var rest = words.Length > 1 ? " " + string.Join(" ", words.Skip(1)) : "";
            return $"{verb} {objectName}{rest}";
        }

        return command;
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

    private CommandResult HandleLook()
    {
        var room = CurrentRoom;
        if (room != null)
        {
            // Disparar Event_OnLook de la sala
            _ = TriggerRoomScriptsAsync(room.Id, "Event_OnLook");
        }
        // Limpiar pantalla antes de mostrar la descripción de la sala
        return CommandResult.SuccessWithClear(DescribeCurrentRoom());
    }

    /// <summary>
    /// Generates a text description of the current room.
    /// Includes visible objects, NPCs, and available exits.
    /// </summary>
    /// <returns>The room description text.</returns>
    public string DescribeCurrentRoom()
    {
        var room = CurrentRoom;
        if (room == null)
            return "Te encuentras en un lugar desconocido.";

        var sb = new StringBuilder();

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
                sb.AppendLine($" - {Cap(obj.Name)}");
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
                sb.AppendLine($" - {Cap(npc.Name)}");
        }

        // Salidas (directas e inversas)
        var allExits = new List<(string Direction, string? DoorId, bool IsLocked)>();

        // Salidas directas definidas en esta sala
        foreach (var exit in room.Exits)
        {
            allExits.Add((exit.Direction, exit.DoorId, exit.IsLocked));
        }

        // Salidas inversas: otras salas que tienen salidas apuntando a esta sala
        var directDirections = new HashSet<string>(
            room.Exits.Select(e => NormalizeDirection(e.Direction)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var candidateRoom in _state.Rooms)
        {
            if (candidateRoom.Id.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var candidateExit in candidateRoom.Exits)
            {
                if (!candidateExit.TargetRoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
                    continue;

                var normCandidate = NormalizeDirection(candidateExit.Direction);
                var opposite = GetOppositeDirectionCode(normCandidate);

                // Solo añadir si no hay ya una salida directa en esa dirección
                if (!directDirections.Contains(opposite))
                {
                    var displayDir = GetDisplayDirection(opposite);
                    allExits.Add((displayDir, candidateExit.DoorId, candidateExit.IsLocked));
                    directDirections.Add(opposite); // Evitar duplicados
                }
            }
        }

        if (allExits.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Salidas:");
            foreach (var (dir, doorId, isLocked) in allExits)
            {
                var doorInfo = "";

                // Comprobar si hay una puerta en esta salida
                if (!string.IsNullOrEmpty(doorId))
                {
                    var door = _state.Doors.FirstOrDefault(d =>
                        d.Id.Equals(doorId, StringComparison.OrdinalIgnoreCase));
                    if (door != null)
                    {
                        var doorName = string.IsNullOrWhiteSpace(door.Name) ? "puerta" : Low(door.Name);
                        var doorState = door.IsOpen ? "abierta" : "cerrada";
                        doorInfo = $" ({doorName} {doorState})";
                    }
                }

                if (isLocked)
                    sb.AppendLine($" - {dir} (bloqueada){doorInfo}");
                else
                    sb.AppendLine($" - {dir}{doorInfo}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetDisplayDirection(string normalizedDir)
    {
        return normalizedDir switch
        {
            "n" => "norte",
            "s" => "sur",
            "e" => "este",
            "o" => "oeste",
            "ne" => "noreste",
            "no" => "noroeste",
            "se" => "sureste",
            "so" => "suroeste",
            "up" => "arriba",
            "down" => "abajo",
            _ => normalizedDir
        };
    }


    /// <summary>
    /// Lists the items currently in the player's inventory.
    /// </summary>
    /// <returns>A formatted list of inventory items, or a message if empty.</returns>
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
                var obj = FindObjectById(id);
                if (obj != null)
                    sb.AppendLine($" - {Cap(obj.Name)}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Describe las puertas de la sala actual con su estado.
    /// </summary>
    public string DescribeDoorsInCurrentRoom()
    {
        var room = CurrentRoom;
        if (room == null)
            return "No hay información de puertas.";

        var doors = GetAllDoorsInRoom(room);
        if (doors.Count == 0)
            return "No hay puertas en esta sala.";

        var sb = new StringBuilder();
        foreach (var (door, direction) in doors)
        {
            var estado = door.IsOpen ? "abierta" : "cerrada";
            var conLlave = door.IsLocked ? " (con llave)" : "";
            sb.AppendLine($"- {door.Name} al {direction}: {estado}{conLlave}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Generates a text summary of the player's stats.
    /// Includes the 5 characteristics and gold.
    /// </summary>
    /// <returns>The player stats text.</returns>
    public string DescribePlayerStats()
    {
        var p = _state.Player;
        var sb = new StringBuilder();
        sb.AppendLine($"Fuerza: {p.Strength}");
        sb.AppendLine($"Constitución: {p.Constitution}");
        sb.AppendLine($"Inteligencia: {p.Intelligence}");
        sb.AppendLine($"Destreza: {p.Dexterity}");
        sb.AppendLine($"Carisma: {p.Charisma}");
        sb.AppendLine();
        sb.AppendLine($"Dinero: {p.Gold} monedas");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Maneja el comando de movimiento del jugador en una dirección especificada.
    /// Soporta movimiento bidireccional: busca salidas directas y también permite
    /// regresar por salidas inversas (si hay una sala con salida hacia aquí, se puede volver).
    /// </summary>
    private CommandResult HandleGo(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("Estás perdido.");

        var dir = parsed.DirectObject ?? string.Empty;
        dir = dir.ToLowerInvariant();

        if (string.IsNullOrEmpty(dir))
            return CommandResult.Error("¿Hacia dónde quieres ir?");

        var normalizedRequested = NormalizeDirection(dir);

        // Primero intentamos encontrar una salida definida en la sala actual.
        Exit? exit = room.Exits.FirstOrDefault(e =>
            string.Equals(NormalizeDirection(e.Direction), normalizedRequested, StringComparison.OrdinalIgnoreCase));

        Room? targetRoom = null;

        if (exit != null)
        {
            // Salida directa encontrada
            targetRoom = _state.Rooms.FirstOrDefault(r =>
                r.Id.Equals(exit.TargetRoomId, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // Si no hay salida directa, intentamos una conexión inversa:
            // Buscamos alguna otra sala que tenga una salida hacia la sala actual,
            // cuya dirección opuesta coincida con la dirección que el jugador ha pedido.
            // Ejemplo: Si estamos en B y A tiene salida "este" hacia B,
            // el jugador puede ir "oeste" desde B hacia A sin que B defina esa salida.
            Exit? reverseExit = null;
            Room? sourceRoom = null;

            foreach (var candidateRoom in _state.Rooms)
            {
                foreach (var candidateExit in candidateRoom.Exits)
                {
                    var normCandidate = NormalizeDirection(candidateExit.Direction);
                    var opposite = GetOppositeDirectionCode(normCandidate);

                    // Si la dirección opuesta de esta salida coincide con lo pedido
                    // y apunta a nuestra sala actual, entonces podemos usarla
                    if (string.Equals(opposite, normalizedRequested, StringComparison.OrdinalIgnoreCase) &&
                        candidateExit.TargetRoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        reverseExit = candidateExit;
                        sourceRoom = candidateRoom;
                        break;
                    }
                }

                if (reverseExit != null)
                    break;
            }

            if (reverseExit != null && sourceRoom != null)
            {
                exit = reverseExit;
                targetRoom = sourceRoom;
            }
        }

        if (exit == null || targetRoom == null)
            return CommandResult.Error("No puedes ir en esa dirección.");

        // Si la salida está asociada a una puerta, usamos el estado de la puerta.
        if (!string.IsNullOrEmpty(exit.DoorId))
        {
            var door = _state.Doors.FirstOrDefault(d => d.Id.Equals(exit.DoorId, StringComparison.OrdinalIgnoreCase));
            if (door != null && !door.IsOpen)
                return CommandResult.Error("La puerta está cerrada.");
        }
        else if (exit.IsLocked)
        {
            return CommandResult.Error("La salida está bloqueada.");
        }

        // Disparar Event_OnExit de la sala actual antes de salir
        _ = TriggerRoomScriptsAsync(room.Id, "Event_OnExit");

        _state.CurrentRoomId = targetRoom.Id;
        WorldLoader.RebuildRoomIndexes(_state); // por si algún script ha cambiado cosas
        OnRoomChanged();
        return CommandResult.Success(DescribeCurrentRoom());
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

    private CommandResult HandleOpen(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("Estás perdido.");

        var arg = (parsed.DirectObject ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(arg))
            return CommandResult.Error("¿Qué quieres abrir?");

        // Primero intentar con objetos contenedores
        var obj = FindObjectInRoomOrInventory(room, arg);
        if (obj != null && obj.IsContainer)
        {
            if (CanOpenContainer(obj, out string message))
            {
                obj.IsOpen = true;
                // Disparar evento de contenedor abierto
                _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnContainerOpen");
                return CommandResult.Success($"Abres {Low(obj.Name)}.");
            }
            return CommandResult.Error(message);
        }

        // Buscar puerta
        var (door, errorMsg) = FindDoorByArgument(room, arg);
        if (door == null)
            return CommandResult.Error(errorMsg ?? "Aquí no hay ninguna puerta así.");

        // Solo los objetos del inventario sirven como llaves
        var result = _doorService.TryOpenDoor(door.Id, room.Id, _state.InventoryObjectIds);

        if (result.MessageKey == "door_opened")
        {
            // Disparar evento de puerta abierta
            _ = TriggerEntityScriptAsync("Door", door.Id, "Event_OnDoorOpen");
            return CommandResult.Success(GetDoorOpenedMessage(door));
        }

        return result.MessageKey switch
        {
            "door_wrong_side" => CommandResult.Error("No puedes abrir la puerta desde este lado."),
            "door_requires_key" => CommandResult.Error("La puerta está cerrada con llave."),
            "door_already_open" => CommandResult.Error("La puerta ya está abierta."),
            _ => CommandResult.Error("Aquí no hay ninguna puerta así.")
        };
    }

    private string GetDoorOpenedMessage(Door door)
    {
        if (!string.IsNullOrWhiteSpace(door.KeyObjectId))
        {
            var keyObj = FindObjectById(door.KeyObjectId);
            if (keyObj != null)
                return $"Abres {Low(door.Name)} con {Low(keyObj.Name)}.";
        }
        return $"Abres {Low(door.Name)}.";
    }

    private CommandResult HandleClose(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("Estás perdido.");

        var arg = (parsed.DirectObject ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(arg))
            return CommandResult.Error("¿Qué quieres cerrar?");

        // Primero intentar con objetos contenedores
        var obj = FindObjectInRoomOrInventory(room, arg);
        if (obj != null && obj.IsContainer)
        {
            if (CanCloseContainer(obj, out string message))
            {
                obj.IsOpen = false;
                // Disparar evento de contenedor cerrado
                _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnContainerClose");
                return CommandResult.Success($"Cierras {Low(obj.Name)}.");
            }
            return CommandResult.Error(message);
        }

        // Buscar puerta
        var (door, errorMsg) = FindDoorByArgument(room, arg);
        if (door == null)
            return CommandResult.Error(errorMsg ?? "Aquí no hay ninguna puerta así.");

        // Solo los objetos del inventario sirven como llaves
        var result = _doorService.TryCloseDoor(door.Id, room.Id, _state.InventoryObjectIds);

        if (result.MessageKey == "door_closed")
        {
            // Disparar evento de puerta cerrada
            _ = TriggerEntityScriptAsync("Door", door.Id, "Event_OnDoorClose");
            return CommandResult.Success(GetDoorClosedMessage(door));
        }

        return result.MessageKey switch
        {
            "door_wrong_side" => CommandResult.Error("No puedes cerrar la puerta desde este lado."),
            "door_requires_key" => CommandResult.Error("No tienes la llave necesaria para cerrar esta puerta."),
            "door_already_closed" => CommandResult.Error("La puerta ya está cerrada."),
            _ => CommandResult.Error("Aquí no hay ninguna puerta así.")
        };
    }

    private string GetDoorClosedMessage(Door door)
    {
        if (!string.IsNullOrWhiteSpace(door.KeyObjectId))
        {
            var keyObj = FindObjectById(door.KeyObjectId);
            if (keyObj != null)
                return $"Cierras {Low(door.Name)} con {Low(keyObj.Name)}.";
        }
        return $"Cierras {Low(door.Name)}.";
    }

    /// <summary>
    /// Busca una puerta basándose en el argumento del jugador.
    /// Soporta: nombre de puerta, dirección, "puerta norte", "puerta del norte", etc.
    /// </summary>
    private (Door? door, string? errorMessage) FindDoorByArgument(Room room, string arg)
    {
        // 1) Si el argumento es "puerta" genérico sin dirección, comprobar cuántas puertas hay
        if (IsDoorWord(arg))
        {
            var allDoors = GetAllDoorsInRoom(room);
            if (allDoors.Count == 0)
                return (null, "Aquí no hay ninguna puerta.");
            if (allDoors.Count == 1)
                return (allDoors[0].Door, null);

            // Múltiples puertas: pedir especificar
            var directions = string.Join(", ", allDoors.Select(d => d.Direction));
            return (null, $"Hay varias puertas aquí. Especifica cuál: {directions}.");
        }

        // 2) Extraer dirección del argumento (ej: "puerta norte", "puerta del este", "norte")
        var direction = ExtractDirectionFromArg(arg);

        // 3) Si hay dirección, buscar puerta en esa dirección
        if (!string.IsNullOrEmpty(direction))
        {
            var door = FindDoorByDirection(room, direction);
            if (door != null)
                return (door, null);
            return (null, $"No hay ninguna puerta en esa dirección.");
        }

        // 4) Buscar puerta por nombre
        var doorByName = FindDoorInCurrentRoomByName(room, arg);
        if (doorByName != null)
            return (doorByName, null);

        return (null, "Aquí no hay ninguna puerta así.");
    }

    /// <summary>
    /// Comprueba si el argumento es una palabra que significa "puerta".
    /// </summary>
    private static bool IsDoorWord(string arg)
    {
        var lower = arg.ToLowerInvariant();
        return lower == "puerta" || lower == "la puerta" || lower == "una puerta";
    }

    /// <summary>
    /// Extrae la dirección de un argumento como "puerta norte", "puerta del este", etc.
    /// </summary>
    private static string? ExtractDirectionFromArg(string arg)
    {
        var lower = arg.ToLowerInvariant().Trim();

        // Patrones: "puerta norte", "puerta del norte", "puerta al norte", "la puerta norte", etc.
        var patterns = new[] { "puerta del ", "puerta al ", "puerta de ", "puerta ", "la puerta del ", "la puerta al ", "la puerta de ", "la puerta " };
        foreach (var pattern in patterns)
        {
            if (lower.StartsWith(pattern))
            {
                var dir = lower.Substring(pattern.Length).Trim();
                if (!string.IsNullOrEmpty(dir))
                    return dir;
            }
        }

        // Si es directamente una dirección
        var normalized = NormalizeDirection(lower);
        if (normalized != lower || IsKnownDirection(lower))
            return lower;

        return null;
    }

    private static bool IsKnownDirection(string dir)
    {
        var known = new[] { "norte", "sur", "este", "oeste", "noreste", "noroeste", "sureste", "suroeste", "arriba", "abajo", "subir", "bajar", "n", "s", "e", "o", "ne", "no", "se", "so", "up", "down" };
        return known.Contains(dir.ToLowerInvariant());
    }

    /// <summary>
    /// Busca una puerta en una dirección específica (directa o inversa).
    /// </summary>
    private Door? FindDoorByDirection(Room room, string direction)
    {
        var normalizedDir = NormalizeDirection(direction);

        // Buscar en salidas directas
        var exit = room.Exits.FirstOrDefault(e =>
            string.Equals(NormalizeDirection(e.Direction), normalizedDir, StringComparison.OrdinalIgnoreCase));

        if (exit != null && !string.IsNullOrEmpty(exit.DoorId))
        {
            return _state.Doors.FirstOrDefault(d => d.Id.Equals(exit.DoorId, StringComparison.OrdinalIgnoreCase));
        }

        // Buscar en salidas inversas
        foreach (var candidateRoom in _state.Rooms)
        {
            if (candidateRoom.Id.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var candidateExit in candidateRoom.Exits)
            {
                if (!candidateExit.TargetRoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
                    continue;

                var opposite = GetOppositeDirectionCode(NormalizeDirection(candidateExit.Direction));
                if (string.Equals(opposite, normalizedDir, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(candidateExit.DoorId))
                {
                    return _state.Doors.FirstOrDefault(d => d.Id.Equals(candidateExit.DoorId, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Obtiene todas las puertas accesibles desde una sala (directas e inversas).
    /// </summary>
    private List<(Door Door, string Direction)> GetAllDoorsInRoom(Room room)
    {
        var result = new List<(Door Door, string Direction)>();
        var addedDoorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Puertas de salidas directas
        foreach (var exit in room.Exits)
        {
            if (string.IsNullOrEmpty(exit.DoorId))
                continue;

            var door = _state.Doors.FirstOrDefault(d => d.Id.Equals(exit.DoorId, StringComparison.OrdinalIgnoreCase));
            if (door != null && addedDoorIds.Add(door.Id))
            {
                result.Add((door, exit.Direction));
            }
        }

        // Puertas de salidas inversas
        foreach (var candidateRoom in _state.Rooms)
        {
            if (candidateRoom.Id.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var candidateExit in candidateRoom.Exits)
            {
                if (!candidateExit.TargetRoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(candidateExit.DoorId))
                    continue;

                var door = _state.Doors.FirstOrDefault(d => d.Id.Equals(candidateExit.DoorId, StringComparison.OrdinalIgnoreCase));
                if (door != null && addedDoorIds.Add(door.Id))
                {
                    var opposite = GetOppositeDirectionCode(NormalizeDirection(candidateExit.Direction));
                    result.Add((door, GetDisplayDirection(opposite)));
                }
            }
        }

        return result;
    }

    private CommandResult HandleUnlock(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("Estás perdido.");

        var arg = (parsed.DirectObject ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(arg))
            return CommandResult.Error("¿Qué quieres desbloquear?");

        var obj = FindObjectInRoomOrInventory(room, arg);
        if (obj == null || !obj.IsContainer)
            return CommandResult.Error("No hay ningún contenedor con ese nombre.");

        // Buscar la llave en el inventario
        var key = _state.InventoryObjectIds
            .Select(FindObjectById)
            .FirstOrDefault(k => k != null && k.Id == obj.KeyId);

        if (CanUnlockContainer(obj, key?.Id, out string message))
        {
            obj.IsLocked = false;
            // Disparar evento de desbloqueo (se usa el mismo evento que para puertas)
            _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnDoorUnlock");
            return CommandResult.Success($"Desbloqueas {Low(obj.Name)} con {Low(key?.Name ?? "")}.");
        }

        return CommandResult.Error(message);
    }

    private CommandResult HandleLock(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("Estás perdido.");

        var arg = (parsed.DirectObject ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(arg))
            return CommandResult.Error("¿Qué quieres bloquear?");

        var obj = FindObjectInRoomOrInventory(room, arg);
        if (obj == null || !obj.IsContainer)
            return CommandResult.Error("No hay ningún contenedor con ese nombre.");

        if (obj.IsLocked)
            return CommandResult.Error($"{Cap(obj.Name)} ya está bloqueado.");

        if (string.IsNullOrWhiteSpace(obj.KeyId))
            return CommandResult.Error($"{Cap(obj.Name)} no tiene cerradura.");

        // Buscar la llave en el inventario
        var key = _state.InventoryObjectIds
            .Select(FindObjectById)
            .FirstOrDefault(k => k != null && k.Id == obj.KeyId);

        if (key == null)
            return CommandResult.Error("No tienes la llave adecuada.");

        obj.IsLocked = true;
        // Disparar evento de bloqueo
        _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnDoorLock");
        return CommandResult.Success($"Bloqueas {Low(obj.Name)} con {Low(key.Name)}.");
    }

    private CommandResult HandlePutIn(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("Estás perdido.");

        // Necesitamos parsear "meter X en Y" - DirectObject es X, Preposition + IndirectObject es "en Y"
        var objectName = (parsed.DirectObject ?? string.Empty).Trim();
        var containerName = (parsed.IndirectObject ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(objectName))
            return CommandResult.Error("¿Qué quieres meter?");

        if (string.IsNullOrEmpty(containerName))
            return CommandResult.Error("¿Dónde quieres meterlo?");

        // Buscar el objeto a meter (puede estar en el inventario o en la sala)
        var objToInsert = FindObjectInRoomOrInventory(room, objectName);

        if (objToInsert == null)
            return CommandResult.Error("No ves ese objeto por aquí.");

        // Verificar que el objeto está en inventario o en la sala (no en otro contenedor)
        var isInInventory = _state.InventoryObjectIds.Contains(objToInsert.Id);
        var isInRoom = string.Equals(objToInsert.RoomId, room.Id, StringComparison.OrdinalIgnoreCase);

        if (!isInInventory && !isInRoom)
            return CommandResult.Error("No puedes coger ese objeto.");

        // Buscar el contenedor
        var container = FindObjectInRoomOrInventory(room, containerName);
        if (container == null || !container.IsContainer)
            return CommandResult.Error("No hay ningún contenedor con ese nombre.");

        if (container.IsOpenable && !container.IsOpen)
            return CommandResult.Error($"{Cap(container.Name)} está cerrado.");

        // Verificar capacidad por volumen
        if (container.MaxCapacity > 0)
        {
            var currentVolume = container.ContainedObjectIds
                .Select(FindObjectById)
                .Where(o => o != null)
                .Sum(o => o!.Volume);

            if (currentVolume + objToInsert.Volume > container.MaxCapacity)
                return CommandResult.Error($"{WithArticleCap(objToInsert)} no cabe en {WithArticle(container)}.");
        }

        // Mover el objeto al contenedor (desde inventario o sala)
        if (isInInventory)
            _state.InventoryObjectIds.Remove(objToInsert.Id);

        container.ContainedObjectIds.Add(objToInsert.Id);
        objToInsert.RoomId = null; // El objeto ya no está en una sala

        return CommandResult.Success($"Metes {WithArticle(objToInsert)} en {WithArticle(container)}.");
    }

    private CommandResult HandleGetFrom(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("Estás perdido.");

        var objectName = (parsed.DirectObject ?? string.Empty).Trim();
        var containerName = (parsed.IndirectObject ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(objectName))
            return CommandResult.Error("¿Qué quieres sacar?");

        if (string.IsNullOrEmpty(containerName))
            return CommandResult.Error("¿De dónde quieres sacarlo?");

        // Buscar el contenedor
        var container = FindObjectInRoomOrInventory(room, containerName);
        if (container == null || !container.IsContainer)
            return CommandResult.Error("No hay ningún contenedor con ese nombre.");

        if (container.IsOpenable && !container.IsOpen && !container.ContentsVisible)
            return CommandResult.Error($"{Cap(container.Name)} está cerrado.");

        // Buscar el objeto dentro del contenedor
        var objToExtract = container.ContainedObjectIds
            .Select(FindObjectById)
            .FirstOrDefault(o => o != null && o.Name.Contains(objectName, StringComparison.OrdinalIgnoreCase));

        if (objToExtract == null)
            return CommandResult.Error($"No hay ningún {objectName} en {WithArticle(container)}.");

        // Mover el objeto del contenedor a la sala
        container.ContainedObjectIds.Remove(objToExtract.Id);
        objToExtract.RoomId = room.Id;

        return CommandResult.Success($"Sacas {WithArticle(objToExtract)} de {WithArticle(container)}.");
    }

    private CommandResult HandleLookIn(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("Estás perdido.");

        var containerName = (parsed.DirectObject ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(containerName))
            return CommandResult.Error("¿Qué quieres mirar?");

        var container = FindObjectInRoomOrInventory(room, containerName);
        if (container == null || !container.IsContainer)
            return CommandResult.Error("No hay ningún contenedor con ese nombre.");

        if (container.IsOpenable && !container.IsOpen && !container.ContentsVisible)
            return CommandResult.Error($"{Cap(container.Name)} está cerrado y no puedes ver su interior.");

        if (container.ContainedObjectIds.Count == 0)
            return CommandResult.Success($"{Cap(container.Name)} está vacío.");

        var sb = new StringBuilder();
        sb.AppendLine($"Dentro de {WithArticle(container)} ves:");

        foreach (var objId in container.ContainedObjectIds)
        {
            var obj = FindObjectById(objId);
            if (obj != null)
                sb.AppendLine($"- {Cap(obj.Name)}");
        }

        return CommandResult.Success(sb.ToString().TrimEnd());
    }

    private CommandResult HandleExamine(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("Estás perdido.");

        var target = (parsed.DirectObject ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(target))
            return CommandResult.Error("¿Qué quieres examinar?");

        // Buscar objeto en la sala o inventario
        var obj = FindObjectInRoomOrInventory(room, target);
        if (obj != null)
        {
            // Disparar script Event_OnExamine
            _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnExamine");

            var sb = new StringBuilder();

            // Descripción base
            if (!string.IsNullOrWhiteSpace(obj.Description))
                sb.Append(obj.Description);
            else
                sb.Append($"No ves nada especial en {WithArticle(obj)}.");

            // Si es contenedor, añadir información adicional
            if (obj.IsContainer)
            {
                // Estado abierto/cerrado si es abrible
                if (obj.IsOpenable)
                {
                    sb.Append(obj.IsOpen ? " Está abierto." : " Está cerrado.");
                    if (obj.IsLocked && !obj.IsOpen)
                        sb.Append(" Parece que necesita una llave.");
                }

                // Mostrar contenido si está abierto o si el contenido es visible
                if (obj.IsOpen || obj.ContentsVisible || !obj.IsOpenable)
                {
                    if (obj.ContainedObjectIds.Count == 0)
                    {
                        sb.Append(" Está vacío.");
                    }
                    else
                    {
                        var contents = obj.ContainedObjectIds
                            .Select(FindObjectById)
                            .Where(o => o != null)
                            .Select(o => Low(o!.Name))
                            .ToList();

                        if (contents.Count > 0)
                            sb.Append($" Dentro hay: {string.Join(", ", contents)}.");
                    }
                }
            }

            return CommandResult.Success(sb.ToString());
        }

        // Buscar NPC en la sala
        var npc = _state.Npcs.FirstOrDefault(n =>
            n.Visible &&
            n.RoomId?.Equals(room.Id, StringComparison.OrdinalIgnoreCase) == true &&
            n.Name.Contains(target, StringComparison.OrdinalIgnoreCase));
        if (npc != null)
        {
            if (!string.IsNullOrWhiteSpace(npc.Description))
                return CommandResult.Success(npc.Description);
            return CommandResult.Success($"No ves nada especial en {Low(npc.Name)}.");
        }

        // Buscar puerta en la sala
        var door = FindDoorInCurrentRoomByName(room, target);
        if (door != null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(door.Description))
            {
                sb.Append(door.Description);
                // Añadir punto si la descripción no termina con signo de puntuación
                if (!door.Description.EndsWith('.') && !door.Description.EndsWith('!') && !door.Description.EndsWith('?'))
                    sb.Append('.');
            }
            else
                sb.Append($"Es {Low(door.Name)}.");

            // Añadir estado de la puerta
            sb.Append(door.IsOpen ? " Está abierta." : " Está cerrada.");
            if (door.IsLocked && !door.IsOpen)
                sb.Append(" Parece que necesita una llave.");

            return CommandResult.Success(sb.ToString());
        }

        return CommandResult.Error("No ves eso por aquí.");
    }

    private CommandResult HandleTake(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("No estás en ninguna parte.");

        var arg = parsed.DirectObject ?? string.Empty;
        arg = arg.ToLowerInvariant();

        if (string.IsNullOrEmpty(arg))
            return CommandResult.Error("¿Qué quieres coger?");

        if (arg.StartsWith("todo"))
        {
            return HandleTakeAll(arg, room);
        }

        // Primero buscar en la sala directamente
        var obj = FindVisibleObjectInRoom(room, arg);
        GameObject? container = null;

        // Si no está en la sala, buscar dentro de contenedores abiertos
        if (obj == null)
        {
            (obj, container) = FindObjectInOpenContainers(room, arg);
        }

        if (obj == null)
            return CommandResult.Error("No ves eso aquí.");

        if (!obj.CanTake)
            return CommandResult.Error("No puedes coger eso.");

        if (!_state.InventoryObjectIds.Contains(obj.Id))
            _state.InventoryObjectIds.Add(obj.Id);

        // Si estaba en un contenedor, sacarlo del contenedor
        if (container != null)
        {
            container.ContainedObjectIds.Remove(obj.Id);
        }
        else
        {
            // Estaba directamente en la sala
            room.ObjectIds.RemoveAll(id => id.Equals(obj.Id, StringComparison.OrdinalIgnoreCase));
        }

        // Disparar evento Event_OnTake del objeto
        _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnTake");

        if (container != null)
            return CommandResult.Success($"Coges {WithArticle(obj)} de {WithArticle(container)}.");

        return CommandResult.Success($"Coges {WithArticle(obj)}.");
    }

    private CommandResult HandleTakeAll(string arg, Room room)
    {
        var exceptName = string.Empty;

        if (arg.StartsWith("todo menos"))
            exceptName = arg.Substring("todo menos".Length).Trim();
        else if (arg == "todo")
            exceptName = string.Empty;

        // Objetos directamente en la sala
        var visibleObjs = _state.Objects
            .Where(o => o.Visible && room.ObjectIds.Contains(o.Id, StringComparer.OrdinalIgnoreCase) && o.CanTake)
            .ToList();

        // Buscar objetos en contenedores abiertos
        var containers = _state.Objects
            .Where(o => o.Visible && o.IsContainer && room.ObjectIds.Contains(o.Id, StringComparer.OrdinalIgnoreCase))
            .Where(o => o.IsOpen || o.ContentsVisible || !o.IsOpenable)
            .ToList();

        var objectsInContainers = new List<(GameObject obj, GameObject container)>();
        foreach (var container in containers)
        {
            var containedObjs = container.ContainedObjectIds
                .Select(FindObjectById)
                .Where(o => o != null && o.Visible && o.CanTake)
                .ToList();

            foreach (var obj in containedObjs)
            {
                objectsInContainers.Add((obj!, container));
            }
        }

        if (!visibleObjs.Any() && !objectsInContainers.Any())
            return CommandResult.Error("No hay nada que puedas coger.");

        var sb = new StringBuilder();

        // Coger objetos directamente de la sala
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

            // Disparar evento Event_OnTake del objeto
            _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnTake");

            sb.AppendLine($"Coges {WithArticle(obj)}.");
        }

        // Coger objetos de contenedores abiertos
        foreach (var (obj, container) in objectsInContainers)
        {
            if (!string.IsNullOrEmpty(exceptName) &&
                obj.Name.Contains(exceptName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!_state.InventoryObjectIds.Contains(obj.Id))
                _state.InventoryObjectIds.Add(obj.Id);

            container.ContainedObjectIds.Remove(obj.Id);

            // Disparar evento Event_OnTake del objeto
            _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnTake");

            sb.AppendLine($"Coges {WithArticle(obj)} de {WithArticle(container)}.");
        }

        if (sb.Length == 0)
            return CommandResult.Error("No coges nada.");

        return CommandResult.Success(sb.ToString().TrimEnd());
    }

    private CommandResult HandleDrop(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("No estás en ninguna parte.");

        var arg = parsed.DirectObject ?? string.Empty;
        arg = arg.ToLowerInvariant();

        if (string.IsNullOrEmpty(arg))
            return CommandResult.Error("¿Qué quieres soltar?");

        var obj = _state.InventoryObjectIds
            .Select(FindObjectById)
            .FirstOrDefault(o => o != null && o.Name.Contains(arg, StringComparison.OrdinalIgnoreCase));

        if (obj == null)
            return CommandResult.Error("No llevas eso.");

        _state.InventoryObjectIds.RemoveAll(id => id.Equals(obj.Id, StringComparison.OrdinalIgnoreCase));
        if (!room.ObjectIds.Contains(obj.Id))
            room.ObjectIds.Add(obj.Id);

        obj.RoomId = room.Id;

        // Disparar evento Event_OnDrop del objeto
        _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnDrop");

        return CommandResult.Success($"Sueltas {WithArticle(obj)}.");
    }

    private CommandResult HandleTalk(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("No estás en ninguna parte.");

        var arg = parsed.DirectObject ?? string.Empty;

        // Si no hay objeto directo, intentar con el indirecto
        if (string.IsNullOrEmpty(arg))
            arg = parsed.IndirectObject ?? string.Empty;

        if (string.IsNullOrEmpty(arg))
            return CommandResult.Error("¿Con quién quieres hablar?");

        // Buscar NPC en la sala
        var npcsInRoom = _state.Npcs
            .Where(n => n.Visible && room.NpcIds.Contains(n.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        // Primero buscar con el objeto directo
        var npc = npcsInRoom.FirstOrDefault(n => n.Name.Contains(arg, StringComparison.OrdinalIgnoreCase));

        // Si no se encuentra y hay objeto indirecto, buscar con él (para "decir hola a gordo")
        if (npc == null && !string.IsNullOrEmpty(parsed.IndirectObject))
        {
            npc = npcsInRoom.FirstOrDefault(n => n.Name.Contains(parsed.IndirectObject, StringComparison.OrdinalIgnoreCase));
        }

        if (npc == null)
            return CommandResult.Error("No ves a esa persona aquí.");

        // Disparar script Event_OnTalk
        _ = TriggerEntityScriptAsync("Npc", npc.Id, "Event_OnTalk");

        // Si el NPC no tiene conversación asignada
        if (string.IsNullOrEmpty(npc.ConversationId))
            return CommandResult.Success($"{Cap(npc.Name)} no tiene nada que decir.");

        // Iniciar conversación con el NPC
        _ = StartConversationWithNpcAsync(npc.Id);
        return CommandResult.Empty; // La UI se actualiza via eventos
    }

    /// <summary>
    /// Inicia una conversación con un NPC de forma asíncrona.
    /// </summary>
    private async Task StartConversationWithNpcAsync(string npcId)
    {
        if (_conversationEngine == null) return;

        try
        {
            await _conversationEngine.StartConversationAsync(npcId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Conversation error: {ex.Message}");
            ScriptMessage?.Invoke("Error al iniciar la conversación.");
        }
    }

    /// <summary>
    /// Maneja la selección de una opción de conversación.
    /// </summary>
    private async Task HandleConversationOptionAsync(int optionIndex)
    {
        if (_conversationEngine == null) return;

        try
        {
            await _conversationEngine.SelectOptionAsync(optionIndex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Conversation option error: {ex.Message}");
            ScriptMessage?.Invoke("Error al seleccionar opción.");
        }
    }

    /// <summary>
    /// Inicia una conversación desde un script (Action_StartConversation).
    /// </summary>
    public async Task StartConversationFromScriptAsync(string npcId)
    {
        await StartConversationWithNpcAsync(npcId);
    }

    private CommandResult HandleUse(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        var objName = parsed.DirectObject ?? string.Empty;
        if (string.IsNullOrWhiteSpace(objName))
            return CommandResult.Error("¿Qué quieres usar?");

        // Buscar el objeto en la sala o inventario
        var obj = room != null ? FindObjectInRoomOrInventory(room, objName) : null;
        if (obj != null)
        {
            // Disparar script Event_OnUse
            _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnUse");
            return CommandResult.Success($"Usas {Low(obj.Name)}.");
        }

        // Si no se encuentra el objeto, mensaje por defecto
        return CommandResult.Error($"No ves ningún '{objName}' que puedas usar.");
    }

    private CommandResult HandleGive(ParsedCommand parsed)
    {
        var objName = parsed.DirectObject ?? string.Empty;
        if (string.IsNullOrWhiteSpace(objName))
            return CommandResult.Error("¿Qué quieres dar?");

        return CommandResult.Error("El sistema de comercio está definido a nivel de datos, pero aquí sólo mostramos un mensaje básico.");
    }

    private CommandResult HandleRead(ParsedCommand parsed)
    {
        var room = CurrentRoom;
        if (room == null)
            return CommandResult.Error("No estás en ninguna parte.");

        var objName = parsed.DirectObject ?? string.Empty;
        if (string.IsNullOrWhiteSpace(objName))
            return CommandResult.Error("¿Qué quieres leer?");

        // Buscar el objeto en la sala o inventario
        var obj = FindObjectInRoomOrInventory(room, objName);
        if (obj == null)
            return CommandResult.Error("No ves eso por aquí.");

        // Verificar que el objeto sea de tipo Texto
        if (obj.Type != ObjectType.Texto)
            return CommandResult.Error($"No puedes leer {WithArticle(obj)}.");

        // Verificar que tenga contenido de texto
        if (string.IsNullOrWhiteSpace(obj.TextContent))
            return CommandResult.Error($"{WithArticleCap(obj)} está en blanco.");

        // Disparar script Event_OnExamine (leer es una forma de examinar)
        _ = TriggerEntityScriptAsync("GameObject", obj.Id, "Event_OnExamine");

        // Mostrar el contenido con formato de texto leído (cursiva simulada con comillas)
        var sb = new StringBuilder();
        sb.AppendLine($"Lees {WithArticle(obj)}:");
        sb.AppendLine();
        sb.AppendLine($"« {obj.TextContent} »");

        return CommandResult.Success(sb.ToString().TrimEnd());
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
 - mirar (describe la sala actual)
 - examinar <algo> / x <algo> (describe un objeto, NPC o puerta)
 - leer <objeto> (lee el contenido de libros, cartas, pergaminos...)
 - ir <dirección> (n, s, e, o, ne, no, se, so, subir, bajar, arriba, abajo)
 - coger <objeto>, coger todo, coger todo menos <objeto>
 - soltar <objeto>
 - inventario / i
 - hablar [npc], decir <n>, opcion <n>
 - usar <objeto> [con <objeto>]
 - dar <objeto> [a <npc>]
 - abrir/cerrar <puerta>
 - misiones
 - guardar / cargar (usa el menú Archivo)
 - limpiar / cls / clear (limpia la pantalla de texto)
 - ? / verbos / comandos (muestra esta lista de verbos)
";
    }

    private static string GetCommandsText()
    {
        return @"╔══════════════════════════════════════════════════════════════╗
║                    VERBOS DISPONIBLES                        ║
╠══════════════════════════════════════════════════════════════╣
║ MOVIMIENTO                                                   ║
║  ir, ve, andar, caminar    → ""ir norte"", ""ve al sur""         ║
║  n, s, e, o, ne, no, se, so → ""n"" (ir al norte)              ║
║  subir, bajar, arriba, abajo                                 ║
╠══════════════════════════════════════════════════════════════╣
║ EXPLORACIÓN                                                  ║
║  mirar, mira, ver, observa  → ""mirar"" (describe la sala)     ║
║  examinar, x                → ""examinar espada"", ""x cofre""   ║
║  leer, lee                  → ""leer pergamino"", ""lee carta""  ║
╠══════════════════════════════════════════════════════════════╣
║ INVENTARIO                                                   ║
║  inventario, inv, i         → ""inventario"" (ver objetos)     ║
║  coger, tomar, recoger      → ""coger llave"", ""coger todo""    ║
║  soltar, dejar, tirar       → ""soltar espada""                ║
╠══════════════════════════════════════════════════════════════╣
║ INTERACCIÓN                                                  ║
║  abrir, abre                → ""abrir cofre"", ""abrir puerta""  ║
║  cerrar, cierra             → ""cerrar puerta""                ║
║  usar, utilizar             → ""usar llave"", ""usar llave con  ║
║                               puerta""                        ║
║  meter, poner, guardar      → ""meter espada en cofre""        ║
║  sacar, quitar              → ""sacar llave del cofre""        ║
╠══════════════════════════════════════════════════════════════╣
║ CONVERSACIÓN                                                 ║
║  hablar, charlar            → ""hablar con mercader""          ║
║  decir, di                  → ""decir 1"", ""di 2""              ║
║  opcion                     → ""opcion 1""                     ║
╠══════════════════════════════════════════════════════════════╣
║ OTROS                                                        ║
║  misiones, quest            → ""misiones"" (ver progreso)      ║
║  ayuda, help                → ""ayuda"" (información básica)   ║
╚══════════════════════════════════════════════════════════════╝

💡 CONSEJO: Puedes escribir frases naturales como:
   ""quiero coger la espada del suelo""
   ""ve hacia el norte y abre la puerta""
   ""examina el libro que hay en la mesa""

🤖 ¿No te entiende? Activa la IA en Opciones para que interprete
   mejor tus comandos con lenguaje natural.
";
    }

    private GameObject? FindVisibleObjectInRoom(Room room, string namePart)
    {
        return _state.Objects
            .Where(o => o.Visible && room.ObjectIds.Contains(o.Id, StringComparer.OrdinalIgnoreCase))
            .FirstOrDefault(o => o.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Busca un objeto dentro de contenedores abiertos en la sala.
    /// Devuelve el objeto encontrado y el contenedor que lo contiene.
    /// </summary>
    private (GameObject? obj, GameObject? container) FindObjectInOpenContainers(Room room, string namePart)
    {
        // Buscar todos los contenedores visibles en la sala
        var containers = _state.Objects
            .Where(o => o.Visible && o.IsContainer && room.ObjectIds.Contains(o.Id, StringComparer.OrdinalIgnoreCase))
            .Where(o => o.IsOpen || o.ContentsVisible || !o.IsOpenable) // Contenedor accesible
            .ToList();

        foreach (var container in containers)
        {
            var objInContainer = container.ContainedObjectIds
                .Select(FindObjectById)
                .FirstOrDefault(o => o != null && o.Visible && o.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase));

            if (objInContainer != null)
                return (objInContainer, container);
        }

        return (null, null);
    }

    private bool HasDistinctRoomMusic(Room room)
    {
        var roomMusicId = room.MusicId;
        var roomMusicBase64 = room.MusicBase64;
        var worldMusicId = _state.WorldMusicId;
        var worldMusicBase64 = _world.Game?.WorldMusicBase64;

        var roomHasMusic = !string.IsNullOrWhiteSpace(roomMusicId) || !string.IsNullOrWhiteSpace(roomMusicBase64);
        if (!roomHasMusic)
            return false;

        var idsEqual = !string.IsNullOrWhiteSpace(roomMusicId) &&
                       !string.IsNullOrWhiteSpace(worldMusicId) &&
                       roomMusicId.Equals(worldMusicId, StringComparison.OrdinalIgnoreCase);

        var base64Equal = !string.IsNullOrWhiteSpace(roomMusicBase64) &&
                          !string.IsNullOrWhiteSpace(worldMusicBase64) &&
                          string.Equals(roomMusicBase64, worldMusicBase64, StringComparison.Ordinal);

        if (idsEqual || base64Equal)
            return false;

        return true;
    }


    private void OnRoomChanged()
    {
        var room = CurrentRoom;
        if (room != null)
        {
            string? roomMusicId = null;
            string? roomMusicBase64 = null;

            if (HasDistinctRoomMusic(room))
            {
                roomMusicId = room.MusicId;
                // Buscar la música en la lista de músicas del mundo
                var musicAsset = _world.Musics.FirstOrDefault(m => m.Id.Equals(roomMusicId, StringComparison.OrdinalIgnoreCase));
                roomMusicBase64 = musicAsset?.Base64;
            }

            _sound.PlayRoomMusic(
                roomMusicId,
                roomMusicBase64,
                null,
                null);

            // Voz: reproducimos la descripción de la sala actual.
            _ = _sound.PlayRoomDescriptionAsync(room.Id, room.Description);

            // Precargamos en segundo plano las voces de las salas cercanas
            // (hasta dos movimientos de distancia) y limpiamos las lejanas.
            _ = PreloadVoicesAroundCurrentRoomAsync();

            // Ejecutar scripts de la sala (Event_OnEnter) - solo si ya se inicializaron los scripts
            if (_initialScriptsReady)
            {
                _ = TriggerRoomScriptsAsync(room.Id, "Event_OnEnter");
            }

            RoomChanged?.Invoke(room);
        }
    }

    /// <summary>
    /// Ejecuta los scripts asociados a un evento de sala de forma asíncrona.
    /// </summary>
    private async Task TriggerRoomScriptsAsync(string roomId, string eventType)
    {
        if (_scriptEngine == null) return;

        try
        {
            await _scriptEngine.TriggerEventAsync("Room", roomId, eventType);
        }
        catch (Exception ex)
        {
            // Log error silently - don't crash the game due to script errors
            System.Diagnostics.Debug.WriteLine($"Script error: {ex.Message}");
        }
    }

    /// <summary>
    /// Ejecuta los scripts asociados a un evento de cualquier entidad.
    /// </summary>
    private async Task TriggerEntityScriptAsync(string ownerType, string ownerId, string eventType)
    {
        if (_scriptEngine == null) return;

        try
        {
            await _scriptEngine.TriggerEventAsync(ownerType, ownerId, eventType);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Script error: {ex.Message}");
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


    private static string GetOppositeDirectionCode(string normalizedDirection)
    {
        return normalizedDirection switch
        {
            "n" => "s",
            "s" => "n",
            "e" => "o",
            "o" => "e",
            "ne" => "so",
            "so" => "ne",
            "no" => "se",
            "se" => "no",
            "up" => "down",
            "down" => "up",
            _ => normalizedDirection
        };
    }
}