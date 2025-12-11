using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Wpf.Windows;

public partial class ScriptEditorWindow : Window
{
    // Routed commands for toggle operations
    public static readonly RoutedCommand ToggleGridCommand = new();
    public static readonly RoutedCommand ToggleSnapCommand = new();

    private readonly WorldModel _world;
    private readonly string _ownerType;
    private readonly string _ownerId;
    private readonly string _ownerName;
    private ScriptDefinition _script;
    private ScriptNode? _selectedNode;

    // Undo/Redo support
    private readonly ScriptUndoRedoManager _undoRedo = new(50);
    private bool _isRestoringSnapshot;

    // Clipboard for copy/paste
    private List<ScriptNode>? _nodesClipboard;
    private List<NodeConnection>? _connectionsClipboard;
    private bool _clipboardIsCut;

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

        Loaded += ScriptEditorWindow_Loaded;
        Closing += ScriptEditorWindow_Closing;

        // Initial undo snapshot
        PushUndoSnapshot();
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

    #region Script Panel Events

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
        if (!_isRestoringSnapshot)
        {
            PushUndoSnapshot();
        }
    }

    private void ScriptPanel_NodeDoubleClicked(ScriptNode node)
    {
        _selectedNode = node;
        UpdatePropertiesPanel();
    }

    private void ScriptNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _script.Name = ScriptNameTextBox.Text;
        if (!_isRestoringSnapshot)
        {
            PushUndoSnapshot();
        }
    }

    #endregion

    #region Undo/Redo

    private void PushUndoSnapshot()
    {
        var snapshot = CreateSnapshot();
        _undoRedo.Push(snapshot);
        CommandManager.InvalidateRequerySuggested();
    }

    private ScriptSnapshot CreateSnapshot()
    {
        // Serialize the current script state
        var options = new JsonSerializerOptions { WriteIndented = false };
        var nodesJson = JsonSerializer.Serialize(_script.Nodes, options);
        var connectionsJson = JsonSerializer.Serialize(_script.Connections, options);

        return new ScriptSnapshot
        {
            Name = _script.Name,
            NodesJson = nodesJson,
            ConnectionsJson = connectionsJson
        };
    }

    private void RestoreSnapshot(ScriptSnapshot snapshot)
    {
        _isRestoringSnapshot = true;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            _script.Name = snapshot.Name;
            _script.Nodes = JsonSerializer.Deserialize<List<ScriptNode>>(snapshot.NodesJson, options) ?? new();
            _script.Connections = JsonSerializer.Deserialize<List<NodeConnection>>(snapshot.ConnectionsJson, options) ?? new();

            // Normalize properties dictionaries
            foreach (var node in _script.Nodes)
            {
                var normalizedProps = new Dictionary<string, object?>(
                    node.Properties, StringComparer.OrdinalIgnoreCase);
                node.Properties = normalizedProps;
            }

            ScriptNameTextBox.Text = _script.Name;
            ScriptPanel.SetScript(_script, _ownerType);
            _selectedNode = null;
            UpdatePropertiesPanel();
        }
        finally
        {
            _isRestoringSnapshot = false;
        }
    }

    private void Undo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _undoRedo.CanUndo;
        e.Handled = true;
    }

    private void Undo_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var snapshot = _undoRedo.Undo();
        if (snapshot != null)
        {
            RestoreSnapshot(snapshot);
        }
    }

    private void Redo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _undoRedo.CanRedo;
        e.Handled = true;
    }

    private void Redo_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var snapshot = _undoRedo.Redo();
        if (snapshot != null)
        {
            RestoreSnapshot(snapshot);
        }
    }

    #endregion

    #region Cut/Copy/Paste

    private void CutCopyCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        var selected = ScriptPanel.GetSelectedNodes();
        e.CanExecute = selected != null && selected.Any();
        e.Handled = true;
    }

    private void PasteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _nodesClipboard != null && _nodesClipboard.Count > 0;
        e.Handled = true;
    }

    private void CutCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selected = ScriptPanel.GetSelectedNodes().ToList();
        if (selected.Count == 0) return;

        CopyNodesToClipboard(selected);
        _clipboardIsCut = true;

        // Remove the cut nodes
        var selectedIds = selected.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _script.Nodes.RemoveAll(n => selectedIds.Contains(n.Id));
        _script.Connections.RemoveAll(c =>
            selectedIds.Contains(c.FromNodeId) || selectedIds.Contains(c.ToNodeId));

        ScriptPanel.SetScript(_script, _ownerType);
        ScriptPanel.ClearSelection();
        PushUndoSnapshot();
    }

    private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selected = ScriptPanel.GetSelectedNodes().ToList();
        if (selected.Count == 0) return;

        CopyNodesToClipboard(selected);
        _clipboardIsCut = false;
    }

    private void PasteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_nodesClipboard == null || _nodesClipboard.Count == 0) return;

        // Create new nodes from clipboard with new IDs
        var idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var newNodes = new List<ScriptNode>();

        foreach (var node in _nodesClipboard)
        {
            var newId = Guid.NewGuid().ToString();
            idMap[node.Id] = newId;

            var newNode = new ScriptNode
            {
                Id = newId,
                NodeType = node.NodeType,
                Category = node.Category,
                X = node.X + 40, // Offset for visibility
                Y = node.Y + 40,
                Comment = node.Comment,
                Properties = new Dictionary<string, object?>(node.Properties, StringComparer.OrdinalIgnoreCase)
            };
            newNodes.Add(newNode);
        }

        // Create new connections
        if (_connectionsClipboard != null)
        {
            foreach (var conn in _connectionsClipboard)
            {
                if (idMap.TryGetValue(conn.FromNodeId, out var newFromId) &&
                    idMap.TryGetValue(conn.ToNodeId, out var newToId))
                {
                    var newConn = new NodeConnection
                    {
                        Id = Guid.NewGuid().ToString(),
                        FromNodeId = newFromId,
                        FromPortName = conn.FromPortName,
                        ToNodeId = newToId,
                        ToPortName = conn.ToPortName
                    };
                    _script.Connections.Add(newConn);
                }
            }
        }

        // Add new nodes to script
        foreach (var node in newNodes)
        {
            _script.Nodes.Add(node);
        }

        // Update the panel and select the new nodes
        ScriptPanel.SetScript(_script, _ownerType);
        ScriptPanel.SelectNodes(newNodes.Select(n => n.Id));
        PushUndoSnapshot();

        // If it was a cut operation, clear the clipboard
        if (_clipboardIsCut)
        {
            _nodesClipboard = null;
            _connectionsClipboard = null;
        }
    }

    private void CopyNodesToClipboard(List<ScriptNode> nodes)
    {
        var options = new JsonSerializerOptions { WriteIndented = false };

        // Clone nodes
        var nodesJson = JsonSerializer.Serialize(nodes, options);
        _nodesClipboard = JsonSerializer.Deserialize<List<ScriptNode>>(nodesJson, options) ?? new();

        // Clone connections between selected nodes
        var nodeIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relevantConnections = _script.Connections
            .Where(c => nodeIds.Contains(c.FromNodeId) && nodeIds.Contains(c.ToNodeId))
            .ToList();

        var connJson = JsonSerializer.Serialize(relevantConnections, options);
        _connectionsClipboard = JsonSerializer.Deserialize<List<NodeConnection>>(connJson, options) ?? new();
    }

    #endregion

    #region Grid/Snap Commands

    private void ToggleGrid_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ScriptPanel.ToggleGridVisibility();
        ToggleGridButton.IsChecked = ScriptPanel.IsGridVisible;
    }

    private void ToggleSnap_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ScriptPanel.ToggleSnapToGrid();
        ToggleSnapButton.IsChecked = ScriptPanel.IsSnapToGridEnabled;
    }

    private void ToggleGridButton_Click(object sender, RoutedEventArgs e)
    {
        ScriptPanel.ToggleGridVisibility();
        ToggleGridButton.IsChecked = ScriptPanel.IsGridVisible;
    }

    private void ToggleSnapButton_Click(object sender, RoutedEventArgs e)
    {
        ScriptPanel.ToggleSnapToGrid();
        ToggleSnapButton.IsChecked = ScriptPanel.IsSnapToGridEnabled;
    }

    private void CenterView_Click(object sender, RoutedEventArgs e)
    {
        ScriptPanel.CenterView();
    }

    #endregion

    #region Properties Panel

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
                FontSize = 14,
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
            FontSize = 12,
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
            FontSize = 14,
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
            FontSize = 13,
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
            FontSize = 13,
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
                PushUndoSnapshot();
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
                PushUndoSnapshot();
            };

            check.Unchecked += (s, e) =>
            {
                _selectedNode.Properties[propDef.Name] = false;
                PushUndoSnapshot();
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

            textBox.LostFocus += (s, e) =>
            {
                if (int.TryParse(textBox.Text, out var intValue))
                {
                    _selectedNode.Properties[propDef.Name] = intValue;
                    PushUndoSnapshot();
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

            textBox.LostFocus += (s, e) =>
            {
                if (float.TryParse(textBox.Text, out var floatValue))
                {
                    _selectedNode.Properties[propDef.Name] = floatValue;
                    PushUndoSnapshot();
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

            textBox.LostFocus += (s, e) =>
            {
                _selectedNode.Properties[propDef.Name] = textBox.Text;
                PushUndoSnapshot();
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
                PushUndoSnapshot();
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

        textBox.LostFocus += (s, e) =>
        {
            if (_selectedNode != null)
            {
                _selectedNode.Comment = string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text;
                PushUndoSnapshot();
            }
        };

        PropertiesPanel.Children.Add(textBox);
    }

    #endregion

    private void ScriptEditorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Centrar la vista automáticamente al abrir
        ScriptPanel.CenterView();
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

/// <summary>
/// Snapshot of script state for undo/redo
/// </summary>
public class ScriptSnapshot
{
    public string Name { get; set; } = "";
    public string NodesJson { get; set; } = "";
    public string ConnectionsJson { get; set; } = "";
}

/// <summary>
/// Simple undo/redo manager for scripts
/// </summary>
public class ScriptUndoRedoManager
{
    private readonly List<ScriptSnapshot> _undoStack = new();
    private readonly List<ScriptSnapshot> _redoStack = new();
    private readonly int _maxSize;

    public ScriptUndoRedoManager(int maxSize = 50)
    {
        _maxSize = maxSize;
    }

    public bool CanUndo => _undoStack.Count > 1;
    public bool CanRedo => _redoStack.Count > 0;

    public void Push(ScriptSnapshot snapshot)
    {
        _undoStack.Add(snapshot);
        _redoStack.Clear();

        while (_undoStack.Count > _maxSize)
        {
            _undoStack.RemoveAt(0);
        }
    }

    public ScriptSnapshot? Undo()
    {
        if (_undoStack.Count <= 1) return null;

        var current = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _redoStack.Add(current);

        return _undoStack[^1];
    }

    public ScriptSnapshot? Redo()
    {
        if (_redoStack.Count == 0) return null;

        var snapshot = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _undoStack.Add(snapshot);

        return snapshot;
    }
}
