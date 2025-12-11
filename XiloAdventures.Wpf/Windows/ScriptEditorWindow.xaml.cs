using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Wpf.Windows;

public partial class ScriptEditorWindow : Window
{
    private readonly WorldModel _world;
    private readonly string _ownerType;
    private readonly string _ownerId;
    private readonly string _ownerName;
    private ScriptDefinition _script;
    private ScriptNode? _selectedNode;
    private bool _isDirty;

    // Providers para selectores de entidades
    public Func<IEnumerable<Room>>? GetRooms { get; set; }
    public Func<IEnumerable<GameObject>>? GetObjects { get; set; }
    public Func<IEnumerable<Npc>>? GetNpcs { get; set; }
    public Func<IEnumerable<Door>>? GetDoors { get; set; }
    public Func<IEnumerable<QuestDefinition>>? GetQuests { get; set; }
    public Func<IEnumerable<FxAsset>>? GetFxs { get; set; }

    public ScriptEditorWindow(WorldModel world, string ownerType, string ownerId, string ownerName)
    {
        InitializeComponent();

        _world = world;
        _ownerType = ownerType;
        _ownerId = ownerId;
        _ownerName = ownerName;

        Title = $"Editor de Scripts - {ownerName} ({GetOwnerTypeDisplayName(ownerType)})";

        // Buscar script existente o crear uno nuevo
        _script = _world.Scripts.FirstOrDefault(s =>
            s.OwnerType == ownerType && s.OwnerId == ownerId)
            ?? CreateNewScript();

        // Configurar controles
        ScriptNameTextBox.Text = _script.Name;
        ScriptNameTextBox.TextChanged += ScriptNameTextBox_TextChanged;

        NodePalette.SetOwnerType(ownerType);

        ScriptPanel.SetScript(_script, ownerType);
        ScriptPanel.NodeSelected += ScriptPanel_NodeSelected;
        ScriptPanel.SelectionCleared += ScriptPanel_SelectionCleared;
        ScriptPanel.ScriptEdited += ScriptPanel_ScriptEdited;
        ScriptPanel.NodeDoubleClicked += ScriptPanel_NodeDoubleClicked;

        // Estado inicial de toggles
        ToggleGridButton.IsChecked = ScriptPanel.IsGridVisible;
        ToggleSnapButton.IsChecked = ScriptPanel.IsSnapToGridEnabled;

        Closing += ScriptEditorWindow_Closing;
    }

    private static string GetOwnerTypeDisplayName(string ownerType)
    {
        return ownerType switch
        {
            "Game" => "Juego",
            "Room" => "Sala",
            "Door" => "Puerta",
            "Npc" => "NPC",
            "GameObject" => "Objeto",
            "Quest" => "Mision",
            _ => ownerType
        };
    }

    private ScriptDefinition CreateNewScript()
    {
        var script = new ScriptDefinition
        {
            Name = $"Script de {_ownerName}",
            OwnerType = _ownerType,
            OwnerId = _ownerId
        };

        _world.Scripts.Add(script);
        return script;
    }

    private void ScriptPanel_NodeSelected(ScriptNode node)
    {
        _selectedNode = node;
        UpdatePropertiesPanel();
    }

    private void ScriptPanel_SelectionCleared()
    {
        _selectedNode = null;
        UpdatePropertiesPanel();
    }

    private void ScriptPanel_ScriptEdited()
    {
        _isDirty = true;
    }

    private void ScriptPanel_NodeDoubleClicked(ScriptNode node)
    {
        _selectedNode = node;
        UpdatePropertiesPanel();
    }

