using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using XiloAdventures.Engine.Models;
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
                            if (_currentObject is not { } target) return;

                            var dlg = new OpenFileDialog
                            {
                                Filter = "Audio (*.mp3;*.wav)|*.mp3;*.wav|Todos los archivos (*.*)|*.*",
                                InitialDirectory = System.IO.Directory.Exists(AppPaths.SoundFolder)
                                    ? AppPaths.SoundFolder
                                    : AppPaths.BaseDirectory
                            };

                            if (dlg.ShowDialog() == true)
                            {
                                var fileName = System.IO.Path.GetFileName(dlg.FileName);
                                tb.Text = fileName;
                                prop.SetValue(target, fileName);
                                PropertyEdited?.Invoke(target, prop.Name);
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
                            if (_currentObject is not { } target) return;

                            var dlg = new OpenFileDialog
                            {
                                Filter = "Imágenes (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos los archivos (*.*)|*.*",
                                InitialDirectory = System.IO.Path.Combine(AppPaths.BaseDirectory, "Images")
                            };

                            if (dlg.ShowDialog() == true)
                            {
                                var fileName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                                tb.Text = fileName;
                                prop.SetValue(target, fileName);
                                PropertyEdited?.Invoke(target, prop.Name);
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
                            if (_currentObject is not { } target) return;

                            var dlg = new OpenFileDialog
                            {
                                Filter = "Audio (*.mp3;*.wav)|*.mp3;*.wav|Todos los archivos (*.*)|*.*",
                                InitialDirectory = AppPaths.SoundFolder
                            };

                            if (dlg.ShowDialog() == true)
                            {
                                var fileName = System.IO.Path.GetFileName(dlg.FileName);
                                tb.Text = fileName;
                                prop.SetValue(target, fileName);
                                PropertyEdited?.Invoke(target, prop.Name);
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
}