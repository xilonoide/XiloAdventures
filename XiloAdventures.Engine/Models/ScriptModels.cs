using System;
using System.Collections.Generic;

namespace XiloAdventures.Engine.Models;

/// <summary>
/// Define un script visual completo asociado a una entidad del juego.
/// </summary>
public class ScriptDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Nuevo Script";

    /// <summary>Tipo de entidad propietaria: "Game", "Room", "Door", "Npc", "GameObject", "Quest"</summary>
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>ID de la entidad propietaria</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Nodos del script</summary>
    public List<ScriptNode> Nodes { get; set; } = new();

    /// <summary>Conexiones entre nodos</summary>
    public List<NodeConnection> Connections { get; set; } = new();
}

/// <summary>
/// Representa un nodo individual en el grafo de script.
/// </summary>
public class ScriptNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Tipo de nodo (determina comportamiento y puertos)</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>Categoria del nodo: Event, Condition, Action, Flow, Variable</summary>
    public NodeCategory Category { get; set; }

    /// <summary>Posicion X en el canvas</summary>
    public double X { get; set; }

    /// <summary>Posicion Y en el canvas</summary>
    public double Y { get; set; }

    /// <summary>Propiedades configurables del nodo</summary>
    public Dictionary<string, object?> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Comentario/descripcion opcional del usuario</summary>
    public string? Comment { get; set; }
}

/// <summary>
/// Define un puerto de entrada o salida en un nodo.
/// </summary>
public class NodePort
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Tipo de puerto: Execution (flujo) o Data (valor)</summary>
    public PortType PortType { get; set; }

    /// <summary>Tipo de dato para puertos Data (string, int, bool, etc.)</summary>
    public string? DataType { get; set; }

    /// <summary>Valor por defecto para puertos de entrada</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Etiqueta a mostrar en el editor</summary>
    public string? Label { get; set; }
}

/// <summary>
/// Conexion entre dos puertos de nodos diferentes.
/// </summary>
public class NodeConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FromNodeId { get; set; } = string.Empty;
    public string FromPortName { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string ToPortName { get; set; } = string.Empty;
}

/// <summary>
/// Categorias de nodos para el editor visual.
/// </summary>
public enum NodeCategory
{
    Event,      // Nodos de evento (entry points) - Verde
    Condition,  // Nodos de condicion - Amarillo
    Action,     // Nodos de accion - Azul
    Flow,       // Control de flujo - Gris
    Variable    // Variables - Naranja
}

/// <summary>
/// Tipos de puertos en los nodos.
/// </summary>
public enum PortType
{
    Execution,  // Puerto de flujo de ejecucion (triangulo)
    Data        // Puerto de datos (circulo)
}

/// <summary>
/// Definicion de un tipo de nodo disponible en el editor.
/// </summary>
public class NodeTypeDefinition
{
    public string TypeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public NodeCategory Category { get; set; }

    /// <summary>Tipos de entidades que pueden usar este nodo ("Game", "Room", etc. o "*" para todos)</summary>
    public string[] OwnerTypes { get; set; } = Array.Empty<string>();

    /// <summary>Puertos de entrada del nodo</summary>
    public NodePort[] InputPorts { get; set; } = Array.Empty<NodePort>();

    /// <summary>Puertos de salida del nodo</summary>
    public NodePort[] OutputPorts { get; set; } = Array.Empty<NodePort>();

    /// <summary>Propiedades editables del nodo</summary>
    public NodePropertyDefinition[] Properties { get; set; } = Array.Empty<NodePropertyDefinition>();
}

/// <summary>
/// Definicion de una propiedad editable de un nodo.
/// </summary>
public class NodePropertyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public object? DefaultValue { get; set; }

    /// <summary>Para propiedades tipo "select", las opciones disponibles</summary>
    public string[]? Options { get; set; }

    /// <summary>Para propiedades que referencian entidades (Room, Object, Npc, etc.)</summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Indica si la propiedad es obligatoria.
    /// Por defecto, las propiedades con EntityType son obligatorias.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Determina si esta propiedad requiere un valor válido.
    /// True si IsRequired es true o si tiene EntityType definido.
    /// </summary>
    public bool RequiresValue => IsRequired || !string.IsNullOrEmpty(EntityType);
}