    private void ScriptNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _script.Name = ScriptNameTextBox.Text;
        _isDirty = true;
    }

    private void UpdatePropertiesPanel()
    {
        PropertiesPanel.Children.Clear();

        if (_selectedNode == null)
        {
            var noSelectionText = new TextBlock
            {
                Text = "Selecciona un nodo para ver sus propiedades",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap
            };
            PropertiesPanel.Children.Add(noSelectionText);
            return;
        }

        var typeDef = NodeTypeRegistry.GetNodeType(_selectedNode.NodeType);
        if (typeDef == null) return;

        // Tipo de nodo
        AddPropertyHeader("Tipo");
        AddPropertyValue(typeDef.DisplayName);

        if (!string.IsNullOrEmpty(typeDef.Description))
        {
            AddPropertyDescription(typeDef.Description);
        }

        AddSeparator();

        // Propiedades editables
        if (typeDef.Properties.Length > 0)
        {
            AddPropertyHeader("Propiedades");

            foreach (var propDef in typeDef.Properties)
            {
                AddEditableProperty(propDef);
            }
        }

        // Comentario
        AddSeparator();
        AddPropertyHeader("Comentario");
        AddCommentEditor();
    }

    private void AddPropertyHeader(string text)
    {
        var header = new TextBlock
        {
            Text = text.ToUpper(),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
            Margin = new Thickness(0, 8, 0, 4)
        };
        PropertiesPanel.Children.Add(header);
    }

    private void AddPropertyValue(string text)
    {
        var value = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 4)
        };
        PropertiesPanel.Children.Add(value);
    }

    private void AddPropertyDescription(string text)
    {
        var desc = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };
        PropertiesPanel.Children.Add(desc);
    }

    private void AddSeparator()
    {
        var sep = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            Margin = new Thickness(0, 8, 0, 4)
        };
        PropertiesPanel.Children.Add(sep);
    }

    private void AddEditableProperty(NodePropertyDefinition propDef)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };

        var label = new TextBlock
        {
            Text = propDef.DisplayName,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 2)
        };
        panel.Children.Add(label);

        // Obtener valor actual
        _selectedNode!.Properties.TryGetValue(propDef.Name, out var currentValue);

        UIElement editor;

        if (propDef.DataType == "select" && propDef.Options != null)
        {
            // ComboBox para selecciones
            var combo = new ComboBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 4, 6, 4)
            };

            foreach (var option in propDef.Options)
            {
                combo.Items.Add(option);
            }

            combo.SelectedItem = currentValue?.ToString() ?? propDef.Options.FirstOrDefault();
            combo.SelectionChanged += (s, e) =>
            {
                _selectedNode.Properties[propDef.Name] = combo.SelectedItem?.ToString();
                _isDirty = true;
                ScriptPanel.InvalidateVisual();
            };

            editor = combo;
        }
        else if (propDef.DataType == "bool")
        {
            // CheckBox para booleanos
            var check = new CheckBox
            {
                IsChecked = currentValue is bool b ? b : (propDef.DefaultValue is bool db && db),
                Foreground = Brushes.White
            };

            check.Checked += (s, e) =>
            {
                _selectedNode.Properties[propDef.Name] = true;
                _isDirty = true;
            };

            check.Unchecked += (s, e) =>
            {
                _selectedNode.Properties[propDef.Name] = false;
                _isDirty = true;
            };

            editor = check;
        }
        else if (propDef.DataType == "int")
        {
            // TextBox para enteros
            var textBox = new TextBox
            {
                Text = currentValue?.ToString() ?? propDef.DefaultValue?.ToString() ?? "0",
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 4, 6, 4)
            };

            textBox.TextChanged += (s, e) =>
            {
                if (int.TryParse(textBox.Text, out var intValue))
                {
                    _selectedNode.Properties[propDef.Name] = intValue;
                    _isDirty = true;
                }
            };

            editor = textBox;
        }
        else if (propDef.DataType == "float")
        {
            // TextBox para flotantes
            var textBox = new TextBox
            {
                Text = currentValue?.ToString() ?? propDef.DefaultValue?.ToString() ?? "0",
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 4, 6, 4)
            };

            textBox.TextChanged += (s, e) =>
            {
                if (float.TryParse(textBox.Text, out var floatValue))
                {
                    _selectedNode.Properties[propDef.Name] = floatValue;
                    _isDirty = true;
                }
            };

            editor = textBox;
        }
        else if (!string.IsNullOrEmpty(propDef.EntityType))
        {
            // ComboBox para referencias a entidades
            editor = CreateEntitySelector(propDef, currentValue?.ToString());
        }
        else
        {
            // TextBox para strings y otros
            var textBox = new TextBox
            {
                Text = currentValue?.ToString() ?? propDef.DefaultValue?.ToString() ?? "",
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 4, 6, 4),
                AcceptsReturn = propDef.Name == "Message",
                TextWrapping = propDef.Name == "Message" ? TextWrapping.Wrap : TextWrapping.NoWrap,
                MinHeight = propDef.Name == "Message" ? 60 : 0
            };

            textBox.TextChanged += (s, e) =>
            {
                _selectedNode.Properties[propDef.Name] = textBox.Text;
                _isDirty = true;
            };

            editor = textBox;
        }

        panel.Children.Add(editor);
        PropertiesPanel.Children.Add(panel);
    }

    private ComboBox CreateEntitySelector(NodePropertyDefinition propDef, string? currentValue)
    {
        var combo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 4, 6, 4)
        };

        combo.Items.Add(new ComboBoxItem { Content = "(Ninguno)", Tag = "" });

        IEnumerable<(string Id, string Name)> entities = propDef.EntityType switch
        {
            "Room" => GetRooms?.Invoke()?.Select(r => (r.Id, r.Name)) ?? Enumerable.Empty<(string, string)>(),
            "GameObject" => GetObjects?.Invoke()?.Select(o => (o.Id, o.Name)) ?? Enumerable.Empty<(string, string)>(),
            "Npc" => GetNpcs?.Invoke()?.Select(n => (n.Id, n.Name)) ?? Enumerable.Empty<(string, string)>(),
            "Door" => GetDoors?.Invoke()?.Select(d => (d.Id, d.Name)) ?? Enumerable.Empty<(string, string)>(),
            "Quest" => GetQuests?.Invoke()?.Select(q => (q.Id, q.Name)) ?? Enumerable.Empty<(string, string)>(),
            "Fx" => GetFxs?.Invoke()?.Select(f => (f.Id, f.Id)) ?? Enumerable.Empty<(string, string)>(),
            _ => Enumerable.Empty<(string, string)>()
        };

        foreach (var (id, name) in entities)
        {
            combo.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }

        // Seleccionar valor actual
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == currentValue)
            {
                combo.SelectedItem = item;
                break;
            }
        }

        if (combo.SelectedItem == null)
            combo.SelectedIndex = 0;

        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedItem is ComboBoxItem selected)
            {
                _selectedNode!.Properties[propDef.Name] = selected.Tag?.ToString();
                _isDirty = true;
            }
        };

        return combo;
    }

    private void AddCommentEditor()
    {
        var textBox = new TextBox
        {
            Text = _selectedNode?.Comment ?? "",
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 4, 6, 4),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 40
        };

        textBox.TextChanged += (s, e) =>
        {
            if (_selectedNode != null)
            {
                _selectedNode.Comment = string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text;
                _isDirty = true;
            }
        };

        PropertiesPanel.Children.Add(textBox);
    }

    // Toolbar handlers
    private void ToggleGrid_Click(object sender, RoutedEventArgs e)
    {
        ScriptPanel.ToggleGridVisibility();
        ToggleGridButton.IsChecked = ScriptPanel.IsGridVisible;
    }

    private void ToggleSnap_Click(object sender, RoutedEventArgs e)
    {
        ScriptPanel.ToggleSnapToGrid();
        ToggleSnapButton.IsChecked = ScriptPanel.IsSnapToGridEnabled;
    }

    private void CenterView_Click(object sender, RoutedEventArgs e)
    {
        ScriptPanel.CenterView();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        ScriptPanel.ZoomIn();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        ScriptPanel.ZoomOut();
    }

    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        ScriptPanel.ResetZoom();
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        ScriptPanel.RemoveSelectedNodes();
    }

    private void ScriptEditorWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Sincronizar posiciones antes de cerrar
        ScriptPanel.SyncPositionsToScript();

        // Si el script está vacío, eliminarlo
        if (_script.Nodes.Count == 0 && _script.Connections.Count == 0)
        {
            _world.Scripts.Remove(_script);
        }
    }
}
