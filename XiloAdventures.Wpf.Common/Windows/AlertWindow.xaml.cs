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
        HideButtonsIfOnlyOk();
    }

    public AlertWindow(string message, string title) : this()
    {
        MessageTextBlock.Text = message;
        Title = title;
        HideButtonsIfOnlyOk();
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
        HideButtonsIfOnlyOk();
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
        HideButtonsIfOnlyOk();
    }

    private void HideButtonsIfOnlyOk()
    {
        // Si solo hay un botón visible (OK) y no hay botón de cancelar, ocultar todos los botones
        if (OkButton.Visibility == Visibility.Visible && CancelButton.Visibility == Visibility.Collapsed)
        {
            ButtonsGrid.Visibility = Visibility.Collapsed;

            // Permitir cerrar con clic en cualquier parte
            MouseLeftButtonUp += (_, _) =>
            {
                DialogResult = true;
                Close();
            };

            // Permitir cerrar con ESC
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    DialogResult = true;
                    Close();
                }
            };
        }
        else
        {
            ButtonsGrid.Visibility = Visibility.Visible;
        }
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
