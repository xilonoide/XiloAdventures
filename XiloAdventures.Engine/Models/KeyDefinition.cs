using System.Collections.Generic;

namespace XiloAdventures.Engine.Models;

/// <summary>
/// Definición lógica de una llave. El objeto físico sigue siendo un objeto normal
/// del mundo; esta clase sólo define qué cerraduras puede abrir/cerrar.
/// </summary>
public class KeyDefinition
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>
    /// Id del objeto que representa físicamente esta llave
    /// en el inventario / en las salas.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Lista de identificadores de cerraduras (LockId) que esta llave puede
    /// abrir y cerrar. Deben coincidir con Door.LockId u otras cerraduras
    /// que definas en el futuro.
    /// </summary>
    public List<string> LockIds { get; set; } = new();
}
