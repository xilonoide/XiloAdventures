namespace XiloAdventures.Engine.Models;

/// <summary>
/// Información sobre un nodo con datos incompletos
/// </summary>
public class IncompleteNodeInfo
{
    public string NodeId { get; set; } = "";
    public string NodeDisplayName { get; set; } = "";
    public List<string> MissingProperties { get; set; } = new();
}

/// <summary>
/// Resultado de validación de un script
/// </summary>
public class ScriptValidationResult
{
    public bool IsValid => !HasErrors;
    public bool HasErrors => !HasEvent || !HasAction || !IsConnected || IncompleteNodes.Count > 0;
    public bool HasWarnings => HasErrors;

    public bool HasEvent { get; set; }
    public bool HasAction { get; set; }
    public bool IsConnected { get; set; }

    /// <summary>Nodos que tienen propiedades obligatorias sin completar</summary>
    public List<IncompleteNodeInfo> IncompleteNodes { get; } = new();

    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public static ScriptValidationResult Empty => new()
    {
        HasEvent = false,
        HasAction = false,
        IsConnected = false
    };
}

/// <summary>
/// Validador de scripts
/// </summary>
public static class ScriptValidator
{
    /// <summary>
    /// Valida un script y devuelve el resultado
    /// </summary>
    public static ScriptValidationResult Validate(ScriptDefinition script)
    {
        var result = new ScriptValidationResult();

        if (script.Nodes.Count == 0)
        {
            return ScriptValidationResult.Empty;
        }

        // Verificar si hay nodos de evento
        var eventNodes = script.Nodes
            .Where(n => IsEventNode(n.NodeType))
            .ToList();
        result.HasEvent = eventNodes.Count > 0;

        if (!result.HasEvent)
        {
            result.Errors.Add("El script no tiene ningún evento. Sin un evento, nunca se ejecutará.");
        }

        // Verificar si hay nodos de acción
        var actionNodes = script.Nodes
            .Where(n => IsActionNode(n.NodeType))
            .ToList();
        result.HasAction = actionNodes.Count > 0;

        if (!result.HasAction)
        {
            result.Errors.Add("El script no tiene ninguna acción. Sin acciones, no hará nada en el juego.");
        }

        // Verificar si hay conexión entre eventos y acciones
        if (result.HasEvent && result.HasAction)
        {
            result.IsConnected = IsEventConnectedToAction(script, eventNodes, actionNodes);

            if (!result.IsConnected)
            {
                result.Errors.Add("El evento no está conectado a ninguna acción. El flujo de ejecución debe llegar del evento a una acción.");
            }
        }
        else
        {
            result.IsConnected = false;
        }

        // Verificar propiedades obligatorias en todos los nodos
        ValidateRequiredProperties(script, result);

        return result;
    }

    /// <summary>
    /// Valida que todas las propiedades obligatorias estén completadas
    /// </summary>
    private static void ValidateRequiredProperties(ScriptDefinition script, ScriptValidationResult result)
    {
        foreach (var node in script.Nodes)
        {
            var typeDef = NodeTypeRegistry.GetNodeType(node.NodeType);
            if (typeDef?.Properties == null || typeDef.Properties.Length == 0)
                continue;

            var missingProps = new List<string>();

            foreach (var propDef in typeDef.Properties)
            {
                if (!propDef.RequiresValue)
                    continue;

                // Verificar si la propiedad tiene valor
                node.Properties.TryGetValue(propDef.Name, out var value);

                bool isMissing = value == null ||
                                 (value is string strVal && string.IsNullOrWhiteSpace(strVal));

                if (isMissing)
                {
                    missingProps.Add(propDef.DisplayName);
                }
            }

            if (missingProps.Count > 0)
            {
                result.IncompleteNodes.Add(new IncompleteNodeInfo
                {
                    NodeId = node.Id,
                    NodeDisplayName = typeDef.DisplayName,
                    MissingProperties = missingProps
                });
            }
        }

        // Añadir errores para nodos incompletos
        if (result.IncompleteNodes.Count > 0)
        {
            foreach (var incomplete in result.IncompleteNodes)
            {
                var propsText = string.Join(", ", incomplete.MissingProperties);
                result.Errors.Add($"El nodo \"{incomplete.NodeDisplayName}\" tiene datos sin completar: {propsText}");
            }
        }
    }

    /// <summary>
    /// Verifica si un tipo de nodo es un evento
    /// </summary>
    public static bool IsEventNode(string nodeType)
    {
        var typeDef = NodeTypeRegistry.GetNodeType(nodeType);
        return typeDef?.Category == NodeCategory.Event;
    }

    /// <summary>
    /// Verifica si un tipo de nodo es una acción
    /// </summary>
    public static bool IsActionNode(string nodeType)
    {
        var typeDef = NodeTypeRegistry.GetNodeType(nodeType);
        return typeDef?.Category == NodeCategory.Action;
    }

    /// <summary>
    /// Verifica si algún evento está conectado a alguna acción a través del flujo de ejecución
    /// </summary>
    private static bool IsEventConnectedToAction(
        ScriptDefinition script,
        List<ScriptNode> eventNodes,
        List<ScriptNode> actionNodes)
    {
        var actionIds = actionNodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var eventNode in eventNodes)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (CanReachAction(script, eventNode.Id, actionIds, visited))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Busca recursivamente si desde un nodo se puede llegar a alguna acción
    /// siguiendo las conexiones de ejecución
    /// </summary>
    private static bool CanReachAction(
        ScriptDefinition script,
        string nodeId,
        HashSet<string> actionIds,
        HashSet<string> visited)
    {
        if (visited.Contains(nodeId))
            return false;

        visited.Add(nodeId);

        // Si este nodo es una acción, hemos llegado
        if (actionIds.Contains(nodeId))
            return true;

        // Buscar conexiones de salida de ejecución desde este nodo
        var outgoingConnections = script.Connections
            .Where(c => string.Equals(c.FromNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var conn in outgoingConnections)
        {
            // Verificar si es una conexión de ejecución
            var fromNode = script.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, conn.FromNodeId, StringComparison.OrdinalIgnoreCase));

            if (fromNode != null)
            {
                var typeDef = NodeTypeRegistry.GetNodeType(fromNode.NodeType);
                var outputPort = typeDef?.OutputPorts?.FirstOrDefault(p =>
                    string.Equals(p.Name, conn.FromPortName, StringComparison.OrdinalIgnoreCase));

                // Solo seguir conexiones de ejecución
                if (outputPort?.PortType == PortType.Execution)
                {
                    if (CanReachAction(script, conn.ToNodeId, actionIds, visited))
                        return true;
                }
            }
        }

        return false;
    }
}
