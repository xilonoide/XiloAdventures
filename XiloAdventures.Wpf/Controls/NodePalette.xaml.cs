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
            ToolTip = nodeDef.Description
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
}
