using XiloAdventures.Engine.Models;
using Xunit;

namespace XiloAdventures.Tests;

public class ScriptValidatorTests
{
    [Fact]
    public void Validate_EmptyScript_ReturnsEmpty()
    {
        var script = new ScriptDefinition
        {
            Id = "test",
            Name = "Test",
            Nodes = new List<ScriptNode>(),
            Connections = new List<NodeConnection>()
        };

        var result = ScriptValidator.Validate(script);

        Assert.False(result.HasEvent);
        Assert.False(result.HasAction);
        Assert.False(result.IsConnected);
    }

    [Fact]
    public void Validate_OnlyEventNode_HasNoAction()
    {
        var script = new ScriptDefinition
        {
            Id = "test",
            Name = "Test",
            Nodes = new List<ScriptNode>
            {
                new ScriptNode
                {
                    Id = "node1",
                    NodeType = "Event_OnEnter",
                    Properties = new Dictionary<string, object?>()
                }
            },
            Connections = new List<NodeConnection>()
        };

        var result = ScriptValidator.Validate(script);

        Assert.True(result.HasEvent);
        Assert.False(result.HasAction);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_OnlyActionNode_HasNoEvent()
    {
        var script = new ScriptDefinition
        {
            Id = "test",
            Name = "Test",
            Nodes = new List<ScriptNode>
            {
                new ScriptNode
                {
                    Id = "node1",
                    NodeType = "Action_ShowMessage",
                    Properties = new Dictionary<string, object?> { ["Message"] = "Hello" }
                }
            },
            Connections = new List<NodeConnection>()
        };

        var result = ScriptValidator.Validate(script);

        Assert.False(result.HasEvent);
        Assert.True(result.HasAction);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ConnectedEventAndAction_IsValid()
    {
        var script = new ScriptDefinition
        {
            Id = "test",
            Name = "Test",
            Nodes = new List<ScriptNode>
            {
                new ScriptNode
                {
                    Id = "event1",
                    NodeType = "Event_OnEnter",
                    Properties = new Dictionary<string, object?>()
                },
                new ScriptNode
                {
                    Id = "action1",
                    NodeType = "Action_ShowMessage",
                    Properties = new Dictionary<string, object?> { ["Message"] = "Hello" }
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    FromNodeId = "event1",
                    FromPortName = "Exec",
                    ToNodeId = "action1",
                    ToPortName = "Exec"
                }
            }
        };

        var result = ScriptValidator.Validate(script);

        Assert.True(result.HasEvent);
        Assert.True(result.HasAction);
        Assert.True(result.IsConnected);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DisconnectedEventAndAction_NotValid()
    {
        var script = new ScriptDefinition
        {
            Id = "test",
            Name = "Test",
            Nodes = new List<ScriptNode>
            {
                new ScriptNode
                {
                    Id = "event1",
                    NodeType = "Event_OnEnter",
                    Properties = new Dictionary<string, object?>()
                },
                new ScriptNode
                {
                    Id = "action1",
                    NodeType = "Action_ShowMessage",
                    Properties = new Dictionary<string, object?> { ["Message"] = "Hello" }
                }
            },
            Connections = new List<NodeConnection>()  // No connections
        };

        var result = ScriptValidator.Validate(script);

        Assert.True(result.HasEvent);
        Assert.True(result.HasAction);
        Assert.False(result.IsConnected);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_MissingRequiredProperty_ReportsIncomplete()
    {
        var script = new ScriptDefinition
        {
            Id = "test",
            Name = "Test",
            Nodes = new List<ScriptNode>
            {
                new ScriptNode
                {
                    Id = "event1",
                    NodeType = "Event_OnEnter",
                    Properties = new Dictionary<string, object?>()
                },
                new ScriptNode
                {
                    Id = "action1",
                    NodeType = "Action_ShowMessage",
                    Properties = new Dictionary<string, object?>()  // Missing required "Message"
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    FromNodeId = "event1",
                    FromPortName = "Exec",
                    ToNodeId = "action1",
                    ToPortName = "Exec"
                }
            }
        };

        var result = ScriptValidator.Validate(script);

        Assert.True(result.IncompleteNodes.Count > 0);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void IsEventNode_ForEventType_ReturnsTrue()
    {
        Assert.True(ScriptValidator.IsEventNode("Event_OnEnter"));
        Assert.True(ScriptValidator.IsEventNode("Event_OnTake"));
        Assert.True(ScriptValidator.IsEventNode("Event_OnGameStart"));
    }

    [Fact]
    public void IsEventNode_ForNonEventType_ReturnsFalse()
    {
        Assert.False(ScriptValidator.IsEventNode("Action_ShowMessage"));
        Assert.False(ScriptValidator.IsEventNode("Condition_HasItem"));
        Assert.False(ScriptValidator.IsEventNode("Flow_Branch"));
    }

    [Fact]
    public void IsActionNode_ForActionType_ReturnsTrue()
    {
        Assert.True(ScriptValidator.IsActionNode("Action_ShowMessage"));
        Assert.True(ScriptValidator.IsActionNode("Action_GiveItem"));
        Assert.True(ScriptValidator.IsActionNode("Action_TeleportPlayer"));
    }

    [Fact]
    public void IsActionNode_ForNonActionType_ReturnsFalse()
    {
        Assert.False(ScriptValidator.IsActionNode("Event_OnEnter"));
        Assert.False(ScriptValidator.IsActionNode("Condition_HasItem"));
        Assert.False(ScriptValidator.IsActionNode("Flow_Branch"));
    }
}
