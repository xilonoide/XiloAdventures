using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Documents;
using System.Diagnostics;
using XiloAdventures.Wpf.Common.Ui;
using XiloAdventures.Wpf.Common.Services;

namespace XiloAdventures.Wpf.Common.Windows;

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



}
