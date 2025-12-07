using System;
using System.Windows;

namespace XiloAdventures.Wpf.Common.Windows;

public partial class AlertWindow : Window
{
    public event EventHandler? Accepted;

    public AlertWindow()
    {
        InitializeComponent();
    }

    public AlertWindow(string message) : this()
    {
        Title = "Aviso";
        MessageTextBlock.Text = message;
    }

    public AlertWindow(string message, string title) : this()
    {
        MessageTextBlock.Text = message;
        Title = title;
    }

    public void SetMessage(string message)
    {
        MessageTextBlock.Text = message;
    }

    public void SetOkButtonText(string text)
    {
        OkButton.Content = text;
    }

    public void ShowCancelButton(bool show = true)
    {
        CancelButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetCustomContent(UIElement? content)
    {
        CustomContentPresenter.Content = content;
        CustomContentPresenter.Visibility = content == null
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public void HideOkButton()
    {
        OkButton.Visibility = Visibility.Collapsed;
    }

    public static void Show(string message, Window? owner = null)
    {
        var w = new AlertWindow(message);
        if (owner != null)
        {
            w.Owner = owner;
        }
        w.ShowDialog();
    }

    public static void Show(string title, string message, Window? owner = null)
    {
        var w = new AlertWindow(title, message);
        if (owner != null)
        {
            w.Owner = owner;
        }
        w.ShowDialog();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Accepted?.Invoke(this, EventArgs.Empty);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
