using System.Collections.Generic;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Windows;
using Xunit;

namespace XiloAdventures.Tests;

public class WorldEditorHelpersTests
{
    [Fact]
    public void DeleteDoor_RemovesDoorAndKeyDefinitions_ButKeepsObjects()
    {
        var door = new Door { Id = "door-1", LockId = "lock-1", HasLock = true };
        var room = new Room
        {
            Id = "room-1",
            Exits = new List<Exit>
            {
                new Exit { DoorId = door.Id, TargetRoomId = "room-2", Direction = "norte" }
            }
        };

        var keyObject = new GameObject { Id = "obj-key", Name = "Llave" };
        var keyDef = new KeyDefinition { ObjectId = keyObject.Id, LockIds = new List<string> { "lock-1" } };

        var world = new WorldModel
        {
            Doors = new List<Door> { door },
            Keys = new List<KeyDefinition> { keyDef },
            Objects = new List<GameObject> { keyObject },
            Rooms = new List<Room> { room }
        };

        var removed = WorldEditorHelpers.DeleteDoor(world, door);

        Assert.True(removed);
        Assert.Empty(world.Doors);
        Assert.Empty(world.Keys);
        Assert.Single(world.Objects); // el objeto físico de la llave sigue existiendo
        Assert.Null(world.Rooms[0].Exits[0].DoorId);
    }

    [Fact]
    public void DeleteObject_RemovesKeyDefinitionsAndUnlocksDoors()
    {
        var door1 = new Door { Id = "door-1", LockId = "lock-1", HasLock = true };
        var door2 = new Door { Id = "door-2", LockId = "lock-2", HasLock = true };

        var keyObj = new GameObject { Id = "obj-key" };
        var otherObj = new GameObject { Id = "obj-other" };

        var keyDef = new KeyDefinition { ObjectId = keyObj.Id, LockIds = new List<string> { "lock-1" } };
        var otherKeyDef = new KeyDefinition { ObjectId = otherObj.Id, LockIds = new List<string> { "lock-2" } };

        var room = new Room
        {
            Id = "room-1",
            ObjectIds = new List<string> { keyObj.Id, otherObj.Id }
        };

        var world = new WorldModel
        {
            Doors = new List<Door> { door1, door2 },
            Keys = new List<KeyDefinition> { keyDef, otherKeyDef },
            Objects = new List<GameObject> { keyObj, otherObj },
            Rooms = new List<Room> { room },
            Npcs = new List<Npc>()
        };

        var removed = WorldEditorHelpers.DeleteObject(world, keyObj);

        Assert.True(removed);
        Assert.DoesNotContain(keyObj, world.Objects);
        Assert.DoesNotContain(keyDef, world.Keys);
        Assert.Contains(otherKeyDef, world.Keys); // no se elimina llave de otro objeto

        Assert.False(door1.HasLock);
        Assert.Null(door1.LockId);

        Assert.True(door2.HasLock); // puerta con otro lock no se toca
        Assert.Equal("lock-2", door2.LockId);

        Assert.DoesNotContain(keyObj.Id, world.Rooms[0].ObjectIds);
        Assert.Contains(otherObj.Id, world.Rooms[0].ObjectIds);
    }
}
