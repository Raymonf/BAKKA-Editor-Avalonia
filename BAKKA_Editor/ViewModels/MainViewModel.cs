using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.ApplicationLifetimes;
using BAKKA_Editor.Data;
using BAKKA_Editor.Views;
using BAKKA_Editor.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace BAKKA_Editor.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private Cursor? Cursor;

    [ObservableProperty] private AppSettingsViewModel appSettings = new();

    [ObservableProperty] private bool areMeasureButtonsVisible;

    [ObservableProperty] private bool darkMode;
    [ObservableProperty] private bool highlightViewedNote = true;

    // Button Text
    [ObservableProperty] private string insertButtonText = "Insert Object (I)";
    [ObservableProperty] private bool selectLastInsertedNote = true;

    // View Model Settings State
    // TODO: move this stuff out of here
    [ObservableProperty] private bool showCursor = true;
    [ObservableProperty] private bool showCursorDuringPlayback;
    [ObservableProperty] private bool placeNoteOnDrag = true;
    [ObservableProperty] private bool showNotesOnBeat = false;

    // Note UI State
    public ObservableCollection<NoteOnBeatItem> NotesOnBeatList { get; } = new();
    [ObservableProperty] private bool endHoldChecked = false;

    // Hold note editing
    [ObservableProperty] private bool bakeHoldMenuItemIsEnabled = false;
    [ObservableProperty] private bool insertHoldSegmentMenuItemIsEnabled = false;
    [ObservableProperty] private bool deleteEntireHoldMenuItemIsEnabled = false;

    // Music UI State
    // TODO: move this stuff out of here
    [ObservableProperty] private double sizeTrackBar = 0.0;
    [ObservableProperty] private int sizeTrackBarMinimum = 4;
    [ObservableProperty] private int sizeTrackBarMaximum = 60;

    [ObservableProperty] private double positionTrackBar = 0.0;
    [ObservableProperty] private int positionTrackBarMinimum = 0;
    [ObservableProperty] private int positionTrackBarMaximum = 59;

    [ObservableProperty] private decimal sizeNumeric = 4;
    [ObservableProperty] private int sizeNumericMinimum = 4;
    [ObservableProperty] private int sizeNumericMaximum = 60;

    [ObservableProperty] private decimal positionNumeric = 0;
    [ObservableProperty] private int positionNumericMinimum = 0;
    [ObservableProperty] private int positionNumericMaximum = 59;

    [ObservableProperty] private decimal measureNumeric = 0;
    [ObservableProperty] private int measureNumericMinimum = 0;
    [ObservableProperty] private int measureNumericMaximum = 9999;

    [ObservableProperty] private decimal beat1Numeric = 0;
    [ObservableProperty] private int beat1NumericMinimum = -1;
    [ObservableProperty] private int beat1NumericMaximum = 1920;


    partial void OnBeat1NumericChanged(decimal value)
    {
        if (value == -1m && MeasureNumeric <= 0)
        {
            Beat1Numeric = 0;
        }
    }

    [ObservableProperty] private decimal beat2Numeric = 16;
    [ObservableProperty] private int beat2NumericMinimum = 1;
    [ObservableProperty] private int beat2NumericMaximum = 1920;

    [ObservableProperty] private double songTrackBar = 0.0;
    [ObservableProperty] private int songTrackBarMaximum = 0;

    [ObservableProperty] private double speedTrackBar = 100;
    [ObservableProperty] private int speedTrackBarMaximum = 100;

    [ObservableProperty] private double volumeTrackBar = 100;
    [ObservableProperty] private int volumeTrackBarMaximum = 100;

    [ObservableProperty] private double hitsoundVolumeTrackBar = 50;
    [ObservableProperty] private int hitsoundVolumeMaximum = 100;

    [ObservableProperty] private decimal visualHiSpeedNumeric = 0.5m;
    [ObservableProperty] private double visualHiSpeedNumericMinimum = 0.001;
    [ObservableProperty] private double visualHiSpeedNumericMaximum = 500;

    [ObservableProperty] private decimal visualBeatDivisionNumeric = 2.00m;
    [ObservableProperty] private double visualBeatDivisionNumericMinimum = 1;
    [ObservableProperty] private double visualBeatDivisionNumericMaximum = 1920;

    [ObservableProperty] private int mirrorAxisNumeric = 30;
    [ObservableProperty] private int mirrorAxisNumericMinimum = -1;
    [ObservableProperty] private int mirrorAxisNumericMaximum = 60;

    [ObservableProperty] private int guideLineSelectedIndex = 0;

    [ObservableProperty] private uint cursorBeatDepthNumeric = 0;
    [ObservableProperty] private uint cursorBeatDepthNumericMinimum = 0;
    [ObservableProperty] private uint cursorBeatDepthNumericMaximum = 0;

    partial void OnCursorBeatDepthNumericChanged(uint value)
    {
        Cursor?.Dive(value);

        var mainWindow = Target();
        if (mainWindow != null)
            mainWindow.UpdateTime();
    }

    public void Setup(UserSettings userSettings, ref Cursor cursor)
    {
        ShowCursor = userSettings.ViewSettings.ShowCursor;
        ShowCursorDuringPlayback = userSettings.ViewSettings.ShowCursorDuringPlayback;
        HighlightViewedNote = userSettings.ViewSettings.HighlightViewedNote;
        SelectLastInsertedNote = userSettings.ViewSettings.SelectLastInsertedNote;
        AreMeasureButtonsVisible = userSettings.ViewSettings.ShowMeasureButtons;
        PlaceNoteOnDrag = userSettings.ViewSettings.PlaceNoteOnDrag;
        VisualHiSpeedNumeric = (decimal)userSettings.ViewSettings.HispeedSetting;
        VolumeTrackBar = userSettings.ViewSettings.Volume;
        HitsoundVolumeTrackBar = Math.Clamp(userSettings.SoundSettings.HitsoundVolume, 0, 100);
        ShowNotesOnBeat = userSettings.ViewSettings.ShowNotesOnBeat;
        VisualBeatDivisionNumeric = (decimal)userSettings.ViewSettings.BeatDivision;
        GuideLineSelectedIndex = userSettings.ViewSettings.GuideLineSelection;

        Cursor = cursor;
    }

    // Commands
    public async Task<bool> NewCommand()
    {
        var mainWindow = Target();
        if (mainWindow != null)
            await mainWindow.NewMenuItem_OnClick();
        return true;
    }

    public async Task<bool> OpenCommand()
    {
        var mainWindow = Target();
        if (mainWindow != null)
            await mainWindow.OpenMenuItem_OnClick();
        return true;
    }

    public async Task<bool> SaveCommand()
    {
        var mainWindow = Target();
        if (mainWindow != null)
            await mainWindow.SaveMenuItem_OnClick();
        return true;
    }

    public bool SaveAsCommand()
    {
        var mainWindow = Target();
        mainWindow?.SaveAsMenuItem_OnClick();
        return true;
    }

    public bool ExitCommand()
    {
        var mainWindow = Target();
        mainWindow?.ExitMenuItem_OnClick();
        return true;
    }

    public bool UndoCommand()
    {
        var mainWindow = Target();
        mainWindow?.UndoMenuItem_OnClick();
        return true;
    }

    public bool RedoCommand()
    {
        var mainWindow = Target();
        mainWindow?.RedoMenuItem_OnClick();
        return true;
    }

    public bool CutCommand()
    {
        var mainWindow = Target();
        mainWindow.CutMenuItem_OnClick();
        return true;
    }

    public bool CopyCommand()
    {
        var mainWindow = Target();
        mainWindow.CopyMenuItem_OnClick();
        return true;
    }

    public bool PasteCommand()
    {
        var mainWindow = Target();
        mainWindow.PasteMenuItem_OnClick();
        return true;
    }

    public bool BakeHoldCommand()
    {
        var mainWindow = Target();
        mainWindow?.BakeHoldMenuItem_OnClick();
        return true;
    }

    public bool InsertHoldSegmentCommand()
    {
        var mainWindow = Target();
        mainWindow?.InsertHoldSegmentMenuItem_OnClick();
        return true;
    }

    public bool DeleteEntireHoldCommand()
    {
        var mainWindow = Target();
        mainWindow?.DeleteEntireHoldMenuItem_OnClick();
        return true;
    }

    public bool SelectHighlightedNoteCommand()
    {
        var mainWindow = Target();
        mainWindow?.OnSelectHighlightedNote_OnClick();
        return true;
    }

    public bool DeselectNotesCommand()
    {
        var mainWindow = Target();
        mainWindow?.OnDeselectNotes_OnClick();
        return true;
    }

    public bool ToggleShowCursorCommand()
    {
        ShowCursor = !ShowCursor;

        var mainWindow = Target();
        mainWindow?.SetShowCursor(ShowCursor);
        return true;
    }

    public bool ToggleShowCursorDuringPlaybackCommand()
    {
        ShowCursorDuringPlayback = !ShowCursorDuringPlayback;

        var mainWindow = Target();
        mainWindow?.SetShowCursorDuringPlayback(ShowCursorDuringPlayback);
        return true;
    }

    public bool ToggleHighlightViewedNoteCommand()
    {
        HighlightViewedNote = !HighlightViewedNote;

        var mainWindow = Target();
        mainWindow?.SetHighlightViewedNote(HighlightViewedNote);
        return true;
    }

    public bool ToggleSelectLastInsertedNoteCommand()
    {
        SelectLastInsertedNote = !SelectLastInsertedNote;

        var mainWindow = Target();
        mainWindow?.SetSelectLastInsertedNote(SelectLastInsertedNote);
        return true;
    }

    public bool TogglePlaceNoteOnDragCommand()
    {
        PlaceNoteOnDrag = !PlaceNoteOnDrag;

        var mainWindow = Target();
        mainWindow?.SetPlaceNoteOnDrag(PlaceNoteOnDrag);
        return true;
    }

    public bool ToggleShowMeasureButtonsCommand()
    {
        AreMeasureButtonsVisible = !AreMeasureButtonsVisible;

        var mainWindow = Target();
        mainWindow?.SetShowMeasureButtons(AreMeasureButtonsVisible);
        return true;
    }

    public async Task<bool> OpenInitialChartSettings()
    {
        var mainWindow = Target();
        if (mainWindow != null)
            await mainWindow.OpenChartSettings_OnClick();
        return true;
    }

    public async Task<bool> OpenSettingsCommand()
    {
        var mainWindow = Target();
        if (mainWindow != null)
            await mainWindow.OpenSettings_OnClick();
        return true;
    }

    private static MainView? Target()
    {
        var lifetime = Application.Current?.ApplicationLifetime;
        if (lifetime is IClassicDesktopStyleApplicationLifetime)
            return ((MainWindow?)
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.MainWindow)?.View;

        return (MainView?)
            (Application.Current?.ApplicationLifetime as ISingleViewApplicationLifetime)
            ?.MainView;
    }
}