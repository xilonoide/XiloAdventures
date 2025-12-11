using System.Windows;

namespace XiloAdventures.Wpf.Windows;

public partial class PromptGeneratorWindow : Window
{
    private const string GeneratorPrompt = @"Necesito que generes un JSON para un motor de aventuras de texto/gráficas. El JSON debe representar un mundo completo de una pequeña aventura (tipo escape room o exploración de una mansión misteriosa).

## ESTRUCTURA DEL JSON

```json
{
  ""Game"": {
    ""Id"": ""game"",
    ""Name"": ""Nombre del juego"",
    ""Description"": ""Descripción general"",
    ""StartingRoomId"": ""id_sala_inicial""
  },
  ""Rooms"": [
    {
      ""Id"": ""room_id"",
      ""Name"": ""Nombre visible"",
      ""Description"": ""Descripción de la sala"",
      ""MapX"": 0,
      ""MapY"": 0,
      ""Exits"": [
        { ""TargetRoomId"": ""otra_sala"", ""Direction"": ""norte"", ""DoorId"": ""door_id o null"" }
      ]
    }
  ],
  ""Objects"": [
    {
      ""Id"": ""obj_id"",
      ""Name"": ""Nombre"",
      ""Description"": ""Descripción"",
      ""Type"": ""tipo (ej: llave, arma, comida, etc.)"",
      ""RoomId"": ""room_id o null si está en inventario/contenedor"",
      ""IsPickable"": true,
      ""IsContainer"": false,
      ""ContainedObjectIds"": []
    }
  ],
  ""Npcs"": [
    {
      ""Id"": ""npc_id"",
      ""Name"": ""Nombre"",
      ""Description"": ""Descripción"",
      ""RoomId"": ""room_id""
    }
  ],
  ""Doors"": [
    {
      ""Id"": ""door_id"",
      ""Name"": ""Nombre de la puerta"",
      ""RoomIdA"": ""sala_1"",
      ""RoomIdB"": ""sala_2"",
      ""IsOpen"": false,
      ""IsLocked"": true,
      ""KeyItemId"": ""obj_llave o null""
    }
  ],
  ""Quests"": [
    {
      ""Id"": ""quest_id"",
      ""Name"": ""Nombre misión"",
      ""Description"": ""Descripción""
    }
  ],
  ""Scripts"": [
    {
      ""Id"": ""script_id"",
      ""Name"": ""Nombre descriptivo"",
      ""OwnerType"": ""Room|GameObject|Npc|Door|Quest|Game"",
      ""OwnerId"": ""id_del_dueño"",
      ""Nodes"": [
        {
          ""Id"": ""node_uuid"",
          ""NodeType"": ""tipo_de_nodo"",
          ""X"": 100,
          ""Y"": 100,
          ""Properties"": { ""propiedad"": ""valor"" }
        }
      ],
      ""Connections"": [
        {
          ""FromNodeId"": ""node_id"",
          ""FromPortName"": ""Exec"",
          ""ToNodeId"": ""otro_node_id"",
          ""ToPortName"": ""Exec""
        }
      ]
    }
  ]
}
```

## TIPOS DE NODOS DISPONIBLES PARA SCRIPTS

### Eventos (inician el script):
- `Event_OnInteract` - Al interactuar con el objeto/NPC
- `Event_OnEnterRoom` - Al entrar a la sala
- `Event_OnExitRoom` - Al salir de la sala
- `Event_OnTalk` - Al hablar con NPC
- `Event_OnUseItem` - Al usar un objeto (Property: ItemId)
- `Event_OnPickup` - Al recoger objeto
- `Event_OnDrop` - Al soltar objeto
- `Event_OnGameStart` - Al iniciar el juego
- `Event_OnDoorOpen` - Al abrir puerta
- `Event_OnDoorClose` - Al cerrar puerta

### Condiciones (bifurcan el flujo, salidas: ""True""/""False""):
- `Condition_CheckFlag` - Properties: { ""FlagName"": ""nombre"" }
- `Condition_HasItem` - Properties: { ""ItemId"": ""obj_id"" }
- `Condition_IsDoorOpen` - Properties: { ""DoorId"": ""door_id"" }
- `Condition_CompareCounter` - Properties: { ""CounterName"": ""nombre"", ""Operator"": "">="", ""Value"": 5 }
- `Condition_IsInRoom` - Properties: { ""RoomId"": ""room_id"" }

### Acciones:
- `Action_ShowMessage` - Properties: { ""Message"": ""texto"" }
- `Action_GiveItem` - Properties: { ""ItemId"": ""obj_id"" }
- `Action_RemoveItem` - Properties: { ""ItemId"": ""obj_id"" }
- `Action_OpenDoor` - Properties: { ""DoorId"": ""door_id"" }
- `Action_CloseDoor` - Properties: { ""DoorId"": ""door_id"" }
- `Action_SetFlag` - Properties: { ""FlagName"": ""nombre"", ""Value"": true }
- `Action_SetCounter` - Properties: { ""CounterName"": ""nombre"", ""Value"": 0 }
- `Action_IncrementCounter` - Properties: { ""CounterName"": ""nombre"", ""Amount"": 1 }
- `Action_MoveToRoom` - Properties: { ""RoomId"": ""room_id"" }
- `Action_StartQuest` - Properties: { ""QuestId"": ""quest_id"" }
- `Action_CompleteQuest` - Properties: { ""QuestId"": ""quest_id"" }

### Control de flujo:
- `Flow_Sequence` - Ejecuta múltiples salidas en orden (salidas: ""Then1"", ""Then2"", ""Then3"")
- `Flow_Branch` - Bifurca según booleano (entrada ""Condition"", salidas ""True""/""False"")

## REQUISITOS DEL MUNDO

Genera un mundo con:
1. **5-8 salas** conectadas lógicamente con MapX/MapY formando un mapa coherente
2. **2-3 puertas** - al menos una cerrada con llave
3. **6-10 objetos** incluyendo:
   - Al menos 1 llave
   - Al menos 1 contenedor (cofre/caja) con objetos dentro
   - Objetos para puzzles
4. **2-3 NPCs** con personalidad y diálogos
5. **1-2 misiones**
6. **Scripts variados** que demuestren:
   - Un objeto que al examinarlo muestra mensaje
   - Una puerta que requiere llave para abrir
   - Un NPC que da un objeto si tienes cierto item
   - Un contenedor que revela contenido al abrirlo
   - Un evento al entrar a cierta sala
   - Un puzzle con contador (ej: activar 3 palancas)
   - Uso de flags para recordar acciones

## NOTAS IMPORTANTES
- Los IDs deben ser snake_case únicos
- **Game.StartingRoomId DEBE coincidir con el Id de una sala existente** - El jugador empieza ahí
- Las coordenadas MapX/MapY deben formar un mapa navegable (ej: sala al norte = Y-1)
- Direcciones válidas: norte, sur, este, oeste, arriba, abajo
- **Los objetos que son llaves DEBEN tener Type=""llave""**
- **Toda puerta con cerradura (KeyItemId no nulo) DEBE tener un objeto llave existente asignado**
- Los nodos de scripts necesitan posiciones X,Y para visualización (separados ~200px)
- Conecta los nodos: evento → condiciones/acciones mediante puerto ""Exec""
- El puerto de salida de eventos y acciones es ""Exec"", el de entrada también es ""Exec""

## FORMATO DE SALIDA

**IMPORTANTE: Genera el resultado como un archivo descargable con extensión .xaw** (no como texto en el chat).
- El JSON debe ser válido y parseable
- Sin markdown code blocks, solo el JSON puro
- El archivo .xaw se abrirá directamente en el editor XiloAdventures

Genera un mundo temático interesante (mansión embrujada, templo antiguo, nave espacial, etc.) con puzzles lógicos y una pequeña historia.";

    public PromptGeneratorWindow()
    {
        InitializeComponent();
        PromptTextBox.Text = GeneratorPrompt;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(PromptTextBox.Text);

        // Visual feedback
        var originalContent = CopyButton.Content;
        CopyButton.Content = "✓ Copiado!";
        CopyButton.IsEnabled = false;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromSeconds(2)
        };
        timer.Tick += (s, args) =>
        {
            CopyButton.Content = originalContent;
            CopyButton.IsEnabled = true;
            timer.Stop();
        };
        timer.Start();
    }

}
