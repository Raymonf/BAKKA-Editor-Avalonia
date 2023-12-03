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
    public int EditorRefreshRate { get; set; } = 60;
    public bool ShowGimmicks { get; set; } = true;
    public bool ShowGimmicksDuringPlayback { get; set; } = false;
    public bool ShowGimmickEffects { get; set; } = true;
    public bool ShowMaskNotes { get; set; } = false;
    public bool ShowMaskNotesDuringPlayback { get; set; } = false;
    public bool ShowMaskEffects { get; set; } = true;
    public bool ShowCursorDepth { get; set; } = true;
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
    private static readonly CultureInfo DefaultParsingCulture = CultureInfo.InvariantCulture;
    public int TouchHotkey { get; set; } = Convert.ToInt32(Key.D1, DefaultParsingCulture);
    public int SlideLeftHotkey { get; set; } = Convert.ToInt32(Key.D2, DefaultParsingCulture);
    public int SlideRightHotkey { get; set; } = Convert.ToInt32(Key.D3, DefaultParsingCulture);
    public int SnapUpHotkey { get; set; } = Convert.ToInt32(Key.D4, DefaultParsingCulture);
    public int SnapDownHotkey { get; set; } = Convert.ToInt32(Key.D5, DefaultParsingCulture);
    public int ChainHotkey { get; set; } = Convert.ToInt32(Key.D6, DefaultParsingCulture);
    public int HoldHotkey { get; set; } = Convert.ToInt32(Key.D7, DefaultParsingCulture);
    public int PlayHotkey { get; set; } = Convert.ToInt32(Key.P, DefaultParsingCulture);

    public bool EnableMeasureChangeHotkeys { get; set; } = true;
    public int MeasureDecreaseHotkey { get; set; } = Convert.ToInt32(Key.Left, DefaultParsingCulture);
    public int MeasureIncreaseHotkey { get; set; } = Convert.ToInt32(Key.Right, DefaultParsingCulture);
    public bool EnableBeatChangeHotkeys { get; set; } = true;
    public int BeatDecreaseHotkey { get; set; } = Convert.ToInt32(Key.Down, DefaultParsingCulture);
    public int BeatIncreaseHotkey { get; set; } = Convert.ToInt32(Key.Up, DefaultParsingCulture);
    public int MeasureChangeHotkeyDelta { get; set; } = 1;
    public int MeasureChangeHotkeyHighDelta { get; set; } = 2;
    public int BeatChangeHotkeyDelta { get; set; } = 1;
    public int BeatChangeHotkeyHighDelta { get; set; } = 2;
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
    public string ColorNoteTap { get; set; } = "#FFFF00FF";
    public string ColorNoteChain { get; set; } = "#FFCCBE2D";
    public string ColorNoteSlideCw { get; set; } = "#FFFF8000";
    public string ColorNoteSlideCcw { get; set; } = "#FF32CD32";
    public string ColorNoteSnapFw { get; set; } = "#FFFF0000";
    public string ColorNoteSnapBw { get; set; } = "#FF00FFFF";
    public string ColorNoteHoldStart { get; set; } = "#FF8C6400";
    public string ColorNoteHoldSegment { get; set; } = "#FFDCB932";
    public string ColorNoteHoldGradient0 { get; set; } = "#BEDCA000";
    public string ColorNoteHoldGradient1 { get; set; } = "#BEDCB932";
}