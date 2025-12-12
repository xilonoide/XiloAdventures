using System.Windows;
using System.Windows.Input;

namespace XiloAdventures.Wpf.Windows;

public partial class PromptGeneratorWindow : Window
{
    private bool _isUpdatingFromRoomCount = false;

    private const string PromptTemplate = @"Necesito que generes un JSON para un motor de aventuras de texto/gráficas. El JSON debe representar un mundo completo de una aventura con temática: **{THEME}**.

## ESTRUCTURA DEL JSON

```json
{
  ""Game"": {
    ""Id"": ""game"",
    ""Title"": ""Nombre del juego"",
    ""StartRoomId"": ""id_sala_inicial""
  },
  ""Player"": {
    ""Name"": ""Nombre aleatorio del protagonista"",
    ""Age"": 25,
    ""Weight"": 70,
    ""Height"": 170,
    ""Strength"": 20,
    ""Constitution"": 20,
    ""Intelligence"": 20,
    ""Dexterity"": 20,
    ""Charisma"": 20,
    ""InitialGold"": 50
  },
  ""Rooms"": [
    {
      ""Id"": ""room_id"",
      ""Name"": ""Nombre visible"",
      ""Description"": ""Descripción de la sala"",
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
      ""Type"": ""ninguno|arma|armadura|comida|bebida|ropa|llave|texto"",
      ""TextContent"": ""Contenido legible (solo para Type=texto)"",
      ""Gender"": ""Masculine|Feminine"",
      ""RoomId"": ""room_id o null si está en inventario/contenedor"",
      ""CanTake"": true,
      ""IsContainer"": false,
      ""ContainedObjectIds"": [],
      ""MaxCapacity"": 50000,
      ""Volume"": 10,
      ""Weight"": 100,
      ""Price"": 10
    }
  ],
  ""Npcs"": [
    {
      ""Id"": ""npc_id"",
      ""Name"": ""Nombre"",
      ""Description"": ""Descripción del NPC y su personalidad"",
      ""RoomId"": ""room_id"",
      ""IsShopkeeper"": false,
      ""ShopInventory"": [],
      ""BuyPriceMultiplier"": 0.5,
      ""SellPriceMultiplier"": 1.0
    }
  ],
  ""Doors"": [
    {
      ""Id"": ""door_id"",
      ""Name"": ""Nombre de la puerta"",
      ""Gender"": ""Feminine"",
      ""RoomIdA"": ""sala_1"",
      ""RoomIdB"": ""sala_2"",
      ""IsOpen"": false,
      ""IsLocked"": true,
      ""KeyObjectId"": ""obj_llave o null""
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
  ],
  ""RoomPositions"": {
    ""room_id"": { ""X"": 0, ""Y"": 0 },
    ""otra_sala"": { ""X"": 0, ""Y"": -150 }
  }
}
```

## TIPOS DE NODOS DISPONIBLES PARA SCRIPTS

**IMPORTANTE**: Usa EXACTAMENTE estos TypeId. Si usas nombres incorrectos, los nodos no funcionarán.

### Eventos (inician el script, solo puerto de salida ""Exec""):
- `Event_OnGameStart` - Al iniciar el juego (OwnerType: Game)
- `Event_OnEnter` - Al entrar a la sala (OwnerType: Room)
- `Event_OnExit` - Al salir de la sala (OwnerType: Room)
- `Event_OnLook` - Al mirar la sala (OwnerType: Room)
- `Event_OnTalk` - Al hablar con NPC (OwnerType: Npc)
- `Event_OnNpcSee` - Cuando el NPC ve al jugador entrar (OwnerType: Npc)
- `Event_OnTake` - Al coger objeto (OwnerType: GameObject)
- `Event_OnDrop` - Al soltar objeto (OwnerType: GameObject)
- `Event_OnUse` - Al usar objeto (OwnerType: GameObject)
- `Event_OnExamine` - Al examinar objeto (OwnerType: GameObject)
- `Event_OnContainerOpen` - Al abrir contenedor (OwnerType: GameObject)
- `Event_OnDoorOpen` - Al abrir puerta (OwnerType: Door)
- `Event_OnDoorClose` - Al cerrar puerta (OwnerType: Door)
- `Event_OnDoorUnlock` - Al desbloquear puerta (OwnerType: Door)
- `Event_OnQuestStart` - Al iniciar misión (OwnerType: Quest)
- `Event_OnQuestComplete` - Al completar misión (OwnerType: Quest)

### Condiciones (puerto entrada ""Exec"", puertos salida ""True""/""False""):
- `Condition_HasFlag` - Properties: { ""FlagName"": ""nombre"" }
- `Condition_HasItem` - Properties: { ""ObjectId"": ""obj_id"" }
- `Condition_IsDoorOpen` - Properties: { ""DoorId"": ""door_id"" }
- `Condition_CompareCounter` - Properties: { ""CounterName"": ""nombre"", ""Operator"": "">="", ""Value"": 5 }
- `Condition_IsInRoom` - Properties: { ""RoomId"": ""room_id"" }
- `Condition_IsQuestStatus` - Properties: { ""QuestId"": ""quest_id"", ""Status"": ""InProgress"" }
- `Condition_Random` - Properties: { ""Probability"": 50 }

### Acciones (puerto entrada ""Exec"", puerto salida ""Exec""):
- `Action_ShowMessage` - Properties: { ""Message"": ""texto"" }
- `Action_GiveItem` - Properties: { ""ObjectId"": ""obj_id"" }
- `Action_RemoveItem` - Properties: { ""ObjectId"": ""obj_id"" }
- `Action_OpenDoor` - Properties: { ""DoorId"": ""door_id"" }
- `Action_CloseDoor` - Properties: { ""DoorId"": ""door_id"" }
- `Action_UnlockDoor` - Properties: { ""DoorId"": ""door_id"" }
- `Action_LockDoor` - Properties: { ""DoorId"": ""door_id"" }
- `Action_SetFlag` - Properties: { ""FlagName"": ""nombre"", ""Value"": true }
- `Action_SetCounter` - Properties: { ""CounterName"": ""nombre"", ""Value"": 0 }
- `Action_IncrementCounter` - Properties: { ""CounterName"": ""nombre"", ""Amount"": 1 }
- `Action_TeleportPlayer` - Properties: { ""RoomId"": ""room_id"" }
- `Action_MoveNpc` - Properties: { ""NpcId"": ""npc_id"", ""RoomId"": ""room_id"" }
- `Action_StartQuest` - Properties: { ""QuestId"": ""quest_id"" }
- `Action_CompleteQuest` - Properties: { ""QuestId"": ""quest_id"" }
- `Action_SetNpcVisible` - Properties: { ""NpcId"": ""npc_id"", ""Visible"": true }
- `Action_SetObjectVisible` - Properties: { ""ObjectId"": ""obj_id"", ""Visible"": true }
- `Action_AddGold` - Properties: { ""Amount"": 10 }
- `Action_RemoveGold` - Properties: { ""Amount"": 10 }

### Control de flujo:
- `Flow_Sequence` - Ejecuta múltiples salidas en orden (salidas: ""Then0"", ""Then1"", ""Then2"")
- `Flow_Branch` - Bifurca según booleano (entrada ""Condition"", salidas ""True""/""False"")
- `Flow_RandomBranch` - Elige salida aleatoria (salidas: ""Out0"", ""Out1"", ""Out2"")

## REQUISITOS DEL MUNDO

Genera un mundo con temática ""{THEME}"" que contenga:
1. **Aproximadamente {ROOM_COUNT} salas** conectadas lógicamente formando un mapa coherente
2. **{DOOR_COUNT} puertas** - al menos una cerrada con llave si hay llaves disponibles
3. **{CONTAINER_COUNT} contenedores** (cofres, cajas, armarios...) con objetos dentro
4. **{TOTAL_OBJECTS} objetos** distribuidos así:
   - {WEAPON_COUNT} armas (Type=""arma"") - espadas, dagas, arcos...
   - {ARMOR_COUNT} armaduras (Type=""armadura"") - escudos, cascos, corazas...
   - {FOOD_COUNT} comida (Type=""comida"") - pan, manzana, carne...
   - {DRINK_COUNT} bebidas (Type=""bebida"") - pociones, agua, vino...
   - {CLOTHING_COUNT} ropa (Type=""ropa"") - capas, túnicas, botas...
   - {KEY_COUNT} llaves (Type=""llave"") - para abrir puertas/contenedores
   - {TEXT_COUNT} documentos legibles (Type=""texto"") - cartas, diarios, pergaminos... con TextContent
   - {OTHER_COUNT} objetos genéricos (Type=""ninguno"") - gemas, monedas, herramientas, objetos de puzzle...
5. **{NPC_COUNT} NPCs** con personalidad acorde a la temática. Si es comerciante, pon IsShopkeeper=true y añade objetos a ShopInventory
6. **{QUEST_COUNT} misiones**
7. **Scripts variados** que demuestren (usa los TypeId EXACTOS de la lista anterior):
   - Un objeto que al examinarlo (`Event_OnExamine`) muestra mensaje (`Action_ShowMessage`)
   - Un NPC que da un objeto (`Action_GiveItem`) si tienes cierto item (`Condition_HasItem`)
   - Un contenedor que al abrirlo (`Event_OnContainerOpen`) muestra mensaje
   - Un evento al entrar a cierta sala (`Event_OnEnter` en Room)
   - Un puzzle con contador (`Action_IncrementCounter` + `Condition_CompareCounter`)
   - Uso de flags para recordar acciones (`Action_SetFlag` + `Condition_HasFlag`)

## NOTAS IMPORTANTES
- Los IDs deben ser snake_case únicos
- **Game.StartRoomId DEBE coincidir con el Id de una sala existente** - El jugador empieza ahí
- Direcciones válidas: norte, sur, este, oeste, arriba, abajo

### Coordenadas en RoomPositions para visualización del mapa
**IMPORTANTE**: Las posiciones van en `RoomPositions` (NO dentro de cada Room).
- La sala inicial debe estar en **(0, 0)**
- Usa incrementos de **220 en X** y **150 en Y** para cada dirección:
  - **Norte**: Y - 150
  - **Sur**: Y + 150
  - **Este**: X + 220
  - **Oeste**: X - 220
- Ejemplo para 5 salas en cruz: sala central (0,0), norte (0,-150), sur (0,150), este (220,0), oeste (-220,0)
- **Type de objetos SOLO puede ser uno de estos valores exactos**: ninguno, arma, armadura, comida, bebida, ropa, llave, texto
- **Los objetos que son llaves DEBEN tener Type=""llave""**
- **Los objetos de tipo ""texto"" DEBEN tener TextContent** con el texto legible (carta, diario, pergamino, libro, nota...). El jugador usará el comando ""leer"" para ver este contenido.
- **Si una puerta tiene IsLocked=true, DEBE tener KeyObjectId con el Id de un objeto llave existente** (no puede haber puertas bloqueadas sin llave asignada)

### Género gramatical (Gender)
- **Gender** indica el género gramatical en español para artículos (el/la): `Masculine` o `Feminine`
- Ejemplos: ""espada"" → Feminine, ""libro"" → Masculine, ""llave"" → Feminine, ""cofre"" → Masculine
- Puertas también tienen Gender (por defecto Feminine: ""la puerta"")

### Estadísticas de objetos
- **Volume**: Volumen en centímetros cúbicos (cm³). Ejemplos: llave=10, libro=1000, espada=500, cofre=50000
- **Weight**: Peso en gramos. Ejemplos: llave=50, libro=500, espada=1500, cofre=5000
- **Price**: Precio en monedas. Asigna valores coherentes con la temática (objetos valiosos más caros)
- **MaxCapacity**: Solo para contenedores (IsContainer=true). Capacidad máxima en cm³. Ej: cofre=100000, bolsa=20000. Usa -1 para ilimitado

### Configuración del jugador (Player)
- **Name**: Inventa un nombre aleatorio acorde a la temática (medieval, sci-fi, etc.)
- **Físico del personaje** (randomiza según la temática y tipo de protagonista):
  - **Age**: Edad en años (mínimo 10, máximo 90)
  - **Weight**: Peso en kg (mínimo 50, máximo 150, incrementos de 5)
  - **Height**: Altura en cm (mínimo 50, máximo 220, incrementos de 5)
  - Ejemplos: guerrero corpulento (Age=35, Weight=95, Height=185), mago anciano (Age=70, Weight=60, Height=165), joven ágil (Age=18, Weight=65, Height=175)
- **Estadísticas** (Strength, Constitution, Intelligence, Dexterity, Charisma):
  - **Mínimo por estadística**: 10
  - **Máximo por estadística**: 100
  - **IMPORTANTE: La suma de las 5 DEBE ser exactamente 100**
  - Randomiza los valores para crear un personaje con personalidad única
  - Ejemplo guerrero: Strength=35, Constitution=25, Intelligence=12, Dexterity=18, Charisma=10 (suma=100)
  - Ejemplo mago: Strength=10, Constitution=15, Intelligence=40, Dexterity=20, Charisma=15 (suma=100)
  - Ejemplo equilibrado: Strength=20, Constitution=20, Intelligence=20, Dexterity=20, Charisma=20 (suma=100)
- **InitialGold**: Dinero inicial. Calcula un valor razonable basándote en los precios de los objetos (que pueda comprar 1-2 objetos baratos)
- Los nodos de scripts necesitan posiciones X,Y para visualización (separados ~200px)
- Conecta los nodos: evento → condiciones/acciones mediante puerto ""Exec""
- El puerto de salida de eventos y acciones es ""Exec"", el de entrada también es ""Exec""

## FORMATO DE SALIDA

**IMPORTANTE: Genera el resultado como un archivo descargable con extensión .xaw** (no como texto en el chat).
- El JSON debe ser válido y parseable
- Sin markdown code blocks, solo el JSON puro
- El archivo .xaw se abrirá directamente en el editor XiloAdventures
- **NO uses caracteres especiales invisibles** (soft hyphens, zero-width spaces, etc.) - solo caracteres UTF-8 estándar
- Usa solo comillas rectas ("") nunca comillas tipográficas ("")

## CONSISTENCIA DE PUERTAS

Cuando una puerta conecta dos salas, **AMBAS salidas deben referenciar la misma puerta**:
- Si `door_X` conecta `room_A` con `room_B`:
  - La salida de `room_A` hacia `room_B` debe tener `DoorId: ""door_X""`
  - La salida de `room_B` hacia `room_A` debe tener `DoorId: ""door_X""`
- Si una puerta tiene llave, **no debe haber rutas alternativas** para saltársela

## ⚠️ REGLA CRÍTICA: ACCESIBILIDAD DE LLAVES

**NUNCA pongas una llave detrás de la puerta que abre.** Esto haría el juego imposible.

### Regla de oro:
La llave SIEMPRE debe estar en una zona accesible SIN pasar por la puerta que abre.

### Ejemplo INCORRECTO (imposible de resolver):
```
Sala_Inicial ──[puerta_cerrada]── Sala_Tesoro (contiene llave_puerta)
```
❌ El jugador empieza en Sala_Inicial pero la llave está en Sala_Tesoro, que está bloqueada. ¡IMPOSIBLE!

### Ejemplo CORRECTO:
```
Sala_Inicial ── Sala_Biblioteca (contiene llave_puerta)
      │
[puerta_cerrada]
      │
Sala_Tesoro
```
✅ El jugador puede ir a Sala_Biblioteca, coger la llave, y luego abrir la puerta.

### Verificación obligatoria:
Antes de finalizar, para CADA puerta cerrada con llave:
1. Identifica dónde está la llave (RoomId del objeto llave)
2. Traza el camino desde Game.StartRoomId hasta esa sala
3. Verifica que ese camino NO pase por la puerta que esa llave abre
4. Si no es posible llegar a la llave, MUEVE la llave a una sala accesible

## IMPORTANTE: SIN SPOILERS

**NO reveles al jugador detalles de la aventura.** Solo proporciona una breve descripción temática (1-2 frases) sin mencionar puzzles, soluciones, ubicación de objetos o secretos. El jugador quiere descubrir la aventura por sí mismo.

Genera el mundo con la temática ""{THEME}"" y puzzles lógicos acordes a esa ambientación.";

