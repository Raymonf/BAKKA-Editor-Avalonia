﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Utilities;
using BAKKA_Editor;
using BAKKA_Editor.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using SkiaSharp;
using BAKKA_Editor.Rendering;
using Avalonia.Styling;
using Avalonia;
using BAKKA_Editor.Audio;

namespace BAKKA_Editor.ViewModels;

public partial class AppSettingsViewModel : ViewModelBase
{
    private UserSettings? UserSettings { get; }
    private MainViewModel? MainViewModel { get; }
    private Localizer? Localizer { get; }
    private Hitsounds? Hitsounds { get; }
    public ContentDialog? Dialog { get; set; }

    private Dictionary<string, string> SupportedLanguages { get; } = new()
    {
        {"en-US", "English"},
        {"zh-Hant", "繁體中文"}
    };

    public AppSettingsViewModel()
    {
    }

    public AppSettingsViewModel(UserSettings userSettings)
    {
        UserSettings = userSettings;
        Localizer = new Localizer();
        selectedLanguage = SupportedLanguages.First();
        SetLanguage(selectedLanguage.Key);
        Localizer.SetLanguage(selectedLanguage.Key); // initial call
    }

    public void Setup(UserSettings userSettings)
    {
        DarkMode = userSettings.ViewSettings.DarkMode;
        ShowBeatVisualSettings = userSettings.ViewSettings.ShowBeatVisualSettings;
        IsActiveCursorTrackingEnabled = userSettings.CursorSettings.IsActiveCursorTrackingEnabled;
        ShowSlideSnapArrows = userSettings.ViewSettings.ShowSlideSnapArrows;
        SlideNoteRotationSpeedNumeric = userSettings.ViewSettings.SlideNoteRotationSpeed;

        ShowGimmickNotes = userSettings.ViewSettings.ShowGimmicks;
        ShowGimmickNotesDuringPlayback = userSettings.ViewSettings.ShowGimmicksDuringPlayback;
        ShowGimmickEffects = userSettings.ViewSettings.ShowGimmickEffects;

        ShowMaskNotes = userSettings.ViewSettings.ShowMaskNotes;
        ShowMaskNotesDuringPlayback = userSettings.ViewSettings.ShowMaskNotesDuringPlayback;
        ShowMaskEffects = userSettings.ViewSettings.ShowMaskEffects;

        UseSpaceKeyToPlay = userSettings.ViewSettings.UseSpaceToPlaySink;

        HitsoundEnabled = userSettings.SoundSettings.HitsoundEnabled;
        HitsoundSwipeEnabled = userSettings.SoundSettings.HitsoundSwipeEnabled;
        HitsoundBonusEnabled = userSettings.SoundSettings.HitsoundBonusEnabled;
        HitsoundFlairEnabled = userSettings.SoundSettings.HitsoundFlairEnabled;

        HitsoundPath = userSettings.SoundSettings.HitsoundPath;
        HitsoundSwipePath = userSettings.SoundSettings.HitsoundSwipePath;
        HitsoundBonusPath = userSettings.SoundSettings.HitsoundBonusPath;
        HitsoundFlairPath = userSettings.SoundSettings.HitsoundFlairPath;

        HitsoundOffsetMs = userSettings.SoundSettings.HitsoundAdditionalOffsetMs;

        ColorNoteTap = Color.Parse(userSettings.ColorSettings.ColorNoteTap);
        ColorNoteChain = Color.Parse(userSettings.ColorSettings.ColorNoteChain);
        ColorNoteSlideCw = Color.Parse(userSettings.ColorSettings.ColorNoteSlideCw);
        ColorNoteSlideCcw = Color.Parse(userSettings.ColorSettings.ColorNoteSlideCcw);
        ColorNoteSnapFw = Color.Parse(userSettings.ColorSettings.ColorNoteSnapFw);
        ColorNoteSnapBw = Color.Parse(userSettings.ColorSettings.ColorNoteSnapBw);
        ColorNoteHoldStart = Color.Parse(userSettings.ColorSettings.ColorNoteHoldStart);
        ColorNoteHoldSegment = Color.Parse(userSettings.ColorSettings.ColorNoteHoldSegment);
        ColorNoteHoldGradient0 = Color.Parse(userSettings.ColorSettings.ColorNoteHoldGradient0);
        ColorNoteHoldGradient1 = Color.Parse(userSettings.ColorSettings.ColorNoteHoldGradient1);
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

    [ObservableProperty] private bool isActiveCursorTrackingEnabled = false;
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

    [ObservableProperty] private bool darkMode = false;
    partial void OnDarkModeChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.ViewSettings.DarkMode = value;

        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    [ObservableProperty] private bool showGimmickNotes = true;
    partial void OnShowGimmickNotesChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.ViewSettings.ShowGimmicks = value;
    }

