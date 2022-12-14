using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BAKKA_Editor.Enums;
using BAKKA_Editor.Operations;
using BAKKA_Editor.SoundEngines;
using BAKKA_Editor.ViewModels;
using FluentAvalonia.UI.Controls;
using SkiaSharp;
using Tomlyn;

namespace BAKKA_Editor.Views;

public partial class MainView : UserControl, IPassSetting
{
    private static bool IsDesktop => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    private static readonly SKColor BackColor = SKColors.LightGray;

    // File selector state
    private string openFilename = "";
    private Stream? openChartFileReadStream;
    private Stream? openChartFileWriteStream;

    private Stream? saveFileStream; // for iOS, or something?
    private string saveFilename = ""; // for desktop

    // View State
    public bool CanShutdown = false;
    
    // Chart
    Chart chart = new();
    string songFilePath = "";
    bool isNewFile = true;
    bool isRecoveredFile = false;

    // Dialogs
    ChartSettingsViewModel chartSettingsViewModel;

    // Operations
    OperationManager opManager;

    // Playfield
    SkCircleView skCircleView;

    // Note Selection
    NoteType currentNoteType = NoteType.TouchNoBonus;
    GimmickType currentGimmickType = GimmickType.NoGimmick;
    BonusType currentBonusType = BonusType.NoBonus;
    int selectedGimmickIndex = -1;
    int selectedNoteIndex = -1;
    Note? lastNote;
    Note? nextSelectedNote; // so that we know the last newly inserted note
    Note? endOfChartNote;

    // Music
    private IBakkaSoundEngine soundEngine;
    private IBakkaSound? currentSong;

    // Timers
    private DispatcherTimer updateTimer;
    private DispatcherTimer autoSaveTimer;

    // Control updates
    enum EventSource
    {
        None,
        MouseWheel,
        SongPlaying,
        TrackBar,
        ManualMeasureSet,
        UpdateTick
    };

    EventSource valueTriggerEvent = EventSource.None;

    // Program info
    string fileVersion = "";
    UserSettings userSettings = new();
    string tempFilePath = "";
    string tempStatusPath = "";
    string autosaveFile = "";

    public MainView()
    {
        InitializeComponent();
        Background = new SolidColorBrush(new Avalonia.Media.Color(BackColor.Alpha, BackColor.Red, BackColor.Green, BackColor.Blue));
        Setup();
    }

    private void Setup()
    {
        soundEngine = new BassBakkaSoundEngine();
        chartSettingsViewModel = new();
        skCircleView = new(new SizeF(611, 611));
        opManager = new();

        SetInitialSong();

        // Operation Manager
        opManager.OperationHistoryChanged += (s, e) =>
        {
            chart.Notes = chart.Notes.OrderBy(x => x.Measure).ToList();
            chart.Gimmicks = chart.Gimmicks.OrderBy(x => x.Measure).ToList();
            if (nextSelectedNote != null)
            {
                var nextSelectedIndex = chart.Notes.IndexOf(nextSelectedNote);
                if (nextSelectedIndex != -1)
                    selectedNoteIndex = nextSelectedIndex;
                nextSelectedNote = null;
            }

            if (selectedNoteIndex >= chart.Notes.Count)
                selectedNoteIndex = chart.Notes.Count - 1;
            else if (selectedNoteIndex == -1 && chart.Notes.Count > 0)
                selectedNoteIndex = 0;
            UpdateNoteLabels();
            endOfChartNote = chart.Notes.FirstOrDefault(x => x.NoteType == NoteType.EndOfChart);
            if (selectedGimmickIndex >= chart.Gimmicks.Count)
                selectedGimmickIndex = chart.Gimmicks.Count - 1;
            else if (selectedGimmickIndex == -1 && chart.Gimmicks.Count > 0)
                selectedGimmickIndex = 0;
            UpdateGimmickLabels();
            SetText();
        };

        // Program info
        /*var asm = Assembly.GetExecutingAssembly();
        var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location);
        if (fvi.FileVersion != null)
        {
            fileVersion = fvi.FileVersion;
            SetText();
        }

        // Look for user settings
        if (File.Exists("settings.toml"))
            userSettings = Toml.ToModel<UserSettings>(File.ReadAllText("settings.toml"));
        else
        {
            userSettings = new UserSettings();
            File.WriteAllText("settings.toml", Toml.FromModel(userSettings));
        }*/
        userSettings = new UserSettings();

        // Apply settings
        var vm = (MainViewModel?) DataContext;
        if (vm != null)
        {
            vm.ShowCursor = userSettings.ViewSettings.ShowCursor;
            vm.ShowCursorDuringPlayback = userSettings.ViewSettings.ShowCursorDuringPlayback;
            vm.HighlightViewedNote = userSettings.ViewSettings.HighlightViewedNote;
            vm.SelectLastInsertedNote = userSettings.ViewSettings.SelectLastInsertedNote;
        }

        autoSaveTimer =
            new DispatcherTimer(TimeSpan.FromMilliseconds(userSettings.SaveSettings.AutoSaveInterval * 60000),
                DispatcherPriority.Background, AutoSaveTimer_Tick);

        // Update hotkey labels
        tapButton.AppendHotkey(userSettings.HotkeySettings.TouchHotkey);
        orangeButton.AppendHotkey(userSettings.HotkeySettings.SlideLeftHotkey);
        greenButton.AppendHotkey(userSettings.HotkeySettings.SlideRightHotkey);
        redButton.AppendHotkey(userSettings.HotkeySettings.SnapUpHotkey);
        blueButton.AppendHotkey(userSettings.HotkeySettings.SnapDownHotkey);
        chainButton.AppendHotkey(userSettings.HotkeySettings.ChainHotkey);
        holdButton.AppendHotkey(userSettings.HotkeySettings.HoldHotkey);
        playButton.AppendHotkey(userSettings.HotkeySettings.PlayHotkey);

        // Create timers
        updateTimer =
            new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Background, UpdateTimer_Tick);
        updateTimer.IsEnabled = false;
        // updateTimer.Start();

        Dispatcher.UIThread.Post(async () => await CheckAutoSaves(), DispatcherPriority.Background);