    public PromptGeneratorWindow()
    {
        InitializeComponent();
        UpdateSlidersFromRoomCount();
        UpdatePrompt();
    }

    private void UpdateSlidersFromRoomCount()
    {
        // Evitar ejecución durante InitializeComponent
        if (DoorsSlider == null || NpcsSlider == null || QuestsSlider == null)
            return;

        var roomCountText = RoomCountTextBox?.Text ?? "6";
        if (!int.TryParse(roomCountText, out var roomCount) || roomCount < 1)
            roomCount = 6;

        _isUpdatingFromRoomCount = true;

        // Fórmulas basadas en el número de salas
        DoorsSlider.Value = Math.Max(1, roomCount / 3);           // 1 puerta cada 3 salas
        NpcsSlider.Value = Math.Max(1, roomCount / 3);            // 1 NPC cada 3 salas
        QuestsSlider.Value = Math.Max(1, roomCount / 6);          // 1 misión cada 6 salas
        ContainersSlider.Value = Math.Max(1, roomCount / 4);      // 1 contenedor cada 4 salas

        // Tipos de objetos según salas
        WeaponsSlider.Value = Math.Max(1, roomCount / 5);         // 1 arma cada 5 salas
        ArmorsSlider.Value = Math.Max(0, (roomCount - 5) / 6);    // armaduras solo en mundos grandes
        FoodSlider.Value = Math.Max(1, roomCount / 4);            // 1 comida cada 4 salas
        DrinksSlider.Value = Math.Max(1, roomCount / 5);          // 1 bebida cada 5 salas
        ClothingSlider.Value = Math.Max(0, (roomCount - 4) / 8);  // ropa solo en mundos medianos+
        KeysSlider.Value = Math.Max(1, roomCount / 6);            // 1 llave cada 6 salas
        TextsSlider.Value = Math.Max(1, roomCount / 5);           // 1 texto cada 5 salas
        OtherObjectsSlider.Value = Math.Max(2, roomCount / 3);    // objetos genéricos

        _isUpdatingFromRoomCount = false;
    }

