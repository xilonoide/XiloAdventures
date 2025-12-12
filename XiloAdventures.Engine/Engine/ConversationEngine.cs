using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Engine.Engine;

/// <summary>
/// Motor de ejecución de conversaciones con NPCs.
/// Maneja el estado de diálogos activos y la interacción con el jugador.
/// </summary>
public class ConversationEngine
{
    private readonly WorldModel _world;
    private readonly GameState _gameState;

    /// <summary>Evento cuando hay texto de diálogo para mostrar.</summary>
    public event Action<ConversationMessage>? OnDialogue;

    /// <summary>Evento cuando hay opciones para el jugador.</summary>
    public event Action<List<DialogueOption>>? OnPlayerOptions;

    /// <summary>Evento cuando se abre la tienda.</summary>
    public event Action<ShopData>? OnShopOpen;

    /// <summary>Evento cuando termina la conversación.</summary>
    public event Action? OnConversationEnded;

    /// <summary>Evento cuando se debe mostrar un mensaje del sistema.</summary>
    public event Action<string>? OnSystemMessage;

    /// <summary>Indica si hay una conversación activa.</summary>
    public bool IsConversationActive => _gameState.ActiveConversation?.IsActive == true;

    public ConversationEngine(WorldModel world, GameState gameState)
    {
        _world = world;
        _gameState = gameState;
    }

    /// <summary>
    /// Inicia una conversación con un NPC.
    /// </summary>
    public async Task StartConversationAsync(string npcId)
    {
        var npc = _gameState.Npcs.FirstOrDefault(n =>
            string.Equals(n.Id, npcId, StringComparison.OrdinalIgnoreCase));
        if (npc == null) return;

        // Determinar qué conversación usar
        var conversationId = npc.ConversationId;
        if (string.IsNullOrEmpty(conversationId)) return;

        var conversation = _world.Conversations.FirstOrDefault(c =>
            string.Equals(c.Id, conversationId, StringComparison.OrdinalIgnoreCase));
        if (conversation == null) return;

        // Inicializar estado
        _gameState.ActiveConversation = new ConversationState
        {
            ConversationId = conversationId,
            NpcId = npcId,
            CurrentNodeId = conversation.StartNodeId ?? FindStartNode(conversation)?.Id ?? "",
            IsActive = true
        };

        // Ejecutar desde el nodo de inicio
        if (!string.IsNullOrEmpty(_gameState.ActiveConversation.CurrentNodeId))
        {
            await ExecuteNodeAsync(conversation, _gameState.ActiveConversation.CurrentNodeId);
        }
    }

    /// <summary>
    /// El jugador selecciona una opción de diálogo.
    /// </summary>
    public async Task SelectOptionAsync(int optionIndex)
    {
        if (_gameState.ActiveConversation == null || !_gameState.ActiveConversation.IsActive) return;

        var conversation = GetCurrentConversation();
        if (conversation == null) return;

        // Verificar que hay opciones disponibles
        if (_gameState.ActiveConversation.CurrentOptions.Count == 0) return;
        if (optionIndex < 0 || optionIndex >= _gameState.ActiveConversation.CurrentOptions.Count) return;

        var selectedOption = _gameState.ActiveConversation.CurrentOptions[optionIndex];

        // Buscar la conexión para esta opción
        var currentNode = GetCurrentNode(conversation);
        if (currentNode == null) return;

        var connection = conversation.Connections.FirstOrDefault(c =>
            string.Equals(c.FromNodeId, currentNode.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.FromPortName, selectedOption.OutputPort, StringComparison.OrdinalIgnoreCase));

        if (connection != null)
        {
            _gameState.ActiveConversation.CurrentNodeId = connection.ToNodeId;
            _gameState.ActiveConversation.CurrentOptions.Clear();
            await ExecuteNodeAsync(conversation, connection.ToNodeId);
        }
    }

