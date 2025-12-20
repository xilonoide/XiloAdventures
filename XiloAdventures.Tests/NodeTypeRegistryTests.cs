using XiloAdventures.Engine.Models;
using Xunit;

namespace XiloAdventures.Tests;

public class NodeTypeRegistryTests
{
    [Fact]
    public void Types_ReturnsNonEmptyDictionary()
    {
        var types = NodeTypeRegistry.Types;

        Assert.NotNull(types);
        Assert.True(types.Count > 0);
    }

    [Fact]
    public void GetNodeType_ValidTypeId_ReturnsDefinition()
    {
        var result = NodeTypeRegistry.GetNodeType("Event_OnEnter");

        Assert.NotNull(result);
        Assert.Equal("Event_OnEnter", result.TypeId);
        Assert.Equal(NodeCategory.Event, result.Category);
    }

    [Fact]
    public void GetNodeType_InvalidTypeId_ReturnsNull()
    {
        var result = NodeTypeRegistry.GetNodeType("NonExistent_Node");

        Assert.Null(result);
    }

    [Fact]
    public void GetNodeType_CaseInsensitive()
    {
        var result = NodeTypeRegistry.GetNodeType("event_onenter");

        Assert.NotNull(result);
        Assert.Equal("Event_OnEnter", result.TypeId);
    }

    [Fact]
    public void GetNodesForOwnerType_Room_ReturnsRoomNodes()
    {
        var roomNodes = NodeTypeRegistry.GetNodesForOwnerType("Room").ToList();

        Assert.True(roomNodes.Count > 0);
        Assert.Contains(roomNodes, n => n.TypeId == "Event_OnEnter");
        Assert.Contains(roomNodes, n => n.TypeId == "Event_OnExit");
    }

    [Fact]
    public void GetNodesForOwnerType_IncludesWildcardNodes()
    {
        var roomNodes = NodeTypeRegistry.GetNodesForOwnerType("Room").ToList();

        // Wildcard nodes (OwnerTypes contains "*") should be included
        Assert.Contains(roomNodes, n => n.TypeId == "Action_ShowMessage");
        Assert.Contains(roomNodes, n => n.TypeId == "Condition_HasItem");
    }

    [Fact]
    public void GetNodesForOwnerType_Npc_ReturnsNpcNodes()
    {
        var npcNodes = NodeTypeRegistry.GetNodesForOwnerType("Npc").ToList();

        Assert.True(npcNodes.Count > 0);
        Assert.Contains(npcNodes, n => n.TypeId == "Event_OnTalk");
        Assert.Contains(npcNodes, n => n.TypeId == "Event_OnNpcAttack");
    }

    [Fact]
    public void GetNodesForOwnerType_GameObject_ReturnsObjectNodes()
    {
        var objectNodes = NodeTypeRegistry.GetNodesForOwnerType("GameObject").ToList();

        Assert.True(objectNodes.Count > 0);
        Assert.Contains(objectNodes, n => n.TypeId == "Event_OnTake");
        Assert.Contains(objectNodes, n => n.TypeId == "Event_OnDrop");
        Assert.Contains(objectNodes, n => n.TypeId == "Event_OnUse");
        Assert.Contains(objectNodes, n => n.TypeId == "Event_OnExamine");
    }

    [Fact]
    public void GetNodesForOwnerType_Npc_ReturnsDialogueNodes()
    {
        // Los nodos de conversación están asociados a NPCs (no a "Conversation")
        var npcNodes = NodeTypeRegistry.GetNodesForOwnerType("Npc").ToList();

        Assert.True(npcNodes.Count > 0);
        Assert.Contains(npcNodes, n => n.TypeId == "Conversation_Start");
        Assert.Contains(npcNodes, n => n.TypeId == "Conversation_NpcSay");
        Assert.Contains(npcNodes, n => n.TypeId == "Conversation_PlayerChoice");
        Assert.Contains(npcNodes, n => n.TypeId == "Conversation_End");
    }

    [Fact]
    public void GetNodesByCategory_Event_ReturnsEventNodes()
    {
        var eventNodes = NodeTypeRegistry.GetNodesByCategory(NodeCategory.Event).ToList();

        Assert.True(eventNodes.Count > 0);
        Assert.All(eventNodes, n => Assert.Equal(NodeCategory.Event, n.Category));
        Assert.Contains(eventNodes, n => n.TypeId == "Event_OnEnter");
        Assert.Contains(eventNodes, n => n.TypeId == "Event_OnGameStart");
    }