    private void UpdateSliderValueTexts()
    {
        // Sliders generales
        if (DoorsValueText != null)
            DoorsValueText.Text = ((int)DoorsSlider.Value).ToString();
        if (NpcsValueText != null)
            NpcsValueText.Text = ((int)NpcsSlider.Value).ToString();
        if (QuestsValueText != null)
            QuestsValueText.Text = ((int)QuestsSlider.Value).ToString();
        if (ContainersValueText != null)
            ContainersValueText.Text = ((int)ContainersSlider.Value).ToString();

        // Sliders de tipos de objetos
        if (WeaponsValueText != null)
            WeaponsValueText.Text = ((int)WeaponsSlider.Value).ToString();
        if (ArmorsValueText != null)
            ArmorsValueText.Text = ((int)ArmorsSlider.Value).ToString();
        if (FoodValueText != null)
            FoodValueText.Text = ((int)FoodSlider.Value).ToString();
        if (DrinksValueText != null)
            DrinksValueText.Text = ((int)DrinksSlider.Value).ToString();
        if (ClothingValueText != null)
            ClothingValueText.Text = ((int)ClothingSlider.Value).ToString();
        if (KeysValueText != null)
            KeysValueText.Text = ((int)KeysSlider.Value).ToString();
        if (TextsValueText != null)
            TextsValueText.Text = ((int)TextsSlider.Value).ToString();
        if (OtherObjectsValueText != null)
            OtherObjectsValueText.Text = ((int)OtherObjectsSlider.Value).ToString();
    }

