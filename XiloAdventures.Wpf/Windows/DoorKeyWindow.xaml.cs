using System.Collections.Generic;
using System.Linq;
using System.Windows;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Wpf.Windows;

public partial class DoorKeyWindow : Window
{
    private readonly Door _door;
    private readonly List<GameObject> _objects;

    public string? SelectedObjectId { get; private set; }

    public DoorKeyWindow(Door door, IEnumerable<GameObject> objects)
    {
        InitializeComponent();

        _door = door;
        _objects = objects.ToList();

        DoorNameText.Text = string.IsNullOrWhiteSpace(door.Name) ? door.Id : door.Name;

        ObjectCombo.ItemsSource = _objects;
        if (_objects.Count > 0)
        {
            ObjectCombo.SelectedIndex = 0;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectCombo.SelectedValue is string id && !string.IsNullOrWhiteSpace(id))
        {
            SelectedObjectId = id;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show(this,
                "Debes seleccionar un objeto que representará la llave.",
                "Crear llave",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
