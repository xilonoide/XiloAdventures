using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Common.Windows;
using XiloAdventures.Wpf.Windows;

namespace XiloAdventures.Wpf.Controls;

public partial class PropertyEditor : UserControl
{
    private PasswordBox? _encryptionPasswordBox;
    private object? _currentObject;

    public event Action<object?, string>? PropertyEdited;

    public PasswordBox? EncryptionPasswordBox => _encryptionPasswordBox;

    public Func<IEnumerable<Room>>? GetRooms { get; set; }


    public PropertyEditor()
    {
        InitializeComponent();
    }

    public void SetObject(object? obj)
    {
        _currentObject = obj;
        _encryptionPasswordBox = null;
        RootPanel.Children.Clear();

        if (obj == null)
            return;

        var props = obj.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .Where(p =>
            {
                var browsable = p.GetCustomAttribute<BrowsableAttribute>();
                return browsable == null || browsable.Browsable;
            })
            .Where(p => p.Name != "Exits")
            .ToList();

        // Agrupar propiedades por categoría
        var groups = GroupProperties(obj, props);

        bool isFirst = true;
        foreach (var group in groups)
        {
            if (!group.Properties.Any())
                continue;

            // Añadir separador entre grupos (excepto el primero)
            if (!isFirst)
            {
                RootPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                    Margin = new Thickness(0, 12, 0, 0)
                });
            }
            isFirst = false;