    [Fact]
    public void GetNodesByCategory_Action_ReturnsActionNodes()
    {
        var actionNodes = NodeTypeRegistry.GetNodesByCategory(NodeCategory.Action).ToList();

        Assert.True(actionNodes.Count > 0);
        Assert.All(actionNodes, n => Assert.Equal(NodeCategory.Action, n.Category));
        Assert.Contains(actionNodes, n => n.TypeId == "Action_ShowMessage");
        Assert.Contains(actionNodes, n => n.TypeId == "Action_GiveItem");
    }

    [Fact]
    public void GetNodesByCategory_Condition_ReturnsConditionNodes()
    {
        var conditionNodes = NodeTypeRegistry.GetNodesByCategory(NodeCategory.Condition).ToList();

        Assert.True(conditionNodes.Count > 0);
        Assert.All(conditionNodes, n => Assert.Equal(NodeCategory.Condition, n.Category));
        Assert.Contains(conditionNodes, n => n.TypeId == "Condition_HasItem");
        Assert.Contains(conditionNodes, n => n.TypeId == "Condition_HasFlag");
    }

    [Fact]
    public void GetNodesByCategory_Flow_ReturnsFlowNodes()
    {
        var flowNodes = NodeTypeRegistry.GetNodesByCategory(NodeCategory.Flow).ToList();

        Assert.True(flowNodes.Count > 0);
        Assert.All(flowNodes, n => Assert.Equal(NodeCategory.Flow, n.Category));
        Assert.Contains(flowNodes, n => n.TypeId == "Flow_Branch");
        Assert.Contains(flowNodes, n => n.TypeId == "Flow_Sequence");
    }

    [Fact]
    public void GetNodesByCategory_Variable_ReturnsVariableNodes()
    {
        var variableNodes = NodeTypeRegistry.GetNodesByCategory(NodeCategory.Variable).ToList();

        Assert.True(variableNodes.Count > 0);
        Assert.All(variableNodes, n => Assert.Equal(NodeCategory.Variable, n.Category));
        Assert.Contains(variableNodes, n => n.TypeId == "Variable_GetFlag");
        Assert.Contains(variableNodes, n => n.TypeId == "Variable_GetCounter");
    }

    [Fact]
    public void GetNodesByCategory_Dialogue_ReturnsDialogueNodes()
    {
        var dialogueNodes = NodeTypeRegistry.GetNodesByCategory(NodeCategory.Dialogue).ToList();

        Assert.True(dialogueNodes.Count > 0);
        Assert.All(dialogueNodes, n => Assert.Equal(NodeCategory.Dialogue, n.Category));
        Assert.Contains(dialogueNodes, n => n.TypeId == "Conversation_Start");
        Assert.Contains(dialogueNodes, n => n.TypeId == "Conversation_NpcSay");
    }

    #region Node Definition Property Tests

    [Fact]
    public void ActionShowMessage_HasRequiredMessageProperty()
    {
        var node = NodeTypeRegistry.GetNodeType("Action_ShowMessage");

        Assert.NotNull(node);
        Assert.NotNull(node.Properties);
        var messageProp = node.Properties.FirstOrDefault(p => p.Name == "Message");
        Assert.NotNull(messageProp);
        Assert.True(messageProp.IsRequired);
    }

    [Fact]
    public void EventOnEnter_HasExecutionOutput()
    {
        var node = NodeTypeRegistry.GetNodeType("Event_OnEnter");

        Assert.NotNull(node);
        Assert.NotNull(node.OutputPorts);
        Assert.Contains(node.OutputPorts, p => p.Name == "Exec" && p.PortType == PortType.Execution);
    }

    [Fact]
    public void ConditionHasItem_HasTrueAndFalseOutputs()
    {
        var node = NodeTypeRegistry.GetNodeType("Condition_HasItem");

        Assert.NotNull(node);
        Assert.NotNull(node.OutputPorts);
        Assert.Contains(node.OutputPorts, p => p.Name == "True" && p.PortType == PortType.Execution);
        Assert.Contains(node.OutputPorts, p => p.Name == "False" && p.PortType == PortType.Execution);
    }

    [Fact]
    public void FlowBranch_HasConditionDataInput()
    {
        var node = NodeTypeRegistry.GetNodeType("Flow_Branch");

        Assert.NotNull(node);
        Assert.NotNull(node.InputPorts);
        var conditionPort = node.InputPorts.FirstOrDefault(p => p.Name == "Condition");
        Assert.NotNull(conditionPort);
        Assert.Equal(PortType.Data, conditionPort.PortType);
        Assert.Equal("bool", conditionPort.DataType);
    }

