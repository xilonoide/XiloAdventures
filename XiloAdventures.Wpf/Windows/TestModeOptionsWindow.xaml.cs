using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Diagnostics;
using XiloAdventures.Wpf.Common.Windows;

namespace XiloAdventures.Wpf.Windows;

public partial class TestModeOptionsWindow : Window
{
    public TestModeOptionsWindow()
    {
        InitializeComponent();
    }

    public bool SoundEnabled
    {
        get => SoundCheckBox.IsChecked == true;
        set => SoundCheckBox.IsChecked = value;
    }

    public bool AiEnabled
    {
        get => AiCheckBox.IsChecked == true;
        set => AiCheckBox.IsChecked = value;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        DialogResult = true;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void AiInfoIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var message = "Si activas la IA, el modo pruebas intentará entender mejor comandos complejos o mal escritos. Además, si subes el volumen de voz en las opciones, oirás las descripciones de las salas.\n\nPara usarla debes tener Docker Desktop instalado y en ejecución.";

        var link = new System.Windows.Controls.TextBlock
        {
            Margin = new Thickness(0, 12, 0, 0)
        };
        var hyperlink = new System.Windows.Documents.Hyperlink
        {
            NavigateUri = new System.Uri("https://docs.docker.com/desktop/setup/install/windows-install/")
        };
        hyperlink.Inlines.Add("Instala Docker Desktop");
        hyperlink.RequestNavigate += AiHelpLink_RequestNavigate;
        link.Inlines.Add(hyperlink);

        var dlg = new AlertWindow(message, "Ayuda sobre la IA")
        {
            Owner = this
        };
        dlg.SetCustomContent(link);
        dlg.HideOkButton();
        dlg.ShowDialog();
    }

    private void AiHelpLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch
        {
            // Ignoramos errores al abrir el navegador
        }
    }
}
