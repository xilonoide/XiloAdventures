using System.Collections.Generic;
using System;
using System.IO;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XiloAdventures.Engine.Models;
using XiloAdventures.Wpf.Windows;
using Microsoft.Win32;

namespace XiloAdventures.Wpf.Controls;

public partial class PropertyEditor : UserControl
{
    private object? _currentObject;

    public event Action<object?, string>? PropertyEdited;

    public Func<IEnumerable<Room>>? GetRooms { get; set; }


    public PropertyEditor()
    {
        InitializeComponent();
    }

    public void SetObject(object? obj)
    {
        _currentObject = obj;
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
    .OrderBy(p => p.Name);

        foreach (var prop in props)
        {
            FrameworkElement editor;
            var label = new TextBlock
            {
                Text = prop.Name,
                Margin = new Thickness(0, 4, 0, 0),
                FontWeight = FontWeights.Bold
            };
            RootPanel.Children.Add(label);

            if (prop.PropertyType == typeof(bool))
            {
                var chk = new CheckBox
                {
                    IsChecked = (bool?)prop.GetValue(obj),
                    Margin = new Thickness(0, 2, 0, 0)
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

                editor = chk;
            }


            else
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
                    var panel = new Grid
                    {
                        Margin = new Thickness(0, 2, 0, 0),
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                            new ColumnDefinition { Width = GridLength.Auto }
                        }
                    };

                    var tbJson = new TextBox
                    {
                        Margin = new Thickness(0, 0, 4, 0),
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        MinHeight = 80,
                        Text = Convert.ToString(prop.GetValue(obj)) ?? string.Empty
                    };
                    Grid.SetColumn(tbJson, 0);

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

                    var helpIcon = new TextBlock
                    {
                        Text = "?",
                        Foreground = Brushes.Yellow,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 2, 0, 0),
                        Cursor = Cursors.Hand,
                        VerticalAlignment = VerticalAlignment.Top,
                        ToolTip = "Ayuda sobre el diccionario del parser"
                    };
                    helpIcon.MouseLeftButtonUp += (_, _) => ShowParserDictionaryHelp();
                    Grid.SetColumn(helpIcon, 1);

                    panel.Children.Add(tbJson);
                    panel.Children.Add(helpIcon);

                    editor = panel;
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
                                    {
                                    var msg = $"El archivo de música es demasiado grande ({fileInfo.Length / (1024 * 1024)} MB).\n" +
                                              "El tamaño máximo permitido es de 20MB.";
                                    new AlertWindow(msg, "Archivo demasiado grande")
                                    {
                                        Owner = Window.GetWindow(this)
                                    }.ShowDialog();
                                };
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
                                    {
                                    var msg = $"El archivo de música es demasiado grande ({fileInfo.Length / (1024 * 1024)} MB).\n" +
                                              "El tamaño máximo permitido es de 20MB.";
                                    new AlertWindow(msg, "Archivo demasiado grande")
                                    {
                                        Owner = Window.GetWindow(this)
                                    }.ShowDialog();
                                };
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

                    var tb = new TextBox
                    {
                        Text = text,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    tb.LostFocus += (_, _) =>
                    {
                        try
                        {
                            if (_currentObject is not { } target) return;

                            object? value = tb.Text;
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
                                var parts = tb.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                                var list = new List<string>();
                                foreach (var p in parts)
                                {
                                    var s = p.Trim();
                                    if (!string.IsNullOrEmpty(s))
                                        list.Add(s);
                                }
                                value = list;
                            }

                            prop.SetValue(target, value);
                            PropertyEdited?.Invoke(target, prop.Name);
                        }
                        catch
                        {
                            // Ignorar errores de conversión
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
            }
            RootPanel.Children.Add(editor);
        }
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
}
