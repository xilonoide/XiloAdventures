using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Wpf.Controls;

public partial class NodePalette : UserControl
{
    private string _ownerType = string.Empty;

    public NodePalette()
    {
        InitializeComponent();
    }

    public void SetOwnerType(string ownerType)
    {
        _ownerType = ownerType;
        CategoriesPanel.Children.Clear();

        // Obtener nodos disponibles para este tipo de entidad
        var availableNodes = NodeTypeRegistry.GetNodesForOwnerType(ownerType);

        // Agrupar por categoría
        var nodesByCategory = availableNodes
            .GroupBy(n => n.Category)
            .OrderBy(g => GetCategoryOrder(g.Key));

        foreach (var categoryGroup in nodesByCategory)
        {
            var expander = CreateCategoryExpander(categoryGroup.Key, categoryGroup.ToList());
            CategoriesPanel.Children.Add(expander);
        }
    }

    private static int GetCategoryOrder(NodeCategory category)
    {
        return category switch
        {
            NodeCategory.Event => 0,
            NodeCategory.Condition => 1,
            NodeCategory.Action => 2,
            NodeCategory.Flow => 3,
            NodeCategory.Variable => 4,
            _ => 99
        };
    }

    private static string GetCategoryDisplayName(NodeCategory category)
    {
        return category switch
        {
            NodeCategory.Event => "Eventos",
            NodeCategory.Condition => "Condiciones",
            NodeCategory.Action => "Acciones",
            NodeCategory.Flow => "Control de Flujo",
            NodeCategory.Variable => "Variables",
            _ => category.ToString()
        };
    }

    private Expander CreateCategoryExpander(NodeCategory category, List<NodeTypeDefinition> nodes)
    {
        var categoryColor = ScriptPanel.CategoryColors.TryGetValue(category, out var cc)
            ? cc
            : Color.FromRgb(80, 80, 80);

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

        // Indicador de color
        var colorIndicator = new Border
        {
            Width = 12,
            Height = 12,
            Background = new SolidColorBrush(categoryColor),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, 8, 0)
        };
        headerPanel.Children.Add(colorIndicator);

        // Nombre de categoría
        var headerText = new TextBlock
        {
            Text = GetCategoryDisplayName(category),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12
        };
        headerPanel.Children.Add(headerText);

        // Contador
        var countText = new TextBlock
        {
            Text = $" ({nodes.Count})",
            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
            FontSize = 11
        };
        headerPanel.Children.Add(countText);

        var nodesPanel = new StackPanel { Margin = new Thickness(4, 2, 4, 4) };

        foreach (var nodeDef in nodes.OrderBy(n => n.DisplayName))
        {
            var nodeItem = CreateNodeItem(nodeDef, categoryColor);
            nodesPanel.Children.Add(nodeItem);
        }

        var expander = new Expander
        {
            Header = headerPanel,
            Content = nodesPanel,
            IsExpanded = category == NodeCategory.Event || category == NodeCategory.Action,
            Foreground = Brushes.White,
            Margin = new Thickness(4, 2, 4, 2)
        };

