using System.Reactive;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;

namespace BAKKA_Editor.ViewModels;

public partial class ChartSettingsViewModel : ObservableObject
{
    [ObservableProperty] private double bpm;
    [ObservableProperty] private double movieOffset;
    [ObservableProperty] private double offset;
    [ObservableProperty] private bool saveSettings;
    [ObservableProperty] private int timeSigLower;
    [ObservableProperty] private int timeSigUpper;

    public ChartSettingsViewModel()
    {
        Bpm = 120.0;
        TimeSigUpper = 4;
        TimeSigLower = 4;
        Offset = 0.0;
        MovieOffset = 0.0;
        SaveSettings = false;
        SaveSettingsCommand = new RelayCommand<UserControl>(OnSaveSettings);
        CloseSettingsCommand = new RelayCommand<Unit>(OnCloseSettings);
    }

    public RelayCommand<UserControl> SaveSettingsCommand { get; }

    public ContentDialog? Dialog { get; set; }
    public RelayCommand<Unit> CloseSettingsCommand { get; set; }

    private void OnSaveSettings(UserControl? userControl)
    {
        SaveSettings = true;

        if (Dialog != null)
            Dialog.Hide();
    }

    private void OnCloseSettings(Unit unit)
    {
        SaveSettings = false;

        if (Dialog != null)
            Dialog.Hide();
    }
}