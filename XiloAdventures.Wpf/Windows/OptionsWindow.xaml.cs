using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            var success = await progressWindow.RunAsync().ConfigureAwait(true);

            if (!success)
            {
                UseLlmCheckBox.IsChecked = false;
                _settings.UseLlmForUnknownCommands = false;

                UiSettingsManager.SaveForWorld(_worldId, _settings);

                new AlertWindow(
                    "No se han podido iniciar los servicios de IA y voz.\n\n" +
                    "Comprueba que Docker Desktop está instalado y en ejecución.",
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

    private void LlmInfoIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var message = "Si activas la IA, el juego intentará entender mejor comandos complejos o mal escritos. " +
                      "Además, si subes el volumen de voz en las opciones, oiras las descripciones de las salas.\n\n" +
                      "Para usarla debes tener Docker Desktop instalado y funcionando. La primera vez que se use " +
                      "se descargará el modelo y puede tardar unos minutos. Después " +
                      "funcionará muy rápido.";

        var dlg = new AlertWindow(message, "Ayuda sobre la IA")
        {
            Owner = this
        };
        dlg.ShowDialog();
    }

    private void ApplyChanges()
    {
        _onChanged(_settings);
        UiSettingsManager.SaveForWorld(_worldId, _settings);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
