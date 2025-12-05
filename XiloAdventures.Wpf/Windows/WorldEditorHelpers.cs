using System;
using System.Collections.Generic;
using System.Linq;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Wpf.Windows;

internal static class WorldEditorHelpers
{
    public static bool DeleteDoor(WorldModel? world, Door? door)
    {
        if (world == null || door == null)
            return false;

        // Quitar referencias a esta puerta desde las salidas
        foreach (var room in world.Rooms)
        {
            foreach (var ex in room.Exits)
            {
                if (string.Equals(ex.DoorId, door.Id, StringComparison.OrdinalIgnoreCase))
                {
                    ex.DoorId = null;
                }
            }
        }

        world.Doors?.Remove(door);

        if (!string.IsNullOrWhiteSpace(door.LockId) && world.Keys != null)
        {
            world.Keys.RemoveAll(k =>
                k.LockIds != null &&
                k.LockIds.Any(l => string.Equals(l, door.LockId, StringComparison.OrdinalIgnoreCase)));
        }

        return true;
    }

    public static bool DeleteObject(WorldModel? world, GameObject? obj)
    {
        if (world == null || obj == null)
            return false;

        world.Objects.Remove(obj);

        // Si el objeto era una llave física, eliminamos las definiciones de llave y limpiamos las puertas afectadas.
        if (world.Keys != null && world.Keys.Count > 0)
        {
            var keysToRemove = world.Keys
                .Where(k => string.Equals(k.ObjectId, obj.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (keysToRemove.Count > 0)
            {
                var lockIds = new HashSet<string>(
                    keysToRemove
                        .Where(k => k.LockIds != null)
                        .SelectMany(k => k.LockIds!)
                        .Where(l => !string.IsNullOrWhiteSpace(l)),
                    StringComparer.OrdinalIgnoreCase);

                if (world.Doors != null && lockIds.Count > 0)
                {
                    foreach (var door in world.Doors)
                    {
                        if (!string.IsNullOrWhiteSpace(door.LockId) && lockIds.Contains(door.LockId))
                        {
                            door.HasLock = false;
                            door.LockId = null;
                        }
                    }
                }

                foreach (var key in keysToRemove)
                {
                    world.Keys.Remove(key);
                }
            }
        }

        // Quitar referencias desde salas y otros objetos / NPCs
        foreach (var room in world.Rooms)
        {
            room.ObjectIds.Remove(obj.Id);
        }

        foreach (var other in world.Objects)
        {
            other.ContainedObjectIds.Remove(obj.Id);
        }

        foreach (var npc in world.Npcs)
        {
            npc.InventoryObjectIds.Remove(obj.Id);
        }

        return true;
    }
}