            // Header del grupo
            var header = new TextBlock
            {
                Text = group.Name.ToUpper(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0xFF)),
                Margin = new Thickness(0, isFirst ? 0 : 12, 0, 4)
            };
            RootPanel.Children.Add(header);

            // Propiedades del grupo
            foreach (var prop in group.Properties)
            {
                AddPropertyControl(obj, prop);
            }
        }
    }

    private record PropertyGroup(string Name, List<PropertyInfo> Properties);

    private List<PropertyGroup> GroupProperties(object obj, List<PropertyInfo> props)
    {
        var groups = new Dictionary<string, List<PropertyInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            ["🔖 Identificación"] = new(),
            ["📝 Descripción"] = new(),
            ["🎵 Multimedia"] = new(),
            ["🔗 Conexiones"] = new(),
            ["⚙️ Comportamiento"] = new(),
            ["📦 Contenido"] = new(),
            ["📊 Estadísticas"] = new(),
            ["🔒 Seguridad"] = new(),
            ["🏷️ Otros"] = new()
        };

        foreach (var prop in props)
        {
            var category = GetPropertyCategory(obj, prop);
            if (groups.ContainsKey(category))
            {
                groups[category].Add(prop);
            }
            else
            {
                groups["🏷️ Otros"].Add(prop);
            }
        }

        // Ordenar propiedades dentro de cada grupo
        foreach (var group in groups.Values)
        {
            group.Sort((a, b) => GetPropertyOrder(a).CompareTo(GetPropertyOrder(b)));
        }

        // Retornar solo los grupos que tienen propiedades, en orden
        var orderedCategories = new[]
        {
            "🔖 Identificación",
            "📝 Descripción",
            "🎵 Multimedia",
            "🔗 Conexiones",
            "⚙️ Comportamiento",
            "📦 Contenido",
            "📊 Estadísticas",
            "🔒 Seguridad",
            "🏷️ Otros"
        };

        return orderedCategories
            .Where(cat => groups[cat].Any())
            .Select(cat => new PropertyGroup(cat, groups[cat]))
            .ToList();
    }

    private static string GetPropertyCategory(object obj, PropertyInfo prop)
    {
        var name = prop.Name;

        // Identificación
        if (name is "Id" or "Name" or "Title")
            return "🔖 Identificación";

        // Descripción
        if (name is "Description" or "Dialogue")
            return "📝 Descripción";

        // Multimedia
        if (name.Contains("Image") || name.Contains("Music"))
            return "🎵 Multimedia";

        // Conexiones (salas, puertas, etc.)
        if (name is "RoomId" or "RoomIdA" or "RoomIdB" or "StartRoomId" or "TargetRoomId" or "Direction")
            return "🔗 Conexiones";

        // Comportamiento
        if (name is "Visible" or "CanTake" or "IsContainer" or "IsLocked" or "IsIlluminated"
            or "IsInterior" or "Behavior" or "StartHour" or "StartWeather" or "MinutesPerGameHour"
            or "RequiredQuestId" or "RequiredQuestStatus")
            return "⚙️ Comportamiento";

        // Contenido
        if (name is "ContainedObjectIds" or "InventoryObjectIds" or "Objectives" or "KeyId" or "LockId" or "LockIds" or "DoorId" or "ObjectId")
            return "📦 Contenido";

        // Estadísticas
        if (name is "Level" or "Strength" or "Dexterity" or "Intelligence" or "MaxHealth"
            or "CurrentHealth" or "Gold" or "BaseValue" or "Quality" or "Stats")
            return "📊 Estadísticas";

        // Seguridad
        if (name is "EncryptionKey")
            return "🔒 Seguridad";

        // Parser Dictionary es especial
        if (name is "ParserDictionaryJson")
            return "⚙️ Comportamiento";

        // Tags
        if (name is "Tags")
            return "🏷️ Otros";

        return "🏷️ Otros";
    }

    private static int GetPropertyOrder(PropertyInfo prop)
    {
        // Orden de prioridad para propiedades dentro de su grupo
        return prop.Name switch
        {
            "Id" => 0,
            "Name" => 1,
            "Title" => 2,
            "Description" => 0,
            "Dialogue" => 1,
            "ImageId" => 0,
            "ImageBase64" => 1,
            "MusicId" => 2,
            "MusicBase64" => 3,
            "WorldMusicId" => 4,
            "WorldMusicBase64" => 5,
            "StartRoomId" => 0,
            "RoomId" => 1,
            "RoomIdA" => 2,
            "RoomIdB" => 3,
            "Direction" => 4,
            "TargetRoomId" => 5,
            _ => 99
        };
    }

    private void AddPropertyControl(object obj, PropertyInfo prop)
    {
        // Booleans: label y checkbox en la misma fila, centrados verticalmente.
        if (prop.PropertyType == typeof(bool))
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var chk = new CheckBox
            {
                IsChecked = (bool?)prop.GetValue(obj),
                VerticalAlignment = VerticalAlignment.Center
            };

            chk.Checked += (_, _) =>
            {
                try
                {
                    if (_currentObject is not { } target) return;
                    prop.SetValue(target, true);
                    PropertyEdited?.Invoke(target, prop.Name);
                }
                catch
                {
                    // Ignorar errores
                }
            };

            chk.Unchecked += (_, _) =>
            {
                try
                {
                    if (_currentObject is not { } target) return;
                    prop.SetValue(target, false);
                    PropertyEdited?.Invoke(target, prop.Name);
                }
                catch
                {
                    // Ignorar errores
                }
            };

            panel.Children.Add(chk);

            var boolLabel = new TextBlock
            {
                Text = GetDisplayLabel(prop),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            panel.Children.Add(boolLabel);

            RootPanel.Children.Add(panel);
            return;
        }

        FrameworkElement editor;
        var label = new TextBlock
        {
            Text = GetDisplayLabel(prop),
            Margin = new Thickness(0, 10, 0, 3),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
        };

        if (obj is GameInfo && prop.Name == "ParserDictionaryJson" && prop.PropertyType == typeof(string))
        {
            var labelPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = label.Margin
            };
            label.Margin = new Thickness(0);
            labelPanel.Children.Add(label);

            var helpIcon = new TextBlock
            {
                Text = "?",
                Foreground = Brushes.Yellow,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Ayuda sobre el diccionario del parser"
            };
            helpIcon.MouseLeftButtonUp += (_, _) => ShowParserDictionaryHelp();
            labelPanel.Children.Add(helpIcon);

            RootPanel.Children.Add(labelPanel);
        }
        else if (obj is Room && prop.Name == "MusicId" && prop.PropertyType == typeof(string))
        {
            var labelPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = label.Margin
            };
            label.Margin = new Thickness(0);
            labelPanel.Children.Add(label);

            var helpIcon = new TextBlock
            {
                Text = "?",
                Foreground = Brushes.Yellow,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Ayuda sobre música de sala"
            };
            helpIcon.MouseLeftButtonUp += (_, _) => ShowMusicIdHelp();
            labelPanel.Children.Add(helpIcon);

            RootPanel.Children.Add(labelPanel);
        }
        else
        {
            RootPanel.Children.Add(label);
        }

        {
            // StartRoomId: selector de sala
            // Selectores de sala (StartRoomId, RoomId, RoomIdA, RoomIdB)
            if (prop.PropertyType == typeof(string) && GetRooms != null &&
                (prop.Name == "StartRoomId" || prop.Name == "RoomId" || prop.Name == "RoomIdA" || prop.Name == "RoomIdB"))
            {
                var rooms = GetRooms().ToList();
                var combo = new ComboBox
                {
                    Margin = new Thickness(0, 2, 0, 0),
                    DisplayMemberPath = "Name",
                    SelectedValuePath = "Id",
                    ItemsSource = rooms
                };

                if (obj is Door && (prop.Name == "RoomIdA" || prop.Name == "RoomIdB"))
                {
                    combo.IsEnabled = false;
                }

                var currentId = Convert.ToString(prop.GetValue(obj)) ?? string.Empty;
                combo.SelectedValue = currentId;

                combo.SelectionChanged += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not { } target) return;
                        if (combo.SelectedValue is string id)
                        {
                            prop.SetValue(target, id);
                            PropertyEdited?.Invoke(target, prop.Name);
                        }
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                };

                editor = combo;
            }
            else if (prop.PropertyType.IsEnum)
            {
                var comboEnum = new ComboBox
                {
                    Margin = new Thickness(0, 2, 0, 0),
                    ItemsSource = Enum.GetValues(prop.PropertyType)
                };

                comboEnum.SelectedItem = prop.GetValue(obj);

                comboEnum.SelectionChanged += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not { } target) return;
                        if (comboEnum.SelectedItem != null)
                        {
                            prop.SetValue(target, comboEnum.SelectedItem);
                            PropertyEdited?.Invoke(target, prop.Name);
                        }
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                };

                editor = comboEnum;
            }
            else if (obj is GameInfo && prop.Name == "MinutesPerGameHour" && prop.PropertyType == typeof(int))
            {
                var comboMinutes = new ComboBox
                {
                    Margin = new Thickness(0, 2, 0, 0),
                    IsEditable = false
                };

                for (int i = 1; i <= 10; i++)
                    comboMinutes.Items.Add(i);

                var current = prop.GetValue(obj) is int v ? v : 6;
                if (current < 1 || current > 10) current = 6;
                comboMinutes.SelectedItem = current;

                comboMinutes.SelectionChanged += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not { } target) return;
                        if (comboMinutes.SelectedItem is int value)
                        {
                            prop.SetValue(target, value);
                            PropertyEdited?.Invoke(target, prop.Name);
                        }
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                };

                editor = comboMinutes;
            }
            else if (obj is GameInfo && prop.Name == "ParserDictionaryJson" && prop.PropertyType == typeof(string))
            {
                var tbJson = new TextBox
                {
                    Margin = new Thickness(0, 2, 0, 0),
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MinHeight = 80,
                    Text = Convert.ToString(prop.GetValue(obj)) ?? string.Empty
                };

                tbJson.LostKeyboardFocus += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not { } target) return;
                        prop.SetValue(target, tbJson.Text);
                        PropertyEdited?.Invoke(target, prop.Name);
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                };

                editor = tbJson;
            }


            else if (prop.Name == "WorldMusicId" && prop.PropertyType == typeof(string))
            {
                var valueObj = prop.GetValue(obj);
                string text = Convert.ToString(valueObj) ?? string.Empty;

                var panel = new Grid
                {
                    Margin = new Thickness(0, 2, 0, 0)
                };
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tb = new TextBox
                {
                    Text = text,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                Grid.SetColumn(tb, 0);

                var btn = new Button
                {
                    Content = "...",
                    Width = 28,
                    Padding = new Thickness(0, 0, 0, 0)
                };
                Grid.SetColumn(btn, 1);

                tb.LostFocus += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not { } target) return;
                        prop.SetValue(target, tb.Text);
                        PropertyEdited?.Invoke(target, prop.Name);
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                };

                btn.Click += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not GameInfo game) return;

                        var dlg = new OpenFileDialog
                        {
                            Filter = "Audio (*.mp3;*.wav)|*.mp3;*.wav|Todos los archivos (*.*)|*.*",
                            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
                        };

                        if (dlg.ShowDialog() == true)
                        {
                            var fileInfo = new FileInfo(dlg.FileName);
                            const long MaxAudioBytes = 20L * 1024 * 1024; // 20 MB

                            if (fileInfo.Length > MaxAudioBytes)
                            {
                                var msg = $"El archivo de música es demasiado grande ({fileInfo.Length / (1024 * 1024)} MB).\n" +
                                          "El tamaño máximo permitido es de 20MB.";
                                new AlertWindow(msg, "Archivo demasiado grande")
                                {
                                    Owner = Window.GetWindow(this)
                                }.ShowDialog();
                                return;
                            }

                            var fileName = Path.GetFileName(dlg.FileName);
                            var bytes = File.ReadAllBytes(dlg.FileName);
                            var base64 = Convert.ToBase64String(bytes);

                            tb.Text = fileName;
                            game.WorldMusicId = fileName;
                            game.WorldMusicBase64 = base64;

                            PropertyEdited?.Invoke(game, nameof(GameInfo.WorldMusicId));
                            PropertyEdited?.Invoke(game, nameof(GameInfo.WorldMusicBase64));
                        }
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                };

                panel.Children.Add(tb);
                panel.Children.Add(btn);
                editor = panel;
            }


            // ImageId de Room: textbox + botón ... para imagen de sala
            else if (prop.Name == "ImageId" && prop.PropertyType == typeof(string))
            {
                var valueObj = prop.GetValue(obj);
                string text = Convert.ToString(valueObj) ?? string.Empty;

                var panel = new Grid
                {
                    Margin = new Thickness(0, 2, 0, 0)
                };
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tb = new TextBox
                {
                    Text = text,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                Grid.SetColumn(tb, 0);

                var btn = new Button
                {
                    Content = "...",
                    Width = 28,
                    Padding = new Thickness(0, 0, 0, 0)
                };
                Grid.SetColumn(btn, 1);

                // Si el usuario edita manualmente el texto, solo cambiamos el nombre de archivo
                tb.LostFocus += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not { } target) return;
                        prop.SetValue(target, tb.Text);
                        PropertyEdited?.Invoke(target, prop.Name);
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                };

                btn.Click += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not Room room) return;

                        var dlg = new OpenFileDialog
                        {
                            Filter = "Imágenes (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos los archivos (*.*)|*.*",
                            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                        };

                        if (dlg.ShowDialog() == true)
                        {
                            // Guardamos el nombre de archivo (con extensión) y la imagen en Base64 dentro del mundo
                            var fileName = Path.GetFileName(dlg.FileName);
                            var bytes = File.ReadAllBytes(dlg.FileName);
                            var base64 = Convert.ToBase64String(bytes);

                            tb.Text = fileName;
                            room.ImageId = fileName;
                            room.ImageBase64 = base64;

                            PropertyEdited?.Invoke(room, nameof(Room.ImageId));
                            PropertyEdited?.Invoke(room, nameof(Room.ImageBase64));
                        }
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                };

                panel.Children.Add(tb);
                panel.Children.Add(btn);
                editor = panel;
            }


            // MusicId de Room: textbox + botón ... para música de sala
            else if (prop.Name == "MusicId" && prop.PropertyType == typeof(string))
            {
                var valueObj = prop.GetValue(obj);
                var text = Convert.ToString(valueObj) ?? string.Empty;

                var panel = new Grid
                {
                    Margin = new Thickness(0, 2, 0, 0)
                };
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tb = new TextBox
                {
                    Text = text,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                Grid.SetColumn(tb, 0);

                var btn = new Button
                {
                    Content = "...",
                    Width = 28,
                    Padding = new Thickness(0, 0, 0, 0)
                };
                Grid.SetColumn(btn, 1);

                tb.LostFocus += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not { } target) return;
                        prop.SetValue(target, tb.Text);
                        PropertyEdited?.Invoke(target, prop.Name);
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                };

                btn.Click += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not Room room) return;

                        var dlg = new OpenFileDialog
                        {
                            Filter = "Audio (*.mp3;*.wav)|*.mp3;*.wav|Todos los archivos (*.*)|*.*",
                            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
                        };

                        if (dlg.ShowDialog() == true)
                        {
                            var fileInfo = new FileInfo(dlg.FileName);
                            const long MaxAudioBytes = 20L * 1024 * 1024; // 20 MB

                            if (fileInfo.Length > MaxAudioBytes)
                            {
                                var msg = $"El archivo de música es demasiado grande ({fileInfo.Length / (1024 * 1024)} MB).\n" +
                                          "El tamaño máximo permitido es de 20MB.";
                                new AlertWindow(msg, "Archivo demasiado grande")
                                {
                                    Owner = Window.GetWindow(this)
                                }.ShowDialog();
                                return;
                            }

                            var fileName = Path.GetFileName(dlg.FileName);
                            var bytes = File.ReadAllBytes(dlg.FileName);
                            var base64 = Convert.ToBase64String(bytes);

                            tb.Text = fileName;
                            room.MusicId = fileName;
                            room.MusicBase64 = base64;

                            PropertyEdited?.Invoke(room, nameof(Room.MusicId));
                            PropertyEdited?.Invoke(room, nameof(Room.MusicBase64));
                        }
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                };

                panel.Children.Add(tb);
                panel.Children.Add(btn);
                editor = panel;
            }
            else if (prop.PropertyType == typeof(string) &&
                     obj is GameInfo &&
                     string.Equals(prop.Name, "EncryptionKey", StringComparison.OrdinalIgnoreCase))
            {
                // Clave de cifrado: se muestra como password
                var valueObj = prop.GetValue(obj);
                var text = Convert.ToString(valueObj) ?? string.Empty;

                var pb = new PasswordBox
                {
                    Password = text,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                _encryptionPasswordBox = pb;

                pb.LostFocus += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not { } target) return;

                        var trimmed = (pb.Password ?? string.Empty).Trim();

                        // Pedir confirmación si la contraseña ha cambiado y es válida
                        if (!string.IsNullOrEmpty(trimmed) && trimmed.Length == 8)
                        {
                            var currentVal = prop.GetValue(target) as string ?? string.Empty;
                            if (trimmed != currentVal)
                            {
                                // Usamos InputWindow para pedir confirmación
                                // Nota: InputWindow es un cuadro de texto normal, no password, 
                                // pero servirá para que el usuario escriba la clave otra vez y verifique.
                                var confirmDlg = new XiloAdventures.Wpf.Common.Windows.InputWindow(
                                    "Por seguridad, confirme la nueva clave de cifrado:",
                                    "Confirmar clave");

                                if (confirmDlg.ShowDialog() != true || confirmDlg.InputText != trimmed)
                                {
                                    new AlertWindow("La confirmación de la clave no coincide.", "Error")
                                    {
                                        Owner = Window.GetWindow(this)
                                    }.ShowDialog();

                                    // Restaurar valor anterior en el UI
                                    pb.Password = currentVal;
                                    return;
                                }
                            }
                        }

                        prop.SetValue(target, trimmed);
                        PropertyEdited?.Invoke(target, prop.Name);
                    }
                    catch
                    {
                        // Ignorar errores de conversion
                    }
                };

                editor = pb;
            }
            else
            {
                // Texto normal / listas
                var valueObj = prop.GetValue(obj);
                string text;
                if (prop.PropertyType == typeof(List<string>) && valueObj is List<string> list)
                {
                    text = string.Join(", ", list);
                }
                else
                {
                    text = Convert.ToString(valueObj) ?? string.Empty;
                }

                bool isMultilineDescription =
                    prop.PropertyType == typeof(string) &&
                    string.Equals(prop.Name, "Description", StringComparison.OrdinalIgnoreCase) &&
                    (obj is Room || obj is GameObject || obj is Npc);

                var tb = new TextBox
                {
                    Text = text,
                    Margin = new Thickness(0, 2, 0, 0),
                    AcceptsReturn = isMultilineDescription,
                    TextWrapping = isMultilineDescription ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    VerticalScrollBarVisibility = isMultilineDescription ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden,
                    MinHeight = isMultilineDescription ? 80 : 0
                };
                var originalText = text;
                tb.LostFocus += (_, _) =>
                {
                    try
                    {
                        if (_currentObject is not { } target) return;

                        object? value = tb.Text;
                        if (prop.PropertyType == typeof(string) &&
                            obj is GameInfo &&
                            string.Equals(prop.Name, "EncryptionKey", StringComparison.OrdinalIgnoreCase))
                        {
                            var trimmed = (tb.Text ?? string.Empty).Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                            {
                                var length = Encoding.UTF8.GetByteCount(trimmed);
                                if (length != 8 && length != 32)
                                {
                                    new AlertWindow("La clave de cifrado debe ser de 8 caracteres", "Clave inválida")
                                    {
                                        Owner = Window.GetWindow(this)
                                    }.ShowDialog();
                                    tb.Text = originalText;
                                    return;
                                }
                            }

                            value = trimmed;
                        }
                        if (prop.PropertyType == typeof(int))
                        {
                            if (int.TryParse(tb.Text, out var i)) value = i;
                        }
                        else if (prop.PropertyType == typeof(double))
                        {
                            if (double.TryParse(tb.Text, out var d)) value = d;
                        }
                        else if (prop.PropertyType == typeof(List<string>))
                        {
                            var parts = (tb.Text ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            var listVal = new List<string>();
                            foreach (var p in parts)
                            {
                                var s = p.Trim();
                                if (!string.IsNullOrEmpty(s))
                                    listVal.Add(s);
                            }
                            value = listVal;
                        }

                        prop.SetValue(target, value);
                        PropertyEdited?.Invoke(target, prop.Name);
                    }
                    catch
                    {
                        // Ignorar errores de conversion
                    }
                };
                // Para propiedades de texto (como Name), actualizamos en vivo al teclear
                if (prop.PropertyType == typeof(string))
                {
                    tb.TextChanged += (_, _) =>
                    {
                        try
                        {
                            if (_currentObject is not { } target) return;
                            prop.SetValue(target, tb.Text);
                            PropertyEdited?.Invoke(target, prop.Name);
                        }
                        catch
                        {
                            // Ignorar errores
                        }
                    };
                }

                editor = tb;
            }

            RootPanel.Children.Add(editor);
        }
    }

    private static readonly Dictionary<string, string> DisplayNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Genericos
        ["Id"] = "Id",
        ["Name"] = "Nombre",
        ["Description"] = "Descripción",
        ["Title"] = "Título",
        ["MusicId"] = "Música",
        ["MusicBase64"] = "Música (Base64)",
        ["WorldMusicId"] = "Música global (id)",
        ["WorldMusicBase64"] = "Música global (Base64)",
        ["EncryptionKey"] = "Clave de cifrado",
        ["ImageBase64"] = "Imagen (Base64)",
        ["ImageId"] = "Imagen (id)",
        ["RoomId"] = "Sala",
        ["RoomIdA"] = "Sala A",
        ["RoomIdB"] = "Sala B",
        ["TargetRoomId"] = "Sala destino",
        ["Direction"] = "Dirección",
        ["IsIlluminated"] = "Iluminada",
        ["IsInterior"] = "Interior",
        ["KeyId"] = "Llave",
        ["ObjectId"] = "Objeto",
        ["LockId"] = "Cerradura",
        ["LockIds"] = "Cerraduras",
        ["DoorId"] = "Puerta",
        ["Tags"] = "Etiquetas",
        ["StartHour"] = "Hora inicial",
        ["StartWeather"] = "Clima inicial",
        ["RequiredQuestId"] = "Misión requerida",
        ["RequiredQuestStatus"] = "Estado de misión requerido",
        ["Visible"] = "Visible",
        ["CanTake"] = "Se puede coger",
        ["IsContainer"] = "Es contenedor",
        ["ContainedObjectIds"] = "Objetos contenidos",
        ["BaseValue"] = "Valor base",
        ["Quality"] = "Calidad",
        ["InventoryObjectIds"] = "Objetos en inventario",
        ["Dialogue"] = "Diálogo",
        ["Behavior"] = "Comportamiento",
        ["Stats"] = "Estadísticas",
        ["Level"] = "Nivel",
        ["Strength"] = "Fuerza",
        ["Dexterity"] = "Destreza",
        ["Intelligence"] = "Inteligencia",
        ["MaxHealth"] = "Salud máxima",
        ["CurrentHealth"] = "Salud actual",
        ["Gold"] = "Oro",
        ["Objectives"] = "Objetivos",

        // Juego
        ["GameInfo.Title"] = "Título",
        ["GameInfo.StartRoomId"] = "Sala inicial",
        ["GameInfo.MinutesPerGameHour"] = "Minutos por hora de juego",
        ["GameInfo.ParserDictionaryJson"] = "Diccionario parser (JSON)",
        ["GameInfo.StartHour"] = "Hora inicial",
        ["GameInfo.StartWeather"] = "Clima inicial",
        ["GameInfo.WorldMusicId"] = "Música global",
        ["GameInfo.WorldMusicBase64"] = "Música global (Base64)",
        ["GameInfo.EncryptionKey"] = "Clave de cifrado",

        // Sala
        ["Room.Name"] = "Nombre",
        ["Room.Description"] = "Descripción",
        ["Room.ImageBase64"] = "Imagen (Base64)",
        ["Room.MusicId"] = "Música",
        ["Room.MusicBase64"] = "Música (Base64)",
        ["Room.ImageId"] = "Imagen (id)",
        ["Room.RequiredQuestId"] = "Misión requerida",
        ["Room.RequiredQuestStatus"] = "Estado de misión requerido",
        ["Room.Tags"] = "Etiquetas",

        // Objeto
        ["GameObject.RoomId"] = "Sala",
        ["GameObject.CanTake"] = "Se puede coger",
        ["GameObject.IsContainer"] = "Es contenedor",
        ["GameObject.ContainedObjectIds"] = "Objetos contenidos",
        ["GameObject.Tags"] = "Etiquetas",
        ["GameObject.Visible"] = "Visible",
        ["GameObject.BaseValue"] = "Valor base",
        ["GameObject.Quality"] = "Calidad",

        // NPC
        ["Npc.RoomId"] = "Sala",
        ["Npc.Dialogue"] = "Diálogo",
        ["Npc.InventoryObjectIds"] = "Objetos en inventario",
        ["Npc.Behavior"] = "Comportamiento",
        ["Npc.Tags"] = "Etiquetas",
        ["Npc.Visible"] = "Visible",
        ["Npc.Stats"] = "Estadísticas",

        // Puerta
        ["Door.RoomIdA"] = "Sala A",
        ["Door.RoomIdB"] = "Sala B",
        ["Door.LockId"] = "Cerradura",
        ["Door.IsLocked"] = "Esta cerrada",
        ["Door.RequiredQuestId"] = "Misión requerida",
        ["Door.RequiredQuestStatus"] = "Estado de misión requerido",
        ["Door.Tags"] = "Etiquetas",

        // Quest
        ["QuestDefinition.Name"] = "Nombre",
        ["QuestDefinition.Description"] = "Descripción",
        ["QuestDefinition.StartRoomId"] = "Sala inicial",
        ["QuestDefinition.Objectives"] = "Objetivos",

        // Llave
        ["KeyDefinition.ObjectId"] = "Objeto",
        ["KeyDefinition.LockIds"] = "Cerraduras",
    };

    private static string GetDisplayLabel(PropertyInfo prop)
    {
        var keyByType = $"{prop.DeclaringType?.Name}.{prop.Name}";
        if (DisplayNameMap.TryGetValue(keyByType, out var typedLabel))
            return typedLabel;

        if (DisplayNameMap.TryGetValue(prop.Name, out var label))
            return label;

        return SplitCamelCase(prop.Name);
    }

    private static string SplitCamelCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sb = new StringBuilder();
        char? prev = null;
        foreach (var c in input)
        {
            if (prev.HasValue && char.IsUpper(c) && (char.IsLower(prev.Value) || char.IsDigit(prev.Value)))
            {
                sb.Append(' ');
            }
            sb.Append(c);
            prev = c;
        }

        return sb.ToString();
    }

    private void ShowParserDictionaryHelp()
    {
        const string exampleJson = @"{
  ""verbs"": {
    ""mirar"": [""examinar"", ""observar""],
    ""coger"": [""tomar"", ""agarrar""],
    ""usar"": [""emplear""]
  },
  ""nouns"": {
    ""llave"": [""llavin""],
    ""puerta"": [""porton""],
    ""cuerda"": []
  },
  ""adjectives"": {
    ""rojo"": [""bermellon""],
    ""oxidado"": []
  },
  ""stopwords"": [""el"", ""la"", ""los"", ""las"", ""de"", ""del"", ""un"", ""una""]
}";

        var message =
            "Diccionario del parser (por mundo):\n\n" +
            "• Usa JSON para definir sinónimos y palabras que el parser debe reconocer solo en este mundo.\n" +
            "• Secciones habituales:\n" +
            "   - verbs: verbo base -> lista de sinónimos.\n" +
            "   - nouns: sustantivo base -> lista de sinónimos.\n" +
            "   - adjectives: adjetivo base -> lista de sinónimos.\n" +
            "   - stopwords: palabras a ignorar (artículos, preposiciones...).\n" +
            "• El verbo/sustantivo/adjetivo base es el que usas en las reglas y textos; los sinónimos se mapearán a él.\n\n" +
            "Ejemplo completo:\n" + exampleJson;

        var owner = Window.GetWindow(this);
        new AlertWindow(message, "Diccionario del parser")
        {
            Owner = owner
        }.ShowDialog();
    }

    private void ShowMusicIdHelp()
    {
        var message =
            "Recomendaciones para la imagen de sala:\n\n" +
            "• Relación de aspecto recomendada: 3.5:1 (panorámica horizontal)\n" +
            "• Resolución recomendada: 1400x400 píxeles\n\n" +
            "Esto asegurará que la imagen se vea correctamente en el visor de la sala.";

        var owner = Window.GetWindow(this);
        new AlertWindow(message, "Imagen de sala")
        {
            Owner = owner
        }.ShowDialog();
    }

    /// <summary>
    /// Fuerza la actualización de los bindings pendientes del control con foco.
    /// Necesario para que los TextBox y PasswordBox actualicen su valor antes de validar.
    /// </summary>
    public void UpdateBindings()
    {
        // Para el PasswordBox de clave de encriptación, actualizamos directamente el valor
        // ya que no usa bindings sino eventos LostFocus que pueden no haberse disparado aún
        if (_encryptionPasswordBox != null && _currentObject is GameInfo gameInfo)
        {
            var trimmed = (_encryptionPasswordBox.Password ?? string.Empty).Trim();
            gameInfo.EncryptionKey = trimmed;
        }

        // Obtener el elemento con foco actual para TextBox
        var focusedElement = FocusManager.GetFocusedElement(this);
        if (focusedElement is TextBox textBox)
        {
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
        }
    }

    /// <summary>
    /// Actualiza el valor de la clave de encriptación desde el PasswordBox al GameInfo proporcionado.
    /// Útil cuando el usuario puede tener otro objeto seleccionado pero se está validando la clave.
    /// </summary>
    public void UpdateEncryptionKey(GameInfo gameInfo)
    {
        if (_encryptionPasswordBox != null && gameInfo != null)
        {
            var trimmed = (_encryptionPasswordBox.Password ?? string.Empty).Trim();
            gameInfo.EncryptionKey = trimmed;
        }
    }
}
