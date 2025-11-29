using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XiloAdventures.Wpf.Ui;

namespace XiloAdventures.Wpf.Windows;

public partial class OptionsWindow : Window
{
    private readonly UiSettings _settings;
    private readonly Action<UiSettings> _onChanged;
    private readonly string _worldId;

    public OptionsWindow(UiSettings settings, Action<UiSettings> onChanged, string worldId)
    {
        _settings = new UiSettings
        {
            SoundEnabled = settings.SoundEnabled,
            FontSize = settings.FontSize,
            UseLlmForUnknownCommands = settings.UseLlmForUnknownCommands
        };
        _onChanged = onChanged;
        _worldId = worldId;

        InitializeComponent();

        Loaded += OptionsWindow_Loaded;
    }

    private void OptionsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SoundCheckBox.IsChecked = _settings.SoundEnabled;
        UseLlmCheckBox.IsChecked = _settings.UseLlmForUnknownCommands;
        FontSizeSlider.Value = _settings.FontSize;
        FontSizeLabel.Text = _settings.FontSize.ToString("0");

        SoundCheckBox.Checked += SoundCheckBox_Changed;
        SoundCheckBox.Unchecked += SoundCheckBox_Changed;

        UseLlmCheckBox.Checked += UseLlmCheckBox_Changed;
        UseLlmCheckBox.Unchecked += UseLlmCheckBox_Changed;
    }

    private void SoundCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _settings.SoundEnabled = SoundCheckBox.IsChecked == true;
        ApplyChanges();
    }


    private void UseLlmCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _settings.UseLlmForUnknownCommands = UseLlmCheckBox.IsChecked == true;
        ApplyChanges();
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontSizeLabel != null)
        {
            _settings.FontSize = e.NewValue;
            FontSizeLabel.Text = _settings.FontSize.ToString("0");
            ApplyChanges();
        }
    }

    private void ApplyChanges()
    {
        _onChanged(_settings);
        UiSettingsManager.SaveForWorld(_worldId, _settings);
    }



    private void LlmInfoIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        const string message =
            "Para usar la IA en las partidas:" + "\n\n" +
            "1. Instala Docker Desktop en tu equipo (Windows)." + "\n" +
            "2. Abre la ventana de Opciones de la partida y marca la casilla \"Usar IA\"." + "\n" +
            "3. Cierra XiloAdventures y vuelve a abrir la aplicación." + "\n" +
            "4. Entra en un mundo (nuevo o cargado) y juega normalmente." + "\n\n" +
            "La primera vez que un comando necesite la IA, XiloAdventures descargará el modelo y arrancará" + "\n" +
            "automáticamente el contenedor Docker. Mientras se descarga, los primeros intentos de usar la IA" + "\n" +
            "pueden dar un mensaje de error. Cuando termine la descarga, la IA funcionará con normalidad.";

        XiloAdventures.Wpf.Windows.AlertWindow.Show("Ayuda sobre IA", message, this);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}