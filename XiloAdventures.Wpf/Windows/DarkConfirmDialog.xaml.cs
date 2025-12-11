using System.Windows;

namespace XiloAdventures.Wpf.Windows;

public partial class DarkConfirmDialog : Window
{
    public DarkConfirmDialog(string title, string message, Window? owner = null)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;

        if (owner != null)
        {
            Owner = owner;
        }
    }

    public static bool Show(string title, string message, Window? owner = null)
    {
        var dialog = new DarkConfirmDialog(title, message, owner);
        return dialog.ShowDialog() == true;
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