    /// <summary>
    /// Continúa la conversación (para nodos sin opciones, como después de NpcSay).
    /// </summary>
    public async Task ContinueAsync()
    {
        if (_gameState.ActiveConversation == null || !_gameState.ActiveConversation.IsActive) return;

        var conversation = GetCurrentConversation();
        if (conversation == null) return;

        var currentNode = GetCurrentNode(conversation);
        if (currentNode == null) return;

        // Buscar siguiente nodo por conexión "Exec"
        var connection = conversation.Connections.FirstOrDefault(c =>
            string.Equals(c.FromNodeId, currentNode.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.FromPortName, "Exec", StringComparison.OrdinalIgnoreCase));

        if (connection != null)
        {
            _gameState.ActiveConversation.CurrentNodeId = connection.ToNodeId;
            await ExecuteNodeAsync(conversation, connection.ToNodeId);
        }
    }

    /// <summary>
    /// Termina la conversación forzadamente.
    /// </summary>
    public void EndConversation()
    {
        if (_gameState.ActiveConversation != null)
        {
            _gameState.ActiveConversation.IsActive = false;
            _gameState.ActiveConversation = null;
        }
        OnConversationEnded?.Invoke();
    }

    private async Task ExecuteNodeAsync(ConversationDefinition conversation, string nodeId)
    {
        var node = conversation.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        if (node == null) return;

        // Marcar como visitado
        _gameState.ActiveConversation?.VisitedNodeIds.Add(nodeId);

        switch (node.NodeType)
        {
            case "Conversation_Start":
                await ContinueAsync();
                break;

            case "Conversation_NpcSay":
                HandleNpcSay(node);
                // Continuar automáticamente al siguiente nodo
                await ContinueAsync();
                break;

            case "Conversation_PlayerChoice":
                HandlePlayerChoice(conversation, node);
                break;

            case "Conversation_Branch":
                await HandleBranchAsync(conversation, node);
                break;

            case "Conversation_Shop":
                HandleShop(node);
                break;

            case "Conversation_BuyItem":
                await HandleBuyItemAsync(conversation, node);
                break;

            case "Conversation_SellItem":
                await HandleSellItemAsync(conversation, node);
                break;

            case "Conversation_Action":
                await HandleActionAsync(conversation, node);
                break;

            case "Conversation_End":
                EndConversation();
                break;

            case "Conversation_Jump":
                await HandleJumpAsync(node);
                break;
        }
    }

    private void HandleNpcSay(ScriptNode node)
    {
        var text = GetProperty<string>(node, "Text", "");
        var speakerName = GetProperty<string>(node, "SpeakerName", "");
        var emotion = GetProperty<string>(node, "Emotion", "Neutral");

        OnDialogue?.Invoke(new ConversationMessage
        {
            Text = text,
            SpeakerName = string.IsNullOrEmpty(speakerName) ? GetCurrentNpcName() : speakerName,
            Emotion = emotion,
            IsNpc = true
        });
    }

    private void HandlePlayerChoice(ConversationDefinition conversation, ScriptNode node)
    {
        var options = new List<DialogueOption>();

        for (int i = 1; i <= 4; i++)
        {
            var text = GetProperty<string>(node, $"Text{i}", "");
            if (!string.IsNullOrWhiteSpace(text))
            {
                var outputPort = $"Option{i}";
                // Verificar si hay conexión para esta opción
                var hasConnection = conversation.Connections.Any(c =>
                    string.Equals(c.FromNodeId, node.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.FromPortName, outputPort, StringComparison.OrdinalIgnoreCase));

                if (hasConnection)
                {
                    options.Add(new DialogueOption
                    {
                        Index = options.Count,
                        Text = text,
                        IsEnabled = true,
                        OutputPort = outputPort
                    });
                }
            }
        }

        if (_gameState.ActiveConversation != null)
        {
            _gameState.ActiveConversation.CurrentOptions = options;
        }

        OnPlayerOptions?.Invoke(options);
    }

    private async Task HandleBranchAsync(ConversationDefinition conversation, ScriptNode node)
    {
        var conditionType = GetProperty<string>(node, "ConditionType", "");
        var result = EvaluateCondition(node, conditionType);

        var outputPort = result ? "True" : "False";
        await FollowConnectionAsync(conversation, node, outputPort);
    }

