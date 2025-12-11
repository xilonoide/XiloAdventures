using System.Collections.Generic;
using System.Linq;

namespace XiloAdventures.Engine.Models;

/// <summary>
/// Registro de tipos de nodos disponibles en el editor de scripts.
/// </summary>
public static class NodeTypeRegistry
{
    private static readonly Dictionary<string, NodeTypeDefinition> _types = new(StringComparer.OrdinalIgnoreCase);

    static NodeTypeRegistry()
    {
        RegisterEventNodes();
        RegisterConditionNodes();
        RegisterActionNodes();
        RegisterFlowNodes();
        RegisterVariableNodes();
        RegisterDataComparisonNodes();
        RegisterMathNodes();
        RegisterLogicNodes();
        RegisterDataActionNodes();
        RegisterSelectionNodes();
    }

    public static IReadOnlyDictionary<string, NodeTypeDefinition> Types => _types;

    public static NodeTypeDefinition? GetNodeType(string typeId)
    {
        return _types.TryGetValue(typeId, out var def) ? def : null;
    }

    public static IEnumerable<NodeTypeDefinition> GetNodesForOwnerType(string ownerType)
    {
        return _types.Values.Where(n =>
            n.OwnerTypes.Contains("*") || n.OwnerTypes.Contains(ownerType));
    }

    public static IEnumerable<NodeTypeDefinition> GetNodesByCategory(NodeCategory category)
    {
        return _types.Values.Where(n => n.Category == category);
    }

    private static void Register(NodeTypeDefinition def)
    {
        _types[def.TypeId] = def;
    }

    #region Event Nodes

