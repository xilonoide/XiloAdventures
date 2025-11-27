
using System.Windows;

namespace XiloAdventures.Wpf.Windows;

public partial class AlertWindow : Window
{
    public AlertWindow(string message, string title)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
