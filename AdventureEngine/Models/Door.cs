using System;

namespace XiloAdventures.Engine.Models;

/// <summary>
/// Puerta bidireccional entre dos salas. Puede estar abierta o cerrada
/// y opcionalmente tener una cerradura controlada por llaves.
/// </summary>
public class Door
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Nombre visible de la puerta (por ejemplo "Puerta de la cocina").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Descripción opcional para el narrador/editor.</summary>
    public string? Description { get; set; }

    /// <summary>Id de la sala A (una de las dos salas que conecta la puerta).</summary>
    public string? RoomIdA { get; set; }

    /// <summary>Id de la sala B (la otra sala que conecta la puerta).</summary>
    public string? RoomIdB { get; set; }

    /// <summary>Indica si actualmente la puerta está abierta.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Indica si la puerta tiene cerradura (LockId no nulo).</summary>
    public bool HasLock { get; set; }

    /// <summary>
    /// Identificador lógico de la cerradura. Las llaves llevan una lista
    /// de LockIds que son capaces de abrir/cerrar.
    /// </summary>
    public string? LockId { get; set; }

    /// <summary>
    /// Desde qué lado se puede abrir/cerrar la puerta. Por defecto ambos lados.
    /// Afecta a los comandos de abrir/cerrar, no al movimiento una vez abierta.
    /// </summary>
    public DoorOpenSide OpenFromSide { get; set; } = DoorOpenSide.Both;
}

/// <summary>
/// Lado desde el que se puede accionar la puerta.
/// </summary>
public enum DoorOpenSide
{
    Both = 0,
    FromAOnly = 1,
    FromBOnly = 2
}
