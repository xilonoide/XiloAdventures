using System.Collections.Generic;
using XiloAdventures.Engine.Models;
using EngineDoorService = XiloAdventures.Engine.DoorService;
using LogicDoorService = XiloAdventures.Engine.Logic.DoorService;
using Xunit;

namespace XiloAdventures.Tests;

public class DoorServiceTests
{
    [Fact]
    public void EngineDoorService_TryOpen_WithKeyFromAllowedSide_Succeeds()
    {
        var door = new Door
        {
            Id = "d1",
            RoomIdA = "a",
            RoomIdB = "b",
            HasLock = true,
            LockId = "lock1",
            IsOpen = false
        };

        var key = new KeyDefinition
        {
            ObjectId = "key-1",
            LockIds = new List<string> { "lock1" }
        };

        var service = new EngineDoorService(new List<Door> { door }, new List<KeyDefinition> { key });

        var result = service.TryOpenDoor("d1", "a", new[] { "KEY-1" });

        Assert.True(result.Success);
        Assert.False(result.MissingKey);
        Assert.True(door.IsOpen);
    }

    [Fact]
    public void EngineDoorService_TryOpen_WrongSide_Fails()
    {
        var door = new Door
        {
            Id = "d2",
            RoomIdA = "a",
            RoomIdB = "b",
            OpenFromSide = DoorOpenSide.FromAOnly
        };

        var service = new EngineDoorService(new List<Door> { door }, new List<KeyDefinition>());

        var result = service.TryOpenDoor("d2", "b", new[] { "anything" });

        Assert.False(result.Success);
        Assert.True(result.WrongSide);
        Assert.False(door.IsOpen);
    }

    [Fact]
    public void EngineDoorService_TryOpen_MissingKey_Fails()
    {
        var door = new Door
        {
            Id = "d3",
            RoomIdA = "a",
            RoomIdB = "b",
            HasLock = true,
            LockId = "lock-3"
        };

        var key = new KeyDefinition
        {
            ObjectId = "key-3",
            LockIds = new List<string> { "lock-3" }
        };

        var service = new EngineDoorService(new List<Door> { door }, new List<KeyDefinition> { key });

        var result = service.TryOpenDoor("d3", "a", new string[0]);

        Assert.False(result.Success);
        Assert.True(result.MissingKey);
        Assert.False(door.IsOpen);
    }

    [Fact]
    public void EngineDoorService_TryOpen_AlreadyOpen_ReportsDesiredState()
    {
        var door = new Door
        {
            Id = "d4",
            RoomIdA = "a",
            RoomIdB = "b",
            IsOpen = true
        };

        var service = new EngineDoorService(new List<Door> { door }, new List<KeyDefinition>());

        var result = service.TryOpenDoor("d4", "a", new[] { "any" });

        Assert.False(result.Success);
        Assert.True(result.AlreadyInDesiredState);
    }

    [Fact]
    public void EngineDoorService_TryClose_WhenClosed_ReportsDesiredState()
    {
        var door = new Door
        {
            Id = "d5",
            RoomIdA = "a",
            RoomIdB = "b",
            IsOpen = false
        };

        var service = new EngineDoorService(new List<Door> { door }, new List<KeyDefinition>());

        var result = service.TryCloseDoor("d5", "a", new[] { "any" });

        Assert.False(result.Success);
        Assert.True(result.AlreadyInDesiredState);
    }

    [Fact]
    public void EngineDoorService_TryOpen_NotFound_ReturnsNotFound()
    {
        var service = new EngineDoorService(new List<Door>(), new List<KeyDefinition>());

        var result = service.TryOpenDoor("missing", "a", new[] { "any" });

        Assert.False(result.Success);
        Assert.True(result.NotFoundDoor);
    }

    [Fact]
    public void LogicDoorService_HasRequiredKey_IsCaseSensitive()
    {
        var door = new Door
        {
            Id = "logic-1",
            RoomIdA = "a",
            RoomIdB = "b",
            HasLock = true,
            LockId = "L1"
        };

        var key = new KeyDefinition
        {
            ObjectId = "key-lower",
            LockIds = new List<string> { "L1" }
        };

        var service = new LogicDoorService(new List<Door> { door }, new List<KeyDefinition> { key });

        var hasKey = service.HasRequiredKey(door, new[] { "KEY-LOWER" });

        Assert.False(hasKey);
        Assert.True(service.HasRequiredKey(door, new[] { "key-lower" }));
    }
}
