using System.Collections.Generic;
using XiloAdventures.Engine.Models;
using DoorService = XiloAdventures.Engine.DoorService;
using Xunit;

namespace XiloAdventures.Tests;

public class DoorServiceTests
{
    [Fact]
    public void DoorService_TryOpen_WithKeyFromAllowedSide_Succeeds()
    {
        var keyObject = new GameObject
        {
            Id = "key-1",
            Name = "Llave dorada",
            Type = ObjectType.Llave
        };

        var door = new Door
        {
            Id = "d1",
            RoomIdA = "a",
            RoomIdB = "b",
            IsLocked = true,
            KeyObjectId = "key-1",
            IsOpen = false
        };

        var service = new DoorService(new List<Door> { door }, new List<GameObject> { keyObject });

        var result = service.TryOpenDoor("d1", "a", new[] { "key-1" });

        Assert.True(result.Success);
        Assert.False(result.MissingKey);
        Assert.True(door.IsOpen);
    }

    [Fact]
    public void DoorService_TryOpen_WrongSide_Fails()
    {
        var door = new Door
        {
            Id = "d2",
            RoomIdA = "a",
            RoomIdB = "b",
            OpenFromSide = DoorOpenSide.FromAOnly
        };

        var service = new DoorService(new List<Door> { door }, new List<GameObject>());

        var result = service.TryOpenDoor("d2", "b", new[] { "anything" });

        Assert.False(result.Success);
        Assert.True(result.WrongSide);
        Assert.False(door.IsOpen);
    }

    [Fact]
    public void DoorService_TryOpen_MissingKey_Fails()
    {
        var keyObject = new GameObject
        {
            Id = "key-3",
            Name = "Llave plateada",
            Type = ObjectType.Llave
        };

        var door = new Door
        {
            Id = "d3",
            RoomIdA = "a",
            RoomIdB = "b",
            IsLocked = true,
            KeyObjectId = "key-3"
        };

        var service = new DoorService(new List<Door> { door }, new List<GameObject> { keyObject });

        var result = service.TryOpenDoor("d3", "a", new string[0]);

        Assert.False(result.Success);
        Assert.True(result.MissingKey);
        Assert.False(door.IsOpen);
    }

    [Fact]
    public void DoorService_TryOpen_AlreadyOpen_ReportsDesiredState()
    {
        var door = new Door
        {
            Id = "d4",
            RoomIdA = "a",
            RoomIdB = "b",
            IsOpen = true
        };

        var service = new DoorService(new List<Door> { door }, new List<GameObject>());

        var result = service.TryOpenDoor("d4", "a", new[] { "any" });

        Assert.False(result.Success);
        Assert.True(result.AlreadyInDesiredState);
    }

    [Fact]
    public void DoorService_TryClose_WhenClosed_ReportsDesiredState()
    {
        var door = new Door
        {
            Id = "d5",
            RoomIdA = "a",
            RoomIdB = "b",
            IsOpen = false
        };

        var service = new DoorService(new List<Door> { door }, new List<GameObject>());

        var result = service.TryCloseDoor("d5", "a", new[] { "any" });

        Assert.False(result.Success);
        Assert.True(result.AlreadyInDesiredState);
    }

    [Fact]
    public void DoorService_TryOpen_NotFound_ReturnsNotFound()
    {
        var service = new DoorService(new List<Door>(), new List<GameObject>());

        var result = service.TryOpenDoor("missing", "a", new[] { "any" });

        Assert.False(result.Success);
        Assert.True(result.NotFoundDoor);
    }
}
