using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using BAKKA_Editor.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;

namespace BAKKA_Editor.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static IPassSetting? Target()
    {
        var lifetime = Application.Current?.ApplicationLifetime;
        if (lifetime is IClassicDesktopStyleApplicationLifetime)
            return (IPassSetting?)
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.MainWindow;

        return (IPassSetting?)
            (Application.Current?.ApplicationLifetime as ISingleViewApplicationLifetime)
            ?.MainView;
    }
    public MainViewModel()
    {
        OpenInitialChartSettings = ReactiveCommand.CreateFromTask(async () =>
        {
            var mainWindow = Target();
            if (mainWindow != null)
                await mainWindow.OpenChartSettings_OnClick();
        });
        NewCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var mainWindow = Target();
            if (mainWindow != null)
                await mainWindow.NewMenuItem_OnClick()!;
        });
        OpenCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var mainWindow = Target();
            if (mainWindow != null)
                await mainWindow.OpenMenuItem_OnClick()!;
        });
        SaveCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var mainWindow = Target();
            if (mainWindow != null)
                await mainWindow.SaveMenuItem_OnClick()!;
        });
        SaveAsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var mainWindow = Target();
            if (mainWindow != null)
                await mainWindow.SaveAsMenuItem_OnClick()!;
        });
        ExitCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var mainWindow = Target();
            if (mainWindow != null)
                await mainWindow.ExitMenuItem_OnClick()!;
        });
        UndoCommand = ReactiveCommand.Create(() =>
        {
            var mainWindow = Target();
            mainWindow?.UndoMenuItem_OnClick();
        });
        RedoCommand = ReactiveCommand.Create(() =>
        {
            var mainWindow = Target();
            mainWindow?.RedoMenuItem_OnClick();
        });
        ToggleShowCursorCommand = ReactiveCommand.Create(() =>
        {
            ShowCursor = !ShowCursor;
            
            var mainWindow = Target();
            mainWindow?.SetShowCursor(ShowCursor);
        });
        ToggleShowCursorDuringPlaybackCommand = ReactiveCommand.Create(() =>
        {
            ShowCursorDuringPlayback = !ShowCursorDuringPlayback;
            
            var mainWindow = Target();
            mainWindow?.SetShowCursorDuringPlayback(ShowCursorDuringPlayback);
        });
        ToggleHighlightViewedNoteCommand = ReactiveCommand.Create(() =>
        {
            HighlightViewedNote = !HighlightViewedNote;
            
            var mainWindow = Target();
            mainWindow?.SetHighlightViewedNote(HighlightViewedNote);
        });
        ToggleSelectLastInsertedNoteCommand = ReactiveCommand.Create(() =>
        {
            SelectLastInsertedNote = !SelectLastInsertedNote;
            
            var mainWindow = Target();
            mainWindow?.SetSelectLastInsertedNote(SelectLastInsertedNote);
        });
        ToggleGimmicksInCircleViewCommand = ReactiveCommand.Create(() =>
        {
            ShowGimmicksInCircleView = !ShowGimmicksInCircleView;
            
            var mainWindow = Target();
            mainWindow?.SetShowGimmicksInCircleView(ShowGimmicksInCircleView);
        });
        ToggleShowGimmicksDuringPlaybackInCircleViewCommand = ReactiveCommand.Create(() =>
        {
            ShowGimmicksDuringPlaybackInCircleView = !ShowGimmicksDuringPlaybackInCircleView;

            var mainWindow = Target();
            mainWindow?.SetShowGimmicksDuringPlaybackInCircleView(ShowGimmicksDuringPlaybackInCircleView);
        });
        ToggleDarkModeViewCommand = ReactiveCommand.Create(() =>
        {
            DarkMode = !DarkMode;

            var mainWindow = Target();
            mainWindow?.SetDarkMode(DarkMode);
        });
        ToggleShowMeasureButtonsCommand = ReactiveCommand.Create(() =>
        {
            AreMeasureButtonsVisible = !AreMeasureButtonsVisible;

            var mainWindow = Target();
            mainWindow?.SetShowMeasureButtons(AreMeasureButtonsVisible);
        });
    }

    // View Model Settings State
    [ObservableProperty] private bool showCursor = true;
    [ObservableProperty] private bool showCursorDuringPlayback = false;
    [ObservableProperty] private bool highlightViewedNote = true;
    [ObservableProperty] private bool selectLastInsertedNote = true;
    [ObservableProperty] private bool showGimmicksInCircleView = true;
    [ObservableProperty] private bool showGimmicksDuringPlaybackInCircleView = true;
    
    [ObservableProperty] private bool darkMode = false;
    [ObservableProperty] private bool areMeasureButtonsVisible = false;
    
    // Button Text
    [ObservableProperty] private string insertButtonText = "Insert Object (I)";

    // Commands
    public ReactiveCommand<Unit, Unit> NewCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; set; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ToggleShowCursorCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ToggleShowCursorDuringPlaybackCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ToggleHighlightViewedNoteCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ToggleSelectLastInsertedNoteCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ToggleGimmicksInCircleViewCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ToggleShowGimmicksDuringPlaybackInCircleViewCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ToggleDarkModeViewCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ToggleShowMeasureButtonsCommand { get; set; }
    public ReactiveCommand<Unit, Unit> OpenInitialChartSettings { get; }
}