        // HACK HACK HACK HACK HACK: run on the render thread since we know it'll only render after everything is initialized :/
        // TODO: how do we fix this?
        Dispatcher.UIThread.Post(OnResize, DispatcherPriority.Render);
    }

    private IStorageProvider GetStorageProvider()
    {
        if (VisualRoot != null && VisualRoot is TopLevel)
        {
            return (VisualRoot as TopLevel)!.StorageProvider;
        }
        else
        {
            throw new Exception(":(");
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (songTrackBar == null || currentSong == null)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            songTrackBar.Value = (int)currentSong.PlayPosition;
            var info = chart.GetBeat(currentSong.PlayPosition);
            if (info.Measure != -1)
            {
                if (valueTriggerEvent == EventSource.None)
                    valueTriggerEvent = EventSource.UpdateTick;
                measureNumeric.Value = info.Measure;
                // TODO: weird rounding behavior for slow scrolling on longer songs...?
                // investigate how to fix this. is it related to playback position precision from bass?
                // +5 seems to work as a primitive "round up"
                var beat1 = (int)((info.Beat + 5) / 1920.0f * (float)beat2Numeric.Value);
                if ((int)beat1Numeric.Value != beat1)
                    beat1Numeric.Value = beat1;
                skCircleView.CurrentMeasure = info.MeasureDecimal;
                if (valueTriggerEvent == EventSource.UpdateTick)
                    valueTriggerEvent = EventSource.None;

                // TODO Fix hi-speed (it needs to be able to display multiple hi-speeds in the circle view at once)
                //// Change hi-speed, if applicable
                //var hispeed = chart.Gimmicks.Where(x => x.Measure <= info.Measure && x.GimmickType == GimmickType.HiSpeedChange).LastOrDefault();
                //if (hispeed != null && hispeed.HiSpeed != circleView.TotalMeasureShowNotes)
                //{
                //    visualHispeedNumeric.Value = (decimal)hispeed.HiSpeed;
                //}
            }
        });
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        if (tempFilePath == "")
            tempFilePath = PlatformUtils.GetTempFileName().Replace(".tmp", ".mer");

        var tempFileStream = File.Open(tempFilePath, FileMode.Create);
        if ((chart.Notes.Count > 0 || chart.Gimmicks.Count > 0) && !chart.IsSaved)
        {
            chart.WriteFile(tempFileStream, false);
            File.WriteAllLines(tempStatusPath, new string[] {"true", DateTime.Now.ToString("yyyy-MM-dd HH:mm")});
        }
        else
        {
            DeleteAutosaves(tempFilePath);
        }
    }

    private void SetInitialSong()
    {
        // :)
        songFilePath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "ERROR.ogg");
        try
        {
            currentSong = soundEngine.Play2D(songFilePath, true, true);
        }
        catch {} // this is ok

        if (currentSong != null)
        {
            /* Volume is represented as a float from 0-1. */
            currentSong.Volume = (float) trackBarVolume.Value / (float) trackBarVolume.Maximum;

            songTrackBar.Value = 0;
            songTrackBar.Maximum = (int) currentSong.PlayLength;
        }
        else
        {
            playButton.IsEnabled = false;
        }
    }

    private void SetText()
    {
        string save = chart.IsSaved ? "" : "*";
        // string name = isRecoveredFile ? "Auto-Save Recover" : (isNewFile ? "New File" : saveFilename);
        // Title = $"{save}BAKKA Editor {fileVersion} - [{name}]";
        // TODO: Title bind support
    }

    private async Task CheckAutoSaves()
    {
        // Look for temp files from previous runs
        var tempFile = Directory.GetFiles(PlatformUtils.GetTempPath(), "*.bakka");
        var oldAutosave = Directory.GetFiles(PlatformUtils.GetTempPath(), "*.mer");
        if (tempFile.Length > 0)
        {
            tempStatusPath = tempFile[0];
            var statusLines = File.ReadAllLines(tempStatusPath);
            if (statusLines.Length > 0)
            {
                bool checkAutosave = false;
                bool.TryParse(statusLines[0], out checkAutosave);
                if (checkAutosave)
                {
                    string autosaveTime = ".";
                    if (statusLines.Length > 1)
                        autosaveTime = " from " + statusLines[1] + ".";
                    if (oldAutosave.Length > 0)
                    {
                        autosaveFile = oldAutosave[0];
                        var dialog = new ContentDialog
                        {
                            Title = "Load Auto-Save Data?",
                            Content = $"Auto-save data found{autosaveTime}\n\nLoad?",
                            PrimaryButtonText = "Yes",
                            CloseButtonText = "No"
                        };
                        Dispatcher.UIThread.Post(
                            async () =>
                            {
                                var result = await dialog.ShowAsync();

                                if (result == ContentDialogResult.Primary)
                                {
                                    openChartFileReadStream = File.Open(autosaveFile, FileMode.Open);
                                    isRecoveredFile = true;
                                    OpenFile();
                                }

                                if (!isRecoveredFile)
                                {
                                    DeleteAutosaves();
                                }
                                
                                autoSaveTimer.Start();
                            });
                    }
                }
            }
        }
        else
        {
            tempStatusPath = PlatformUtils.GetTempFileName().Replace(".tmp", ".bakka");
            File.WriteAllText(tempStatusPath, "false");
                                
            autoSaveTimer.Start();
        }
    }

    private bool OpenFile()
    {
        chart = new();
        if (!chart.ParseFile(openChartFileReadStream))
        {
            Dispatcher.UIThread.Post(
                async () => await ShowBlockingMessageBox("Error", "Failed to parse file. Ensure it is not corrupted."));
            chart = new();
            openChartFileReadStream.Close();
            return false;
        }

        // Successful parse
        var initGimmicks = chart.Gimmicks.Where(x => x.StartTime == 0);
        var initBpm = initGimmicks.FirstOrDefault(x => x.GimmickType == GimmickType.BpmChange);
        double bpm = initBpm != null ? initBpm.BPM : 120.0;
        var initTimeSig = initGimmicks.FirstOrDefault(x => x.GimmickType == GimmickType.TimeSignatureChange);
        int timeSigUpper = initTimeSig != null ? initTimeSig.TimeSig.Upper : 4;
        int timeSigLower = initTimeSig != null ? initTimeSig.TimeSig.Lower : 4;
        chartSettingsViewModel.Bpm = bpm;
        chartSettingsViewModel.TimeSigUpper = timeSigUpper;
        chartSettingsViewModel.TimeSigLower = timeSigLower;
        chartSettingsViewModel.Offset = chart.Offset;
        chartSettingsViewModel.MovieOffset = chart.MovieOffset;
        ResetChartTime();
        UpdateNoteLabels(chart.Notes.Count > 0 ? 0 : -1);
        UpdateGimmickLabels(chart.Gimmicks.Count > 0 ? 0 : -1);
        if (!IsDesktop)
            saveFileStream = openChartFileWriteStream;
        saveFilename = openFilename;
        chart.IsSaved = true;
        isNewFile = false;
        SetText();
        return true;
    }

    private void DeleteAutosaves(string keep = "")
    {
        var oldAutosave = Directory.GetFiles(PlatformUtils.GetTempPath(), "*.mer");
        foreach (var file in oldAutosave)
        {
            if (file != keep)
                File.Delete(file);
        }
    }

    private void RenderCanvas(SKCanvas canvas)
    {
        skCircleView.SetCanvas(canvas);

        skCircleView.DrawBackground(BackColor);

        // Draw masks
        skCircleView.DrawMasks(chart);

        // Draw base and measure circle.
        skCircleView.DrawCircle();

        // Draw degree lines
        skCircleView.DrawDegreeLines();

        // Draw holds
        skCircleView.DrawHolds(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex);

        // Draw notes
        skCircleView.DrawNotes(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex);

        // Determine if cursor should be showing
        bool showCursor = userSettings.ViewSettings.ShowCursor || skCircleView.mouseDownPos != -1;
        if (currentSong != null && !currentSong.Paused)
        {
            showCursor = userSettings.ViewSettings.ShowCursorDuringPlayback;
        }

        // Draw cursor
        if (showCursor)
        {
            skCircleView.DrawCursor(currentNoteType, (float) positionNumeric.Value, (float) sizeNumeric.Value);
        }
    }

    private double curDelta = 0;

    private void CircleControl_OnWheel(object? sender, PointerWheelEventArgs e)
    {
        // throttle mouse wheel delta for trackpads which spam wheel events 
        curDelta += e.Delta.Y;
        if (curDelta is > -0.5 and < 0.5)
            return;
        var delta = curDelta;
        curDelta = 0;
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            switch (beat2Numeric.Value)
            {
                // Shift beat division by standard musical quantization
                // TODO: Take time signature into account?
                case < 2:
                {
                    if (delta > 0)
                        beat2Numeric.Value = 2;
                    return;
                }
                case 2 when delta < 0:
                    beat2Numeric.Value = 1;
                    return;
            }

            var low = 0;
            var high = 1;
            while (!(beat2Numeric.Value >= (1 << low) && beat2Numeric.Value <= (1 << high)))
            {
                low++;
                high++;
            }

            if (delta < 0)
                beat2Numeric.Value = 1 << low;
            else if (high < 10)
                beat2Numeric.Value = 1 << (high + 1);
        }
        else if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
        }
        else if ((e.KeyModifiers & KeyModifiers.Control) != 0)
        {
        }
        else
        {
            valueTriggerEvent = EventSource.MouseWheel;
            if (delta > 0)
                beat1Numeric.Value = (int) beat1Numeric.Value + 1;
            else
                beat1Numeric.Value = (int) beat1Numeric.Value - 1;
        }
    }

    private bool IsSongPlaying()
    {
        if (currentSong != null && !currentSong.Paused)
            return true;
        else
            return false;
    }

    private void Beat1Numeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (beat1Numeric.Value >= beat2Numeric.Value)
        {
            measureNumeric.Value++;
            beat1Numeric.Value = 0;
            return;
        }
        else if (beat1Numeric.Value < 0)
        {
            if (measureNumeric.Value > 0)
            {
                measureNumeric.Value--;
                beat1Numeric.Value = beat2Numeric.Value - 1;
                return;
            }
            else if (measureNumeric.Value == 0)
            {
                beat1Numeric.Value = 0;
                return;
            }
        }

        updateTime();
        if (currentSong != null && !IsSongPlaying() && valueTriggerEvent != EventSource.TrackBar)
        {
            songTrackBar.Value = chart.GetTime(new BeatInfo((int)measureNumeric.Value, (int)beat1Numeric.Value * 1920 / (int)beat2Numeric.Value));
        }

        valueTriggerEvent = EventSource.None;
    }

    private void Beat2Numeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        updateTime();
    }

    private void UpdateGimmickLabels(int val = -2)
    {
        if (val != -2 && val < chart.Gimmicks.Count)
            selectedGimmickIndex = val;
        if (selectedGimmickIndex == -1)
        {
            gimmickMeasureLabel.Text = "None";
            gimmickBeatLabel.Text = "None";
            gimmickTypeLabel.Text = "None";
            gimmickValueLabel.Text = "None";
            return;
        }

        var gimmick = chart.Gimmicks[selectedGimmickIndex];

        gimmickMeasureLabel.Text = gimmick.BeatInfo.Measure.ToString();
        var quant = Utils.GetQuantization(gimmick.BeatInfo.Beat, 16);
        gimmickBeatLabel.Text = $"{quant.Item1} / {quant.Item2}";
        gimmickTypeLabel.Text = gimmick.GimmickType.ToLabel();
        switch (gimmick.GimmickType)
        {
            case GimmickType.BpmChange:
                gimmickValueLabel.Text = gimmick.BPM.ToString("F6");
                break;
            case GimmickType.TimeSignatureChange:
                gimmickValueLabel.Text = gimmick.TimeSig.Upper.ToString() + " / " + gimmick.TimeSig.Lower.ToString();
                break;
            case GimmickType.HiSpeedChange:
                gimmickValueLabel.Text = gimmick.HiSpeed.ToString("F6");
                break;
            case GimmickType.NoGimmick:
            case GimmickType.ReverseStart:
            case GimmickType.ReverseMiddle:
            case GimmickType.ReverseEnd:
            case GimmickType.StopStart:
            case GimmickType.StopEnd:
            default:
                gimmickValueLabel.Text = "No value";
                break;
        }

        // Prevent deletetion of initial BPM and time signature
        if (chart.Gimmicks.Count == 0
            || (gimmick.Measure == 0 && (gimmick.GimmickType == GimmickType.BpmChange ||
                                         gimmick.GimmickType == GimmickType.TimeSignatureChange)))
        {
            gimmickDeleteButton.IsEnabled = false;
        }
        else
        {
            gimmickDeleteButton.IsEnabled = true;
        }
    }

    private void SetSelectedObject(NoteType type)
    {
        currentNoteType = type;
        switch (type)
        {
            case NoteType.TouchNoBonus:
                updateLabel("Touch");
                break;
            case NoteType.TouchBonus:
                updateLabel("Touch [Bonus]");
                break;
            case NoteType.SnapRedNoBonus:
                updateLabel("Snap (R)");
                break;
            case NoteType.SnapBlueNoBonus:
                updateLabel("Snap (B)");
                break;
            case NoteType.SlideOrangeNoBonus:
                updateLabel("Slide (O)");
                break;
            case NoteType.SlideOrangeBonus:
                updateLabel("Slide (O) [Bonus]");
                break;
            case NoteType.SlideGreenNoBonus:
                updateLabel("Slide (G)");
                break;
            case NoteType.SlideGreenBonus:
                updateLabel("Slide (G) [Bonus]");
                break;
            case NoteType.HoldStartNoBonus:
                updateLabel("Hold Start");
                break;
            case NoteType.HoldJoint:
                if (endHoldCheck.IsChecked.Value)
                {
                    updateLabel("Hold End");
                    currentNoteType = NoteType.HoldEnd;
                }
                else
                    updateLabel("Hold Middle");

                break;
            case NoteType.HoldEnd:
                updateLabel("Hold End");
                break;
            case NoteType.MaskAdd:
                if (clockwiseMaskRadio.IsChecked.Value)
                    updateLabel("Mask Add (Clockwise)");
                else if (cClockwiseMaskRadio.IsChecked.Value)
                    updateLabel("Mask Add (Counter-Clockwise)");
                else
                    updateLabel("Mask Add (From Center)");
                break;
            case NoteType.MaskRemove:
                if (clockwiseMaskRadio.IsChecked.Value)
                    updateLabel("Mask Remove (Clockwise)");
                else if (cClockwiseMaskRadio.IsChecked.Value)
                    updateLabel("Mask Remove (Counter-Clockwise)");
                else
                    updateLabel("Mask Remove (From Center)");
                break;
            case NoteType.EndOfChart:
                updateLabel("End of Chart");
                break;
            case NoteType.Chain:
                updateLabel("Chain");
                break;
            case NoteType.TouchBonusFlair:
                updateLabel("Touch [R Note]");
                break;
            case NoteType.SnapRedBonusFlair:
                updateLabel("Snap (R) [R Note]");
                break;
            case NoteType.SnapBlueBonusFlair:
                updateLabel("Snap (B) [R Note]");
                break;
            case NoteType.SlideOrangeBonusFlair:
                updateLabel("Slide (O) [R Note]");
                break;
            case NoteType.SlideGreenBonusFlair:
                updateLabel("Slide (G) [R Note]");
                break;
            case NoteType.HoldStartBonusFlair:
                updateLabel("Hold Start [R Note]");
                break;
            case NoteType.ChainBonusFlair:
                updateLabel("Chain [R Note]");
                break;
            default:
                updateLabel("None Selected");
                break;
        }
    }

    void updateLabel(string text)
    {
        currentSelectionLabel.Text = text;
    }

    private void SetSelectedObject(GimmickType gimmick)
    {
        currentGimmickType = gimmick;
        updateLabel(gimmick.ToLabel());
    }

    private void UpdateNoteLabels(int val = -2)
    {
        if (val != -2)
            selectedNoteIndex = val;
        if (selectedNoteIndex <= -1 || selectedNoteIndex >= chart.Notes.Count)
        {
            noteMeasureLabel.Text = "None";
            noteBeatLabel.Text = "None";
            noteTypeLabel.Text = "None";
            notePositionLabel.Text = "None";
            noteSizeLabel.Text = "None";
            noteMaskLabel.Text = "N/A";
            return;
        }

        var note = chart.Notes[selectedNoteIndex];

        noteMeasureLabel.Text = note.BeatInfo.Measure.ToString();
        var quant = Utils.GetQuantization(note.BeatInfo.Beat, 16);
        noteBeatLabel.Text = $"{quant.Item1} / {quant.Item2}";
        noteTypeLabel.Text = note.NoteType.ToLabel();
        notePositionLabel.Text = note.Position.ToString();
        noteSizeLabel.Text = note.Size.ToString();
        if (!note.IsMask)
            noteMaskLabel.Text = "N/A";
        else
        {
            switch (note.MaskFill)
            {
                case MaskType.Clockwise:
                    noteMaskLabel.Text = "Clockwise";
                    break;
                case MaskType.CounterClockwise:
                    noteMaskLabel.Text = "C-Clockwise";
                    break;
                case MaskType.Center:
                    noteMaskLabel.Text = "From Center";
                    break;
                default:
                    break;
            }
        }
    }

    private void ResetChartTime()
    {
        measureNumeric.Value = beat1Numeric.Value = 0;
        positionNumeric.Value = 0;
        sizeNumeric.Value = 1;
        updateTime();
    }

    private void updateTime()
    {
        if (currentSong == null || (currentSong != null && currentSong.Paused))
        {
            skCircleView.CurrentMeasure =
                (float) measureNumeric.Value + ((float) beat1Numeric.Value / (float) beat2Numeric.Value);
        }

        if (currentNoteType is NoteType.HoldJoint or NoteType.HoldEnd)
        {
            insertButton.IsEnabled = !(lastNote.BeatInfo.MeasureDecimal >= skCircleView.CurrentMeasure);
        }
        else if (endOfChartNote != null)
        {
            insertButton.IsEnabled = !(endOfChartNote.BeatInfo.MeasureDecimal <= skCircleView.CurrentMeasure);
        }
        else if (currentSong != null && !currentSong.Paused)
        {
            insertButton.IsEnabled = false;
        }
        else
        {
            insertButton.IsEnabled = true;
        }
    }

    private void tapButton_Click(object sender, RoutedEventArgs e)
    {
        if (noBonusRadio.IsChecked.Value)
            SetSelectedObject(NoteType.TouchNoBonus);
        else if (bonusRadio.IsChecked.Value)
            SetSelectedObject(NoteType.TouchBonus);
        else if (flairRadio.IsChecked.Value)
            SetSelectedObject(NoteType.TouchBonusFlair);

        currentGimmickType = GimmickType.NoGimmick;
    }

    private void orangeButton_Click(object sender, RoutedEventArgs e)
    {
        if (noBonusRadio.IsChecked.Value)
            SetSelectedObject(NoteType.SlideOrangeNoBonus);
        else if (bonusRadio.IsChecked.Value)
            SetSelectedObject(NoteType.SlideOrangeBonus);
        else if (flairRadio.IsChecked.Value)
            SetSelectedObject(NoteType.SlideOrangeBonusFlair);

        currentGimmickType = GimmickType.NoGimmick;
    }

    private void greenButton_Click(object sender, RoutedEventArgs e)
    {
        if (noBonusRadio.IsChecked.Value)
            SetSelectedObject(NoteType.SlideGreenNoBonus);
        else if (bonusRadio.IsChecked.Value)
            SetSelectedObject(NoteType.SlideGreenBonus);
        else if (flairRadio.IsChecked.Value)
            SetSelectedObject(NoteType.SlideGreenBonusFlair);

        currentGimmickType = GimmickType.NoGimmick;
    }

    private void redButton_Click(object sender, RoutedEventArgs e)
    {
        if (noBonusRadio.IsChecked.Value)
            SetSelectedObject(NoteType.SnapRedNoBonus);
        else if (bonusRadio.IsChecked.Value)
        {
            flairRadio.IsChecked = true;
            SetSelectedObject(NoteType.SnapRedBonusFlair);
        }
        else if (flairRadio.IsChecked.Value)
            SetSelectedObject(NoteType.SnapRedBonusFlair);

        currentGimmickType = GimmickType.NoGimmick;
    }

    private void blueButton_Click(object sender, RoutedEventArgs e)
    {
        if (noBonusRadio.IsChecked.Value)
            SetSelectedObject(NoteType.SnapBlueNoBonus);
        else if (bonusRadio.IsChecked.Value)
        {
            flairRadio.IsChecked = true;
            SetSelectedObject(NoteType.SnapBlueBonusFlair);
        }
        else if (flairRadio.IsChecked.Value)
            SetSelectedObject(NoteType.SnapBlueBonusFlair);

        currentGimmickType = GimmickType.NoGimmick;
    }

    private void chainButton_Click(object sender, RoutedEventArgs e)
    {
        if (noBonusRadio.IsChecked.Value)
            SetSelectedObject(NoteType.Chain);
        else if (bonusRadio.IsChecked.Value)
        {
            flairRadio.IsChecked = true;
            SetSelectedObject(NoteType.ChainBonusFlair);
        }
        else if (flairRadio.IsChecked.Value)
            SetSelectedObject(NoteType.ChainBonusFlair);

        currentGimmickType = GimmickType.NoGimmick;
    }

    private void holdButton_Click(object sender, RoutedEventArgs e)
    {
        holdButtonClicked();
    }

    private void holdButtonClicked()
    {
        if (noBonusRadio.IsChecked.Value)
            SetSelectedObject(NoteType.HoldStartNoBonus);
        else if (bonusRadio.IsChecked.Value)
        {
            flairRadio.IsChecked = true;
            SetSelectedObject(NoteType.HoldStartBonusFlair);
        }
        else if (flairRadio.IsChecked.Value)
            SetSelectedObject(NoteType.HoldStartBonusFlair);

        currentGimmickType = GimmickType.NoGimmick;
    }

    private void endChartButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedObject(NoteType.EndOfChart);
        currentGimmickType = GimmickType.NoGimmick;
    }

    private void BonusRadioCheck(object sender, RoutedEventArgs e)
    {
        if (noBonusRadio.IsChecked.Value)
            currentBonusType = BonusType.NoBonus;
        else if (bonusRadio.IsChecked.Value)
            currentBonusType = BonusType.Bonus;
        else
            currentBonusType = BonusType.Flair;
        UpdateSelectedNote();
    }

    private void UpdateSelectedNote()
    {
        switch (currentNoteType)
        {
            case NoteType.TouchNoBonus:
            case NoteType.TouchBonus:
            case NoteType.TouchBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        SetSelectedObject(NoteType.TouchNoBonus);
                        break;
                    case BonusType.Bonus:
                        SetSelectedObject(NoteType.TouchBonus);
                        break;
                    case BonusType.Flair:
                        SetSelectedObject(NoteType.TouchBonusFlair);
                        break;
                    default:
                        break;
                }

                break;
            case NoteType.SnapRedNoBonus:
            case NoteType.SnapRedBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        SetSelectedObject(NoteType.SnapRedNoBonus);
                        break;
                    case BonusType.Bonus:
                        flairRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SnapRedBonusFlair);
                        break;
                    case BonusType.Flair:
                        SetSelectedObject(NoteType.SnapRedBonusFlair);
                        break;
                    default:
                        break;
                }

                break;
            case NoteType.SnapBlueNoBonus:
            case NoteType.SnapBlueBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        SetSelectedObject(NoteType.SnapBlueNoBonus);
                        break;
                    case BonusType.Bonus:
                        flairRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SnapBlueBonusFlair);
                        break;
                    case BonusType.Flair:
                        SetSelectedObject(NoteType.SnapBlueBonusFlair);
                        break;
                    default:
                        break;
                }

                break;
            case NoteType.SlideOrangeNoBonus:
            case NoteType.SlideOrangeBonus:
            case NoteType.SlideOrangeBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        SetSelectedObject(NoteType.SlideOrangeNoBonus);
                        break;
                    case BonusType.Bonus:
                        SetSelectedObject(NoteType.SlideOrangeBonus);
                        break;
                    case BonusType.Flair:
                        SetSelectedObject(NoteType.SlideOrangeBonusFlair);
                        break;
                    default:
                        break;
                }

                break;
            case NoteType.SlideGreenNoBonus:
            case NoteType.SlideGreenBonus:
            case NoteType.SlideGreenBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        SetSelectedObject(NoteType.SlideGreenNoBonus);
                        break;
                    case BonusType.Bonus:
                        SetSelectedObject(NoteType.SlideGreenBonus);
                        break;
                    case BonusType.Flair:
                        SetSelectedObject(NoteType.SlideGreenBonusFlair);
                        break;
                    default:
                        break;
                }

                break;
            case NoteType.HoldStartNoBonus:
            case NoteType.HoldStartBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        SetSelectedObject(NoteType.HoldStartNoBonus);
                        break;
                    case BonusType.Bonus:
                        flairRadio.IsChecked = true;
                        SetSelectedObject(NoteType.HoldStartBonusFlair);
                        break;
                    case BonusType.Flair:
                        SetSelectedObject(NoteType.HoldStartBonusFlair);
                        break;
                    default:
                        break;
                }

                break;
            case NoteType.HoldJoint:
                break;
            case NoteType.HoldEnd:
                break;
            case NoteType.MaskAdd:
                break;
            case NoteType.MaskRemove:
                break;
            case NoteType.EndOfChart:
                break;
            case NoteType.Chain:
            case NoteType.ChainBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        SetSelectedObject(NoteType.Chain);
                        break;
                    case BonusType.Bonus:
                        flairRadio.IsChecked = true;
                        SetSelectedObject(NoteType.ChainBonusFlair);
                        break;
                    case BonusType.Flair:
                        SetSelectedObject(NoteType.ChainBonusFlair);
                        break;
                    default:
                        break;
                }

                break;
            default:
                break;
        }
    }

    private void MeasureNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        updateTime();
    }

    private void PositionNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (positionNumeric != null && positionTrackBar != null &&
            (int) positionTrackBar.Value != (int) positionNumeric.Value)
            positionTrackBar.Value = (int) positionNumeric.Value;
    }

    private void PositionTrackBar_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // TODO: tighten property check
        if (positionNumeric != null && positionTrackBar != null &&
            (int) positionNumeric.Value != (int) positionTrackBar.Value)
            positionNumeric.Value = (int) positionTrackBar.Value;
    }

    private void SizeNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        // TODO: tighten property check
        if (sizeNumeric != null && sizeTrackBar != null && (int) sizeTrackBar.Value != (int) sizeNumeric.Value)
            sizeTrackBar.Value = (int) sizeNumeric.Value;
    }

    private void SizeTrackBar_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // TODO: tighten property check
        if (sizeNumeric != null && sizeTrackBar != null && (int) sizeNumeric.Value != (int) sizeTrackBar.Value)
            sizeNumeric.Value = (int) sizeTrackBar.Value;
    }

    private void insertButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertObject();
    }

    private void InsertObject()
    {
        if (!insertButton.IsEnabled)
            return;

        var currentBeat = new BeatInfo((int) measureNumeric.Value,
            (int) beat1Numeric.Value * 1920 / (int) beat2Numeric.Value);

        if (currentGimmickType == GimmickType.NoGimmick)
        {
            Note tempNote = new Note()
            {
                BeatInfo = currentBeat,
                NoteType = currentNoteType,
                Position = (int) positionNumeric.Value!,
                Size = (int) sizeNumeric.Value!,
                HoldChange = true
            };
            switch (currentNoteType)
            {
                case NoteType.HoldStartNoBonus:
                case NoteType.HoldStartBonusFlair:
                    SetSelectedObject(NoteType.HoldJoint);
                    lastNote = tempNote;
                    SetNonHoldButtonState(false);
                    break;
                case NoteType.HoldJoint:
                case NoteType.HoldEnd:
                    tempNote.PrevNote = lastNote;
                    if (lastNote != null)
                        tempNote.PrevNote.NextNote = tempNote;
                    if (endHoldCheck.IsChecked!.Value)
                    {
                        tempNote.NoteType = NoteType.HoldEnd;
                        SetNonHoldButtonState(true);
                        endHoldCheck.IsChecked = false;
                        holdButtonClicked();
                    }
                    else
                        lastNote = tempNote;

                    break;
                case NoteType.MaskAdd:
                case NoteType.MaskRemove:
                    if (clockwiseMaskRadio.IsChecked!.Value)
                        tempNote.MaskFill = MaskType.Clockwise;
                    else if (cClockwiseMaskRadio.IsChecked!.Value)
                        tempNote.MaskFill = MaskType.CounterClockwise;
                    else
                        tempNote.MaskFill = MaskType.Center;
                    break;
                case NoteType.EndOfChart:
                    if (endOfChartNote != null)
                    {
                        Dispatcher.UIThread.Post(
                            async () => await ShowBlockingMessageBox("Error", "Cannot place more than one 'End of Chart' Note."));
                        return;
                    }

                    if (chart.Notes.Count > 0)
                    {
                        var finalNote = chart.Notes.Aggregate((agg, next) =>
                            next.BeatInfo.MeasureDecimal > agg.BeatInfo.MeasureDecimal ? next : agg);
                        if (finalNote != null && finalNote.BeatInfo.MeasureDecimal >= currentBeat.MeasureDecimal)
                        {
                            Dispatcher.UIThread.Post(
                                async () => await ShowBlockingMessageBox("Error", "Cannot place 'End of Chart' Note before another note."));
                            return;
                        }
                    }

                    break;
                default:
                    break;
            }

            // new object so update the temporary last note to the new one
            if (userSettings.ViewSettings.SelectLastInsertedNote)
                nextSelectedNote = tempNote;
            chart.Notes.Add(tempNote);
            chart.IsSaved = false;
            switch (currentNoteType)
            {
                case NoteType.HoldStartNoBonus:
                case NoteType.HoldStartBonusFlair:
                case NoteType.HoldJoint:
                case NoteType.HoldEnd:
                    opManager.Push(new InsertHoldNote(chart, tempNote));
                    break;
                default:
                    opManager.Push(new InsertNote(chart, tempNote));
                    break;
            }
        }
    }

    private void SetNonHoldButtonState(bool state)
    {
        tapButton.IsEnabled = state;
        orangeButton.IsEnabled = state;
        greenButton.IsEnabled = state;
        redButton.IsEnabled = state;
        blueButton.IsEnabled = state;
        chainButton.IsEnabled = state;
        endChartButton.IsEnabled = state;
        maskButton.IsEnabled = state;
        bpmChangeButton.IsEnabled = state;
        timeSigButton.IsEnabled = state;
        hiSpeedButton.IsEnabled = state;
        stopButton.IsEnabled = state;
        reverseButton.IsEnabled = state;
    }

    private void EndHoldCheck_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (endHoldCheck == null || e.Property.Name != "IsChecked")
            return;

        if (endHoldCheck.IsChecked.Value && currentNoteType == NoteType.HoldJoint)
        {
            SetSelectedObject(NoteType.HoldEnd);
        }

        if (!endHoldCheck.IsChecked.Value && currentNoteType == NoteType.HoldEnd)
        {
            SetSelectedObject(NoteType.HoldJoint);
        }
    }

    private void OnResize()
    {
        var zoneWidth = RightStackPanel.Bounds.Left - LeftScrollViewer.Bounds.Right - 18;
        var zoneHeight = BottomStackPanel.Bounds.Top - LeftStackPanel.Bounds.Top - 6;
        if (zoneWidth > zoneHeight)
        {
            CircleControl.Width = zoneHeight;
            CircleControl.Height = zoneHeight;
        }
        else
        {
            CircleControl.Width = zoneWidth;
            CircleControl.Height = zoneWidth;
        }

        var paddingLeft = (zoneWidth - CircleControl.Width) / 2;
        CircleControl.Padding = new Thickness(paddingLeft, 0, 0, 0);
        skCircleView.Update(new SizeF((float) CircleControl.Width, (float) CircleControl.Height));
    }

    private void AvaloniaObject_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            Dispatcher.UIThread.Post(() => OnResize());
        }
    }

    private void CircleControl_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(CircleControl);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        // X and Y are relative to the upper left of the panel
        var xCen = point.Position.X - (CircleControl.DesiredSize.Width / 2);
        var yCen = -(point.Position.Y - (CircleControl.DesiredSize.Height / 2));
        // Update the location of mouse click inside the circle
        skCircleView.UpdateMouseDown((float) xCen, (float) yCen, point.Position.ToSystemDrawing());
        positionNumeric.Value = skCircleView.mouseDownPos;
    }

    private void CircleControl_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var point = e.GetCurrentPoint(CircleControl);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased ||
            skCircleView.mouseDownPos <= -1)
        {
            return;
        }

        var dist = Utils.GetDist(point.Position.ToSystemDrawing(), skCircleView.mouseDownPt);
        if (dist > 5.0f)
            InsertObject();
        skCircleView.UpdateMouseUp();
    }

    private void CircleControl_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(CircleControl);
        // Mouse down position wasn't within the window or wasn't a left click, do nothing.
        if (!point.Properties.IsLeftButtonPressed || skCircleView.mouseDownPos <= -1)
        {
            return;
        }

        {
            // X and Y are relative to the upper left of the panel
            var xCen = point.Position.X - (CircleControl.DesiredSize.Width / 2);
            var yCen = -(point.Position.Y - (CircleControl.DesiredSize.Height / 2));
            // Update the location of mouse click inside the circle.
            int theta = skCircleView.UpdateMouseMove((float) xCen, (float) yCen);
            // Left click will alter the note width and possibly position depending on which direction we move
            if (theta == skCircleView.mouseDownPos)
            {
                positionNumeric.Value = skCircleView.mouseDownPos;
                sizeNumeric.Value = 1;
            }
            else if ((theta > skCircleView.mouseDownPos || skCircleView.rolloverPos) && !skCircleView.rolloverNeg)
            {
                positionNumeric.Value = skCircleView.mouseDownPos;
                if (skCircleView.rolloverPos)
                    sizeNumeric.Value = Math.Min(theta + 60 - skCircleView.mouseDownPos + 1, 60);
                else
                    sizeNumeric.Value = theta - skCircleView.mouseDownPos + 1;
            }
            else if (theta < skCircleView.mouseDownPos || skCircleView.rolloverNeg)
            {
                positionNumeric.Value = theta;
                if (skCircleView.rolloverNeg)
                    sizeNumeric.Value = Math.Min(skCircleView.mouseDownPos + 60 - theta + 1, 60);
                else
                    sizeNumeric.Value = skCircleView.mouseDownPos - theta + 1;
            }
        }
    }

    public async Task ShowInitialSettings()
    {
        var window = new InitChartSettingsView();
        window.DataContext = chartSettingsViewModel;
        chartSettingsViewModel.SaveSettings = false;
        
        var dialog = new ContentDialog
        {
            Title = "Initial Chart Settings",
            Content = window,
            PrimaryButtonCommand = chartSettingsViewModel.SaveSettingsCommand,
            PrimaryButtonText = "Save Settings",
            IsPrimaryButtonEnabled = true,
            // CloseButtonCommand = chartSettingsViewModel.CloseSettingsCommand,
            CloseButtonText = "Cancel"
        };
        chartSettingsViewModel.Dialog = dialog;
        Dispatcher.UIThread.Post(
            async () =>
            {
                await dialog.ShowAsync();
                
                if (!chartSettingsViewModel.SaveSettings)
                    return;

                chartSettingsViewModel.SaveSettings = false;
                chartSettingsViewModel.Dialog = null;
                var initBpm = chart.Gimmicks.FirstOrDefault(x => x.Measure == 0.0f && x.GimmickType == GimmickType.BpmChange);
                if (initBpm != null)
                    initBpm.BPM = chartSettingsViewModel.Bpm;
                else
                    chart.Gimmicks.Add(new Gimmick()
                        {BPM = chartSettingsViewModel.Bpm, BeatInfo = new BeatInfo(0, 0), GimmickType = GimmickType.BpmChange});

                var initTimSig =
                    chart.Gimmicks.FirstOrDefault(x => x.Measure == 0.0f && x.GimmickType == GimmickType.TimeSignatureChange);
                if (initTimSig != null)
                {
                    initTimSig.TimeSig.Upper = chartSettingsViewModel.TimeSigUpper;
                    initTimSig.TimeSig.Lower = chartSettingsViewModel.TimeSigLower;
                }
                else
                    chart.Gimmicks.Add(
                        new Gimmick()
                        {
                            TimeSig = new TimeSignature()
                                {Upper = chartSettingsViewModel.TimeSigUpper, Lower = chartSettingsViewModel.TimeSigLower},
                            BeatInfo = new BeatInfo(0, 0),
                            GimmickType = GimmickType.TimeSignatureChange
                        });

                chart.Offset = chartSettingsViewModel.Offset;
                chart.MovieOffset = chartSettingsViewModel.MovieOffset;

                if (selectedGimmickIndex == -1)
                    selectedGimmickIndex = 0;
                UpdateGimmickLabels();
                chart.Gimmicks = chart.Gimmicks.OrderBy(x => x.Measure).ToList();
                chart.RecalcTime();
            });
    }

    public async Task SaveMenuItem_OnClick()
    {
        await SaveFile(isNewFile || isRecoveredFile);
    }

    public async Task SaveAsMenuItem_OnClick()
    {
        await SaveFile(true);
    }

    public async Task ExitMenuItem_OnClick()
    {
        if (!await PromptSave())
            return;
        CanShutdown = true;
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private async Task<bool> SaveFile(bool prompt = true)
    {
        var result = prompt;
        
        if (prompt || saveFilename.Length < 1)
        {
            var file = await GetStorageProvider().SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                DefaultExtension = "mer",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("MER files")
                    {
                        Patterns = new[] {"*.mer"},
                        AppleUniformTypeIdentifiers = new[] {"public.item"}
                    }
                }
            });
            result = file is {CanOpenWrite: true};
            if (result)
            {
                if (!IsDesktop)
                    saveFileStream = await file.OpenWriteAsync();
                if (file.TryGetUri(out var uri))
                    saveFilename = uri.LocalPath;
                result = true;
            }
        }

        // if we had to prompt and didn't get a file, stop now
        if (prompt && !result) return false;

        // use file paths instead of streams on desktop
        if (IsDesktop)
            chart.WriteFile(saveFilename);
        else
            chart.WriteFile(saveFileStream);
        isNewFile = false;
        if (isRecoveredFile)
        {
            DeleteAutosaves();
            autosaveFile = "";
        }

        isRecoveredFile = false;
        await File.WriteAllTextAsync(tempStatusPath, "false");
        SetText();
        return result;
    }


    /// <summary>
    /// Prompts for a save if the chart is not currently saved.
    /// </summary>
    /// <returns>TRUE if the calling method should continue, or FALSE if the calling method should return</returns>
    private async Task<bool> PromptSave()
    {
        if (chart.IsSaved)
            return true;

        var result = await ShowBlockingMessageBox("Save Changes",
            "Current chart is unsaved. Do you wish to save your changes?", MessageBoxType.YesNoCancel);
        switch (result)
        {
            case ContentDialogResult.None:
                return false;
            case ContentDialogResult.Primary:
                if (!await SaveFile())
                    return false;
                break;
        }

        return true;
    }

    public async Task NewMenuItem_OnClick()
    {
        if (!await PromptSave())
            return;

        chart = new();
        isNewFile = true;
        isRecoveredFile = false;
        ResetChartTime();
        DeleteAutosaves();
        UpdateNoteLabels(-1);
        UpdateGimmickLabels(-1);
        SetText();
        opManager.Clear();
    }

    public async Task OpenChartSettings_OnClick()
    {
        await ShowInitialSettings();
    }

    public async Task OpenMenuItem_OnClick()
    {
        if (!await PromptSave())
            return;

        var result = await GetStorageProvider().OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("MER files")
                {
                    Patterns = new[] {"*.mer"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"}
                }
            },
        });
        if (result.Count < 1 || !result[0].TryGetUri(out var uri))
        {
            // await ShowBlockingMessageBox("Error", "No file selected.");
            return;
        }

        openChartFileReadStream = await result[0].OpenReadAsync();
        openFilename = uri.LocalPath;
        if (OpenFile() && !IsDesktop)
            openChartFileWriteStream = await result[0].OpenWriteAsync();
    }

    private void maskRatio_CheckChanged(object? sender, RoutedEventArgs e)
    {
        if (currentNoteType is NoteType.MaskAdd or NoteType.MaskRemove)
            maskButton_Click(this, new());
    }

    private void maskButton_Click(object sender, RoutedEventArgs e)
    {
        if (addMaskRadio.IsChecked.Value)
            SetSelectedObject(NoteType.MaskAdd);
        else
            SetSelectedObject(NoteType.MaskRemove);
        SetSelectedObject(GimmickType.NoGimmick);
    }

    private void bpmChangeButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new GimmickView();
        var vm = new GimmicksViewModel();
        window.DataContext = vm;
        window.SetGimmick(new()
        {
            BeatInfo = new BeatInfo((int)measureNumeric.Value, (int)beat1Numeric.Value * 1920 / (int)beat2Numeric.Value),
            GimmickType = GimmickType.BpmChange
        }, GimmicksViewModel.FormReason.New);
        
        
        var dialog = new ContentDialog
        {
            Title = "Gimmick Settings",
            Content = window,
            PrimaryButtonCommand = vm.OkCommand,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel"
        };
        vm.Dialog = dialog;
        Dispatcher.UIThread.Post(
            async () =>
            {
                var result = await dialog.ShowAsync();
                if (vm.DialogSuccess)
                    InsertGimmick(vm.OutGimmicks);
            });
    }

    private void timeSigButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new GimmickView();
        var vm = new GimmicksViewModel();
        window.DataContext = vm;
        window.SetGimmick(
            new Gimmick()
            {
                BeatInfo = new BeatInfo((int)measureNumeric.Value, (int)beat1Numeric.Value * 1920 / (int)beat2Numeric.Value),
                GimmickType = GimmickType.TimeSignatureChange
            }, GimmicksViewModel.FormReason.New);
        var dialog = new ContentDialog
        {
            Title = "Gimmick Settings",
            Content = window,
            PrimaryButtonCommand = vm.OkCommand,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel"
        };
        vm.Dialog = dialog;
        Dispatcher.UIThread.Post(
            async () =>
            {
                var result = await dialog.ShowAsync();
                if (vm.DialogSuccess)
                    InsertGimmick(vm.OutGimmicks);
            });
    }

    private void hiSpeedButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new GimmickView();
        var vm = new GimmicksViewModel();
        window.DataContext = vm;
        window.SetGimmick(
            new Gimmick()
            {
                BeatInfo = new BeatInfo((int)measureNumeric.Value, (int)beat1Numeric.Value * 1920 / (int)beat2Numeric.Value),
                GimmickType = GimmickType.HiSpeedChange
            }, GimmicksViewModel.FormReason.New);
        var dialog = new ContentDialog
        {
            Title = "Gimmick Settings",
            Content = window,
            PrimaryButtonCommand = vm.OkCommand,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel"
        };
        vm.Dialog = dialog;
        Dispatcher.UIThread.Post(
            async () =>
            {
                var result = await dialog.ShowAsync();
                if (vm.DialogSuccess)
                    InsertGimmick(vm.OutGimmicks);
            });
    }

    private void stopButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new GimmickView();
        var vm = new GimmicksViewModel();
        window.DataContext = vm;
        window.SetGimmick(
            new Gimmick()
            {
                BeatInfo = new BeatInfo((int)measureNumeric.Value, (int)beat1Numeric.Value * 1920 / (int)beat2Numeric.Value),
                GimmickType = GimmickType.StopStart
            }, GimmicksViewModel.FormReason.New);
        var dialog = new ContentDialog
        {
            Title = "Gimmick Settings",
            Content = window,
            PrimaryButtonCommand = vm.OkCommand,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel"
        };
        vm.Dialog = dialog;
        Dispatcher.UIThread.Post(
            async () =>
            {
                var result = await dialog.ShowAsync();
                if (vm.DialogSuccess)
                    InsertGimmick(vm.OutGimmicks);
            });
    }

    private void reverseButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new GimmickView();
        var vm = new GimmicksViewModel();
        window.DataContext = vm;
        window.SetGimmick(
            new Gimmick()
            {
                BeatInfo = new BeatInfo((int)measureNumeric.Value, (int)beat1Numeric.Value * 1920 / (int)beat2Numeric.Value),
                GimmickType = GimmickType.ReverseStart
            }, GimmicksViewModel.FormReason.New);
        var dialog = new ContentDialog
        {
            Title = "Gimmick Settings",
            Content = window,
            PrimaryButtonCommand = vm.OkCommand, 
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel"
        };
        vm.Dialog = dialog;
        Dispatcher.UIThread.Post(
            async () =>
            {
                var result = await dialog.ShowAsync();
                if (vm.DialogSuccess)
                    InsertGimmick(vm.OutGimmicks);
            });
    }

    private void InsertGimmick(List<Gimmick> gimmicks)
    {
        var operations = new List<InsertGimmick>();
        foreach (var gim in gimmicks)
        {
            chart.Gimmicks.Add(gim);
            operations.Add(new Operations.InsertGimmick(chart, gim));
        }

        chart.IsSaved = false;
        opManager.Push(new CompositeOperation(operations[0].Description, operations));
    }

    private void NotePrevButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (chart.Notes.Count == 0)
            return;

        if (selectedNoteIndex == 0)
            selectedNoteIndex = chart.Notes.Count - 1;
        else
            selectedNoteIndex -= 1;

        UpdateNoteLabels();
    }

    private void NoteNextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (chart.Notes.Count == 0)
            return;

        if (selectedNoteIndex == chart.Notes.Count - 1)
            selectedNoteIndex = 0;
        else
            selectedNoteIndex += 1;

        UpdateNoteLabels();
    }

    private void NotePrevMeasureButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (chart.Notes.Count == 0 || selectedNoteIndex < 0)
            return;

        int lastMeasure = chart.Notes[selectedNoteIndex].BeatInfo.Measure;
        var note = chart.Notes.LastOrDefault(x => x.BeatInfo.Measure < lastMeasure);
        if (note != null)
        {
            selectedNoteIndex = chart.Notes.IndexOf(note);
        }
        else
        {
            note = chart.Notes.LastOrDefault(x => x.BeatInfo.Measure > lastMeasure);
            if (note != null)
            {
                selectedNoteIndex = chart.Notes.IndexOf(note);
            }
        }

        UpdateNoteLabels();
    }

    private void NoteNextMeasureButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (chart.Notes.Count == 0 || selectedNoteIndex >= chart.Notes.Count)
            return;

        int lastMeasure = chart.Notes[selectedNoteIndex].BeatInfo.Measure;
        var note = chart.Notes.FirstOrDefault(x => x.BeatInfo.Measure > lastMeasure);
        if (note != null)
        {
            selectedNoteIndex = chart.Notes.IndexOf(note);
        }
        else
        {
            note = chart.Notes.FirstOrDefault(x => x.BeatInfo.Measure < lastMeasure);
            if (note != null)
            {
                selectedNoteIndex = chart.Notes.IndexOf(note);
            }
        }

        UpdateNoteLabels();
    }

    private void NoteEditSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (selectedNoteIndex == -1)
            return;
        var currentNote = chart.Notes[selectedNoteIndex];

        if (currentNote.NoteType == NoteType.EndOfChart)
            return;

        var newNote = new Note()
        {
            BeatInfo = currentNote.BeatInfo,
            Position = (int) positionNumeric.Value.Value,
            Size = (int) sizeNumeric.Value.Value
        };
        opManager.InvokeAndPush(new EditNote(currentNote, newNote));
        UpdateNoteLabels();
    }

    private void NoteDeleteSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (selectedNoteIndex == -1)
            return;

        int delIndex = selectedNoteIndex;
        NoteOperation op = chart.Notes[selectedNoteIndex].IsHold
            ? new RemoveHoldNote(chart, chart.Notes[selectedNoteIndex])
            : new RemoveNote(chart, chart.Notes[selectedNoteIndex]);
        opManager.InvokeAndPush(op);
        UpdateControlsFromOperation(op, OperationDirection.Redo);
        if (selectedNoteIndex == delIndex)
        {
            UpdateNoteLabels(delIndex - 1);
        }
    }

    public void UndoMenuItem_OnClick()
    {
        if (opManager.CanUndo)
        {
            var op = opManager.Undo();
            if (op != null)
            {
                UpdateControlsFromOperation(op, OperationDirection.Undo);
            }
        }
    }

    public void RedoMenuItem_OnClick()
    {
        if (opManager.CanRedo)
        {
            var op = opManager.Redo();
            if (op != null)
            {
                UpdateControlsFromOperation(op, OperationDirection.Redo);
            }
        }
    }

    private void UpdateControlsFromOperation(IOperation op, OperationDirection dir)
    {
        if (dir == OperationDirection.Undo)
        {
            bool isInsertHold = op.GetType() == typeof(InsertHoldNote);
            bool isRemoveHold = op.GetType() == typeof(RemoveHoldNote);
            var note = op.GetType().IsSubclassOf(typeof(NoteOperation)) ? (op as NoteOperation).Note : null;
            if (note != null)
            {
                if (note.NoteType == NoteType.HoldStartNoBonus || note.NoteType == NoteType.HoldStartBonusFlair)
                {
                    if (isInsertHold)
                    {
                        SetNonHoldButtonState(true);
                        SetSelectedObject(note.NoteType);
                    }

                    if (isRemoveHold)
                    {
                        if (note.NextNote == null)
                        {
                            SetNonHoldButtonState(true);
                            SetSelectedObject(note.NoteType);
                        }
                    }
                }
                else if (note.NoteType == NoteType.HoldJoint)
                {
                    if (isInsertHold)
                    {
                        lastNote = note.PrevNote;
                    }
                }
                else if (note.NoteType == NoteType.HoldEnd)
                {
                    if (isInsertHold)
                    {
                        SetNonHoldButtonState(false);
                        SetSelectedObject(note.NoteType);
                        lastNote = note.PrevNote;
                    }
                }

                updateTime();
            }
        }
        else
        {
            bool isInsertHold = op.GetType() == typeof(InsertHoldNote);
            bool isRemoveHold = op.GetType() == typeof(RemoveHoldNote);
            var note = op.GetType().IsSubclassOf(typeof(NoteOperation)) ? (op as NoteOperation).Note : null;
            if (note != null)
            {
                if (note.NoteType == NoteType.HoldStartNoBonus || note.NoteType == NoteType.HoldStartBonusFlair)
                {
                    if (isInsertHold)
                    {
                        SetNonHoldButtonState(true);
                        SetSelectedObject(NoteType.HoldJoint);
                    }

                    if (isRemoveHold)
                    {
                        SetNonHoldButtonState(true);
                        SetSelectedObject(note.NoteType);
                    }
                }
                else if (note.NoteType == NoteType.HoldJoint)
                {
                    if (isInsertHold)
                    {
                        lastNote = note;
                    }
                }
                else if (note.NoteType == NoteType.HoldEnd)
                {
                    if (isInsertHold)
                    {
                        SetNonHoldButtonState(true);
                        SetSelectedObject(flairRadio.IsChecked.Value
                            ? NoteType.HoldStartBonusFlair
                            : NoteType.HoldStartNoBonus);
                        lastNote = note;
                    }
                }

                updateTime();
            }
        }
    }

    private void GimmickPrevButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (chart.Gimmicks.Count == 0)
            return;

        if (selectedGimmickIndex == 0)
            selectedGimmickIndex = chart.Gimmicks.Count - 1;
        else
            selectedGimmickIndex -= 1;

        UpdateGimmickLabels();
    }

    private void GimmickNextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (chart.Gimmicks.Count == 0)
            return;

        if (selectedGimmickIndex == chart.Gimmicks.Count - 1)
            selectedGimmickIndex = 0;
        else
            selectedGimmickIndex += 1;

        UpdateGimmickLabels();
    }

    private void GimmickEditButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (selectedGimmickIndex == -1)
            return;

        var gimmick = chart.Gimmicks[selectedGimmickIndex];
        Gimmick? gim1 = null;
        Gimmick? gim2 = null;
        if (gimmick.Measure == 0 && (gimmick.GimmickType == GimmickType.BpmChange ||
                                     gimmick.GimmickType == GimmickType.TimeSignatureChange))
            Dispatcher.UIThread.Post(async () => await ShowInitialSettings(), DispatcherPriority.Background);
        else
        {
            switch (gimmick.GimmickType)
            {
                case GimmickType.ReverseStart:
                    gim1 = chart.Gimmicks.FirstOrDefault(x =>
                        x.Measure > gimmick.Measure && x.GimmickType == GimmickType.ReverseMiddle);
                    gim2 = chart.Gimmicks.FirstOrDefault(x =>
                        x.Measure > gimmick.Measure && x.GimmickType == GimmickType.ReverseEnd);
                    break;
                case GimmickType.ReverseMiddle:
                    gim1 = chart.Gimmicks.LastOrDefault(x =>
                        x.Measure < gimmick.Measure && x.GimmickType == GimmickType.ReverseStart);
                    gim2 = chart.Gimmicks.FirstOrDefault(x =>
                        x.Measure > gimmick.Measure && x.GimmickType == GimmickType.ReverseEnd);
                    break;
                case GimmickType.ReverseEnd:
                    gim1 = chart.Gimmicks.LastOrDefault(x =>
                        x.Measure < gimmick.Measure && x.GimmickType == GimmickType.ReverseStart);
                    gim2 = chart.Gimmicks.LastOrDefault(x =>
                        x.Measure < gimmick.Measure && x.GimmickType == GimmickType.ReverseMiddle);
                    break;
                case GimmickType.StopStart:
                    gim1 = chart.Gimmicks.FirstOrDefault(x =>
                        x.Measure > gimmick.Measure && x.GimmickType == GimmickType.StopEnd);
                    break;
                case GimmickType.StopEnd:
                    gim1 = chart.Gimmicks.LastOrDefault(x =>
                        x.Measure < gimmick.Measure && x.GimmickType == GimmickType.StopStart);
                    break;
                default:
                    break;
            }

            var window = new GimmickView();
            var vm = new GimmicksViewModel();
            window.DataContext = vm;
            window.SetGimmick(gimmick, GimmicksViewModel.FormReason.Edit, gim1, gim2);
            
            var dialog = new ContentDialog
            {
                Title = "Gimmick Settings",
                Content = window,
                PrimaryButtonText = "Yes",
                CloseButtonText = "No"
            };
            vm.Dialog = dialog;
            Dispatcher.UIThread.Post(
                async () =>
                {
                    var result = await dialog.ShowAsync();
                    if (!vm.DialogSuccess) return;
                    var opList = new List<EditGimmick>();

                    switch (gimmick.GimmickType)
                    {
                        case GimmickType.BpmChange:
                        case GimmickType.TimeSignatureChange:
                        case GimmickType.HiSpeedChange:
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[0]));
                            break;
                        case GimmickType.ReverseStart:
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[0]));
                            opList.Add(new EditGimmick(gim1, vm.OutGimmicks[1]));
                            opList.Add(new EditGimmick(gim2, vm.OutGimmicks[2]));
                            break;
                        case GimmickType.ReverseMiddle:
                            opList.Add(new EditGimmick(gim1, vm.OutGimmicks[0]));
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[1]));
                            opList.Add(new EditGimmick(gim2, vm.OutGimmicks[2]));
                            break;
                        case GimmickType.ReverseEnd:
                            opList.Add(new EditGimmick(gim1, vm.OutGimmicks[0]));
                            opList.Add(new EditGimmick(gim2, vm.OutGimmicks[1]));
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[2]));
                            break;
                        case GimmickType.StopStart:
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[0]));
                            opList.Add(new EditGimmick(gim1, vm.OutGimmicks[1]));
                            break;
                        case GimmickType.StopEnd:
                            opList.Add(new EditGimmick(gim1, vm.OutGimmicks[0]));
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[1]));
                            break;
                        default:
                            break;
                    }

                    opManager.InvokeAndPush(new CompositeOperation(opList[0].Description, opList));
                    UpdateGimmickLabels();
                });
        }
    }

    private void GimmickDeleteButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (selectedGimmickIndex == -1)
            return;

        float measure = chart.Gimmicks[selectedGimmickIndex].Measure;
        var type = chart.Gimmicks[selectedGimmickIndex].GimmickType;
        var gimmicks = new List<Gimmick>();
        gimmicks.Add(chart.Gimmicks[selectedGimmickIndex]);
        chart.Gimmicks.RemoveAt(selectedGimmickIndex);
        switch (type)
        {
            case GimmickType.ReverseStart:
                gimmicks.Add(
                    chart.Gimmicks.First(x => x.Measure > measure && x.GimmickType == GimmickType.ReverseMiddle));
                gimmicks.Add(chart.Gimmicks.First(x => x.Measure > measure && x.GimmickType == GimmickType.ReverseEnd));
                break;
            case GimmickType.ReverseMiddle:
                gimmicks.Add(chart.Gimmicks.Last(x =>
                    x.Measure < measure && x.GimmickType == GimmickType.ReverseStart));
                gimmicks.Add(chart.Gimmicks.First(x => x.Measure > measure && x.GimmickType == GimmickType.ReverseEnd));
                break;
            case GimmickType.ReverseEnd:
                gimmicks.Add(chart.Gimmicks.Last(x =>
                    x.Measure < measure && x.GimmickType == GimmickType.ReverseStart));
                gimmicks.Add(
                    chart.Gimmicks.Last(x => x.Measure < measure && x.GimmickType == GimmickType.ReverseMiddle));
                break;
            case GimmickType.StopStart:
                gimmicks.Add(chart.Gimmicks.First(x => x.Measure > measure && x.GimmickType == GimmickType.StopEnd));
                break;
            case GimmickType.StopEnd:
                gimmicks.Add(chart.Gimmicks.Last(x => x.Measure < measure && x.GimmickType == GimmickType.StopStart));
                break;
            default:
                break;
        }

        var ops = new List<RemoveGimmick>();
        foreach (var gim in gimmicks)
            ops.Add(new RemoveGimmick(chart, gim));
        opManager.InvokeAndPush(new CompositeOperation(ops[0].Description, ops));
        if (selectedGimmickIndex >= chart.Gimmicks.Count)
            selectedGimmickIndex = chart.Gimmicks.Count - 1;
        UpdateGimmickLabels();
    }

    private async Task<ContentDialogResult> ShowBlockingMessageBox(string title, string text, MessageBoxType type = MessageBoxType.Ok)
    {
        string? primaryText = null;
        string? secondaryText = null;
        string? closeText = null;

        if (type == MessageBoxType.Ok)
            closeText = "OK";
        else if (type == MessageBoxType.YesNo)
        {
            primaryText = "Yes";
            closeText = "No";
        }
        else if (type == MessageBoxType.YesNoCancel)
        {
            primaryText = "Yes";
            secondaryText = "No";
            closeText = "Cancel";
        }
        
        var dialog = new ContentDialog
        {
            Title = title,
            Content = text,
            PrimaryButtonText = primaryText,
            SecondaryButtonText = secondaryText,
            CloseButtonText = closeText
        };
        return await dialog.ShowAsync();
    }

    private void PlayButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (currentSong != null)
        {
            if (!chart.HasInitEvents)
            {
                Dispatcher.UIThread.Post(
                    async () => await ShowBlockingMessageBox("Warning!", "Set Initial Chart Settings (from Chart Menu)."));
                return;
            }

            currentSong.PlayPosition = (uint) songTrackBar.Value;
            currentSong.Paused = !currentSong.Paused;
            if (currentSong.Paused)
            {
                playButton.Content = "Play";
                updateTimer.IsEnabled = false;
                updateTimer.Stop();
            }
            else
            {
                playButton.Content = "Pause";
                updateTimer.IsEnabled = true;
                updateTimer.Start();
            }

            // AV(fps): Round down so we can properly see newly added notes after pausing 
            measureNumeric.Value = (int) measureNumeric.Value!;
            beat1Numeric.Value = (int) beat1Numeric.Value!;
            beat2Numeric.Value = (int) beat2Numeric.Value!;
            playButton.AppendHotkey(userSettings.HotkeySettings.PlayHotkey);
        }

        updateTime();
    }

    private async Task ShowOpenSongDialog()
    {
        var result = await GetStorageProvider().OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Audio Files (*.ogg;*.wav)")
                {
                    Patterns = new[] {"*.ogg", "*.wav"},
                    MimeTypes = new[] {"audio/ogg", "audio/wav"},
                    AppleUniformTypeIdentifiers = new[] {"public.audio"}
                },
                new("WAV file")
                {
                    Patterns = new[] {"*.wav"},
                    MimeTypes = new[] {"audio/wav"},
                    AppleUniformTypeIdentifiers = new[] {"public.audio"}
                },
                new("OGG file")
                {
                    Patterns = new[] {"*.ogg"},
                    MimeTypes = new[] {"audio/ogg"},
                    AppleUniformTypeIdentifiers = new[] {"public.audio"}
                },
                new("All Files")
                {
                    Patterns = new[] {"*.*"},
                    AppleUniformTypeIdentifiers = new[] {"public.item", "public.audio"}
                }
            },
        });
        
        if (result.Count < 1 || !result[0].TryGetUri(out var uri))
            return;
        
        songFilePath = uri.LocalPath;
        songFileLabel.Text = Path.GetFileName(songFilePath);

        if (currentSong != null)
        {
            currentSong.Paused = true;
        }

        try
        {
            if (PlatformUtils.OsType == OperatingSystemType.iOS)
                currentSong = soundEngine.Play2D(await result[0].OpenReadAsync(), true, true);
            else
                currentSong = soundEngine.Play2D(songFilePath, true, true);
        }
        catch (Exception exception)
        {
            Dispatcher.UIThread.Post(async () => await ShowBlockingMessageBox("Error", exception.Message));
            playButton.IsEnabled = false;
            updateTimer.IsEnabled = false;
            updateTimer.Stop();
            return;
        }

        if (currentSong != null)
        {
            /* Volume is represented as a float from 0-1. */
            currentSong.Volume = (float) trackBarVolume.Value / (float) trackBarVolume.Maximum;

            songTrackBar.Value = 0;
            songTrackBar.Maximum = (int) currentSong.PlayLength;
            playButton.IsEnabled = true;
        }
    }

    private void SelectSongButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post( async () => await ShowOpenSongDialog(), DispatcherPriority.Background);
    }

    private void SongTrackBar_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // TODO: tighten property check
        if (currentSong == null || songTrackBar == null)
            return;
        if (IsSongPlaying())
            return;

        currentSong.PlayPosition = (uint) songTrackBar.Value;
        var info = chart.GetBeat(currentSong.PlayPosition);
        var ignoreBeatSet = valueTriggerEvent == EventSource.ManualMeasureSet;
        if (info.Measure != -1 && valueTriggerEvent != EventSource.MouseWheel)
        {
            if (!ignoreBeatSet)
            {
                valueTriggerEvent = EventSource.TrackBar;
                measureNumeric.Value = info.Measure;
                beat1Numeric.Value = (int) ((float) info.Beat / 1920.0f * (float) beat2Numeric.Value);
            }

            skCircleView.CurrentMeasure = info.MeasureDecimal;
        }

        if (valueTriggerEvent != EventSource.MouseWheel)
            valueTriggerEvent = EventSource.None;
    }

    private void TrackBarVolume_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (trackBarVolume == null)
            return;
        if (currentSong != null && e.Property.Name == "Value")
            currentSong.Volume = (float) trackBarVolume.Value / (float) trackBarVolume.Maximum;
    }

    private void TrackBarSpeed_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        /* No song, nothing to do. */
        if (trackBarSpeed == null || currentSong == null)
            return;
        currentSong.PlaybackSpeed = (float) (trackBarSpeed.Value / (float) trackBarSpeed.Maximum);
        LabelSpeed.Text = $"Speed (x{currentSong.PlaybackSpeed:0.00})";
    }

    private void VisualHispeedNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        var value = (float) visualHispeedNumeric.Value;
        if (value >= (float) visualHispeedNumeric.Minimum && value <= (float) visualHispeedNumeric.Maximum)
            skCircleView.TotalMeasureShowNotes = value;
        else
            visualHispeedNumeric.Value = (decimal) skCircleView.TotalMeasureShowNotes;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.I:
                if (insertButton.IsEnabled)
                    insertButton_Click(null, e);
                insertButton.Focus();
                e.Handled = true;
                return;
            case Key.Up:
            case Key.Down:
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    return;
                playbackVolumeChange(e.Key == Key.Up);
                e.Handled = true;
                return;
            default:
                var key = (int)e.Key;
                if (key == userSettings.HotkeySettings.TouchHotkey)
                {
                    if (tapButton.IsEnabled)
                        tapButton_Click(null, e);
                    tapButton.Focus();
                    e.Handled = true;
                }
                else if (key == userSettings.HotkeySettings.SlideLeftHotkey)
                {
                    if (orangeButton.IsEnabled)
                        orangeButton_Click(null, e);
                    orangeButton.Focus();
                    e.Handled = true;
                }
                else if (key == userSettings.HotkeySettings.SlideRightHotkey)
                {
                    if (greenButton.IsEnabled)
                        greenButton_Click(null, e);
                    greenButton.Focus();
                    e.Handled = true;
                }
                else if (key == userSettings.HotkeySettings.SnapUpHotkey)
                {
                    if (redButton.IsEnabled)
                        redButton_Click(null, e);
                    redButton.Focus();
                    e.Handled = true;
                }
                else if (key == userSettings.HotkeySettings.SnapDownHotkey)
                {
                    if (blueButton.IsEnabled)
                        blueButton_Click(null, e);
                    blueButton.Focus();
                    e.Handled = true;
                }
                else if (key == userSettings.HotkeySettings.ChainHotkey)
                {
                    if (chainButton.IsEnabled)
                        chainButton_Click(null, e);
                    chainButton.Focus();
                    e.Handled = true;
                }
                else if (key == userSettings.HotkeySettings.HoldHotkey)
                {
                    if (holdButton.IsEnabled)
                        holdButton_Click(null, e);
                    holdButton.Focus();
                    e.Handled = true;
                }
                else if (key == userSettings.HotkeySettings.PlayHotkey)
                {
                    if (playButton.IsEnabled)
                        PlayButton_OnClick(null, e);
                    playButton.Focus();
                    e.Handled = true;
                }
                else
                {
                    base.OnKeyDown(e);
                }
                return;
        }
    }

    private void playbackVolumeChange(bool increase)
    {
        var val = (int) trackBarVolume.LargeChange;
        /* Bounds check. */
        if (increase && (trackBarVolume.Value + val > trackBarVolume.Maximum))
        {
            val = (int) (trackBarVolume.Maximum - trackBarVolume.Value);
        }
        else if (!increase && (trackBarVolume.Value - val < trackBarVolume.Minimum))
        {
            val = (int) (trackBarVolume.Value - trackBarVolume.Minimum);
        }

        if (!increase)
        {
            val *= -1;
        }

        /*
         * Updating the trackbar volume will call the trackBarVolume_Changed
         * callback, which will update the current song volume.
         */
        trackBarVolume.Value += val;
        trackBarVolume.Focus();
    }

    public void SetShowCursor(bool value)
    {
        userSettings.ViewSettings.ShowCursor = value;
    }

    public void SetShowCursorDuringPlayback(bool value)
    {
        userSettings.ViewSettings.ShowCursorDuringPlayback = value;
    }

    public void SetHighlightViewedNote(bool value)
    {
        userSettings.ViewSettings.HighlightViewedNote = value;
    }

    public void SetSelectLastInsertedNote(bool value)
    {
        userSettings.ViewSettings.SelectLastInsertedNote = value;
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        // we need this condition because Shutdown() calls Close()
        Dispatcher.UIThread.Post(async () => await ExitMenuItem_OnClick(), DispatcherPriority.Background);
        e.Cancel = true;
    }
}
