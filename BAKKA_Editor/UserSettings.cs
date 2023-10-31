using System;
using System.Globalization;
using Avalonia.Input;
using BAKKA_Editor.Views.Settings;
using DynamicData;

namespace BAKKA_Editor;

public class UserSettings
{
    public ViewSettings ViewSettings { get; set; } = new();
    public SaveSettings SaveSettings { get; set; } = new();
    public HotkeySettings HotkeySettings { get; set; } = new();
    public SoundSettings SoundSettings { get; set; } = new();
    public CursorSettings CursorSettings { get; set; } = new();
    public ColorSettings ColorSettings { get; set; } = new();
}

public class ViewSettings
{
    public bool ShowCursor { get; set; } = true;
    public bool ShowCursorDuringPlayback { get; set; } = false;
    public bool HighlightViewedNote { get; set; } = true;
    public bool SelectLastInsertedNote { get; set; } = true;
    public bool ShowGimmicks { get; set; } = true;
    public bool ShowGimmicksDuringPlayback { get; set; } = true;
    public float HispeedSetting { get; set; } = 1.5f;
    public int Volume { get; set; } = 100;
    public bool DarkMode { get; set; } = false;
    public bool ShowMeasureButtons { get; set; } = false;
    public int SliderScrollFactor { get; set; } = 1;
    public bool UseSpaceToPlaySink { get; set; } = false;
    public bool RenderSafelyButSlowly { get; set; } = true;
    public bool HandleOverflowPositionNumericScroll { get; set; } = false;
    public bool HandleOverflowSizeNumericScroll { get; set; } = false;
    public bool PlaceNoteOnDrag { get; set; } = true;
    public bool ShowNotesOnBeat { get; set; } = false;
    public string Language { get; set; } = "en-US";
    public bool ShowBeatVisualSettings { get; set; } = true;
    public bool ShowSlideSnapArrows { get; set; } = true;
    public int GuideLineSelection { get; set; } = 1;
    public float BeatDivision { get; set; } = 1;
    public float NoteScaleMultiplier { get; set; } = 1;
    public float SlideNoteRotationSpeed { get; set; } = 1;
}

public class SaveSettings
{
    /// <summary>
    ///     How frequently autosave occurs (in minutes)
    /// </summary>
    public int AutoSaveInterval { get; set; } = 1;
}

public class HotkeySettings
{
    private static readonly CultureInfo _defaultParsingCulture = CultureInfo.InvariantCulture;
    public int TouchHotkey { get; set; } = Convert.ToInt32(Key.D1, _defaultParsingCulture);
    public int SlideLeftHotkey { get; set; } = Convert.ToInt32(Key.D2, _defaultParsingCulture);
    public int SlideRightHotkey { get; set; } = Convert.ToInt32(Key.D3, _defaultParsingCulture);
    public int SnapUpHotkey { get; set; } = Convert.ToInt32(Key.D4, _defaultParsingCulture);
    public int SnapDownHotkey { get; set; } = Convert.ToInt32(Key.D5, _defaultParsingCulture);
    public int ChainHotkey { get; set; } = Convert.ToInt32(Key.D6, _defaultParsingCulture);
    public int HoldHotkey { get; set; } = Convert.ToInt32(Key.D7, _defaultParsingCulture);
    public int PlayHotkey { get; set; } = Convert.ToInt32(Key.P, _defaultParsingCulture);
}

public class SoundSettings
{
    public bool HitsoundEnabled { get; set; } = false;
    public string HitsoundPath { get; set; } = "";
    public int HitsoundVolume { get; set; } = 50;
    public int HitsoundAdditionalOffsetMs { get; set; } = 0;
}

public class CursorSettings
{
    public bool IsActiveCursorTrackingEnabled { get; set; } = false;
}

public class ColorSettings
{
    public string colorNoteTap { get; set; } = "#FFFF00FF";
    public string colorNoteChain { get; set; } = "#FFCCBE2D";
    public string colorNoteSlideCW { get; set; } = "#FFFF8000";
    public string colorNoteSlideCCW { get; set; } = "#FF32CD32";
    public string colorNoteSnapFW { get; set; } = "#FFFF0000";
    public string colorNoteSnapBW { get; set; } = "#FF00FFFF";
    public string colorNoteHoldStart { get; set; } = "#FF8C6400";
    public string colorNoteHoldSegment { get; set; } = "#FFDCB932";
    public string colorNoteHoldGradient0 { get; set; } = "#BEDCA000";
    public string colorNoteHoldGradient1 { get; set; } = "#BEDCB932";
}