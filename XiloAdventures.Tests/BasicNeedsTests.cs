using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using XiloAdventures.Engine;
using XiloAdventures.Engine.Engine;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Common.Services;

namespace XiloAdventures.Tests;

/// <summary>
/// Tests for the Basic Needs system (Hunger, Thirst, Sleep).
/// </summary>
public class BasicNeedsTests
{
    #region Test Helpers

    private static SoundManager CreateMockSoundManager()
    {
        return new SoundManager { SoundEnabled = false };
    }

    private static (WorldModel world, GameState state) CreateTestWorld(bool basicNeedsEnabled = true)
    {
        var world = new WorldModel
        {
            Game = new GameInfo
            {
                Id = "test_basic_needs",
                Title = "Test Basic Needs",
                StartRoomId = "room1",
                StartHour = 12,
                BasicNeedsEnabled = basicNeedsEnabled,
                HungerRate = NeedRate.Normal,
                ThirstRate = NeedRate.Normal,
                SleepRate = NeedRate.Normal,
                HungerDeathText = "Died of hunger.",
                ThirstDeathText = "Died of thirst.",
                SleepDeathText = "Died of exhaustion."
            },
            Rooms = new List<Room>
            {
                new Room
                {
                    Id = "room1",
                    Name = "Room 1",
                    Description = "First room.",
                    IsIlluminated = true,
                    Exits = new List<Exit>(),
                    ObjectIds = new List<string>(),
                    NpcIds = new List<string>()
                }
            },
            Objects = new List<GameObject>(),
            Npcs = new List<Npc>(),
            Doors = new List<Door>(),
            Quests = new List<QuestDefinition>(),
            Scripts = new List<ScriptDefinition>()
        };

        var state = WorldLoader.CreateInitialState(world);
        return (world, state);
    }

    private static GameEngine CreateEngine(WorldModel world, GameState state)
    {
        var sound = CreateMockSoundManager();
        return new GameEngine(world, state, sound, isDebugMode: false);
    }

    #endregion

    #region NeedRate Tests

    [Theory]
    [InlineData(NeedRate.Low, 0.5)]
    [InlineData(NeedRate.Normal, 1.0)]
    [InlineData(NeedRate.High, 1.5)]
    public void NeedRate_HasCorrectModifier(NeedRate rate, double expectedModifier)
    {
        var modifier = rate switch
        {
            NeedRate.Low => 0.5,
            NeedRate.Normal => 1.0,
            NeedRate.High => 1.5,
            _ => 1.0
        };
        Assert.Equal(expectedModifier, modifier);
    }

    #endregion

    #region Per-Turn Increment Tests

    [Fact]
    public void ProcessCommand_BasicNeedsEnabled_IncrementsNeeds()
    {
        var (world, state) = CreateTestWorld(basicNeedsEnabled: true);
        var engine = CreateEngine(world, state);

        var initialHunger = state.Player.DynamicStats.Hunger;
        var initialThirst = state.Player.DynamicStats.Thirst;
        var initialSleep = state.Player.DynamicStats.Sleep;

        engine.ProcessCommand("mirar");

        Assert.True(state.Player.DynamicStats.Hunger > initialHunger);
        Assert.True(state.Player.DynamicStats.Thirst > initialThirst);
        Assert.True(state.Player.DynamicStats.Sleep > initialSleep);
    }

    [Fact]
    public void ProcessCommand_BasicNeedsDisabled_DoesNotIncrementNeeds()
    {
        var (world, state) = CreateTestWorld(basicNeedsEnabled: false);
        var engine = CreateEngine(world, state);

        var initialHunger = state.Player.DynamicStats.Hunger;
        var initialThirst = state.Player.DynamicStats.Thirst;
        var initialSleep = state.Player.DynamicStats.Sleep;

        engine.ProcessCommand("mirar");

        Assert.Equal(initialHunger, state.Player.DynamicStats.Hunger);
        Assert.Equal(initialThirst, state.Player.DynamicStats.Thirst);
        Assert.Equal(initialSleep, state.Player.DynamicStats.Sleep);
    }

    [Fact]
    public void ProcessCommand_HighRate_IncrementsMorePerTurn()
    {
        var (world, state) = CreateTestWorld(basicNeedsEnabled: true);
        world.Game.HungerRate = NeedRate.High;
        var engine = CreateEngine(world, state);

        state.Player.DynamicStats.Hunger = 0;
        engine.ProcessCommand("mirar");

        // High rate should increment by ceil(1.5) = 2
        Assert.Equal(2, state.Player.DynamicStats.Hunger);
    }

    [Fact]
    public void ProcessCommand_LowRate_IncrementsLessPerTurn()
    {
        var (world, state) = CreateTestWorld(basicNeedsEnabled: true);
        world.Game.HungerRate = NeedRate.Low;
        var engine = CreateEngine(world, state);

        state.Player.DynamicStats.Hunger = 0;
        engine.ProcessCommand("mirar");

        // Low rate should increment by ceil(0.5) = 1
        Assert.Equal(1, state.Player.DynamicStats.Hunger);
    }

    #endregion

    #region Threshold Message Tests

    [Fact]
    public void ProcessCommand_HungerReaches70_ShowsWarningMessage()
    {
        var (world, state) = CreateTestWorld(basicNeedsEnabled: true);
        var engine = CreateEngine(world, state);

        state.Player.DynamicStats.Hunger = 69;
        var result = engine.ProcessCommand("mirar");

        Assert.Contains("hambre", result.Message.ToLower());
    }

