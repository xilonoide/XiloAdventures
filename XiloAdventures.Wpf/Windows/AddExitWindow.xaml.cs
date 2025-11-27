using System.Collections.Generic;
using System.Linq;
using System.Windows;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Wpf.Windows;

public partial class AddExitWindow : Window
{
    private readonly List<Room> _rooms;

    public string SelectedDirection => DirectionComboBox.SelectedItem as string ?? string.Empty;

    public string SelectedTargetRoomId
    {
        get
        {
            if (RoomComboBox.SelectedItem is Room room)
                return room.Id;
            return string.Empty;
        }
    }

    public AddExitWindow(IEnumerable<Room> rooms, Room currentRoom)
    {
        InitializeComponent();

        _rooms = rooms
            .Where(r => !string.Equals(r.Id, currentRoom.Id, System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        var directions = new[]
        {
            "norte",
            "sur",
            "este",
            "oeste",
            "noreste",
            "noroeste",
            "sureste",
            "suroeste",
            "arriba",
            "abajo"
        };

        DirectionComboBox.ItemsSource = directions;
        DirectionComboBox.SelectedIndex = 0;

        RoomComboBox.ItemsSource = _rooms;
        if (_rooms.Count > 0)
        {
            var selected = _rooms.FirstOrDefault(r => r.Id == currentRoom.Id) ?? _rooms[0];
            RoomComboBox.SelectedItem = selected;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (DirectionComboBox.SelectedItem == null || RoomComboBox.SelectedItem == null)
        {
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
