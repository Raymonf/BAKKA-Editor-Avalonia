using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Utilities;
using BAKKA_Editor;
using BAKKA_Editor.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using SkiaSharp;
using BAKKA_Editor.Rendering;

namespace BAKKA_Editor.ViewModels;

public partial class AppSettingsViewModel : ViewModelBase
{
    private UserSettings? UserSettings { get; }
    private MainViewModel? MainViewModel { get; }
    private Localizer? Localizer { get; }
    public ContentDialog? Dialog { get; set; }

    private Dictionary<string, string> SupportedLanguages { get; } = new()
    {
        {"en-US", "English"},
        {"zh-Hant", "繁體中文"}
    };

    public AppSettingsViewModel()
    {
    }

    public AppSettingsViewModel(UserSettings userSettings, MainViewModel mainVM)
    {
        UserSettings = userSettings;
        MainViewModel = mainVM; // grab main view model to set colors. janky but again i have no idea how to do this more nicely
        Localizer = new Localizer();
        selectedLanguage = SupportedLanguages.First();
        SetLanguage(selectedLanguage.Key);
        Localizer.SetLanguage(selectedLanguage.Key); // initial call
    }

    [ObservableProperty] private bool showBeatVisualSettings = true;

    partial void OnShowBeatVisualSettingsChanged(bool show)
    {
        if (UserSettings != null)
        {
            UserSettings.ViewSettings.ShowBeatVisualSettings = show;
        }
    }

    [ObservableProperty] private bool showSlideSnapArrows = true;

    partial void OnShowSlideSnapArrowsChanged(bool value)
    {
        if (UserSettings != null)
        {
            UserSettings.ViewSettings.ShowSlideSnapArrows = value;
        }
    }

    [ObservableProperty] private float noteScaleMultiplierNumeric = 1.0f;
    [ObservableProperty] private float noteScaleMultiplierNumericMaximum = 3.0f;
    [ObservableProperty] private float noteScaleMultiplierNumericMinimum = 0.5f;

    partial void OnNoteScaleMultiplierNumericChanged(float value)
    {
        if (UserSettings != null)
        {
            UserSettings.ViewSettings.NoteScaleMultiplier = value;
        }
    }

    [ObservableProperty] private bool isActiveCursorTrackingEnabled = false;

    [ObservableProperty] private float slideNoteRotationSpeedNumeric = 1.0f;
    [ObservableProperty] private float slideNoteRotationSpeedNumericMaximum = 3.0f;
    [ObservableProperty] private float slideNoteRotationSpeedNumericMinimum = 0.0f;

    partial void OnSlideNoteRotationSpeedNumericChanged(float value)
    {
        if (UserSettings != null)
        {
            UserSettings.ViewSettings.SlideNoteRotationSpeed = value;
        }
    }

    partial void OnIsActiveCursorTrackingEnabledChanged(bool value)
    {
        if (UserSettings != null)
        {
            UserSettings.CursorSettings.IsActiveCursorTrackingEnabled = value;
        }
    }

    [ObservableProperty] private KeyValuePair<string, string> selectedLanguage;

    partial void OnSelectedLanguageChanged(KeyValuePair<string, string> kv)
    {
        Localizer?.SetLanguage(kv.Key);
        if (UserSettings != null)
            UserSettings.ViewSettings.Language = kv.Key;

        if (Dialog != null)
        {
            Dialog.Title = this.L("L.Settings.SettingsHeader");
            Dialog.CloseButtonText = this.L("L.Generic.CloseButtonText");
        }
    }

    public bool SetLanguage(string language)
    {
        if (!SupportedLanguages.ContainsKey(language))
        {
            return false;
        }

        SelectedLanguage = SupportedLanguages.First(x => x.Key == language);
        return true;
    }

    [ObservableProperty] private static IColorPalette _notePalette = new NoteColorPalette();
    [ObservableProperty] private Color colorNoteTap = _notePalette.GetColor(0, 0);
    [ObservableProperty] private Color colorNoteChain = _notePalette.GetColor(0, 1);
    [ObservableProperty] private Color colorNoteSlideCw = _notePalette.GetColor(0, 2);
    [ObservableProperty] private Color colorNoteSlideCcw = _notePalette.GetColor(0, 3);
    [ObservableProperty] private Color colorNoteSnapFw = _notePalette.GetColor(0, 4);
    [ObservableProperty] private Color colorNoteSnapBw = _notePalette.GetColor(0, 5);
    [ObservableProperty] private Color colorNoteHoldStart = _notePalette.GetColor(1, 0);
    [ObservableProperty] private Color colorNoteHoldSegment = _notePalette.GetColor(1, 1);
    [ObservableProperty] private Color colorNoteHoldGradient0 = _notePalette.GetColor(1, 2);
    [ObservableProperty] private Color colorNoteHoldGradient1 = _notePalette.GetColor(1, 3);

    partial void OnColorNoteTapChanged(Color value)
    {
        if (UserSettings != null)
            UserSettings.ColorSettings.ColorNoteTap = "#" + value.ToUInt32().ToString("X8");
    }

    partial void OnColorNoteChainChanged(Color value)
    {
        if (UserSettings != null)
            UserSettings.ColorSettings.ColorNoteChain = "#" + value.ToUInt32().ToString("X8");
    }

    partial void OnColorNoteSlideCwChanged(Color value)
    {
        if (UserSettings != null)
            UserSettings.ColorSettings.ColorNoteSlideCw = "#" + value.ToUInt32().ToString("X8");
    }

    partial void OnColorNoteSlideCcwChanged(Color value)
    {
        if (UserSettings != null)
            UserSettings.ColorSettings.ColorNoteSlideCcw = "#" + value.ToUInt32().ToString("X8");
    }

    partial void OnColorNoteSnapFwChanged(Color value)
    {
        if (UserSettings != null)
            UserSettings.ColorSettings.ColorNoteSnapFw = "#" + value.ToUInt32().ToString("X8");
    }

    partial void OnColorNoteSnapBwChanged(Color value)
    {
        if (UserSettings != null)
            UserSettings.ColorSettings.ColorNoteSnapBw = "#" + value.ToUInt32().ToString("X8");
    }

    partial void OnColorNoteHoldStartChanged(Color value)
    {
        if (UserSettings != null)
            UserSettings.ColorSettings.ColorNoteHoldStart = "#" + value.ToUInt32().ToString("X8");
    }

    partial void OnColorNoteHoldSegmentChanged(Color value)
    {
        if (UserSettings != null)
            UserSettings.ColorSettings.ColorNoteHoldSegment = "#" + value.ToUInt32().ToString("X8");
    }

    partial void OnColorNoteHoldGradient0Changed(Color value)
    {
        if (UserSettings != null) 
            UserSettings.ColorSettings.ColorNoteHoldGradient0 = "#" + value.ToUInt32().ToString("X8");
    }

    partial void OnColorNoteHoldGradient1Changed(Color value)
    {
        if (UserSettings != null)
            UserSettings.ColorSettings.ColorNoteHoldGradient1 = "#" + value.ToUInt32().ToString("X8");
    }
}