        return expander;
    }

    private Border CreateNodeItem(NodeTypeDefinition nodeDef, Color categoryColor)
    {
        var itemPanel = new StackPanel { Orientation = Orientation.Horizontal };

        // Mini indicador de color
        var miniIndicator = new Border
        {
            Width = 4,
            Height = 16,
            Background = new SolidColorBrush(categoryColor),
            CornerRadius = new CornerRadius(1),
            Margin = new Thickness(0, 0, 6, 0)
        };
        itemPanel.Children.Add(miniIndicator);

        // Nombre del nodo
        var nameText = new TextBlock
        {
            Text = nodeDef.DisplayName,
            Foreground = Brushes.White,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        itemPanel.Children.Add(nameText);

        var border = new Border
        {
            Child = itemPanel,
            Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 1, 0, 1),
            Cursor = Cursors.Hand,
            ToolTip = CreateNodeTooltip(nodeDef, categoryColor)
        };

        // Hover effect
        border.MouseEnter += (s, e) =>
        {
            border.Background = new SolidColorBrush(Color.FromRgb(65, 65, 65));
        };

        border.MouseLeave += (s, e) =>
        {
            border.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50));
        };

        // Drag start
        border.MouseLeftButtonDown += (s, e) =>
        {
            var data = new DataObject("NodeType", nodeDef.TypeId);
            DragDrop.DoDragDrop(border, data, DragDropEffects.Copy);
        };

        return border;
    }

    private ToolTip CreateNodeTooltip(NodeTypeDefinition nodeDef, Color categoryColor)
    {
        var panel = new StackPanel { MaxWidth = 300 };

        // Header con nombre y categoría
        var headerBorder = new Border
        {
            Background = new SolidColorBrush(categoryColor),
            CornerRadius = new CornerRadius(3, 3, 0, 0),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(-4, -4, -4, 0)
        };

        var headerText = new TextBlock
        {
            Text = nodeDef.DisplayName,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = Brushes.White
        };
        headerBorder.Child = headerText;
        panel.Children.Add(headerBorder);

        // Descripción
        var descText = new TextBlock
        {
            Text = nodeDef.Description,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220))
        };
        panel.Children.Add(descText);

        // Ejemplo de uso
        var example = GetNodeExample(nodeDef.TypeId);
        if (!string.IsNullOrEmpty(example))
        {
            var exampleLabel = new TextBlock
            {
                Text = "💡 Ejemplo:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 2),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 220, 100))
            };
            panel.Children.Add(exampleLabel);

            var exampleText = new TextBlock
            {
                Text = example,
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180))
            };
            panel.Children.Add(exampleText);
        }

        // Info de puertos si tiene
        var portsInfo = GetPortsInfo(nodeDef);
        if (!string.IsNullOrEmpty(portsInfo))
        {
            var portsLabel = new TextBlock
            {
                Text = "🔌 Conexiones:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 2),
                Foreground = new SolidColorBrush(Color.FromRgb(150, 200, 255))
            };
            panel.Children.Add(portsLabel);

            var portsText = new TextBlock
            {
                Text = portsInfo,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160))
            };
            panel.Children.Add(portsText);
        }

        return new ToolTip
        {
            Content = panel,
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            BorderBrush = new SolidColorBrush(categoryColor),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };
    }

    private static string GetPortsInfo(NodeTypeDefinition nodeDef)
    {
        var parts = new List<string>();

        var execInputs = nodeDef.InputPorts?.Count(p => p.PortType == PortType.Execution) ?? 0;
        var dataInputs = nodeDef.InputPorts?.Count(p => p.PortType == PortType.Data) ?? 0;
        var execOutputs = nodeDef.OutputPorts?.Count(p => p.PortType == PortType.Execution) ?? 0;
        var dataOutputs = nodeDef.OutputPorts?.Count(p => p.PortType == PortType.Data) ?? 0;

        if (execInputs > 0) parts.Add($"▶ {execInputs} entrada{(execInputs > 1 ? "s" : "")} de ejecución");
        if (dataInputs > 0) parts.Add($"● {dataInputs} entrada{(dataInputs > 1 ? "s" : "")} de datos");
        if (execOutputs > 0) parts.Add($"▶ {execOutputs} salida{(execOutputs > 1 ? "s" : "")} de ejecución");
        if (dataOutputs > 0) parts.Add($"● {dataOutputs} salida{(dataOutputs > 1 ? "s" : "")} de datos");

        return string.Join("\n", parts);
    }

    private static string GetNodeExample(string typeId)
    {
        return typeId switch
        {
            // === EVENTOS ===
            "Event_OnInteract" => "Cuando el jugador examina un cofre, mostrar su contenido o abrir un diálogo.",
            "Event_OnEnterRoom" => "Al entrar a la taberna, reproducir música ambiental y mostrar descripción.",
            "Event_OnExitRoom" => "Al salir del bosque, detener el sonido de pájaros.",
            "Event_OnTalk" => "Al hablar con el mercader, iniciar su diálogo de compra/venta.",
            "Event_OnUseItem" => "Cuando el jugador usa la llave en la puerta, abrirla.",
            "Event_OnPickup" => "Al recoger la espada mágica, mostrar mensaje y dar buff.",
            "Event_OnDrop" => "Al soltar el amuleto maldito, eliminar el debuff.",
            "Event_OnGameStart" => "Al iniciar el juego, establecer flags iniciales y mostrar intro.",
            "Event_OnTimePassed" => "Cada hora de juego, verificar si el jugador tiene hambre.",
            "Event_OnTurnStart" => "Al inicio de cada turno, reducir contador de veneno.",
            "Event_OnWeatherChange" => "Cuando llueve, hacer que los NPCs entren a sus casas.",
            "Event_OnDoorOpen" => "Al abrir la puerta secreta, revelar la habitación oculta.",
            "Event_OnDoorClose" => "Al cerrar la celda, atrapar al enemigo dentro.",
            "Event_OnQuestStart" => "Al iniciar la misión, marcar objetivo en el mapa.",
            "Event_OnQuestComplete" => "Al completar la misión, dar recompensa al jugador.",
            "Event_OnQuestFail" => "Si falla la misión, mostrar consecuencias.",

            // === CONDICIONES ===
            "Condition_CheckFlag" => "Si 'puerta_abierta' es true → permitir pasar; si no → decir 'está cerrada'.",
            "Condition_CompareCounter" => "Si 'monedas' >= 100 → poder comprar; si no → 'no tienes suficiente oro'.",
            "Condition_IsTimeOfDay" => "Si es de noche → el vampiro ataca; de día → está dormido.",
            "Condition_HasItem" => "Si tiene 'llave_maestra' → abrir cualquier puerta.",
            "Condition_IsInRoom" => "Si está en 'mazmorra' → los monstruos son agresivos.",
            "Condition_IsQuestActive" => "Si la misión 'rescate' está activa → el NPC da pistas.",
            "Condition_IsQuestCompleted" => "Si completó 'tutorial' → desbloquear zona avanzada.",
            "Condition_RandomChance" => "25% de probabilidad de encontrar tesoro al buscar.",
            "Condition_IsDoorOpen" => "Si la puerta está abierta → poder pasar; si no → buscar llave.",

            // === ACCIONES ===
            "Action_ShowMessage" => "Mostrar '¡Has encontrado un cofre del tesoro!'",
            "Action_GiveItem" => "Dar 'espada_legendaria' al inventario del jugador.",
            "Action_RemoveItem" => "Quitar 'llave_oxidada' después de usarla.",
            "Action_MoveToRoom" => "Teletransportar al jugador a 'sala_del_trono'.",
            "Action_SetFlag" => "Establecer 'jefe_derrotado' = true.",
            "Action_SetCounter" => "Establecer 'vidas' = 3.",
            "Action_IncrementCounter" => "Incrementar 'experiencia' en 50.",
            "Action_PlaySound" => "Reproducir 'fanfarria_victoria.wav'.",
            "Action_Wait" => "Esperar 2 segundos antes de la siguiente acción.",
            "Action_StartQuest" => "Iniciar la misión 'el_dragon_dormido'.",
            "Action_CompleteQuest" => "Marcar 'rescatar_princesa' como completada.",
            "Action_FailQuest" => "Fallar 'proteger_aldea' si mueren todos los NPCs.",
            "Action_OpenDoor" => "Abrir 'puerta_del_castillo'.",
            "Action_CloseDoor" => "Cerrar 'compuerta_trampa' detrás del jugador.",
            "Action_ToggleDoor" => "Cambiar estado de 'puerta_secreta' (abierta↔cerrada).",
            "Action_SetWeather" => "Cambiar el clima a 'tormenta'.",
            "Action_AdvanceTime" => "Avanzar el tiempo 6 horas (amanecer→mediodía).",

            // === FLUJO ===
            "Flow_Sequence" => "Ejecutar en orden: mostrar mensaje → dar objeto → reproducir sonido.",
            "Flow_Branch" => "Si tiene llave → abrir puerta; si no → decir 'necesitas una llave'.",
            "Flow_Delay" => "Esperar 3 segundos antes de que aparezca el fantasma.",
            "Flow_Loop" => "Repetir 5 veces: dar 1 moneda de oro.",
            "Flow_RandomBranch" => "Elegir aleatoriamente entre 3 respuestas del NPC.",

            // === VARIABLES ===
            "Variable_GetFlag" => "Obtener el valor de 'puerta_abierta' para usarlo en comparaciones.",
            "Variable_GetCounter" => "Obtener 'nivel_jugador' para calcular daño.",
            "Variable_GetCurrentRoom" => "Obtener sala actual para verificar ubicación.",
            "Variable_GetCurrentTime" => "Obtener hora del día para eventos temporales.",
            "Variable_GetItemCount" => "Contar cuántas 'pociones' tiene el jugador.",
            "Variable_Constant_Int" => "Usar el número 100 como valor fijo para comparaciones.",
            "Variable_Constant_String" => "Usar 'sala_secreta' como ID de destino.",
            "Variable_Constant_Bool" => "Usar 'true' como valor constante.",

            // === COMPARACIONES ===
            "Data_CompareInt" => "Comparar nivel del jugador con nivel requerido (10).",
            "Data_CompareString" => "Verificar si el nombre del objeto es 'espada_magica'.",
            "Data_CompareBool" => "Verificar si dos flags tienen el mismo valor.",
            "Data_CheckFlag" => "Verificar si 'boss_derrotado' es true (salida booleana).",
            "Data_CompareCounter" => "Verificar si 'oro' > 500 (salida booleana).",

            // === MATEMÁTICAS ===
            "Math_Add" => "Sumar daño base + bonus de arma.",
            "Math_Subtract" => "Restar defensa del daño recibido.",
            "Math_Multiply" => "Multiplicar experiencia × 2 por bonus.",
            "Math_Divide" => "Dividir precio entre 2 para descuento.",
            "Math_Random" => "Generar daño aleatorio entre 5 y 15.",

            // === LÓGICA ===
            "Logic_And" => "Si tiene llave Y la puerta no está rota → abrir.",
            "Logic_Or" => "Si es de día O tiene antorcha → ver en la cueva.",
            "Logic_Not" => "Si NO ha hablado con el rey → no puede entrar.",

            // === ACCIONES CON DATOS ===
            "Action_SetFlagData" => "Establecer flag usando valor de otra conexión.",
            "Action_SetCounterData" => "Establecer contador usando resultado de operación matemática.",
            "Action_IncrementCounterData" => "Incrementar contador usando valor calculado.",

            // === SELECCIÓN ===
            "Select_Bool" => "Seleccionar entre 'amigo' o 'enemigo' según flag de reputación.",
            "Select_Int" => "Seleccionar entre precio normal o con descuento.",

            _ => ""
        };
    }
}