    private bool EvaluateCondition(ScriptNode node, string conditionType)
    {
        return conditionType switch
        {
            "HasFlag" => EvaluateHasFlag(node),
            "HasItem" => EvaluateHasItem(node),
            "HasGold" => EvaluateHasGold(node),
            "QuestStatus" => EvaluateQuestStatus(node),
            "VisitedNode" => _gameState.ActiveConversation?.VisitedNodeIds.Contains(node.Id) ?? false,
            _ => false
        };
    }

    private bool EvaluateHasFlag(ScriptNode node)
    {
        var flagName = GetProperty<string>(node, "FlagName", "");
        return !string.IsNullOrEmpty(flagName) &&
               _gameState.Flags.TryGetValue(flagName, out var value) && value;
    }

    private bool EvaluateHasItem(ScriptNode node)
    {
        var itemId = GetProperty<string>(node, "ItemId", "");
        return !string.IsNullOrEmpty(itemId) &&
               _gameState.InventoryObjectIds.Any(id =>
                   string.Equals(id, itemId, StringComparison.OrdinalIgnoreCase));
    }

    private bool EvaluateHasGold(ScriptNode node)
    {
        var amount = GetProperty<int>(node, "GoldAmount", 0);
        return _gameState.Player.Gold >= amount;
    }

    private bool EvaluateQuestStatus(ScriptNode node)
    {
        var questId = GetProperty<string>(node, "QuestId", "");
        var expectedStatus = GetProperty<string>(node, "QuestStatus", "NotStarted");

        if (string.IsNullOrEmpty(questId)) return false;

        var currentStatus = _gameState.Quests.TryGetValue(questId, out var state)
            ? state.Status
            : QuestStatus.NotStarted;

        return Enum.TryParse<QuestStatus>(expectedStatus, out var expected) &&
               currentStatus == expected;
    }

    private void HandleShop(ScriptNode node)
    {
        var npc = GetCurrentNpc();
        if (npc == null) return;

        var shopData = new ShopData
        {
            Title = GetProperty<string>(node, "ShopTitle", "Tienda"),
            WelcomeMessage = GetProperty<string>(node, "WelcomeMessage", ""),
            NpcId = npc.Id,
            BuyPriceMultiplier = npc.BuyPriceMultiplier,
            SellPriceMultiplier = npc.SellPriceMultiplier
        };

        // Objetos para vender (del inventario del NPC)
        foreach (var objId in npc.ShopInventory)
        {
            var obj = _gameState.Objects.FirstOrDefault(o =>
                string.Equals(o.Id, objId, StringComparison.OrdinalIgnoreCase));
            if (obj != null)
            {
                shopData.ItemsForSale.Add(new ShopItem
                {
                    ObjectId = obj.Id,
                    Name = obj.Name,
                    Price = (int)(obj.Price * npc.SellPriceMultiplier)
                });
            }
        }

        // Objetos del jugador que puede vender
        foreach (var objId in _gameState.InventoryObjectIds)
        {
            var obj = _gameState.Objects.FirstOrDefault(o =>
                string.Equals(o.Id, objId, StringComparison.OrdinalIgnoreCase));
            if (obj != null && obj.Price > 0)
            {
                shopData.ItemsToSell.Add(new ShopItem
                {
                    ObjectId = obj.Id,
                    Name = obj.Name,
                    Price = (int)(obj.Price * npc.BuyPriceMultiplier)
                });
            }
        }

        OnShopOpen?.Invoke(shopData);
    }

    private async Task HandleBuyItemAsync(ConversationDefinition conversation, ScriptNode node)
    {
        var objectId = GetProperty<string>(node, "ObjectId", "");
        var price = GetProperty<int>(node, "Price", 0);

        if (_gameState.Player.Gold >= price)
        {
            _gameState.Player.Gold -= price;
            if (!_gameState.InventoryObjectIds.Contains(objectId))
                _gameState.InventoryObjectIds.Add(objectId);

            var obj = _gameState.Objects.FirstOrDefault(o =>
                string.Equals(o.Id, objectId, StringComparison.OrdinalIgnoreCase));
            OnSystemMessage?.Invoke($"Has comprado {obj?.Name ?? objectId} por {price} monedas.");

            await FollowConnectionAsync(conversation, node, "Success");
        }
        else
        {
            OnSystemMessage?.Invoke("No tienes suficiente oro.");
            await FollowConnectionAsync(conversation, node, "NotEnoughGold");
        }
    }

