using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Text.Json;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Engine;

public static class WorldLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static bool TryParseWorldFromText(string text, out WorldModel? world)
    {
        world = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string json;
        if (!TryDecodeZippedJson(text, out json))
        {
            // Formato antiguo: el contenido desencriptado es directamente JSON.
            json = text;
        }

        try
        {
            world = JsonSerializer.Deserialize<WorldModel>(json, Options);
            return world != null;
        }
        catch
        {
            return false;
        }
    }

    public static WorldModel LoadWorldModel(string path, string? encryptionKey = null, Func<string?>? promptForKey = null)
    {
        // Leer archivo directamente sin cifrado
        var rawText = File.ReadAllText(path, Encoding.UTF8);
        if (TryParseWorldFromText(rawText, out var parsedWorld))
            return NormalizeWorld(parsedWorld);

        throw new InvalidDataException("No se pudo leer el mundo. El archivo puede estar corrupto.");
    }

    private static WorldModel NormalizeWorld(WorldModel? world)
    {
        world ??= new WorldModel();

        // Asegurar listas inicializadas
        world.Rooms ??= new List<Room>();
        world.Objects ??= new List<GameObject>();
        world.Npcs ??= new List<Npc>();
        world.Quests ??= new List<QuestDefinition>();
        world.UseRules ??= new List<UseRule>();
        world.TradeRules ??= new List<TradeRule>();
        world.Events ??= new List<EventRule>();
        world.Doors ??= new List<Door>();
        world.Keys ??= new List<KeyDefinition>();
        world.RoomPositions ??= new Dictionary<string, MapPosition>();

        return world;
    }


    public static GameState CreateInitialState(WorldModel world)
    {
        var state = new GameState
        {
            WorldId = world.Game.Id,
            WorldMusicId = world.Game.WorldMusicId,
            CurrentRoomId = world.Game.StartRoomId,
            Rooms = CloneList(world.Rooms),
            Objects = CloneList(world.Objects),
            Npcs = CloneList(world.Npcs),
            UseRules = CloneList(world.UseRules),
            TradeRules = CloneList(world.TradeRules),
            Events = CloneList(world.Events),
            Doors = CloneList(world.Doors),
            Keys = CloneList(world.Keys)
        };


        // Inicializar hora y clima de la partida según la configuración del juego.
        var startHour = world.Game.StartHour;
        if (startHour < 0) startHour = 0;
        if (startHour > 23) startHour = 23;
        var today = DateTime.Today;
        state.GameTime = new DateTime(today.Year, today.Month, today.Day, startHour, 0, 0);
        state.Weather = world.Game.StartWeather;


        state.Quests = new Dictionary<string, QuestState>();
        foreach (var q in world.Quests)
        {
            state.Quests[q.Id] = new QuestState
            {
                QuestId = q.Id,
                Status = QuestStatus.NotStarted,
                CurrentObjectiveIndex = 0
            };
        }

        // Copiamos la clave de cifrado del mundo al estado para que el motor
        // pueda usarla al guardar la partida del jugador.
        state.WorldEncryptionKey = world.Game.EncryptionKey;

        RebuildRoomIndexes(state);
        return state;
    }

    public static void RebuildRoomIndexes(GameState state)
    {
        var roomsById = state.Rooms.ToDictionary(r => r.Id, r => r, StringComparer.OrdinalIgnoreCase);

        foreach (var room in state.Rooms)
        {
            room.ObjectIds.Clear();
            room.NpcIds.Clear();
        }

        foreach (var obj in state.Objects)
        {
            if (!string.IsNullOrWhiteSpace(obj.RoomId) &&
                roomsById.TryGetValue(obj.RoomId, out var room))
            {
                room.ObjectIds.Add(obj.Id);
            }
        }

        foreach (var npc in state.Npcs)
        {
            if (!string.IsNullOrWhiteSpace(npc.RoomId) &&
                roomsById.TryGetValue(npc.RoomId, out var room))
            {
                room.NpcIds.Add(npc.Id);
            }
        }
    }

    public static void SaveWorldModel(WorldModel world, string path)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));

        // Normalizamos campos dependientes de Ids para que, si el usuario
        // ha dejado en blanco los textbox de Id en el editor, se limpien
        // también los contenidos en Base64 antes de serializar el mundo.
        if (world.Game is not null)
        {
            if (string.IsNullOrWhiteSpace(world.Game.WorldMusicId))
            {
                world.Game.WorldMusicId = null;
                world.Game.WorldMusicBase64 = null;
            }
        }

        if (world.Rooms != null)
        {
            foreach (var room in world.Rooms)
            {
                if (room is null) continue;

                // Si el usuario ha borrado el MusicId de la sala en el editor,
                // descartamos también la música embebida en Base64.
                if (string.IsNullOrWhiteSpace(room.MusicId))
                {
                    room.MusicId = null;
                    room.MusicBase64 = null;
                }

                // Si el usuario ha borrado el ImageId de la sala en el editor,
                // descartamos también la imagen embebida en Base64.
                if (string.IsNullOrWhiteSpace(room.ImageId))
                {
                    room.ImageId = null;
                    room.ImageBase64 = null;
                }
            }
        }

        var json = JsonSerializer.Serialize(world, Options);

        // Comprimir el JSON en un ZIP (entrada world.json) y codificarlo en Base64
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry("world.json", CompressionLevel.SmallestSize);
            using var entryStream = entry.Open();
            entryStream.Write(jsonBytes, 0, jsonBytes.Length);
        }

        var compressedBytes = ms.ToArray();
        var base64 = Convert.ToBase64String(compressedBytes);

        // Guardar directamente sin cifrado
        File.WriteAllText(path, base64, Encoding.UTF8);
    }



    /// <summary>
    /// Intenta interpretar el texto desencriptado como un ZIP codificado en Base64
    /// que contiene un único JSON (world.json). Devuelve true si se ha podido
    /// obtener el JSON correctamente.
    /// </summary>
    private static bool TryDecodeZippedJson(string text, out string json)
    {
        json = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            var compressedBytes = Convert.FromBase64String(text);
            using var ms = new MemoryStream(compressedBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            // Intentamos obtener la entrada "world.json". Si no existe,
            // usamos la primera entrada disponible.
            var entry = zip.GetEntry("world.json") ?? zip.Entries.FirstOrDefault();
            if (entry == null)
                return false;

            using var entryStream = entry.Open();
            using var sr = new StreamReader(entryStream, Encoding.UTF8);
            json = sr.ReadToEnd();
            return !string.IsNullOrWhiteSpace(json);
        }
        catch
        {
            // Si falla cualquier cosa (no es Base64, no es ZIP, etc.), asumimos
            // que no está comprimido y devolvemos false.
            json = string.Empty;
            return false;
        }
    }

    private static List<T> CloneList<T>(List<T> source)
    {
        // Serialización simple para clonar; suficiente para este proyecto.
        var json = JsonSerializer.Serialize(source, Options);
        return JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>();
    }
}
