using System.Collections.Generic;
using System.Linq;
using Xunit;
using XiloAdventures.Engine;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Tests;

/// <summary>
/// Unit tests for the GameEngine class.
/// Tests core game mechanics including movement, inventory, room description, and commands.
/// </summary>
public class GameEngineTests
{
    /// <summary>
    /// Creates a minimal test world with two connected rooms.
    /// </summary>
    private static (WorldModel world, GameState state) CreateTestWorld()
    {
        var world = new WorldModel
        {
            Game = new GameInfo
            {
                Id = "test_world",
                Title = "Test World",
                StartRoomId = "room1",
                StartHour = 12  // Daytime for lighting
            },
            Rooms = new List<Room>
            {
                new Room
                {
                    Id = "room1",
                    Name = "Starting Room",
                    Description = "You are in the starting room.",
                    IsIlluminated = true,
                    IsInterior = false,
                    Exits = new List<Exit>
                    {
                        new Exit { Direction = "norte", TargetRoomId = "room2" }
                    },
                    ObjectIds = new List<string> { "obj_sword" },
                    NpcIds = new List<string>()
                },
                new Room
                {
                    Id = "room2",
                    Name = "North Room",
                    Description = "You are in the north room.",
                    IsIlluminated = true,
                    IsInterior = false,
                    Exits = new List<Exit>
                    {
                        new Exit { Direction = "sur", TargetRoomId = "room1" }
                    },
                    ObjectIds = new List<string>(),
                    NpcIds = new List<string>()
                }
            },
            Objects = new List<GameObject>
            {
                new GameObject
                {
                    Id = "obj_sword",
                    Name = "espada oxidada",
                    Description = "Una vieja espada oxidada.",
                    CanTake = true,
                    Visible = true,
                    RoomId = "room1"
                },
                new GameObject
                {
                    Id = "obj_rock",
                    Name = "roca pesada",
                    Description = "Una roca muy pesada.",
                    CanTake = false,
                    Visible = true,
                    RoomId = "room1"
                }
            },
            Npcs = new List<Npc>(),
            Doors = new List<Door>(),
            Quests = new List<QuestDefinition>()
        };

        var state = WorldLoader.CreateInitialState(world);
        return (world, state);
    }

    /// <summary>
    /// Creates a mock SoundManager that does nothing (for testing without audio).
    /// </summary>
    private static SoundManager CreateMockSoundManager()
    {
        var sound = new SoundManager { SoundEnabled = false };
        return sound;
    }

    #region Movement Tests

    [Fact]
    public void ProcessCommand_GoNorth_MovesToCorrectRoom()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        Assert.Equal("room1", engine.State.CurrentRoomId);

        // Act
        var result = engine.ProcessCommand("ir norte");

