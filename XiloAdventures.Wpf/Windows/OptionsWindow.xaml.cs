using System;
using System.Windows;
using System.Windows.Controls;
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
            FontSize = settings.FontSize
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

        SoundCheckBox.Checked += SoundCheckBox_Changed;
        SoundCheckBox.Unchecked += SoundCheckBox_Changed;
    }

    private void SoundCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _settings.SoundEnabled = SoundCheckBox.IsChecked == true;
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