    private static void RegisterEventNodes()
    {
        // === GAME EVENTS ===
        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnGameStart",
            DisplayName = "Al Iniciar Juego",
            Description = "Se ejecuta cuando el jugador inicia una nueva partida",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Game" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnGameEnd",
            DisplayName = "Al Terminar Juego",
            Description = "Se ejecuta cuando el jugador termina la partida",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Game" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_EveryMinute",
            DisplayName = "Cada Minuto",
            Description = "Se ejecuta cada minuto de tiempo de juego",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Game" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_EveryHour",
            DisplayName = "Cada Hora",
            Description = "Se ejecuta cada hora de tiempo de juego",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Game" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnTurnStart",
            DisplayName = "Al Inicio del Turno",
            Description = "Se ejecuta al inicio de cada turno del jugador",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "*" },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" },
                new NodePort { Name = "TurnNumber", PortType = PortType.Data, DataType = "int", Label = "Turno" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnWeatherChange",
            DisplayName = "Al Cambiar Clima",
            Description = "Se ejecuta cuando cambia el clima",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Game" },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" },
                new NodePort { Name = "NewWeather", PortType = PortType.Data, DataType = "string", Label = "Clima" }
            }
        });

        // === ROOM EVENTS ===
        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnEnter",
            DisplayName = "Al Entrar",
            Description = "Se ejecuta cuando el jugador entra en la sala",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Room" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnExit",
            DisplayName = "Al Salir",
            Description = "Se ejecuta cuando el jugador sale de la sala",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Room" },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" },
                new NodePort { Name = "Direction", PortType = PortType.Data, DataType = "string", Label = "Direccion" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnLook",
            DisplayName = "Al Mirar",
            Description = "Se ejecuta cuando el jugador mira la sala",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Room" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        // === DOOR EVENTS ===
        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnDoorOpen",
            DisplayName = "Al Abrir Puerta",
            Description = "Se ejecuta cuando se abre la puerta",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Door" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnDoorClose",
            DisplayName = "Al Cerrar Puerta",
            Description = "Se ejecuta cuando se cierra la puerta",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Door" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnDoorLock",
            DisplayName = "Al Bloquear Puerta",
            Description = "Se ejecuta cuando se bloquea la puerta",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Door" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnDoorUnlock",
            DisplayName = "Al Desbloquear Puerta",
            Description = "Se ejecuta cuando se desbloquea la puerta",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Door" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnDoorKnock",
            DisplayName = "Al Llamar Puerta",
            Description = "Se ejecuta cuando el jugador llama a la puerta",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Door" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        // === NPC EVENTS ===
        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnTalk",
            DisplayName = "Al Hablar",
            Description = "Se ejecuta cuando el jugador habla con el NPC",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Npc" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnNpcAttack",
            DisplayName = "Al Atacar NPC",
            Description = "Se ejecuta cuando el jugador ataca al NPC",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Npc" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnNpcDeath",
            DisplayName = "Al Morir NPC",
            Description = "Se ejecuta cuando el NPC muere",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Npc" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnNpcSee",
            DisplayName = "Al Ver Jugador",
            Description = "Se ejecuta cuando el NPC ve al jugador entrar en su sala",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Npc" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        // === OBJECT EVENTS ===
        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnTake",
            DisplayName = "Al Coger",
            Description = "Se ejecuta cuando el jugador coge el objeto",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "GameObject" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnDrop",
            DisplayName = "Al Soltar",
            Description = "Se ejecuta cuando el jugador suelta el objeto",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "GameObject" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnUse",
            DisplayName = "Al Usar",
            Description = "Se ejecuta cuando el jugador usa el objeto",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "GameObject" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnExamine",
            DisplayName = "Al Examinar",
            Description = "Se ejecuta cuando el jugador examina el objeto",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "GameObject" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnContainerOpen",
            DisplayName = "Al Abrir Contenedor",
            Description = "Se ejecuta cuando el jugador abre el contenedor",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "GameObject" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnContainerClose",
            DisplayName = "Al Cerrar Contenedor",
            Description = "Se ejecuta cuando el jugador cierra el contenedor",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "GameObject" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        // === QUEST EVENTS ===
        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnQuestStart",
            DisplayName = "Al Iniciar Mision",
            Description = "Se ejecuta cuando se inicia la mision",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Quest" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnQuestComplete",
            DisplayName = "Al Completar Mision",
            Description = "Se ejecuta cuando se completa la mision",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Quest" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnQuestFail",
            DisplayName = "Al Fallar Mision",
            Description = "Se ejecuta cuando se falla la mision",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Quest" },
            OutputPorts = new[] { new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" } }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Event_OnObjectiveComplete",
            DisplayName = "Al Completar Objetivo",
            Description = "Se ejecuta cuando se completa un objetivo de la mision",
            Category = NodeCategory.Event,
            OwnerTypes = new[] { "Quest" },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" },
                new NodePort { Name = "ObjectiveIndex", PortType = PortType.Data, DataType = "int", Label = "Indice" }
            }
        });
    }

    #endregion

    #region Condition Nodes

    private static void RegisterConditionNodes()
    {
        Register(new NodeTypeDefinition
        {
            TypeId = "Condition_HasItem",
            DisplayName = "Tiene Objeto",
            Description = "Verifica si el jugador tiene un objeto en su inventario",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "True", PortType = PortType.Execution, Label = "Si" },
                new NodePort { Name = "False", PortType = PortType.Execution, Label = "No" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "ObjectId", DisplayName = "Objeto", DataType = "string", EntityType = "GameObject" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Condition_IsInRoom",
            DisplayName = "Esta en Sala",
            Description = "Verifica si el jugador esta en una sala especifica",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "True", PortType = PortType.Execution, Label = "Si" },
                new NodePort { Name = "False", PortType = PortType.Execution, Label = "No" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "RoomId", DisplayName = "Sala", DataType = "string", EntityType = "Room" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Condition_IsQuestStatus",
            DisplayName = "Estado de Mision",
            Description = "Verifica el estado de una mision",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "True", PortType = PortType.Execution, Label = "Si" },
                new NodePort { Name = "False", PortType = PortType.Execution, Label = "No" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "QuestId", DisplayName = "Mision", DataType = "string", EntityType = "Quest" },
                new NodePropertyDefinition { Name = "Status", DisplayName = "Estado", DataType = "select", Options = new[] { "NotStarted", "InProgress", "Completed", "Failed" } }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Condition_HasFlag",
            DisplayName = "Tiene Flag",
            Description = "Verifica si un flag esta activo",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "True", PortType = PortType.Execution, Label = "Si" },
                new NodePort { Name = "False", PortType = PortType.Execution, Label = "No" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "FlagName", DisplayName = "Nombre del Flag", DataType = "string" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Condition_CompareCounter",
            DisplayName = "Comparar Contador",
            Description = "Compara el valor de un contador",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "True", PortType = PortType.Execution, Label = "Si" },
                new NodePort { Name = "False", PortType = PortType.Execution, Label = "No" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "CounterName", DisplayName = "Contador", DataType = "string" },
                new NodePropertyDefinition { Name = "Operator", DisplayName = "Operador", DataType = "select", Options = new[] { "==", "!=", "<", "<=", ">", ">=" } },
                new NodePropertyDefinition { Name = "Value", DisplayName = "Valor", DataType = "int", DefaultValue = 0 }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Condition_IsTimeOfDay",
            DisplayName = "Es Hora del Dia",
            Description = "Verifica la hora del juego",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "True", PortType = PortType.Execution, Label = "Si" },
                new NodePort { Name = "False", PortType = PortType.Execution, Label = "No" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "TimeRange", DisplayName = "Periodo", DataType = "select", Options = new[] { "Manana", "Tarde", "Noche", "Madrugada" } }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Condition_IsDoorOpen",
            DisplayName = "Puerta Abierta",
            Description = "Verifica si una puerta esta abierta",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "True", PortType = PortType.Execution, Label = "Si" },
                new NodePort { Name = "False", PortType = PortType.Execution, Label = "No" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "DoorId", DisplayName = "Puerta", DataType = "string", EntityType = "Door" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Condition_IsNpcVisible",
            DisplayName = "NPC Visible",
            Description = "Verifica si un NPC es visible",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "True", PortType = PortType.Execution, Label = "Si" },
                new NodePort { Name = "False", PortType = PortType.Execution, Label = "No" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "NpcId", DisplayName = "NPC", DataType = "string", EntityType = "Npc" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Condition_Random",
            DisplayName = "Probabilidad",
            Description = "Se cumple con una probabilidad dada",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "True", PortType = PortType.Execution, Label = "Si" },
                new NodePort { Name = "False", PortType = PortType.Execution, Label = "No" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "Probability", DisplayName = "Probabilidad (%)", DataType = "int", DefaultValue = 50 }
            }
        });
    }

    #endregion

    #region Action Nodes

    private static void RegisterActionNodes()
    {
        Register(new NodeTypeDefinition
        {
            TypeId = "Action_ShowMessage",
            DisplayName = "Mostrar Mensaje",
            Description = "Muestra un mensaje al jugador",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "Message", DisplayName = "Mensaje", DataType = "string", DefaultValue = "" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_GiveItem",
            DisplayName = "Dar Objeto",
            Description = "Da un objeto al jugador",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "ObjectId", DisplayName = "Objeto", DataType = "string", EntityType = "GameObject" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_RemoveItem",
            DisplayName = "Quitar Objeto",
            Description = "Quita un objeto del inventario del jugador",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "ObjectId", DisplayName = "Objeto", DataType = "string", EntityType = "GameObject" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_TeleportPlayer",
            DisplayName = "Teletransportar Jugador",
            Description = "Mueve al jugador a otra sala",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "RoomId", DisplayName = "Sala destino", DataType = "string", EntityType = "Room" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_MoveNpc",
            DisplayName = "Mover NPC",
            Description = "Mueve un NPC a otra sala",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "NpcId", DisplayName = "NPC", DataType = "string", EntityType = "Npc" },
                new NodePropertyDefinition { Name = "RoomId", DisplayName = "Sala destino", DataType = "string", EntityType = "Room" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_SetFlag",
            DisplayName = "Establecer Flag",
            Description = "Activa o desactiva un flag",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "FlagName", DisplayName = "Nombre del Flag", DataType = "string" },
                new NodePropertyDefinition { Name = "Value", DisplayName = "Valor", DataType = "bool", DefaultValue = true }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_SetCounter",
            DisplayName = "Establecer Contador",
            Description = "Establece el valor de un contador",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "CounterName", DisplayName = "Contador", DataType = "string" },
                new NodePropertyDefinition { Name = "Value", DisplayName = "Valor", DataType = "int", DefaultValue = 0 }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_IncrementCounter",
            DisplayName = "Incrementar Contador",
            Description = "Incrementa o decrementa un contador",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "CounterName", DisplayName = "Contador", DataType = "string" },
                new NodePropertyDefinition { Name = "Amount", DisplayName = "Cantidad", DataType = "int", DefaultValue = 1 }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_PlaySound",
            DisplayName = "Reproducir Sonido",
            Description = "Reproduce un efecto de sonido",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "SoundId", DisplayName = "Sonido", DataType = "string", EntityType = "Fx" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_StartQuest",
            DisplayName = "Iniciar Mision",
            Description = "Inicia una mision",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "QuestId", DisplayName = "Mision", DataType = "string", EntityType = "Quest" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_CompleteQuest",
            DisplayName = "Completar Mision",
            Description = "Marca una mision como completada",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "QuestId", DisplayName = "Mision", DataType = "string", EntityType = "Quest" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_FailQuest",
            DisplayName = "Fallar Mision",
            Description = "Marca una mision como fallida",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "QuestId", DisplayName = "Mision", DataType = "string", EntityType = "Quest" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_OpenDoor",
            DisplayName = "Abrir Puerta",
            Description = "Abre una puerta",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "DoorId", DisplayName = "Puerta", DataType = "string", EntityType = "Door" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_CloseDoor",
            DisplayName = "Cerrar Puerta",
            Description = "Cierra una puerta",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "DoorId", DisplayName = "Puerta", DataType = "string", EntityType = "Door" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_LockDoor",
            DisplayName = "Bloquear Puerta",
            Description = "Bloquea una puerta",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "DoorId", DisplayName = "Puerta", DataType = "string", EntityType = "Door" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_UnlockDoor",
            DisplayName = "Desbloquear Puerta",
            Description = "Desbloquea una puerta",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "DoorId", DisplayName = "Puerta", DataType = "string", EntityType = "Door" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_SetNpcVisible",
            DisplayName = "Visibilidad NPC",
            Description = "Muestra u oculta un NPC",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "NpcId", DisplayName = "NPC", DataType = "string", EntityType = "Npc" },
                new NodePropertyDefinition { Name = "Visible", DisplayName = "Visible", DataType = "bool", DefaultValue = true }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_SetObjectVisible",
            DisplayName = "Visibilidad Objeto",
            Description = "Muestra u oculta un objeto",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "ObjectId", DisplayName = "Objeto", DataType = "string", EntityType = "GameObject" },
                new NodePropertyDefinition { Name = "Visible", DisplayName = "Visible", DataType = "bool", DefaultValue = true }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_AddGold",
            DisplayName = "Dar Oro",
            Description = "Da oro al jugador",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "Amount", DisplayName = "Cantidad", DataType = "int", DefaultValue = 10 }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_RemoveGold",
            DisplayName = "Quitar Oro",
            Description = "Quita oro al jugador",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "Amount", DisplayName = "Cantidad", DataType = "int", DefaultValue = 10 }
            }
        });
    }

    #endregion

    #region Flow Nodes

    private static void RegisterFlowNodes()
    {
        Register(new NodeTypeDefinition
        {
            TypeId = "Flow_Branch",
            DisplayName = "Bifurcacion",
            Description = "Bifurca el flujo segun una condicion (usar con nodos de condicion)",
            Category = NodeCategory.Flow,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" },
                new NodePort { Name = "Condition", PortType = PortType.Data, DataType = "bool", Label = "Condicion" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "True", PortType = PortType.Execution, Label = "Si" },
                new NodePort { Name = "False", PortType = PortType.Execution, Label = "No" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Flow_Sequence",
            DisplayName = "Secuencia",
            Description = "Ejecuta multiples salidas en orden",
            Category = NodeCategory.Flow,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Then0", PortType = PortType.Execution, Label = "1" },
                new NodePort { Name = "Then1", PortType = PortType.Execution, Label = "2" },
                new NodePort { Name = "Then2", PortType = PortType.Execution, Label = "3" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Flow_Delay",
            DisplayName = "Esperar",
            Description = "Espera un tiempo antes de continuar",
            Category = NodeCategory.Flow,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "Seconds", DisplayName = "Segundos", DataType = "float", DefaultValue = 1.0f }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Flow_RandomBranch",
            DisplayName = "Rama Aleatoria",
            Description = "Elige una salida aleatoriamente",
            Category = NodeCategory.Flow,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Out0", PortType = PortType.Execution, Label = "1" },
                new NodePort { Name = "Out1", PortType = PortType.Execution, Label = "2" },
                new NodePort { Name = "Out2", PortType = PortType.Execution, Label = "3" }
            }
        });
    }

    #endregion

    #region Variable Nodes

    private static void RegisterVariableNodes()
    {
        Register(new NodeTypeDefinition
        {
            TypeId = "Variable_GetFlag",
            DisplayName = "Obtener Flag",
            Description = "Obtiene el valor de un flag",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = Array.Empty<NodePort>(),
            OutputPorts = new[]
            {
                new NodePort { Name = "Value", PortType = PortType.Data, DataType = "bool", Label = "Valor" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "FlagName", DisplayName = "Nombre del Flag", DataType = "string" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Variable_GetCounter",
            DisplayName = "Obtener Contador",
            Description = "Obtiene el valor de un contador",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = Array.Empty<NodePort>(),
            OutputPorts = new[]
            {
                new NodePort { Name = "Value", PortType = PortType.Data, DataType = "int", Label = "Valor" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "CounterName", DisplayName = "Contador", DataType = "string" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Variable_GetCurrentRoom",
            DisplayName = "Sala Actual",
            Description = "Obtiene la sala actual del jugador",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = Array.Empty<NodePort>(),
            OutputPorts = new[]
            {
                new NodePort { Name = "RoomId", PortType = PortType.Data, DataType = "string", Label = "Sala" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Variable_GetGameHour",
            DisplayName = "Hora de Juego",
            Description = "Obtiene la hora actual del juego",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = Array.Empty<NodePort>(),
            OutputPorts = new[]
            {
                new NodePort { Name = "Hour", PortType = PortType.Data, DataType = "int", Label = "Hora" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Variable_GetPlayerGold",
            DisplayName = "Oro del Jugador",
            Description = "Obtiene el oro actual del jugador",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = Array.Empty<NodePort>(),
            OutputPorts = new[]
            {
                new NodePort { Name = "Gold", PortType = PortType.Data, DataType = "int", Label = "Oro" }
            }
        });

        // Constantes
        Register(new NodeTypeDefinition
        {
            TypeId = "Variable_ConstantInt",
            DisplayName = "Entero Constante",
            Description = "Un valor entero constante",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = Array.Empty<NodePort>(),
            OutputPorts = new[]
            {
                new NodePort { Name = "Value", PortType = PortType.Data, DataType = "int", Label = "Valor" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "Value", DisplayName = "Valor", DataType = "int", DefaultValue = 0 }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Variable_ConstantBool",
            DisplayName = "Booleano Constante",
            Description = "Un valor booleano constante (verdadero/falso)",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = Array.Empty<NodePort>(),
            OutputPorts = new[]
            {
                new NodePort { Name = "Value", PortType = PortType.Data, DataType = "bool", Label = "Valor" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "Value", DisplayName = "Valor", DataType = "bool", DefaultValue = false }
            }
        });
    }

    #endregion

    #region Comparaciones con entrada de datos

    private static void RegisterDataComparisonNodes()
    {
        // Comparar dos enteros
        Register(new NodeTypeDefinition
        {
            TypeId = "Compare_Int",
            DisplayName = "Comparar Enteros",
            Description = "Compara dos valores enteros y produce un resultado booleano",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "int", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "int", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "bool", Label = "Resultado" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "Operator", DisplayName = "Operador", DataType = "select", Options = new[] { "==", "!=", "<", "<=", ">", ">=" } }
            }
        });

        // Comparar oro del jugador con un valor
        Register(new NodeTypeDefinition
        {
            TypeId = "Compare_PlayerGold",
            DisplayName = "Comparar Oro",
            Description = "Compara el oro del jugador con un valor",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "CompareValue", PortType = PortType.Data, DataType = "int", Label = "Comparar con" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "bool", Label = "Resultado" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "Operator", DisplayName = "Operador", DataType = "select", Options = new[] { "==", "!=", "<", "<=", ">", ">=" } }
            }
        });

        // Comparar contador con un valor
        Register(new NodeTypeDefinition
        {
            TypeId = "Compare_Counter",
            DisplayName = "Comparar Contador",
            Description = "Compara un contador con un valor",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "CompareValue", PortType = PortType.Data, DataType = "int", Label = "Comparar con" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "bool", Label = "Resultado" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "CounterName", DisplayName = "Contador", DataType = "string" },
                new NodePropertyDefinition { Name = "Operator", DisplayName = "Operador", DataType = "select", Options = new[] { "==", "!=", "<", "<=", ">", ">=" } }
            }
        });
    }

    #endregion

    #region Operaciones Matematicas

    private static void RegisterMathNodes()
    {
        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Add",
            DisplayName = "Sumar",
            Description = "Suma dos valores enteros",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "int", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "int", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Subtract",
            DisplayName = "Restar",
            Description = "Resta dos valores enteros (A - B)",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "int", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "int", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Multiply",
            DisplayName = "Multiplicar",
            Description = "Multiplica dos valores enteros",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "int", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "int", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Divide",
            DisplayName = "Dividir",
            Description = "Divide dos valores enteros (A / B)",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "int", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "int", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Modulo",
            DisplayName = "Modulo",
            Description = "Obtiene el resto de la division (A % B)",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "int", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "int", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Negate",
            DisplayName = "Negar",
            Description = "Cambia el signo de un valor entero",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Value", PortType = PortType.Data, DataType = "int", Label = "Valor" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Abs",
            DisplayName = "Valor Absoluto",
            Description = "Obtiene el valor absoluto de un entero",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Value", PortType = PortType.Data, DataType = "int", Label = "Valor" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Min",
            DisplayName = "Minimo",
            Description = "Obtiene el menor de dos valores",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "int", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "int", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Max",
            DisplayName = "Maximo",
            Description = "Obtiene el mayor de dos valores",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "int", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "int", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Clamp",
            DisplayName = "Limitar",
            Description = "Limita un valor entre un minimo y un maximo",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Value", PortType = PortType.Data, DataType = "int", Label = "Valor" },
                new NodePort { Name = "Min", PortType = PortType.Data, DataType = "int", Label = "Min" },
                new NodePort { Name = "Max", PortType = PortType.Data, DataType = "int", Label = "Max" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Math_Random",
            DisplayName = "Aleatorio",
            Description = "Genera un numero aleatorio entre Min y Max (inclusive)",
            Category = NodeCategory.Variable,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Min", PortType = PortType.Data, DataType = "int", Label = "Min" },
                new NodePort { Name = "Max", PortType = PortType.Data, DataType = "int", Label = "Max" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });
    }

    #endregion

    #region Operaciones Logicas

    private static void RegisterLogicNodes()
    {
        Register(new NodeTypeDefinition
        {
            TypeId = "Logic_And",
            DisplayName = "Y (AND)",
            Description = "Devuelve verdadero si ambas entradas son verdaderas",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "bool", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "bool", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "bool", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Logic_Or",
            DisplayName = "O (OR)",
            Description = "Devuelve verdadero si al menos una entrada es verdadera",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "bool", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "bool", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "bool", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Logic_Not",
            DisplayName = "No (NOT)",
            Description = "Invierte el valor booleano",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Value", PortType = PortType.Data, DataType = "bool", Label = "Valor" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "bool", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Logic_Xor",
            DisplayName = "O Exclusivo (XOR)",
            Description = "Devuelve verdadero si exactamente una entrada es verdadera",
            Category = NodeCategory.Condition,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "A", PortType = PortType.Data, DataType = "bool", Label = "A" },
                new NodePort { Name = "B", PortType = PortType.Data, DataType = "bool", Label = "B" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "bool", Label = "Resultado" }
            }
        });
    }

    #endregion

    #region Acciones con entrada de datos

    private static void RegisterDataActionNodes()
    {
        Register(new NodeTypeDefinition
        {
            TypeId = "Action_SetGold",
            DisplayName = "Establecer Oro",
            Description = "Establece el oro del jugador a un valor especifico",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" },
                new NodePort { Name = "Amount", PortType = PortType.Data, DataType = "int", Label = "Cantidad" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_AddGoldData",
            DisplayName = "Dar Oro (Datos)",
            Description = "Da oro al jugador usando un valor de conexion",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" },
                new NodePort { Name = "Amount", PortType = PortType.Data, DataType = "int", Label = "Cantidad" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_RemoveGoldData",
            DisplayName = "Quitar Oro (Datos)",
            Description = "Quita oro al jugador usando un valor de conexion",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" },
                new NodePort { Name = "Amount", PortType = PortType.Data, DataType = "int", Label = "Cantidad" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_SetCounterData",
            DisplayName = "Establecer Contador (Datos)",
            Description = "Establece un contador a un valor especifico usando conexion",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" },
                new NodePort { Name = "Value", PortType = PortType.Data, DataType = "int", Label = "Valor" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "CounterName", DisplayName = "Contador", DataType = "string" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Action_IncrementCounterData",
            DisplayName = "Incrementar Contador (Datos)",
            Description = "Incrementa un contador usando un valor de conexion",
            Category = NodeCategory.Action,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" },
                new NodePort { Name = "Amount", PortType = PortType.Data, DataType = "int", Label = "Cantidad" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Exec", PortType = PortType.Execution, Label = "" }
            },
            Properties = new[]
            {
                new NodePropertyDefinition { Name = "CounterName", DisplayName = "Contador", DataType = "string" }
            }
        });
    }

    #endregion

    #region Nodos de seleccion

    private static void RegisterSelectionNodes()
    {
        Register(new NodeTypeDefinition
        {
            TypeId = "Select_Int",
            DisplayName = "Seleccionar Entero",
            Description = "Selecciona entre dos valores enteros segun una condicion",
            Category = NodeCategory.Flow,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Condition", PortType = PortType.Data, DataType = "bool", Label = "Condicion" },
                new NodePort { Name = "True", PortType = PortType.Data, DataType = "int", Label = "Si verdadero" },
                new NodePort { Name = "False", PortType = PortType.Data, DataType = "int", Label = "Si falso" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "int", Label = "Resultado" }
            }
        });

        Register(new NodeTypeDefinition
        {
            TypeId = "Select_Bool",
            DisplayName = "Seleccionar Booleano",
            Description = "Selecciona entre dos valores booleanos segun una condicion",
            Category = NodeCategory.Flow,
            OwnerTypes = new[] { "*" },
            InputPorts = new[]
            {
                new NodePort { Name = "Condition", PortType = PortType.Data, DataType = "bool", Label = "Condicion" },
                new NodePort { Name = "True", PortType = PortType.Data, DataType = "bool", Label = "Si verdadero" },
                new NodePort { Name = "False", PortType = PortType.Data, DataType = "bool", Label = "Si falso" }
            },
            OutputPorts = new[]
            {
                new NodePort { Name = "Result", PortType = PortType.Data, DataType = "bool", Label = "Resultado" }
            }
        });
    }

    #endregion
}