    [Fact]
    public void MathAdd_HasTwoIntInputsAndIntOutput()
    {
        var node = NodeTypeRegistry.GetNodeType("Math_Add");

        Assert.NotNull(node);
        Assert.NotNull(node.InputPorts);
        Assert.NotNull(node.OutputPorts);

        var inputA = node.InputPorts.FirstOrDefault(p => p.Name == "A");
        var inputB = node.InputPorts.FirstOrDefault(p => p.Name == "B");
        var output = node.OutputPorts.FirstOrDefault(p => p.Name == "Result");

        Assert.NotNull(inputA);
        Assert.NotNull(inputB);
        Assert.NotNull(output);
        Assert.Equal("int", inputA.DataType);
        Assert.Equal("int", inputB.DataType);
        Assert.Equal("int", output.DataType);
    }

    [Fact]
    public void ConversationPlayerChoice_HasFourOptionOutputs()
    {
        var node = NodeTypeRegistry.GetNodeType("Conversation_PlayerChoice");

        Assert.NotNull(node);
        Assert.NotNull(node.OutputPorts);
        Assert.Contains(node.OutputPorts, p => p.Name == "Option1");
        Assert.Contains(node.OutputPorts, p => p.Name == "Option2");
        Assert.Contains(node.OutputPorts, p => p.Name == "Option3");
        Assert.Contains(node.OutputPorts, p => p.Name == "Option4");
    }

    [Fact]
    public void ConversationShop_HasShopOutputs()
    {
        var node = NodeTypeRegistry.GetNodeType("Conversation_Shop");

        Assert.NotNull(node);
        Assert.NotNull(node.OutputPorts);
        Assert.Contains(node.OutputPorts, p => p.Name == "OnClose");
        Assert.Contains(node.OutputPorts, p => p.Name == "OnBuy");
        Assert.Contains(node.OutputPorts, p => p.Name == "OnSell");
    }

    #endregion

    #region Specific Node Type Existence Tests

    [Theory]
    [InlineData("Event_OnGameStart")]
    [InlineData("Event_OnEnter")]
    [InlineData("Event_OnExit")]
    [InlineData("Event_OnTalk")]
    [InlineData("Event_OnTake")]
    [InlineData("Event_OnDrop")]
    [InlineData("Event_OnUse")]
    [InlineData("Event_OnExamine")]
    [InlineData("Event_OnQuestStart")]
    [InlineData("Event_OnQuestComplete")]
    public void EventNodes_Exist(string typeId)
    {
        var node = NodeTypeRegistry.GetNodeType(typeId);
        Assert.NotNull(node);
        Assert.Equal(NodeCategory.Event, node.Category);
    }

    [Theory]
    [InlineData("Action_ShowMessage")]
    [InlineData("Action_GiveItem")]
    [InlineData("Action_RemoveItem")]
    [InlineData("Action_TeleportPlayer")]
    [InlineData("Action_SetFlag")]
    [InlineData("Action_SetCounter")]
    [InlineData("Action_StartQuest")]
    [InlineData("Action_CompleteQuest")]
    [InlineData("Action_AddGold")]
    [InlineData("Action_RemoveGold")]
    public void ActionNodes_Exist(string typeId)
    {
        var node = NodeTypeRegistry.GetNodeType(typeId);
        Assert.NotNull(node);
        Assert.Equal(NodeCategory.Action, node.Category);
    }

    [Theory]
    [InlineData("Condition_HasItem")]
    [InlineData("Condition_IsInRoom")]
    [InlineData("Condition_HasFlag")]
    [InlineData("Condition_CompareCounter")]
    [InlineData("Condition_Random")]
    public void ConditionNodes_Exist(string typeId)
    {
        var node = NodeTypeRegistry.GetNodeType(typeId);
        Assert.NotNull(node);
        Assert.Equal(NodeCategory.Condition, node.Category);
    }

    [Theory]
    [InlineData("Flow_Branch")]
    [InlineData("Flow_Sequence")]
    [InlineData("Flow_Delay")]
    [InlineData("Flow_RandomBranch")]
    public void FlowNodes_Exist(string typeId)
    {
        var node = NodeTypeRegistry.GetNodeType(typeId);
        Assert.NotNull(node);
        Assert.Equal(NodeCategory.Flow, node.Category);
    }

    [Theory]
    [InlineData("Conversation_Start")]
    [InlineData("Conversation_NpcSay")]
    [InlineData("Conversation_PlayerChoice")]
    [InlineData("Conversation_Branch")]
    [InlineData("Conversation_End")]
    [InlineData("Conversation_Shop")]
    public void ConversationNodes_Exist(string typeId)
    {
        var node = NodeTypeRegistry.GetNodeType(typeId);
        Assert.NotNull(node);
        Assert.Equal(NodeCategory.Dialogue, node.Category);
    }

    #endregion
}
