using System.Reactive;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using ReactiveUI;

namespace BAKKA_Editor.ViewModels;

public partial class ChartSettingsViewModel : ObservableObject
{
    public ChartSettingsViewModel()
    {
        Bpm = 120.0;
        TimeSigUpper = 4;
        TimeSigLower = 4;
        Offset = 0.0;
        MovieOffset = 0.0;
        SaveSettings = false;
        SaveSettingsCommand = new RelayCommand<UserControl>(OnSaveSettings);
        CloseSettingsCommand = new(OnCloseSettings);
    }
    
    [ObservableProperty] private double bpm;
    [ObservableProperty] private int timeSigUpper;
    [ObservableProperty] private int timeSigLower;
    [ObservableProperty] private double offset;
    [ObservableProperty] private double movieOffset;
    [ObservableProperty] private bool saveSettings;
    
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