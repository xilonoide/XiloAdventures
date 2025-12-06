using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Documents;
using System.Diagnostics;
using XiloAdventures.Wpf.Ui;
using XiloAdventures.Wpf.Services;

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
            UseLlmForUnknownCommands = settings.UseLlmForUnknownCommands,
            MusicVolume = settings.MusicVolume,
            EffectsVolume = settings.EffectsVolume,
            MasterVolume = settings.MasterVolume,
            VoiceVolume = settings.VoiceVolume
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

        MusicVolumeSlider.Value = _settings.MusicVolume;
        EffectsVolumeSlider.Value = _settings.EffectsVolume;
        MasterVolumeSlider.Value = _settings.MasterVolume;
        VoiceVolumeSlider.Value = _settings.VoiceVolume;

        MusicVolumeLabel.Text = _settings.MusicVolume.ToString("0");
        EffectsVolumeLabel.Text = _settings.EffectsVolume.ToString("0");
        MasterVolumeLabel.Text = _settings.MasterVolume.ToString("0");
        VoiceVolumeLabel.Text = _settings.VoiceVolume.ToString("0");

        var soundEnabled = _settings.SoundEnabled;
        MusicVolumeSlider.IsEnabled = soundEnabled;
        EffectsVolumeSlider.IsEnabled = soundEnabled;
        MasterVolumeSlider.IsEnabled = soundEnabled;
        VoiceVolumeSlider.IsEnabled = soundEnabled;

        SoundCheckBox.Checked += SoundCheckBox_Changed;
        SoundCheckBox.Unchecked += SoundCheckBox_Changed;

        UseLlmCheckBox.Checked += UseLlmCheckBox_Changed;
        UseLlmCheckBox.Unchecked += UseLlmCheckBox_Changed;
    }

    private void SoundCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = SoundCheckBox.IsChecked == true;
        _settings.SoundEnabled = enabled;

        MusicVolumeSlider.IsEnabled = enabled;
        EffectsVolumeSlider.IsEnabled = enabled;
        MasterVolumeSlider.IsEnabled = enabled;
        VoiceVolumeSlider.IsEnabled = enabled;

        ApplyChanges();
    }


    private async void UseLlmCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (UseLlmCheckBox.IsChecked == true)
        {
            var progressWindow = new DockerProgressWindow
            {
                Owner = this
            };

            var result = await progressWindow.RunAsync().ConfigureAwait(true);

            if (result.Canceled)
            {
                UseLlmCheckBox.IsChecked = false;
                _settings.UseLlmForUnknownCommands = false;

                UiSettingsManager.SaveForWorld(_worldId, _settings);
                return;
            }

            if (!result.Success)
            {
                UseLlmCheckBox.IsChecked = false;
                _settings.UseLlmForUnknownCommands = false;

                UiSettingsManager.SaveForWorld(_worldId, _settings);

                new AlertWindow(
                    "No se han podido iniciar los servicios de IA y voz." +
                    "Comprueba que Docker Desktop estÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ instalado y en ejecuciÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³n.",
                    "Error")
                {
                    Owner = this
                }.ShowDialog();

                return;
            }

            _settings.UseLlmForUnknownCommands = true;
        }
        else
        {
            _settings.UseLlmForUnknownCommands = false;
        }

        ApplyChanges();
    }

    private void MusicVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MusicVolumeLabel != null)
        {
            _settings.MusicVolume = e.NewValue;
            MusicVolumeLabel.Text = _settings.MusicVolume.ToString("0");
            ApplyChanges();
        }
    }

    private void EffectsVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (EffectsVolumeLabel != null)
        {
            _settings.EffectsVolume = e.NewValue;
            EffectsVolumeLabel.Text = _settings.EffectsVolume.ToString("0");
            ApplyChanges();
        }
    }

    private void VoiceVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VoiceVolumeLabel != null)
        {
            _settings.VoiceVolume = e.NewValue;
            VoiceVolumeLabel.Text = _settings.VoiceVolume.ToString("0");
            ApplyChanges();
        }
    }

    private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MasterVolumeLabel != null)
        {
            _settings.MasterVolume = e.NewValue;
            MasterVolumeLabel.Text = _settings.MasterVolume.ToString("0");
            ApplyChanges();
        }
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
        UiSettingsManager.SaveForWorld(_worldId, _settings);
        _onChanged?.Invoke(_settings);
    }

    private void LlmInfoIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var message = "Si activas la IA, el juego intentara entender mejor comandos complejos o mal escritos. Ademas, si subes el volumen de voz en las opciones, oiras las descripciones de las salas.\n\nPara usarla debes tener Docker Desktop instalado y funcionando. La primera vez que se use se descargaran algunas cosas y puede tardar unos minutos. Despues funcionara muy rapido.";

        var link = new TextBlock
        {
            Margin = new Thickness(0, 12, 0, 0)
        };
        var hyperlink = new Hyperlink
        {
            NavigateUri = new Uri("https://docs.docker.com/desktop/setup/install/windows-install/")
        };
        hyperlink.Inlines.Add("Instala Docker Desktop");
        hyperlink.RequestNavigate += LlmHelpLink_RequestNavigate;
        link.Inlines.Add(hyperlink);

        var dlg = new AlertWindow(message, "IA y voz")
        {
            Owner = this
        };
        dlg.SetCustomContent(link);
        dlg.HideOkButton();
        dlg.ShowDialog();
    }

    private void LlmHelpLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
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
            // Ignorar errores al abrir el navegador
        }
    }
}