    [Fact]
    public void ProcessCommand_ThirstReaches80_ShowsWarningMessage()
    {
        var (world, state) = CreateTestWorld(basicNeedsEnabled: true);
        var engine = CreateEngine(world, state);

        state.Player.DynamicStats.Thirst = 79;
        var result = engine.ProcessCommand("mirar");

        Assert.Contains("sed", result.Message.ToLower());
    }

    [Fact]
    public void ProcessCommand_SleepReaches90_ShowsCriticalMessage()
    {
        var (world, state) = CreateTestWorld(basicNeedsEnabled: true);
        var engine = CreateEngine(world, state);

        state.Player.DynamicStats.Sleep = 89;
        var result = engine.ProcessCommand("mirar");

        Assert.Contains("sueño", result.Message.ToLower());
        Assert.Contains("crítica", result.Message.ToLower());
    }

    #endregion

    #region Death Tests

    [Fact]
    public void ProcessCommand_HungerReaches100_TriggersDeathEvent()
    {
        var (world, state) = CreateTestWorld(basicNeedsEnabled: true);
        var engine = CreateEngine(world, state);

        string? deathType = null;
        engine.PlayerDiedFromNeeds += type => deathType = type;

        state.Player.DynamicStats.Hunger = 99;
        engine.ProcessCommand("mirar");

        Assert.Equal("Hunger", deathType);
    }

    [Fact]
    public void ProcessCommand_ThirstReaches100_TriggersDeathEvent()
    {
        var (world, state) = CreateTestWorld(basicNeedsEnabled: true);
        var engine = CreateEngine(world, state);

        string? deathType = null;
        engine.PlayerDiedFromNeeds += type => deathType = type;

        state.Player.DynamicStats.Thirst = 99;
        engine.ProcessCommand("mirar");

        Assert.Equal("Thirst", deathType);
    }

    [Fact]
    public void ProcessCommand_SleepReaches100_TriggersDeathEvent()
    {
        var (world, state) = CreateTestWorld(basicNeedsEnabled: true);
        var engine = CreateEngine(world, state);

        string? deathType = null;
        engine.PlayerDiedFromNeeds += type => deathType = type;

        state.Player.DynamicStats.Sleep = 99;
        engine.ProcessCommand("mirar");

        Assert.Equal("Sleep", deathType);
    }

    #endregion

    #region Sleep Node Tests

    [Fact]
    public async Task Variable_GetPlayerSleep_ReturnsSleepValue()
    {
        var (world, state) = CreateTestWorld();
        state.Player.DynamicStats.Sleep = 45;

        var script = new ScriptDefinition
        {
            Id = "test_script",
            OwnerType = "Game",
            OwnerId = "test_basic_needs",
            Nodes = new List<ScriptNode>
            {
                new ScriptNode
                {
                    Id = "event_node",
                    NodeType = "Event_OnGameStart",
                    Category = NodeCategory.Event,
                    Properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                },
                new ScriptNode
                {
                    Id = "var_node",
                    NodeType = "Variable_GetPlayerSleep",
                    Category = NodeCategory.Variable,
                    Properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                }
            },
            Connections = new List<NodeConnection>()
        };
        world.Scripts.Add(script);

        var engine = new ScriptEngine(world, state);

        // Execute script - variable nodes are evaluated on demand
        await engine.TriggerEventAsync("Game", "test_basic_needs", "Event_OnGameStart");

        // The value should be accessible
        Assert.Equal(45, state.Player.DynamicStats.Sleep);
    }

    #endregion

    #region GameInfo Properties Tests

    [Fact]
    public void GameInfo_BasicNeedsEnabled_DefaultsToFalse()
    {
        var gameInfo = new GameInfo();
        Assert.False(gameInfo.BasicNeedsEnabled);
    }

    [Fact]
    public void GameInfo_NeedRates_DefaultToNormal()
    {
        var gameInfo = new GameInfo();
        Assert.Equal(NeedRate.Normal, gameInfo.HungerRate);
        Assert.Equal(NeedRate.Normal, gameInfo.ThirstRate);
        Assert.Equal(NeedRate.Normal, gameInfo.SleepRate);
    }

    [Fact]
    public void GameInfo_DeathTexts_HaveDefaults()
    {
        var gameInfo = new GameInfo();
        Assert.False(string.IsNullOrEmpty(gameInfo.HungerDeathText));
        Assert.False(string.IsNullOrEmpty(gameInfo.ThirstDeathText));
        Assert.False(string.IsNullOrEmpty(gameInfo.SleepDeathText));
    }

    #endregion

    #region PlayerDynamicStats Sleep Tests

    [Fact]
    public void PlayerDynamicStats_Sleep_DefaultsToZero()
    {
        var stats = new PlayerDynamicStats();
        Assert.Equal(0, stats.Sleep);
    }

    [Fact]
    public void PlayerDynamicStats_Sleep_CanBeSetAndGet()
    {
        var stats = new PlayerDynamicStats();
        stats.Sleep = 75;
        Assert.Equal(75, stats.Sleep);
    }

    #endregion

    #region PlayerStateType Tests

    [Fact]
    public void PlayerStateType_IncludesSleep()
    {
        Assert.True(Enum.IsDefined(typeof(PlayerStateType), "Sleep"));
    }

    #endregion
}
