using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Engine;

public class SaveData
{
    public string WorldId { get; set; } = string.Empty;
    public string CurrentRoomId { get; set; } = string.Empty;
    public PlayerStats Player { get; set; } = new();

    // Inventario: solo guardamos los Ids de objetos que lleva el jugador.
    public List<string> InventoryObjectIds { get; set; } = new();

    // Estado del mundo
    public List<Room>? Rooms { get; set; }
    public List<GameObject>? Objects { get; set; }
    public List<Npc>? Npcs { get; set; }

    public Dictionary<string, QuestState>? Quests { get; set; }
    public List<UseRule>? UseRules { get; set; }
    public List<TradeRule>? TradeRules { get; set; }
    public List<EventRule>? Events { get; set; }

    public Dictionary<string, bool>? Flags { get; set; }

    public int TurnCounter { get; set; }
    public string TimeOfDay { get; set; } = "día";
    public string Weather { get; set; } = "despejado";

    public string? WorldMusicId { get; set; }
}

public static class SaveManager
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void SaveToPath(GameState state, string path)
    {
        var data = new SaveData
        {
            WorldId = state.WorldId,
            CurrentRoomId = state.CurrentRoomId,
            Player = state.Player,
            InventoryObjectIds = new List<string>(state.InventoryObjectIds),

            Rooms = new List<Room>(state.Rooms),
            Objects = new List<GameObject>(state.Objects),
            Npcs = new List<Npc>(state.Npcs),

            Quests = new Dictionary<string, QuestState>(state.Quests),
            UseRules = new List<UseRule>(state.UseRules),
            TradeRules = new List<TradeRule>(state.TradeRules),
            Events = new List<EventRule>(state.Events),

            TurnCounter = state.TurnCounter,
            TimeOfDay = state.TimeOfDay,
            Weather = state.Weather,

            Flags = new Dictionary<string, bool>(state.Flags),
            WorldMusicId = state.WorldMusicId
        };

        var json = JsonSerializer.Serialize(data, Options);
        CryptoUtil.EncryptToFile(path, json, "xas");
    }

    public static GameState LoadFromPath(string path, WorldModel world)
    {
        var json = CryptoUtil.DecryptFromFile(path);
        var data = JsonSerializer.Deserialize<SaveData>(json, Options) ?? new SaveData();

        // Compatibilidad: si la partida es antigua y no tiene algunos campos, usamos los del mundo original.
        var rooms = data.Rooms ?? world.Rooms;
        var objects = data.Objects ?? world.Objects;
        var npcs = data.Npcs ?? world.Npcs;
        var quests = data.Quests ?? new Dictionary<string, QuestState>(StringComparer.OrdinalIgnoreCase);

        var state = new GameState
        {
            WorldId = string.IsNullOrEmpty(data.WorldId) ? world.Game.Id : data.WorldId,
            WorldMusicId = data.WorldMusicId ?? world.Game.WorldMusicId,
            CurrentRoomId = string.IsNullOrEmpty(data.CurrentRoomId)
                ? world.Game.StartRoomId
                : data.CurrentRoomId,

            Player = data.Player ?? new PlayerStats(),

            Rooms = rooms,
            Objects = objects,
            Npcs = npcs,

            Quests = quests,
            UseRules = data.UseRules ?? world.UseRules,
            TradeRules = data.TradeRules ?? world.TradeRules,
            Events = data.Events ?? world.Events,

            InventoryObjectIds = data.InventoryObjectIds ?? new List<string>(),
            TurnCounter = data.TurnCounter,
            TimeOfDay = string.IsNullOrEmpty(data.TimeOfDay) ? "día" : data.TimeOfDay,
            Weather = string.IsNullOrEmpty(data.Weather) ? "despejado" : data.Weather,
            Flags = data.Flags ?? new Dictionary<string, bool>()
        };

        // Asegurar que las listas de objetos/NPCs por sala se recalculan
        WorldLoader.RebuildRoomIndexes(state);

        return state;
    }

    public static void AutoSave(GameState state, string savesFolder)
    {
        Directory.CreateDirectory(savesFolder);
        var fileName = $"autosave_{state.WorldId}.xas";
        var path = Path.Combine(savesFolder, fileName);
        SaveToPath(state, path);
    }
}