    private async Task HandleSellItemAsync(ConversationDefinition conversation, ScriptNode node)
    {
        var objectId = GetProperty<string>(node, "ObjectId", "");
        var price = GetProperty<int>(node, "Price", 0);

        if (_gameState.InventoryObjectIds.Any(id =>
            string.Equals(id, objectId, StringComparison.OrdinalIgnoreCase)))
        {
            _gameState.InventoryObjectIds.RemoveAll(id =>
                string.Equals(id, objectId, StringComparison.OrdinalIgnoreCase));
            _gameState.Player.Gold += price;

            var obj = _gameState.Objects.FirstOrDefault(o =>
                string.Equals(o.Id, objectId, StringComparison.OrdinalIgnoreCase));
            OnSystemMessage?.Invoke($"Has vendido {obj?.Name ?? objectId} por {price} monedas.");

            await FollowConnectionAsync(conversation, node, "Success");
        }
        else
        {
            OnSystemMessage?.Invoke("No tienes ese objeto.");
            await FollowConnectionAsync(conversation, node, "NoItem");
        }
    }

    private async Task HandleActionAsync(ConversationDefinition conversation, ScriptNode node)
    {
        var actionType = GetProperty<string>(node, "ActionType", "");

        switch (actionType)
        {
            case "GiveItem":
                var giveObjectId = GetProperty<string>(node, "ObjectId", "");
                if (!string.IsNullOrEmpty(giveObjectId) &&
                    !_gameState.InventoryObjectIds.Contains(giveObjectId))
                {
                    _gameState.InventoryObjectIds.Add(giveObjectId);
                    var obj = _gameState.Objects.FirstOrDefault(o =>
                        string.Equals(o.Id, giveObjectId, StringComparison.OrdinalIgnoreCase));
                    OnSystemMessage?.Invoke($"Has recibido: {obj?.Name ?? giveObjectId}");
                }
                break;

            case "RemoveItem":
                var removeId = GetProperty<string>(node, "ObjectId", "");
                _gameState.InventoryObjectIds.RemoveAll(id =>
                    string.Equals(id, removeId, StringComparison.OrdinalIgnoreCase));
                break;

            case "AddGold":
                var addAmount = GetProperty<int>(node, "Amount", 0);
                _gameState.Player.Gold += addAmount;
                if (addAmount > 0)
                    OnSystemMessage?.Invoke($"Has recibido {addAmount} monedas.");
                break;

            case "RemoveGold":
                var removeAmount = GetProperty<int>(node, "Amount", 0);
                _gameState.Player.Gold = Math.Max(0, _gameState.Player.Gold - removeAmount);
                break;

            case "SetFlag":
                var flagName = GetProperty<string>(node, "FlagName", "");
                if (!string.IsNullOrEmpty(flagName))
                    _gameState.Flags[flagName] = true;
                break;

            case "StartQuest":
                var startQuestId = GetProperty<string>(node, "QuestId", "");
                if (!string.IsNullOrEmpty(startQuestId))
                {
                    _gameState.Quests[startQuestId] = new QuestState
                    {
                        QuestId = startQuestId,
                        Status = QuestStatus.InProgress
                    };
                    var quest = _world.Quests.FirstOrDefault(q =>
                        string.Equals(q.Id, startQuestId, StringComparison.OrdinalIgnoreCase));
                    OnSystemMessage?.Invoke($"Nueva misión: {quest?.Name ?? startQuestId}");
                }
                break;

            case "CompleteQuest":
                var completeQuestId = GetProperty<string>(node, "QuestId", "");
                if (!string.IsNullOrEmpty(completeQuestId) &&
                    _gameState.Quests.TryGetValue(completeQuestId, out var qState))
                {
                    qState.Status = QuestStatus.Completed;
                    var quest = _world.Quests.FirstOrDefault(q =>
                        string.Equals(q.Id, completeQuestId, StringComparison.OrdinalIgnoreCase));
                    OnSystemMessage?.Invoke($"Misión completada: {quest?.Name ?? completeQuestId}");
                }
                break;

            case "ShowMessage":
                var message = GetProperty<string>(node, "Message", "");
                if (!string.IsNullOrEmpty(message))
                    OnSystemMessage?.Invoke(message);
                break;
        }

        await ContinueAsync();
    }

