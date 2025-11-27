using System;
using System.Collections.Generic;
using System.IO;
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

    public static WorldModel LoadWorldModel(string path)
    {
        var json = CryptoUtil.DecryptFromFile(path);
        var world = JsonSerializer.Deserialize<WorldModel>(json, Options) ?? new WorldModel();

        // Asegurar listas inicializadas
        world.Rooms ??= new List<Room>();
        world.Objects ??= new List<GameObject>();
        world.Npcs ??= new List<Npc>();
        world.Quests ??= new List<QuestDefinition>();
        world.UseRules ??= new List<UseRule>();
        world.TradeRules ??= new List<TradeRule>();
        world.Events ??= new List<EventRule>();
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
            Events = CloneList(world.Events)
        };

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
        var json = JsonSerializer.Serialize(world, Options);
        CryptoUtil.EncryptToFile(path, json, "xaw");
    }

    private static List<T> CloneList<T>(List<T> source)
    {
        // Serialización simple para clonar; suficiente para este proyecto.
        var json = JsonSerializer.Serialize(source, Options);
        return JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>();
    }
}