    [ObservableProperty] private bool showGimmickNotesDuringPlayback = true;
    partial void OnShowGimmickNotesDuringPlaybackChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.ViewSettings.ShowGimmicksDuringPlayback = value;
    }

    [ObservableProperty] private bool showGimmickEffects = true;
    partial void OnShowGimmickEffectsChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.ViewSettings.ShowGimmickEffects = value;
    }

    [ObservableProperty] private bool showMaskNotes = true;
    partial void OnShowMaskNotesChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.ViewSettings.ShowMaskNotes = value;
    }

    [ObservableProperty] private bool showMaskNotesDuringPlayback = false;
    partial void OnShowMaskNotesDuringPlaybackChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.ViewSettings.ShowMaskNotesDuringPlayback = value;
    }

    [ObservableProperty] private bool showMaskEffects = true;
    partial void OnShowMaskEffectsChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.ViewSettings.ShowMaskEffects = value;
    }

    [ObservableProperty] private bool showCursorDepth = true;
    partial void OnShowCursorDepthChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.ViewSettings.ShowCursorDepth = value;
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

    [ObservableProperty] private bool useSpaceKeyToPlay = false;

    partial void OnUseSpaceKeyToPlayChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.ViewSettings.UseSpaceToPlaySink = value;
    }

    [ObservableProperty] private bool hitsoundEnabled = false;

    partial void OnHitsoundEnabledChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.SoundSettings.HitsoundEnabled = value;
    }

    [ObservableProperty] private string hitsoundPath = "";

    partial void OnHitsoundPathChanged(string value)
    {
        if (UserSettings != null)
            UserSettings.SoundSettings.HitsoundPath = value;
    }

    [ObservableProperty] private bool hitsoundSwipeEnabled = false;
    partial void OnHitsoundSwipeEnabledChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.SoundSettings.HitsoundSwipeEnabled = value;
    }

    [ObservableProperty] private string hitsoundSwipePath = "";
    partial void OnHitsoundSwipePathChanged(string value)
    {
        if (UserSettings != null)
            UserSettings.SoundSettings.HitsoundSwipePath = value;
    }

    [ObservableProperty] private bool hitsoundBonusEnabled = false;
    partial void OnHitsoundBonusEnabledChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.SoundSettings.HitsoundBonusEnabled = value;
    }

    [ObservableProperty] private string hitsoundBonusPath = "";
    partial void OnHitsoundBonusPathChanged(string value)
    {
        if (UserSettings != null)
            UserSettings.SoundSettings.HitsoundBonusPath = value;
    }

    [ObservableProperty] private bool hitsoundFlairEnabled = false;
    partial void OnHitsoundFlairEnabledChanged(bool value)
    {
        if (UserSettings != null)
            UserSettings.SoundSettings.HitsoundFlairEnabled = value;
    }

    [ObservableProperty] private string hitsoundFlairPath = "";
    partial void OnHitsoundFlairPathChanged(string value)
    {
        if (UserSettings != null)
            UserSettings.SoundSettings.HitsoundFlairPath = value;
    }

    [ObservableProperty] private decimal hitsoundOffsetMs = 0;

    partial void OnHitsoundOffsetMsChanged(decimal value)
    {
        if (UserSettings != null)
            UserSettings.SoundSettings.HitsoundAdditionalOffsetMs = (int) value;
    }

    public async Task SelectHitsoundPathCommand()
    {
        // TODO: MOVE THIS OUT MOVE THIS OUT MOVE THIS OUT

        var topLevel = TopLevel.GetTopLevel(Dialog);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Hitsound File",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            HitsoundPath = System.Uri.UnescapeDataString(files[0].Path.AbsolutePath);
        }
    }

    public async Task SelectHitsoundSwipePathCommand()
    {
        // TODO: MOVE THIS OUT MOVE THIS OUT MOVE THIS OUT

        var topLevel = TopLevel.GetTopLevel(Dialog);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Hitsound File",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            HitsoundSwipePath = System.Uri.UnescapeDataString(files[0].Path.AbsolutePath);
        }
    }

    public async Task SelectHitsoundBonusPathCommand()
    {
        // TODO: MOVE THIS OUT MOVE THIS OUT MOVE THIS OUT

        var topLevel = TopLevel.GetTopLevel(Dialog);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Hitsound File",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            HitsoundBonusPath = System.Uri.UnescapeDataString(files[0].Path.AbsolutePath);
        }
    }

    public async Task SelectHitsoundFlairPathCommand()
    {
        // TODO: MOVE THIS OUT MOVE THIS OUT MOVE THIS OUT

        var topLevel = TopLevel.GetTopLevel(Dialog);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Hitsound File",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            // unescape path to allow for spaces in filenames/directories
            HitsoundFlairPath = System.Uri.UnescapeDataString(files[0].Path.AbsolutePath);
        }
    }
}