    private void UpdatePrompt()
    {
        // Evitar ejecución durante InitializeComponent
        if (PromptTextBox == null)
            return;

        var theme = ThemeTextBox?.Text ?? "mansión embrujada";
        var roomCountText = RoomCountTextBox?.Text ?? "6";

        if (string.IsNullOrWhiteSpace(theme))
            theme = "mansión embrujada";

        if (!int.TryParse(roomCountText, out var roomCount) || roomCount < 1)
            roomCount = 6;

        // Obtener valores de los sliders generales
        var doorCount = DoorsSlider != null ? (int)DoorsSlider.Value : 2;
        var npcCount = NpcsSlider != null ? (int)NpcsSlider.Value : 2;
        var questCount = QuestsSlider != null ? (int)QuestsSlider.Value : 1;
        var containerCount = ContainersSlider != null ? (int)ContainersSlider.Value : 1;

        // Obtener valores de tipos de objetos
        var weaponCount = WeaponsSlider != null ? (int)WeaponsSlider.Value : 1;
        var armorCount = ArmorsSlider != null ? (int)ArmorsSlider.Value : 0;
        var foodCount = FoodSlider != null ? (int)FoodSlider.Value : 1;
        var drinkCount = DrinksSlider != null ? (int)DrinksSlider.Value : 1;
        var clothingCount = ClothingSlider != null ? (int)ClothingSlider.Value : 0;
        var keyCount = KeysSlider != null ? (int)KeysSlider.Value : 1;
        var textCount = TextsSlider != null ? (int)TextsSlider.Value : 1;
        var otherCount = OtherObjectsSlider != null ? (int)OtherObjectsSlider.Value : 2;

        // Calcular total de objetos
        var totalObjects = weaponCount + armorCount + foodCount + drinkCount +
                          clothingCount + keyCount + textCount + otherCount;

        UpdateSliderValueTexts();

        var prompt = PromptTemplate
            .Replace("{THEME}", theme)
            .Replace("{ROOM_COUNT}", roomCount.ToString())
            .Replace("{DOOR_COUNT}", doorCount.ToString())
            .Replace("{NPC_COUNT}", npcCount.ToString())
            .Replace("{QUEST_COUNT}", questCount.ToString())
            .Replace("{CONTAINER_COUNT}", containerCount.ToString())
            .Replace("{WEAPON_COUNT}", weaponCount.ToString())
            .Replace("{ARMOR_COUNT}", armorCount.ToString())
            .Replace("{FOOD_COUNT}", foodCount.ToString())
            .Replace("{DRINK_COUNT}", drinkCount.ToString())
            .Replace("{CLOTHING_COUNT}", clothingCount.ToString())
            .Replace("{KEY_COUNT}", keyCount.ToString())
            .Replace("{TEXT_COUNT}", textCount.ToString())
            .Replace("{OTHER_COUNT}", otherCount.ToString())
            .Replace("{TOTAL_OBJECTS}", totalObjects.ToString());

        PromptTextBox.Text = prompt;
    }

    private void ThemeTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdatePrompt();
    }

    private void RoomCountTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateSlidersFromRoomCount();
        UpdatePrompt();
    }

    private void RoomCountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Solo permitir números
        e.Handled = !int.TryParse(e.Text, out _);
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

    private void DoorsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingFromRoomCount)
            UpdatePrompt();
    }

    private void NpcsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingFromRoomCount)
            UpdatePrompt();
    }

    private void QuestsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingFromRoomCount)
            UpdatePrompt();
    }

    private void ContainersSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingFromRoomCount)
            UpdatePrompt();
    }

    private void ObjectTypeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingFromRoomCount)
            UpdatePrompt();
    }
}
