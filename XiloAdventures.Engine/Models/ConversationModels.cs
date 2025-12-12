using System;
using System.Collections.Generic;

namespace XiloAdventures.Engine.Models;

/// <summary>
/// Define una conversación editable visualmente (blueprints).
/// Similar a ScriptDefinition pero especializada para diálogos con NPCs.
/// </summary>
public class ConversationDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Nueva Conversación";

    /// <summary>Nodos de la conversación (reutiliza ScriptNode para compatibilidad).</summary>
    public List<ScriptNode> Nodes { get; set; } = new();

    /// <summary>Conexiones entre nodos.</summary>
    public List<NodeConnection> Connections { get; set; } = new();

    /// <summary>ID del nodo de inicio (tipo Conversation_Start).</summary>
    public string? StartNodeId { get; set; }
}

/// <summary>
/// Estado de una conversación activa en runtime.
/// </summary>
public class ConversationState
{
    /// <summary>ID de la conversación activa.</summary>
    public string ConversationId { get; set; } = "";

    /// <summary>ID del NPC con quien se habla.</summary>
    public string NpcId { get; set; } = "";

    /// <summary>ID del nodo actual en la conversación.</summary>
    public string CurrentNodeId { get; set; } = "";

    /// <summary>Indica si la conversación está activa.</summary>
    public bool IsActive { get; set; }

    /// <summary>IDs de nodos ya visitados en esta conversación.</summary>
    public List<string> VisitedNodeIds { get; set; } = new();

    /// <summary>Opciones de diálogo disponibles actualmente para el jugador.</summary>
    public List<DialogueOption> CurrentOptions { get; set; } = new();

    /// <summary>Variables locales de la conversación.</summary>
    public Dictionary<string, object?> LocalVariables { get; set; } = new();
}

/// <summary>
/// Mensaje de diálogo para mostrar en la UI.
/// </summary>
public class ConversationMessage
{
    /// <summary>Texto del diálogo.</summary>
    public string Text { get; set; } = "";

    /// <summary>Nombre del hablante.</summary>
    public string SpeakerName { get; set; } = "";

    /// <summary>Emoción del hablante (Neutral, Feliz, Triste, Enfadado, Sorprendido).</summary>
    public string Emotion { get; set; } = "Neutral";

    /// <summary>True si habla el NPC, false si habla el jugador.</summary>
    public bool IsNpc { get; set; }
}

/// <summary>
/// Opción de diálogo para el jugador.
/// </summary>
public class DialogueOption
{
    /// <summary>Índice de la opción (0-based).</summary>
    public int Index { get; set; }

    /// <summary>Texto de la opción.</summary>
    public string Text { get; set; } = "";

    /// <summary>Si la opción está habilitada.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Razón por la que está deshabilitada (si aplica).</summary>
    public string? DisabledReason { get; set; }

    /// <summary>Nombre del puerto de salida asociado (Option1, Option2, etc.).</summary>
    public string OutputPort { get; set; } = "";
}

/// <summary>
/// Datos de la tienda para la UI de comercio.
/// </summary>
public class ShopData
{
    /// <summary>Título de la tienda.</summary>
    public string Title { get; set; } = "Tienda";

    /// <summary>Mensaje de bienvenida del comerciante.</summary>
    public string WelcomeMessage { get; set; } = "";

    /// <summary>ID del NPC comerciante.</summary>
    public string NpcId { get; set; } = "";

    /// <summary>Multiplicador de precio al comprar del jugador.</summary>
    public double BuyPriceMultiplier { get; set; } = 0.5;

    /// <summary>Multiplicador de precio al vender al jugador.</summary>
    public double SellPriceMultiplier { get; set; } = 1.0;

    /// <summary>Objetos disponibles para comprar.</summary>
    public List<ShopItem> ItemsForSale { get; set; } = new();

    /// <summary>Objetos del jugador que puede vender.</summary>
    public List<ShopItem> ItemsToSell { get; set; } = new();
}

/// <summary>
/// Objeto en la tienda con precio calculado.
/// </summary>
public class ShopItem
{
    /// <summary>ID del objeto.</summary>
    public string ObjectId { get; set; } = "";

    /// <summary>Nombre del objeto.</summary>
    public string Name { get; set; } = "";

    /// <summary>Precio para esta transacción.</summary>
    public int Price { get; set; }

    /// <summary>Cantidad disponible (-1 = ilimitado).</summary>
    public int Stock { get; set; } = -1;
}