        // Assert
        Assert.Equal("room2", engine.State.CurrentRoomId);
        Assert.Contains("north room", result.Message.ToLowerInvariant());
    }

    [Fact]
    public void ProcessCommand_GoInvalidDirection_ReturnsError()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.ProcessCommand("ir oeste");

        // Assert
        Assert.Equal("room1", engine.State.CurrentRoomId);  // Didn't move
        Assert.Contains("no puedes ir", result.Message.ToLowerInvariant());
    }

    [Fact]
    public void ProcessCommand_GoWithoutDirection_AsksForDirection()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.ProcessCommand("ir");

        // Assert
        Assert.Contains("dónde", result.Message.ToLowerInvariant());
    }

    #endregion

    #region Look/Describe Tests

    [Fact]
    public void ProcessCommand_Look_DescribesCurrentRoom()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.ProcessCommand("mirar");

        // Assert
        Assert.Contains("starting room", result.Message.ToLowerInvariant());
        Assert.Contains("espada", result.Message.ToLowerInvariant());  // Should show objects
        Assert.Contains("norte", result.Message.ToLowerInvariant());   // Should show exits
    }

    [Fact]
    public void DescribeCurrentRoom_WithObjects_ListsVisibleObjects()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var description = engine.DescribeCurrentRoom();

        // Assert
        Assert.Contains("espada oxidada", description.ToLowerInvariant());
        Assert.Contains("ves aquí", description.ToLowerInvariant());
    }

    #endregion

    #region Inventory Tests

    [Fact]
    public void ProcessCommand_Take_AddsObjectToInventory()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        Assert.Empty(engine.State.InventoryObjectIds);

        // Act
        var result = engine.ProcessCommand("coger espada");

        // Assert
        Assert.Contains("obj_sword", engine.State.InventoryObjectIds);
        Assert.Contains("coges", result.Message.ToLowerInvariant());
    }

    [Fact]
    public void ProcessCommand_TakeNonTakeable_ReturnsError()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        // Add rock to room1
        state.Rooms.First(r => r.Id == "room1").ObjectIds.Add("obj_rock");
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.ProcessCommand("coger roca");

        // Assert
        Assert.DoesNotContain("obj_rock", engine.State.InventoryObjectIds);
        Assert.Contains("no puedes coger", result.Message.ToLowerInvariant());
    }

    [Fact]
    public void ProcessCommand_TakeNonExistent_ReturnsNotFound()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.ProcessCommand("coger dragón");

        // Assert
        Assert.Empty(engine.State.InventoryObjectIds);
        Assert.Contains("no ves eso", result.Message.ToLowerInvariant());
    }

    [Fact]
    public void ProcessCommand_Drop_RemovesFromInventory()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        state.InventoryObjectIds.Add("obj_sword");
        state.Rooms.First(r => r.Id == "room1").ObjectIds.Remove("obj_sword");
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.ProcessCommand("soltar espada");

        // Assert
        Assert.DoesNotContain("obj_sword", engine.State.InventoryObjectIds);
        Assert.Contains("sueltas", result.Message.ToLowerInvariant());
    }

    [Fact]
    public void ProcessCommand_DropNotInInventory_ReturnsError()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.ProcessCommand("soltar espada");

        // Assert
        Assert.Contains("no llevas eso", result.Message.ToLowerInvariant());
    }

    [Fact]
    public void DescribeInventory_WhenEmpty_ReturnsEmptyMessage()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.DescribeInventory();

        // Assert
        Assert.Contains("no llevas nada", result.ToLowerInvariant());
    }

    [Fact]
    public void DescribeInventory_WithItems_ListsItems()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        state.InventoryObjectIds.Add("obj_sword");
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.DescribeInventory();

        // Assert
        Assert.Contains("espada oxidada", result.ToLowerInvariant());
    }

    #endregion

    #region Help and Other Commands Tests

    [Fact]
    public void ProcessCommand_Help_ReturnsHelpText()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.ProcessCommand("ayuda");

        // Assert
        Assert.Contains("comandos básicos", result.Message.ToLowerInvariant());
        Assert.Contains("mirar", result.Message.ToLowerInvariant());
        Assert.Contains("coger", result.Message.ToLowerInvariant());
    }

    [Fact]
    public void ProcessCommand_Inventory_DescribesInventory()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.ProcessCommand("inventario");

        // Assert
        Assert.Contains("no llevas nada", result.Message.ToLowerInvariant());
    }

    [Fact]
    public void ProcessCommand_UnknownCommand_ReturnsNotUnderstood()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act
        var result = engine.ProcessCommand("xyz123");

        // Assert
        Assert.Contains("no entiendo", result.Message.ToLowerInvariant());
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void ProcessCommand_IncrementsTurnCounter()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);
        var initialTurn = engine.State.TurnCounter;

        // Act
        engine.ProcessCommand("mirar");

        // Assert
        Assert.Equal(initialTurn + 1, engine.State.TurnCounter);
    }

    [Fact]
    public void CurrentRoom_ReturnsCorrectRoom()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        // Act & Assert
        Assert.NotNull(engine.CurrentRoom);
        Assert.Equal("room1", engine.CurrentRoom!.Id);
        Assert.Equal("Starting Room", engine.CurrentRoom.Name);
    }

    [Fact]
    public void LoadState_UpdatesEngineState()
    {
        // Arrange
        var (world, state) = CreateTestWorld();
        var sound = CreateMockSoundManager();
        var engine = new GameEngine(world, state, sound);

        var newState = WorldLoader.CreateInitialState(world);
        newState.CurrentRoomId = "room2";

        // Act
        engine.LoadState(newState);

        // Assert
        Assert.Equal("room2", engine.State.CurrentRoomId);
        Assert.Equal("room2", engine.CurrentRoom?.Id);
    }

    #endregion
}