    private async Task HandleJumpAsync(ScriptNode node)
    {
        var targetConversationId = GetProperty<string>(node, "ConversationId", "");
        if (string.IsNullOrEmpty(targetConversationId)) return;

        var targetConversation = _world.Conversations.FirstOrDefault(c =>
            string.Equals(c.Id, targetConversationId, StringComparison.OrdinalIgnoreCase));
        if (targetConversation == null) return;

        if (_gameState.ActiveConversation != null)
        {
            _gameState.ActiveConversation.ConversationId = targetConversationId;
            _gameState.ActiveConversation.CurrentNodeId = targetConversation.StartNodeId ??
                FindStartNode(targetConversation)?.Id ?? "";
            _gameState.ActiveConversation.VisitedNodeIds.Clear();
            _gameState.ActiveConversation.CurrentOptions.Clear();
        }

        if (!string.IsNullOrEmpty(_gameState.ActiveConversation?.CurrentNodeId))
        {
            await ExecuteNodeAsync(targetConversation, _gameState.ActiveConversation.CurrentNodeId);
        }
    }

    private async Task FollowConnectionAsync(ConversationDefinition conversation, ScriptNode node, string outputPort)
    {
        var connection = conversation.Connections.FirstOrDefault(c =>
            string.Equals(c.FromNodeId, node.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.FromPortName, outputPort, StringComparison.OrdinalIgnoreCase));

        if (connection != null && _gameState.ActiveConversation != null)
        {
            _gameState.ActiveConversation.CurrentNodeId = connection.ToNodeId;
            await ExecuteNodeAsync(conversation, connection.ToNodeId);
        }
    }

    private ConversationDefinition? GetCurrentConversation()
    {
        if (_gameState.ActiveConversation == null) return null;
        return _world.Conversations.FirstOrDefault(c =>
            string.Equals(c.Id, _gameState.ActiveConversation.ConversationId, StringComparison.OrdinalIgnoreCase));
    }

    private ScriptNode? GetCurrentNode(ConversationDefinition conversation)
    {
        if (_gameState.ActiveConversation == null) return null;
        return conversation.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, _gameState.ActiveConversation.CurrentNodeId, StringComparison.OrdinalIgnoreCase));
    }

    private ScriptNode? FindStartNode(ConversationDefinition conversation)
    {
        return conversation.Nodes.FirstOrDefault(n =>
            string.Equals(n.NodeType, "Conversation_Start", StringComparison.OrdinalIgnoreCase));
    }

    private Npc? GetCurrentNpc()
    {
        if (_gameState.ActiveConversation == null) return null;
        return _gameState.Npcs.FirstOrDefault(n =>
            string.Equals(n.Id, _gameState.ActiveConversation.NpcId, StringComparison.OrdinalIgnoreCase));
    }

    private string GetCurrentNpcName()
    {
        return GetCurrentNpc()?.Name ?? "???";
    }

    private T GetProperty<T>(ScriptNode node, string name, T defaultValue)
    {
        if (node.Properties.TryGetValue(name, out var value) && value != null)
        {
            // Si es JsonElement (deserializado de JSON)
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                try
                {
                    if (typeof(T) == typeof(string))
                        return (T)(object)(jsonElement.GetString() ?? defaultValue?.ToString() ?? "");
                    if (typeof(T) == typeof(int))
                        return (T)(object)jsonElement.GetInt32();
                    if (typeof(T) == typeof(double))
                        return (T)(object)jsonElement.GetDouble();
                    if (typeof(T) == typeof(bool))
                        return (T)(object)jsonElement.GetBoolean();
                }
                catch
                {
                    return defaultValue;
                }
            }

            if (value is T typedValue)
                return typedValue;

            // Intentar conversión para tipos numéricos
            if (typeof(T) == typeof(int))
            {
                if (int.TryParse(value.ToString(), out var intVal))
                    return (T)(object)intVal;
            }
            if (typeof(T) == typeof(double))
            {
                if (double.TryParse(value.ToString(), out var doubleVal))
                    return (T)(object)doubleVal;
            }
        }
        return defaultValue;
    }
}
