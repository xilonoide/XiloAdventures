using System.Windows;

namespace XiloAdventures.Wpf.Windows;

public enum SaveChangesResult
{
    Save,
    DontSave,
    Cancel
}

public partial class SaveChangesWindow : Window
{
    public SaveChangesResult Result { get; private set; }

    public SaveChangesWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        Result = SaveChangesResult.Cancel;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Result = SaveChangesResult.Save;
        DialogResult = true;
        Close();
    }

    private void DontSaveButton_Click(object sender, RoutedEventArgs e)
    {
        Result = SaveChangesResult.DontSave;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = SaveChangesResult.Cancel;
        DialogResult = false;
        Close();
    }
}
