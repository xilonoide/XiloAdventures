using System.Windows;
using System.Windows.Input;

namespace XiloAdventures.Wpf.Windows;

/// <summary>
/// Diálogo de error con tema oscuro que muestra mensajes detallados.
/// </summary>
public partial class DarkErrorDialog : Window
{
    public DarkErrorDialog(string title, string message, Window? owner = null, bool showCopyButton = false)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;
        CopyButton.Visibility = showCopyButton ? Visibility.Visible : Visibility.Collapsed;

        if (owner != null)
        {
            Owner = owner;
        }
    }

    /// <summary>
    /// Muestra un diálogo de error con tema oscuro.
    /// </summary>
    /// <param name="title">Título del diálogo.</param>
    /// <param name="message">Mensaje de error detallado.</param>
    /// <param name="owner">Ventana padre opcional.</param>
    public static void Show(string title, string message, Window? owner = null)
    {
        var dialog = new DarkErrorDialog(title, message, owner, showCopyButton: false);
        dialog.ShowDialog();
    }

    /// <summary>
    /// Muestra un diálogo de error para una excepción (con botón copiar).
    /// </summary>
    /// <param name="title">Título del diálogo.</param>
    /// <param name="ex">Excepción a mostrar.</param>
    /// <param name="owner">Ventana padre opcional.</param>
    public static void ShowException(string title, Exception ex, Window? owner = null)
    {
        var message = ex.Message;
        if (ex.InnerException != null)
        {
            message += "\n\nDetalles internos:\n" + ex.InnerException.Message;
        }
        var dialog = new DarkErrorDialog(title, message, owner, showCopyButton: true);
        dialog.ShowDialog();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Doble clic para maximizar/restaurar
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(MessageText.Text);
            CopyButton.Content = "Copiado!";

            // Restaurar el texto después de 2 segundos
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, args) =>
            {
                CopyButton.Content = "Copiar";
                timer.Stop();
            };
            timer.Start();
        }
        catch
        {
            // Ignorar errores del portapapeles
        }
    }
}
