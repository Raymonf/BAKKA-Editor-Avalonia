using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using BAKKA_Editor.Data;
using BAKKA_Editor.Enums;
using BAKKA_Editor.Operations;
using BAKKA_Editor.SoundEngines;
using BAKKA_Editor.ViewModels;
using DynamicData;
using FluentAvalonia.UI.Controls;
using SkiaSharp;
using Tomlyn;

namespace BAKKA_Editor.Views;

public partial class MainView : UserControl
{
    private static SKColor BackColor = SKColors.LightGray;
    private string autosaveFile = "";
    private DispatcherTimer autoSaveTimer;

    // View State
    public bool CanShutdown;
    private MainViewModel _vm;
    private bool SwallowKeyUp = false;

    // Chart
    private Chart chart = new();

    // Dialogs
    private ChartSettingsViewModel chartSettingsViewModel;

    private double curDelta;
    private BonusType currentBonusType = BonusType.NoBonus;
    private GimmickType currentGimmickType = GimmickType.NoGimmick;
    private int currentNoteIndex;

    // Note Selection
    private NoteType currentNoteType = NoteType.TouchNoBonus;
    private IBakkaSound? currentSong;
    private Note? endOfChartNote;

    // Program info
    private string fileVersion = "";
    private IBakkaSampleChannel? hitsoundChannel;

    // Hitsounds
    private IBakkaSample? hitsoundSample;
    private DispatcherTimer hitsoundTimer;
    private bool isInsertingHold;
    private bool isNewFile = true;
    private bool isRecoveredFile;

    private readonly float lastMeasure = 0.0f;
    private Note? lastNote;
    private Note? nextSelectedNote; // so that we know the last newly inserted note
    private Stream? openChartFileReadStream;
    private Stream? openChartFileWriteStream;

    // File selector state
    private string openFilename = "";

    // Operations
    private OperationManager opManager;

    // Threading pain hack
    public bool ResetBackColor = true;
    private string saveFilename = ""; // for desktop

    private Stream? saveFileStream; // for iOS, or something?
    private int selectedGimmickIndex = -1;
    private int selectedNoteIndex = -1;

    // Playfield
    private SkCircleView skCircleView;
    private string songFilePath = "";

    // Music
    private IBakkaSoundEngine soundEngine;
    private string tempFilePath = "";
    private string tempStatusPath = "";

    // Timers
    private DispatcherTimer updateTimer;
    private UserSettings userSettings = new();

    private EventSource valueTriggerEvent = EventSource.None;

    public MainView()
    {
        Application.Current.RequestedThemeVariant = ThemeVariant.Dark; //.Light;

        // set up the data context before InitializeComponent()
        DataContext ??= new MainViewModel();
        InitializeComponent();
        Setup();
        KeyDownEvent.AddClassHandler<TopLevel>(OnPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyUpEvent.AddClassHandler<TopLevel>(OnPreviewKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private static bool IsDesktop => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                                     RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public async Task SaveMenuItem_OnClick()
    {
        await SaveFile(isNewFile || isRecoveredFile);
    }

    public async Task SaveAsMenuItem_OnClick()
    {
        await SaveFile();
    }

    public async Task ExitMenuItem_OnClick()
    {
        if (!await PromptSave())
            return;

        if (tempStatusPath != "")
            File.Delete(tempStatusPath);
        if (tempFilePath != "")
            File.Delete(tempFilePath);

        // Update user settings.toml
        try
        {
            if (File.Exists("settings.toml"))
                File.WriteAllText("settings.toml", Toml.FromModel(userSettings));
        }
        catch (Exception ex)
        {
            await ShowBlockingMessageBox("Warning",
                $"Settings failed to save: {ex}");
        }

        CanShutdown = true;
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    public async Task NewMenuItem_OnClick()
    {
        if (!await PromptSave())
            return;

        chart = new Chart();
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

        var result = await GetStorageProvider().OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("MER files")
                {
                    Patterns = new[] {"*.mer"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"}
                }
            }
        });
        if (result.Count < 1)
            // await ShowBlockingMessageBox("Error", "No file selected.");
            return;

        openChartFileReadStream = await result[0].OpenReadAsync();
        openFilename = result[0].Path.LocalPath;
        if (OpenFile() && !IsDesktop)
            openChartFileWriteStream = await result[0].OpenWriteAsync();
    }

    public void UndoMenuItem_OnClick()
    {
        if (opManager.CanUndo)
        {
            var op = opManager.Undo();
            if (op != null)
            {
                UpdateControlsFromOperation(op, OperationDirection.Undo);
                //check for if it's a segment hold, if so treat them as 1 object by doing everything twice
                if (op.GetType() == typeof(RemoveHoldNote))
                    foreach (var note in chart.Notes)
                        if (note.IsHold && note.NextNote == null && note.PrevNote == null)
                        {
                            if (opManager.CanUndo)
                            {
                                var op2 = opManager.Undo();
                                if (op2 != null)
                                {
                                    UpdateControlsFromOperation(op2, OperationDirection.Undo);
                                    //check for the edge case of someone placing a hold start then deleting it
                                    if (op2.GetType() == typeof(InsertHoldNote)) RedoMenuItem_OnClick();
                                }
                            }

                            return;
                        }
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
                //check for if it's a segment hold, if so treat them as 1 object by doing everything twice
                if (op.GetType() == typeof(RemoveHoldNote))
                    foreach (var note in chart.Notes)
                        if (note.IsHold && note.NextNote == null && note.PrevNote == null)
                        {
                            if (opManager.CanRedo)
                            {
                                var op2 = opManager.Redo();
                                if (op2 != null)
                                {
                                    UpdateControlsFromOperation(op2, OperationDirection.Redo);
                                    //check for the edge case of someone placing a hold start then deleting it
                                    if (op2.GetType() == typeof(InsertHoldNote)) UndoMenuItem_OnClick();
                                }
                            }

                            return;
                        }
            }
        }
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

    public void SetShowGimmicksInCircleView(bool value)
    {
        userSettings.ViewSettings.ShowGimmicks = value;
    }

    public void SetShowGimmicksDuringPlaybackInCircleView(bool value)
    {
        userSettings.ViewSettings.ShowGimmicksDuringPlayback = value;
    }

    public void SetDarkMode(bool value)
    {
        userSettings.ViewSettings.DarkMode = value;
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
        BackColor = SKColor.Parse(value ? "#ff444444" : "#FFF3F3F3");
        ResetBackColor = true;
    }

    public void SetShowMeasureButtons(bool value)
    {
        userSettings.ViewSettings.ShowMeasureButtons = value;
    }

    private void Setup()
    {
        soundEngine = new BassBakkaSoundEngine();
        chartSettingsViewModel = new ChartSettingsViewModel();
        skCircleView = new SkCircleView(new SizeF(611, 611));
        opManager = new OperationManager();

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
         }*/

        // Look for user settings
        if (File.Exists("settings.toml"))
        {
            userSettings = Toml.ToModel<UserSettings>(File.ReadAllText("settings.toml"));
        }
        else
        {
            userSettings = new UserSettings();
            File.WriteAllText("settings.toml", Toml.FromModel(userSettings));
        }

        // Apply settings
        // TODO: REFACTOR THIS SHIT!!!!!!!!!
        _vm = (MainViewModel) DataContext!;
        _vm.ShowCursor = userSettings.ViewSettings.ShowCursor;
        _vm.ShowCursorDuringPlayback = userSettings.ViewSettings.ShowCursorDuringPlayback;
        _vm.HighlightViewedNote = userSettings.ViewSettings.HighlightViewedNote;
        _vm.SelectLastInsertedNote = userSettings.ViewSettings.SelectLastInsertedNote;
        _vm.ShowGimmicksInCircleView = userSettings.ViewSettings.ShowGimmicks;
        _vm.ShowGimmicksDuringPlaybackInCircleView = userSettings.ViewSettings.ShowGimmicksDuringPlayback;
        _vm.DarkMode = userSettings.ViewSettings.DarkMode;
        _vm.AreMeasureButtonsVisible = userSettings.ViewSettings.ShowMeasureButtons;
        _vm.PlaceNoteOnDrag = userSettings.ViewSettings.PlaceNoteOnDrag;
        _vm.VisualHiSpeedNumeric = (decimal) userSettings.ViewSettings.HispeedSetting;
        _vm.VolumeTrackBar = userSettings.ViewSettings.Volume;
        _vm.HitsoundVolumeTrackBar = Math.Clamp(userSettings.SoundSettings.HitsoundVolume, 0, 100);
        _vm.ShowNotesOnBeat = userSettings.ViewSettings.ShowNotesOnBeat;
        SetDarkMode(userSettings.ViewSettings.DarkMode);

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

        hitsoundTimer =
            new DispatcherTimer(TimeSpan.FromMilliseconds(8), DispatcherPriority.Background, HitsoundTimer_Tick);
        hitsoundTimer.IsEnabled = false;

        Dispatcher.UIThread.Post(async () => await CheckAutoSaves(), DispatcherPriority.Background);

        // HACK HACK HACK HACK HACK: run on the render thread since we know it'll only render after everything is initialized :/
        // TODO: how do we fix this?
        Dispatcher.UIThread.Post(OnResize, DispatcherPriority.Render);

        HitsoundSetup();
    }

    private void HitsoundSetup()
    {
        if (!userSettings.SoundSettings.HitsoundEnabled)
        {
            Dispatcher.UIThread.Post(async () => { HitsoundPanel.IsVisible = false; });
            return;
        }

        var hitsoundPath = userSettings.SoundSettings.HitsoundPath;
        if (!File.Exists(hitsoundPath))
        {
            Dispatcher.UIThread.Post(
                async () => await ShowBlockingMessageBox("Hitsound Error",
                    "Hitsounds were enabled but the path to the hitsound file is not valid (not found)."));
            return;
        }

        try
        {
            hitsoundSample = new BassBakkaSample(hitsoundPath);
            if (hitsoundSample.Loaded)
                hitsoundChannel = hitsoundSample.GetChannel();
            else
                Dispatcher.UIThread.Post(async () => await ShowBlockingMessageBox("Error",
                    "Failed to load the hitsound. Ensure it is a valid flac, wav, or ogg (Vorbis) file."));
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                async () => await ShowBlockingMessageBox("Hitsound Error",
                    "An error occurred trying to create the hitsound channel: " + ex.Message));
        }
    }

    private IStorageProvider GetStorageProvider()
    {
        if (VisualRoot != null && VisualRoot is TopLevel)
            return (VisualRoot as TopLevel)!.StorageProvider;
        throw new Exception(":(");
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (currentSong == null)
            return;

        // actually pause the song if we're at the end
        if (currentSong.PlayPosition >= currentSong.PlayLength && !currentSong.Paused)
        {
            currentSong.Paused = true;
            OnPauseSong();

            // move to the beginning
            currentSong.PlayPosition = 0;
        }

        _vm.SongTrackBar = (int) currentSong.PlayPosition;
        var info = chart.GetBeat(currentSong.PlayPosition);
        if (info.Measure != -1)
        {
            if (valueTriggerEvent == EventSource.None)
                valueTriggerEvent = EventSource.UpdateTick;
            if (info.Measure < 0) _vm.MeasureNumeric = 0;
            else _vm.MeasureNumeric = info.Measure;
            // TODO: weird rounding behavior for slow scrolling on longer songs...?
            // investigate how to fix this. is it related to playback position precision from bass?
            // +5 seems to work as a primitive "round up"
            var beat1 = (int) ((info.Beat + 5) / 1920.0f * (float) _vm.Beat2Numeric);
            if ((int) _vm.Beat1Numeric != beat1)
                _vm.Beat1Numeric = beat1;
            skCircleView.CurrentMeasure = info.MeasureDecimal;
            if (valueTriggerEvent == EventSource.UpdateTick)
                valueTriggerEvent = EventSource.None;


            // TODO Fix hi-speed (it needs to be able to display multiple hi-speeds in the circle view at once)
            //// Change hi-speed, if applicable
            //var hispeed = chart.Gimmicks.Where(x => x.Measure <= info.Measure && x.GimmickType == GimmickType.HiSpeedChange).LastOrDefault();
            //if (hispeed != null && hispeed.HiSpeed != circleView.TotalMeasureShowNotes)
            //{
            //    _vm.VisualHiSpeedNumeric = (decimal)hispeed.HiSpeed;
            //}
        }
    }

    private void HitsoundTimer_Tick(object? sender, EventArgs e)
    {
        if (hitsoundChannel == null || currentSong == null)
            return;

        // we call bass_init with the latency flag, so we can get the latency from bass_info
        var latency = soundEngine.GetLatency();
        var offset = 0.0;
        if (userSettings.SoundSettings.HitsoundAdditionalOffsetMs != 0)
            offset = userSettings.SoundSettings.HitsoundAdditionalOffsetMs / 1000.0;
        var info = chart.GetBeat(currentSong.PlayPosition);
        var currentMeasure = info.MeasureDecimal + latency + offset; // offset by latency
        while (currentNoteIndex < chart.Notes.Count &&
               chart.Notes[currentNoteIndex].BeatInfo.MeasureDecimal <= currentMeasure)
        {
            var note = chart.Notes[currentNoteIndex];
            var isSoundedNote =
                (int) note.NoteType is >= (int) NoteType.TouchNoBonus and <= (int) NoteType.HoldStartNoBonus
                or >= (int) NoteType.Chain || (int) note.NoteType == (int) NoteType.HoldEnd;
            if (isSoundedNote && note.BeatInfo.MeasureDecimal > lastMeasure) hitsoundChannel?.Play(true);
            currentNoteIndex++;
        }
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        if (tempFilePath == "")
            tempFilePath = PlatformUtils.GetTempFileName().Replace(".tmp", ".mer");

        var tempFileStream = File.Open(tempFilePath, FileMode.Create);
        if ((chart.Notes.Count > 0 || chart.Gimmicks.Count > 0) && !chart.IsSaved)
        {
            chart.WriteFile(tempFileStream, false);
            File.WriteAllLines(tempStatusPath, new[] {"true", DateTime.Now.ToString("yyyy-MM-dd HH:mm")});
        }
        else
        {
            DeleteAutosaves(tempFilePath);
        }
    }

    private void SetInitialSong()
    {
        // :)
        songFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ERROR.ogg");
        try
        {
            currentSong = soundEngine.Play2D(songFilePath, false, true);
        }
        catch
        {
        } // this is ok

        if (currentSong != null)
        {
            /* Volume is represented as a float from 0-1. */
            UpdateSongVolume();

            _vm.SongTrackBar = 0;
            _vm.SongTrackBarMaximum = (int) currentSong.PlayLength;
        }
        else
        {
            playButton.IsEnabled = false;
        }
    }

    private void SetText()
    {
        var save = chart.IsSaved ? "" : "*";
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
                var checkAutosave = false;
                bool.TryParse(statusLines[0], out checkAutosave);
                if (checkAutosave)
                {
                    var autosaveTime = ".";
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

                                if (!isRecoveredFile) DeleteAutosaves();

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
        chart = new Chart();
        lock (chart)
        {
            var loadSuccess = chart.ParseFile(openChartFileReadStream);
            if (!loadSuccess)
            {
                Dispatcher.UIThread.Post(
                    async () => await ShowBlockingMessageBox("Error",
                        "Failed to parse file. Ensure it is not corrupted."));
                chart = new Chart();
                openChartFileReadStream.Close();
                return false;
            }

            // Successful parse
            var initGimmicks = chart.Gimmicks.Where(x => x.StartTime == 0).ToList();
            var initBpm = initGimmicks.FirstOrDefault(x => x.GimmickType == GimmickType.BpmChange);
            var bpm = initBpm?.BPM ?? 120.0;
            var initTimeSig = initGimmicks.FirstOrDefault(x => x.GimmickType == GimmickType.TimeSignatureChange);
            var timeSigUpper = initTimeSig != null ? initTimeSig.TimeSig.Upper : 4;
            var timeSigLower = initTimeSig != null ? initTimeSig.TimeSig.Lower : 4;
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
        }
        return true;
    }

    private void DeleteAutosaves(string keep = "")
    {
        var oldAutosave = Directory.GetFiles(PlatformUtils.GetTempPath(), "*.mer");
        foreach (var file in oldAutosave)
        {
            try
            {
                if (file != keep)
                    File.Delete(file);
            }
            catch
            {
            }
        }
    }

    private void RenderCanvas(SKCanvas canvas)
    {
        if (userSettings.ViewSettings.RenderSafelyButSlowly)
        {
            try
            {
                DoCanvasRender(canvas);
            }
            catch (InvalidOperationException ex)
            {
                Dispatcher.UIThread.Post(
                    async () => await ShowBlockingMessageBox("Warning",
                        "Caught an exception while rendering. A collection has potentially changed during render. Please yell at Ray to check this out, since you should never ever _ever_ see this." + Environment.NewLine + ex.Message));
            }
        }
        else
        {
            DoCanvasRender(canvas);
        }
    }

    private void DoCanvasRender(SKCanvas canvas)
    {
        skCircleView.showHispeed = userSettings.ViewSettings.ShowGimmicksDuringPlayback;

        skCircleView.SetCanvas(canvas);

        skCircleView.DrawBackground(BackColor, ResetBackColor);

        lock (chart)
        {
            // Draw masks
            skCircleView.DrawMasks(chart);

            // Draw base and measure circle.
            skCircleView.DrawCircle(chart);

            // Draw degree lines
            skCircleView.DrawDegreeLines();

            // Draw Gimmicks
            skCircleView.DrawGimmicks(chart, userSettings.ViewSettings.ShowGimmicks, selectedGimmickIndex);

            // Draw holds
            //skCircleView.DrawHolds(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex);
            skCircleView.DrawHoldsSingle(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex);

            // Draw notes
            skCircleView.DrawNotes(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex);
        }

        // Determine if cursor should be showing
        var showCursor = userSettings.ViewSettings.ShowCursor || skCircleView.mouseDownPos != -1;
        if (currentSong != null && !currentSong.Paused)
            showCursor = userSettings.ViewSettings.ShowCursorDuringPlayback;

        // Draw cursor
        if (showCursor)
            skCircleView.DrawCursor(currentNoteType, (float) _vm.PositionNumeric, (float) _vm.SizeNumeric);
    }

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
            switch (_vm.Beat2Numeric)
            {
                // Shift beat division by standard musical quantization
                // TODO: Take time signature into account?
                case < 2:
                {
                    if (delta > 0)
                        _vm.Beat2Numeric = 2;
                    return;
                }
                case 2 when delta < 0:
                    _vm.Beat2Numeric = 1;
                    return;
            }

            var low = 0;
            var high = 1;
            while (!(_vm.Beat2Numeric >= 1 << low && _vm.Beat2Numeric <= 1 << high))
            {
                low++;
                high++;
            }

            if (delta < 0)
                _vm.Beat2Numeric = 1 << low;
            else if (high < 10)
                _vm.Beat2Numeric = 1 << (high + 1);
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
                _vm.Beat1Numeric = (int) _vm.Beat1Numeric + 1;
            else
                _vm.Beat1Numeric = (int) _vm.Beat1Numeric - 1;
        }
    }

    private bool IsSongPlaying()
    {
        if (currentSong != null && !currentSong.Paused)
            return true;
        return false;
    }

    private void Beat1Numeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (e.NewValue == null)
            return;

        var value = e.NewValue;

        if (value >= _vm.Beat2Numeric)
        {
            _vm.MeasureNumeric++;
            _vm.Beat1Numeric = 0;
            return;
        }

        if (value < 0)
        {
            if (_vm.MeasureNumeric > 0)
            {
                _vm.MeasureNumeric--;
                _vm.Beat1Numeric = _vm.Beat2Numeric - 1;
                return;
            }

            if (_vm.MeasureNumeric == 0)
            {
                _vm.Beat1Numeric = 0;
                return;
            }
        }

        if (currentSong != null && !IsSongPlaying() && valueTriggerEvent != EventSource.TrackBar)
        {
            var time = chart.GetTime(new BeatInfo((int) _vm.MeasureNumeric,
                (int) value * 1920 / (int) _vm.Beat2Numeric));
            if (time < 0)
                _vm.SongTrackBar = 0;
            else
                _vm.SongTrackBar = time;
        }

        updateTime();

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
                gimmickValueLabel.Text = gimmick.TimeSig.Upper + " / " + gimmick.TimeSig.Lower;
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
            gimmickDeleteButton.IsEnabled = false;
        else
            gimmickDeleteButton.IsEnabled = true;
    }

    private void SetSelectedObject(NoteType type)
    {
        currentNoteType = type;

        // bonus variants are only for tap and slide
        bonusRadio.IsEnabled = currentNoteType.IsTouchNote() || currentNoteType.IsSlideNote();

        var minSize = 1;
        switch (type)
        {
            case NoteType.TouchNoBonus:
                updateLabel("Touch");
                minSize = 4;
                break;
            case NoteType.TouchBonus:
                updateLabel("Touch [Bonus]");
                minSize = 5;
                break;
            case NoteType.SnapRedNoBonus:
                updateLabel("Snap (R)");
                minSize = 6;
                break;
            case NoteType.SnapBlueNoBonus:
                updateLabel("Snap (B)");
                minSize = 6;
                break;
            case NoteType.SlideOrangeNoBonus:
                updateLabel("Slide (O)");
                minSize = 5;
                break;
            case NoteType.SlideOrangeBonus:
                updateLabel("Slide (O) [Bonus]");
                minSize = 7;
                break;
            case NoteType.SlideGreenNoBonus:
                updateLabel("Slide (G)");
                minSize = 5;
                break;
            case NoteType.SlideGreenBonus:
                updateLabel("Slide (G) [Bonus]");
                minSize = 7;
                break;
            case NoteType.HoldStartNoBonus:
                updateLabel("Hold Start");
                minSize = 2;
                break;
            case NoteType.HoldJoint:
                if (endHoldCheck.IsChecked!.Value)
                {
                    updateLabel("Hold End");
                    currentNoteType = NoteType.HoldEnd;
                    minSize = 1;
                }
                else
                {
                    updateLabel("Hold Middle");
                    minSize = 0;
                }

                break;
            case NoteType.HoldEnd:
                updateLabel("Hold End");
                minSize = 1;
                break;
            case NoteType.MaskAdd:
                if (clockwiseMaskRadio.IsChecked!.Value)
                    updateLabel("Mask Add (Clockwise)");
                else if (cClockwiseMaskRadio.IsChecked!.Value)
                    updateLabel("Mask Add (Counter-Clockwise)");
                else
                    updateLabel("Mask Add (From Center)");
                minSize = 1;
                break;
            case NoteType.MaskRemove:
                if (clockwiseMaskRadio.IsChecked!.Value)
                    updateLabel("Mask Remove (Clockwise)");
                else if (cClockwiseMaskRadio.IsChecked!.Value)
                    updateLabel("Mask Remove (Counter-Clockwise)");
                else
                    updateLabel("Mask Remove (From Center)");
                minSize = 1;
                break;
            case NoteType.EndOfChart:
                updateLabel("End of Chart");
                minSize = 60;
                break;
            case NoteType.Chain:
                updateLabel("Chain");
                minSize = 4;
                break;
            case NoteType.TouchBonusFlair:
                updateLabel("Touch [R Note]");
                minSize = 6;
                break;
            case NoteType.SnapRedBonusFlair:
                updateLabel("Snap (R) [R Note]");
                minSize = 8;
                break;
            case NoteType.SnapBlueBonusFlair:
                updateLabel("Snap (B) [R Note]");
                minSize = 8;
                break;
            case NoteType.SlideOrangeBonusFlair:
                updateLabel("Slide (O) [R Note]");
                minSize = 10;
                break;
            case NoteType.SlideGreenBonusFlair:
                updateLabel("Slide (G) [R Note]");
                minSize = 10;
                break;
            case NoteType.HoldStartBonusFlair:
                updateLabel("Hold Start [R Note]");
                minSize = 8;
                break;
            case NoteType.ChainBonusFlair:
                updateLabel("Chain [R Note]");
                minSize = 10;
                break;
            default:
                updateLabel("None Selected");
                minSize = 1;
                break;
        }

        if (_vm.SizeNumeric < minSize) _vm.SizeNumeric = minSize;
        if (_vm.SizeTrackBar < minSize) _vm.SizeTrackBar = minSize;
        _vm.SizeNumericMinimum = minSize;
        _vm.SizeTrackBarMinimum = minSize;


    }

    private void updateLabel(string text)
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
            }
    }

    private void ResetChartTime()
    {
        _vm.MeasureNumeric = _vm.Beat1Numeric = 0;
        _vm.PositionNumeric = _vm.PositionNumericMinimum;
        _vm.SizeNumeric = _vm.SizeNumericMinimum;
        updateTime();
    }

    private void updateTime()
    {
        if (currentSong == null || (currentSong != null && currentSong.Paused))
            skCircleView.CurrentMeasure =
                (float) _vm.MeasureNumeric + (float) _vm.Beat1Numeric / (float) _vm.Beat2Numeric;

        if (currentNoteType is NoteType.HoldJoint or NoteType.HoldEnd)
            insertButton.IsEnabled = !(lastNote.BeatInfo.MeasureDecimal >= skCircleView.CurrentMeasure);
        else if (endOfChartNote != null)
            insertButton.IsEnabled = !(endOfChartNote.BeatInfo.MeasureDecimal <= skCircleView.CurrentMeasure);
        else if (currentSong != null && !currentSong.Paused)
            insertButton.IsEnabled = false;
        else
            insertButton.IsEnabled = true;

        UpdateNotesOnBeat();
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
        {
            SetSelectedObject(NoteType.SnapRedNoBonus);
        }
        else if (bonusRadio.IsChecked.Value)
        {
            flairRadio.IsChecked = true;
            SetSelectedObject(NoteType.SnapRedBonusFlair);
        }
        else if (flairRadio.IsChecked.Value)
        {
            SetSelectedObject(NoteType.SnapRedBonusFlair);
        }

        currentGimmickType = GimmickType.NoGimmick;
    }

    private void blueButton_Click(object sender, RoutedEventArgs e)
    {
        if (noBonusRadio.IsChecked.Value)
        {
            SetSelectedObject(NoteType.SnapBlueNoBonus);
        }
        else if (bonusRadio.IsChecked.Value)
        {
            flairRadio.IsChecked = true;
            SetSelectedObject(NoteType.SnapBlueBonusFlair);
        }
        else if (flairRadio.IsChecked.Value)
        {
            SetSelectedObject(NoteType.SnapBlueBonusFlair);
        }

        currentGimmickType = GimmickType.NoGimmick;
    }

    private void chainButton_Click(object sender, RoutedEventArgs e)
    {
        if (noBonusRadio.IsChecked.Value)
        {
            SetSelectedObject(NoteType.Chain);
        }
        else if (bonusRadio.IsChecked.Value)
        {
            flairRadio.IsChecked = true;
            SetSelectedObject(NoteType.ChainBonusFlair);
        }
        else if (flairRadio.IsChecked.Value)
        {
            SetSelectedObject(NoteType.ChainBonusFlair);
        }

        currentGimmickType = GimmickType.NoGimmick;
    }

    private void holdButton_Click(object sender, RoutedEventArgs e)
    {
        holdButtonClicked();
    }

    private void holdButtonClicked()
    {
        // don't reset hold state if we're already inserting a hold
        if (isInsertingHold)
            return;

        if (noBonusRadio.IsChecked.Value)
        {
            SetSelectedObject(NoteType.HoldStartNoBonus);
        }
        else if (bonusRadio.IsChecked.Value)
        {
            flairRadio.IsChecked = true;
            SetSelectedObject(NoteType.HoldStartBonusFlair);
        }
        else if (flairRadio.IsChecked.Value)
        {
            SetSelectedObject(NoteType.HoldStartBonusFlair);
        }

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
                    case BonusType.Flair:
                        flairRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SnapRedBonusFlair);
                        break;
                }

                break;
            case NoteType.SnapBlueNoBonus:
            case NoteType.SnapBlueBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        noBonusRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SnapBlueNoBonus);
                        break;
                    case BonusType.Bonus:
                    case BonusType.Flair:
                        flairRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SnapBlueBonusFlair);
                        break;
                }

                break;
            case NoteType.SlideOrangeNoBonus:
            case NoteType.SlideOrangeBonus:
            case NoteType.SlideOrangeBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        noBonusRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SlideOrangeNoBonus);
                        break;
                    case BonusType.Bonus:
                        bonusRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SlideOrangeBonus);
                        break;
                    case BonusType.Flair:
                        flairRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SlideOrangeBonusFlair);
                        break;
                }

                break;
            case NoteType.SlideGreenNoBonus:
            case NoteType.SlideGreenBonus:
            case NoteType.SlideGreenBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        noBonusRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SlideGreenNoBonus);
                        break;
                    case BonusType.Bonus:
                        bonusRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SlideGreenBonus);
                        break;
                    case BonusType.Flair:
                        flairRadio.IsChecked = true;
                        SetSelectedObject(NoteType.SlideGreenBonusFlair);
                        break;
                }

                break;
            case NoteType.HoldStartNoBonus:
            case NoteType.HoldStartBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        noBonusRadio.IsChecked = true;
                        SetSelectedObject(NoteType.HoldStartNoBonus);
                        break;
                    case BonusType.Bonus:
                    case BonusType.Flair:
                        flairRadio.IsChecked = true;
                        SetSelectedObject(NoteType.HoldStartBonusFlair);
                        break;
                }

                break;
            case NoteType.HoldJoint:
            case NoteType.HoldEnd:
            case NoteType.MaskAdd:
            case NoteType.MaskRemove:
            case NoteType.EndOfChart:
                break;
            case NoteType.Chain:
            case NoteType.ChainBonusFlair:
                switch (currentBonusType)
                {
                    case BonusType.NoBonus:
                        noBonusRadio.IsChecked = true;
                        SetSelectedObject(NoteType.Chain);
                        break;
                    case BonusType.Bonus:
                    case BonusType.Flair:
                        flairRadio.IsChecked = true;
                        SetSelectedObject(NoteType.ChainBonusFlair);
                        break;
                }

                break;
        }
    }

    private void MeasureNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        updateTime();
    }

    private void PositionNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (e.NewValue == null)
            return;
        var newValue = Convert.ToInt32(e.NewValue);
        if ((int) _vm.PositionTrackBar != newValue)
            _vm.PositionTrackBar = newValue;
    }

    private void PositionTrackBar_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!e.Property.Name.Equals("Value", StringComparison.InvariantCultureIgnoreCase) || e.NewValue == null)
            return;
        var newValue = Convert.ToInt32(e.NewValue);
        if ((int) _vm.PositionNumeric != newValue)
            _vm.PositionNumeric = newValue;
    }

    private void SizeNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (e.NewValue == null)
            return;
        var newValue = Convert.ToInt32(e.NewValue);
        if ((int) _vm.SizeTrackBar != newValue)
            _vm.SizeTrackBar = newValue;
    }

    private void SizeTrackBar_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!e.Property.Name.Equals("Value", StringComparison.InvariantCultureIgnoreCase) || e.NewValue == null)
            return;
        var newValue = Convert.ToInt32(e.NewValue);
        if ((int) _vm.SizeNumeric != newValue)
            _vm.SizeNumeric = newValue;
    }

    private void insertButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertObject();
    }

    private void InsertObject()
    {
        if (!insertButton.IsEnabled)
            return;

        var currentBeat = new BeatInfo((int) _vm.MeasureNumeric,
            (int) _vm.Beat1Numeric * 1920 / (int) _vm.Beat2Numeric);

        if (currentGimmickType == GimmickType.NoGimmick)
        {
            var tempNote = new Note
            {
                BeatInfo = currentBeat,
                NoteType = currentNoteType,
                Position = (int) _vm.PositionNumeric,
                Size = (int) _vm.SizeNumeric,
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
                    {
                        lastNote = tempNote;
                    }

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
                            async () => await ShowBlockingMessageBox("Error",
                                "Cannot place more than one 'End of Chart' Note."));
                        return;
                    }

                    if (chart.Notes.Count > 0)
                    {
                        var finalNote = chart.Notes.Aggregate((agg, next) =>
                            next.BeatInfo.MeasureDecimal > agg.BeatInfo.MeasureDecimal ? next : agg);
                        if (finalNote != null && finalNote.BeatInfo.MeasureDecimal >= currentBeat.MeasureDecimal)
                        {
                            Dispatcher.UIThread.Post(
                                async () => await ShowBlockingMessageBox("Error",
                                    "Cannot place 'End of Chart' Note before another note."));
                            return;
                        }
                    }

                    break;
            }

            // new object so update the temporary last note to the new one
            if (userSettings.ViewSettings.SelectLastInsertedNote)
                nextSelectedNote = tempNote;

            lock (chart)
                chart.Notes.Add(tempNote);

            UpdateNotesOnBeat();
            
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
        isInsertingHold = !state;

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

        if (endHoldCheck.IsChecked.Value && currentNoteType == NoteType.HoldJoint) SetSelectedObject(NoteType.HoldEnd);

        if (!endHoldCheck.IsChecked.Value && currentNoteType == NoteType.HoldEnd) SetSelectedObject(NoteType.HoldJoint);
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
        if (e.Property == BoundsProperty) Dispatcher.UIThread.Post(() => OnResize());
    }

    private void CircleControl_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(CircleControl);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;

        // X and Y are relative to the upper left of the panel
        var xCen = point.Position.X - CircleControl.DesiredSize.Width / 2;
        var yCen = -(point.Position.Y - CircleControl.DesiredSize.Height / 2);
        // Update the location of mouse click inside the circle
        skCircleView.UpdateMouseDown((float) xCen, (float) yCen, point.Position.ToSystemDrawing());
        _vm.PositionNumeric = skCircleView.mouseDownPos;
    }

    private void CircleControl_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var point = e.GetCurrentPoint(CircleControl);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased ||
            skCircleView.mouseDownPos <= -1)
            return;

        var dist = Utils.GetDist(point.Position.ToSystemDrawing(), skCircleView.mouseDownPt);
        if (dist > 5.0f && userSettings.ViewSettings.PlaceNoteOnDrag)
            InsertObject();
        skCircleView.UpdateMouseUp();
    }

    private void CircleControl_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(CircleControl);
        // Mouse down position wasn't within the window or wasn't a left click, do nothing.
        if (!point.Properties.IsLeftButtonPressed || skCircleView.mouseDownPos <= -1) return;

        var initialSize = (int) _vm.SizeNumeric;

        {
            // X and Y are relative to the upper left of the panel
            var xCen = point.Position.X - CircleControl.DesiredSize.Width / 2;
            var yCen = -(point.Position.Y - CircleControl.DesiredSize.Height / 2);
            // Update the location of mouse click inside the circle.
            var theta = skCircleView.UpdateMouseMove((float) xCen, (float) yCen);
            // Left click will alter the note width and possibly position depending on which direction we move
            if (theta == skCircleView.mouseDownPos)
            {
                _vm.PositionNumeric = skCircleView.mouseDownPos;
                initialSize = 1;
            }
            else if ((theta > skCircleView.mouseDownPos || skCircleView.rolloverPos) && !skCircleView.rolloverNeg)
            {
                _vm.PositionNumeric = skCircleView.mouseDownPos;
                if (skCircleView.rolloverPos)
                    initialSize = Math.Min(theta + 60 - skCircleView.mouseDownPos + 1, 60);
                else
                    initialSize = theta - skCircleView.mouseDownPos + 1;
            }
            else if (theta < skCircleView.mouseDownPos || skCircleView.rolloverNeg)
            {
                _vm.PositionNumeric = theta;
                if (skCircleView.rolloverNeg)
                    initialSize = Math.Min(skCircleView.mouseDownPos + 60 - theta + 1, 60);
                else
                    initialSize = skCircleView.mouseDownPos - theta + 1;
            }

            if (initialSize < _vm.SizeNumericMinimum) _vm.SizeNumeric = _vm.SizeNumericMinimum;
            else if (initialSize > 60) _vm.SizeNumeric = 60;
            else _vm.SizeNumeric = initialSize;
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
                var initBpm =
                    chart.Gimmicks.FirstOrDefault(x => x.Measure == 0.0f && x.GimmickType == GimmickType.BpmChange);
                if (initBpm != null)
                    initBpm.BPM = chartSettingsViewModel.Bpm;
                else
                    lock (chart)
                    {
                        chart.Gimmicks.Add(new Gimmick
                        {
                            BPM = chartSettingsViewModel.Bpm, BeatInfo = new BeatInfo(0, 0),
                            GimmickType = GimmickType.BpmChange
                        });
                    }

                var initTimSig =
                    chart.Gimmicks.FirstOrDefault(x =>
                        x.Measure == 0.0f && x.GimmickType == GimmickType.TimeSignatureChange);
                if (initTimSig != null)
                {
                    initTimSig.TimeSig.Upper = chartSettingsViewModel.TimeSigUpper;
                    initTimSig.TimeSig.Lower = chartSettingsViewModel.TimeSigLower;
                }
                else
                {
                    lock (chart)
                    {
                        chart.Gimmicks.Add(
                            new Gimmick
                            {
                                TimeSig = new TimeSignature
                                {
                                    Upper = chartSettingsViewModel.TimeSigUpper,
                                    Lower = chartSettingsViewModel.TimeSigLower
                                },
                                BeatInfo = new BeatInfo(0, 0),
                                GimmickType = GimmickType.TimeSignatureChange
                            });
                    }
                }

                chart.Offset = chartSettingsViewModel.Offset;
                chart.MovieOffset = chartSettingsViewModel.MovieOffset;

                if (selectedGimmickIndex == -1)
                    selectedGimmickIndex = 0;
                UpdateGimmickLabels();
                chart.RecalcTime();
            });
    }

    private async Task<bool> SaveFile(bool prompt = true)
    {
        // check if we have an end of chart note
        var hasEndOfChart = chart.Notes.Any(x => x.NoteType == NoteType.EndOfChart);
        if (!hasEndOfChart)
        {
            var shouldSave = await ShowBlockingMessageBox("Save Warning",
                "This chart does not have an 'End of Chart' note. Do you wish to save anyway?\n\nPlease note that this chart will cause the game to crash in its current state.", MessageBoxType.YesNo);
            if  (shouldSave == ContentDialogResult.None)
                return false;
        }

        var result = prompt;

        if (prompt || saveFilename.Length < 1)
        {
            var file = await GetStorageProvider().SaveFilePickerAsync(new FilePickerSaveOptions
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
            if (file != null)
            {
                if (!IsDesktop)
                    saveFileStream = await file.OpenWriteAsync();
                saveFilename = file.Path.LocalPath;
                result = true;
            }
        }

        // prevent crash later
        if (string.IsNullOrEmpty(saveFilename))
            return false;

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
    ///     Prompts for a save if the chart is not currently saved.
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

    private void maskRatio_CheckChanged(object? sender, RoutedEventArgs e)
    {
        if (currentNoteType is NoteType.MaskAdd or NoteType.MaskRemove)
            maskButton_Click(this, new RoutedEventArgs());
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
        window.SetGimmick(new Gimmick
        {
            BeatInfo = new BeatInfo((int) _vm.MeasureNumeric,
                (int) _vm.Beat1Numeric * 1920 / (int) _vm.Beat2Numeric),
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
            new Gimmick
            {
                BeatInfo = new BeatInfo((int) _vm.MeasureNumeric,
                    (int) _vm.Beat1Numeric * 1920 / (int) _vm.Beat2Numeric),
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
            new Gimmick
            {
                BeatInfo = new BeatInfo((int) _vm.MeasureNumeric,
                    (int) _vm.Beat1Numeric * 1920 / (int) _vm.Beat2Numeric),
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
            new Gimmick
            {
                BeatInfo = new BeatInfo((int) _vm.MeasureNumeric,
                    (int) _vm.Beat1Numeric * 1920 / (int) _vm.Beat2Numeric),
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
            new Gimmick
            {
                BeatInfo = new BeatInfo((int) _vm.MeasureNumeric,
                    (int) _vm.Beat1Numeric * 1920 / (int) _vm.Beat2Numeric),
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
        lock (chart)
        {
            var operations = new List<InsertGimmick>();
            foreach (var gim in gimmicks)
            {
                chart.Gimmicks.Add(gim);
                operations.Add(new InsertGimmick(chart, gim));
            }

            chart.IsSaved = false;
            opManager.Push(new CompositeOperation(operations[0].Description, operations));
        }
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

    private void NoteJumpToCurrTimeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (chart.Notes.Count == 0 || selectedNoteIndex >= chart.Notes.Count)
            return;

        var currentMeasure = skCircleView.CurrentMeasure;
        var note = chart.Notes.FirstOrDefault(x => x.BeatInfo.MeasureDecimal >= currentMeasure);
        if (note != null)
        {
            selectedNoteIndex = chart.Notes.IndexOf(note);
        }
        else
        {
            note = chart.Notes.FirstOrDefault(x => x.BeatInfo.MeasureDecimal <= currentMeasure);
            if (note != null) selectedNoteIndex = chart.Notes.IndexOf(note);
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

        var newNote = new Note
        {
            BeatInfo = currentNote.BeatInfo,
            Position = (int) _vm.PositionNumeric,
            Size = (int) _vm.SizeNumeric
        };
        opManager.InvokeAndPush(new EditNote(currentNote, newNote));
        UpdateNoteLabels();
    }

    private void NoteDeleteSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (selectedNoteIndex == -1)
            return;

        var delIndex = selectedNoteIndex;
        NoteOperation op = chart.Notes[selectedNoteIndex].IsHold
            ? new RemoveHoldNote(chart, chart.Notes[selectedNoteIndex])
            : new RemoveNote(chart, chart.Notes[selectedNoteIndex]);
        NoteOperation op2 = null;
        if (chart.Notes[selectedNoteIndex].NoteType == NoteType.HoldStartBonusFlair ||
            chart.Notes[selectedNoteIndex].NoteType == NoteType.HoldStartNoBonus)
            if (chart.Notes[selectedNoteIndex].NextNote != null)
                if (chart.Notes[selectedNoteIndex].NextNote.NoteType == NoteType.HoldEnd)
                    op2 = new RemoveHoldNote(chart, chart.Notes[selectedNoteIndex].NextNote);
        if (chart.Notes[selectedNoteIndex].NoteType == NoteType.HoldEnd)
            if (chart.Notes[selectedNoteIndex].PrevNote != null)
                if (chart.Notes[selectedNoteIndex].PrevNote.NoteType == NoteType.HoldStartBonusFlair ||
                    chart.Notes[selectedNoteIndex].PrevNote.NoteType == NoteType.HoldStartNoBonus)
                    op2 = new RemoveHoldNote(chart, chart.Notes[selectedNoteIndex].PrevNote);
        opManager.InvokeAndPush(op);
        if (op2 != null) opManager.InvokeAndPush(op2);
        UpdateControlsFromOperation(op, OperationDirection.Redo);
        if (selectedNoteIndex == delIndex) UpdateNoteLabels(delIndex - 1);
    }

    private void UpdateControlsFromOperation(IOperation op, OperationDirection dir)
    {
        if (dir == OperationDirection.Undo)
        {
            var isInsertHold = op.GetType() == typeof(InsertHoldNote);
            var isRemoveHold = op.GetType() == typeof(RemoveHoldNote);
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
                        if (note.NextNote == null)
                        {
                            SetNonHoldButtonState(true);
                            SetSelectedObject(note.NoteType);
                        }
                }
                else if (note.NoteType == NoteType.HoldJoint)
                {
                    if (isInsertHold) lastNote = note.PrevNote;
                }
                else if (note.NoteType == NoteType.HoldEnd)
                {
                    if (isInsertHold)
                    {
                        SetNonHoldButtonState(false);
                        SetSelectedObject(NoteType.HoldJoint);
                        lastNote = note.PrevNote;
                    }
                }

                updateTime();
            }
        }
        else
        {
            var isInsertHold = op.GetType() == typeof(InsertHoldNote);
            var isRemoveHold = op.GetType() == typeof(RemoveHoldNote);
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
                    if (isInsertHold) lastNote = note;
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
        {
            Dispatcher.UIThread.Post(async () => await ShowInitialSettings(), DispatcherPriority.Background);
        }
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
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[0], chart));
                            break;
                        case GimmickType.ReverseStart:
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[0], chart));
                            opList.Add(new EditGimmick(gim1, vm.OutGimmicks[1], chart));
                            opList.Add(new EditGimmick(gim2, vm.OutGimmicks[2], chart));
                            break;
                        case GimmickType.ReverseMiddle:
                            opList.Add(new EditGimmick(gim1, vm.OutGimmicks[0], chart));
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[1], chart));
                            opList.Add(new EditGimmick(gim2, vm.OutGimmicks[2], chart));
                            break;
                        case GimmickType.ReverseEnd:
                            opList.Add(new EditGimmick(gim1, vm.OutGimmicks[0], chart));
                            opList.Add(new EditGimmick(gim2, vm.OutGimmicks[1], chart));
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[2], chart));
                            break;
                        case GimmickType.StopStart:
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[0], chart));
                            opList.Add(new EditGimmick(gim1, vm.OutGimmicks[1], chart));
                            break;
                        case GimmickType.StopEnd:
                            opList.Add(new EditGimmick(gim1, vm.OutGimmicks[0], chart));
                            opList.Add(new EditGimmick(gimmick, vm.OutGimmicks[1], chart));
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

        lock (chart)
        {
            var measure = chart.Gimmicks[selectedGimmickIndex].Measure;
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
            }

            var ops = new List<RemoveGimmick>();
            foreach (var gim in gimmicks)
                ops.Add(new RemoveGimmick(chart, gim));
            opManager.InvokeAndPush(new CompositeOperation(ops[0].Description, ops));
            if (selectedGimmickIndex >= chart.Gimmicks.Count)
                selectedGimmickIndex = chart.Gimmicks.Count - 1;
            UpdateGimmickLabels();
        }
    }

    private async Task<ContentDialogResult> ShowBlockingMessageBox(string title, string text,
        MessageBoxType type = MessageBoxType.Ok)
    {
        string? primaryText = null;
        string? secondaryText = null;
        string? closeText = null;

        if (type == MessageBoxType.Ok)
        {
            closeText = "OK";
        }
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

    private void OnPauseSong()
    {
        currentNoteIndex = 0;
        Dispatcher.UIThread.Invoke(() =>
        {
            playButton.Content = "Play";
            updateTimer.IsEnabled = false;
            updateTimer.Stop();
            hitsoundTimer.Stop();
        });
    }

    private void OnPlaySong()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            playButton.Content = "Pause";
            updateTimer.IsEnabled = true;
            updateTimer.Start();
            hitsoundTimer.Start();
        });
    }

    private void PlayButton_OnClick(object? sender, RoutedEventArgs e)
    {
        currentNoteIndex = 0;

        if (currentSong != null)
        {
            if (!chart.HasInitEvents)
            {
                Dispatcher.UIThread.Post(
                    async () => await ShowBlockingMessageBox("Warning!",
                        "Set Initial Chart Settings (from Chart Menu)."));
                return;
            }

            UpdateSongVolume();
            currentSong.Paused = !currentSong.Paused;
            currentSong.PlayPosition = (uint) _vm.SongTrackBar;
            if (currentSong.Paused)
                OnPauseSong();
            else
                OnPlaySong();

            // AV(fps): Round down so we can properly see newly added notes after pausing
            _vm.MeasureNumeric = (int) _vm.MeasureNumeric!;
            _vm.Beat1Numeric = (int) _vm.Beat1Numeric!;
            _vm.Beat2Numeric = (int) _vm.Beat2Numeric!;
            playButton.AppendHotkey(userSettings.HotkeySettings.PlayHotkey);
        }

        updateTime();
    }

    private void UpdateSongVolume()
    {
        if (currentSong != null)
            currentSong.Volume = (float) _vm.VolumeTrackBar / (float) _vm.VolumeTrackBarMaximum;

        // just in case, we'll set the hitsound volume too
        hitsoundChannel?.SetVolume(
            Math.Clamp((float) _vm.HitsoundVolumeTrackBar / (float) _vm.HitsoundVolumeMaximum, 0.0f, 1.0f));
    }

    private async Task ShowOpenSongDialog()
    {
        var result = await GetStorageProvider().OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Audio Files (*.ogg;*.wav,*.flac)")
                {
                    Patterns = new[] {"*.ogg", "*.wav", "*.flac"},
                    MimeTypes = new[] {"audio/ogg", "audio/wav", "audio/flac"},
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
                new("FLAC file")
                {
                    Patterns = new[] {"*.flac"},
                    MimeTypes = new[] {"audio/flac"},
                    AppleUniformTypeIdentifiers = new[] {"public.audio"}
                },
                new("All Files")
                {
                    Patterns = new[] {"*.*"},
                    AppleUniformTypeIdentifiers = new[] {"public.item", "public.audio"}
                }
            }
        });

        if (result.Count < 1)
            return;

        songFilePath = result[0].Path.LocalPath;
        songFileLabel.Text = Path.GetFileName(songFilePath);

        if (currentSong != null) currentSong.Paused = true;

        try
        {
            if (PlatformUtils.FormFactorType == FormFactorType.Mobile)
                currentSong = soundEngine.Play2D(await result[0].OpenReadAsync(), false, true);
            else
                currentSong = soundEngine.Play2D(songFilePath, false, true);
        }
        catch (Exception exception)
        {
            Dispatcher.UIThread.Post(async () => await ShowBlockingMessageBox("Error", exception.Message));
            playButton.IsEnabled = false;
            updateTimer.IsEnabled = false;
            updateTimer.Stop();
            hitsoundTimer.Stop();
            return;
        }

        if (currentSong != null)
        {
            /* Volume is represented as a float from 0-1. */
            UpdateSongVolume();

            _vm.SongTrackBar = 0;
            _vm.SongTrackBarMaximum = (int) currentSong.PlayLength;
            playButton.IsEnabled = true;
            _vm.MeasureNumeric = 0;
            _vm.Beat1Numeric = 0;
        }
    }

    private void SelectSongButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(async () => await ShowOpenSongDialog(), DispatcherPriority.Background);
    }

    private void SongTrackBar_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // TODO: tighten property check
        if (currentSong == null)
            return;
        if (IsSongPlaying())
            return;

        currentSong.PlayPosition = (uint) _vm.SongTrackBar;
        var info = chart.GetBeat(currentSong.PlayPosition);
        if (info.Measure != -1 && valueTriggerEvent != EventSource.MouseWheel)
        {
            valueTriggerEvent = EventSource.TrackBar;
            _vm.MeasureNumeric = info.Measure < 0 ? 0 : info.Measure;
            _vm.Beat1Numeric = (int) (info.Beat / 1920.0f * (float) _vm.Beat2Numeric);
            skCircleView.CurrentMeasure = info.MeasureDecimal;
        }

        // if (valueTriggerEvent != EventSource.MouseWheel)
        valueTriggerEvent = EventSource.None;
    }

    private void TrackBarVolume_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (trackBarVolume == null)
            return;
        userSettings.ViewSettings.Volume = (int) _vm.VolumeTrackBar;
        if (currentSong != null && e.Property.Name == "Value")
            UpdateSongVolume();
    }

    private void TrackBarSpeed_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        /* No song, nothing to do. */
        if (currentSong == null)
            return;
        currentSong.PlaybackSpeed = (float) (_vm.SpeedTrackBar / (float) _vm.SpeedTrackBarMaximum);
        LabelSpeed.Text = $"Speed (x{currentSong.PlaybackSpeed:0.00})";
    }

    private void VisualHispeedNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        var value = (float) _vm.VisualHiSpeedNumeric;
        if (value >= (float) _vm.VisualHiSpeedNumericMinimum && value <= (float) _vm.VisualHiSpeedNumericMaximum)
            // update
            skCircleView.Hispeed = value;
        else
            // revert
            _vm.VisualHiSpeedNumeric = (decimal) skCircleView.Hispeed;
    }

    protected void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (!userSettings.ViewSettings.UseSpaceToPlaySink || !SwallowKeyUp)
            return;
        e.Handled = true;
        SwallowKeyUp = false;
    }

    protected void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
            return;

        try
        {
            switch (e.Key)
            {
                case Key.Space:
                    if (!userSettings.ViewSettings.UseSpaceToPlaySink)
                    {
                        base.OnKeyDown(e);
                        return;
                    }
                    e.Handled = true;
                    PlayButton_OnClick(null, e);
                    return;
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
                    var key = (int) e.Key;
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
        finally
        {
            SwallowKeyUp = e.Handled;
        }
    }

    private void playbackVolumeChange(bool increase)
    {
        var val = (int) trackBarVolume.LargeChange;
        /* Bounds check. */
        if (increase && _vm.VolumeTrackBar + val > _vm.VolumeTrackBarMaximum)
            val = (int) (_vm.VolumeTrackBarMaximum - _vm.VolumeTrackBar);
        else if (!increase && _vm.VolumeTrackBar - val < trackBarVolume.Minimum)
            val = (int) (_vm.VolumeTrackBar - trackBarVolume.Minimum);

        if (!increase) val *= -1;

        /*
         * Updating the trackbar volume will call the trackBarVolume_Changed
         * callback, which will update the current song volume.
         */
        _vm.VolumeTrackBar += val;
        trackBarVolume.Focus();
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        // we need this condition because Shutdown() calls Close()
        Dispatcher.UIThread.Post(async () => await ExitMenuItem_OnClick(), DispatcherPriority.Background);
        e.Cancel = true;
    }

    private void GimmickJumpToCurrTimeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (chart.Gimmicks.Count == 0)
            return;

        var currentMeasure = skCircleView.CurrentMeasure;
        var gimmick = chart.Gimmicks.FirstOrDefault(x => x.BeatInfo.MeasureDecimal >= currentMeasure);
        if (gimmick != null)
        {
            selectedGimmickIndex = chart.Gimmicks.IndexOf(gimmick);
        }
        else
        {
            gimmick = chart.Gimmicks.FirstOrDefault(x => x.BeatInfo.MeasureDecimal <= currentMeasure);
            if (gimmick != null) selectedGimmickIndex = chart.Gimmicks.IndexOf(gimmick);
        }

        UpdateGimmickLabels();
    }

    private void TrackBarHitsounds_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        userSettings.SoundSettings.HitsoundVolume = (int) _vm.HitsoundVolumeTrackBar;
        var volume = (float) _vm.HitsoundVolumeTrackBar / (float) _vm.HitsoundVolumeMaximum;
        hitsoundChannel?.SetVolume(Math.Clamp(volume, 0.0f, 1.0f));
    }

    private void MeasurePrevButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (chart.Notes.Count == 0)
            return;

        var last = chart.Notes[selectedNoteIndex].BeatInfo.Measure;
        var note = chart.Notes.LastOrDefault(x => x.BeatInfo.Measure < last);
        if (note != null)
        {
            selectedNoteIndex = chart.Notes.IndexOf(note);
        }
        else
        {
            note = chart.Notes.LastOrDefault(x => x.BeatInfo.Measure > last);
            if (note != null) selectedNoteIndex = chart.Notes.IndexOf(note);
        }

        UpdateNoteLabels();
    }

    private void MeasureNextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (chart.Notes.Count == 0)
            return;

        var last = chart.Notes[selectedNoteIndex].BeatInfo.Measure;
        var note = chart.Notes.FirstOrDefault(x => x.BeatInfo.Measure > last);
        if (note != null)
        {
            selectedNoteIndex = chart.Notes.IndexOf(note);
        }
        else
        {
            note = chart.Notes.FirstOrDefault(x => x.BeatInfo.Measure < last);
            if (note != null) selectedNoteIndex = chart.Notes.IndexOf(note);
        }

        UpdateNoteLabels();
    }

    // Control updates
    private enum EventSource
    {
        None,
        MouseWheel,
        SongPlaying,
        TrackBar,
        UpdateTick
    }

    private void PositionTrackBar_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;

        var factor = userSettings.ViewSettings.SliderScrollFactor;
        var delta = e.Delta.Y > 0 ? factor : -factor;

        if (userSettings.ViewSettings.HandleOverflowPositionNumericScroll)
        {
            var newVal = _vm.PositionTrackBar + delta;
            if (newVal > _vm.PositionTrackBarMaximum)
            {
                _vm.PositionTrackBar = _vm.PositionTrackBarMinimum;
            }
            else if (newVal < _vm.PositionTrackBarMinimum)
            {
                _vm.PositionTrackBar = _vm.PositionTrackBarMaximum;
            }
            else
            {
                _vm.PositionTrackBar = newVal;
            }
        }
        else
        {
            _vm.PositionTrackBar = Math.Clamp(_vm.PositionTrackBar + delta, _vm.PositionTrackBarMinimum,
                _vm.PositionTrackBarMaximum);
        }
    }

    private void SizeTrackBar_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;

        var factor = userSettings.ViewSettings.SliderScrollFactor;
        var delta = e.Delta.Y > 0 ? factor : -factor;

        if (userSettings.ViewSettings.HandleOverflowSizeNumericScroll)
        {
            var newVal = _vm.SizeTrackBar + delta;
            if (newVal > _vm.SizeTrackBarMaximum)
            {
                _vm.SizeTrackBar = _vm.SizeTrackBarMinimum;
            }
            else if (newVal < _vm.SizeTrackBarMinimum)
            {
                _vm.SizeTrackBar = _vm.SizeTrackBarMaximum;
            }
            else
            {
                _vm.SizeTrackBar = newVal;
            }
        }
        else
        {
            _vm.SizeTrackBar = Math.Clamp(_vm.SizeTrackBar + delta, _vm.SizeTrackBarMinimum, _vm.SizeTrackBarMaximum);
        }
    }

    public void SetPlaceNoteOnDrag(bool value)
    {
        userSettings.ViewSettings.PlaceNoteOnDrag = value;
    }
    
    private void UpdateNotesOnBeat()
    {
        if (!userSettings.ViewSettings.ShowNotesOnBeat)
            return;
        
        // Fill list of notes on beat
        _vm.NotesOnBeatList.Clear();
        _vm.NotesOnBeatList.AddRange(
            chart.Notes.Where(n => Math.Abs(n.BeatInfo.MeasureDecimal - skCircleView.CurrentMeasure) < 0.00001)
            .Select(n => new NoteOnBeatItem(n.NoteType.ToLabel(), n.Position, n.Size))
            .ToList()
        );
    }

    private void NotesOnBeatListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!userSettings.ViewSettings.ShowNotesOnBeat || e.AddedItems.Count < 1)
            return;
        
        var selectedNote = (NoteOnBeatItem?) e.AddedItems[0];
        if (selectedNote == null)
            return;
        var noteType = selectedNote.Type;
        var position = selectedNote.Position;
        var size = selectedNote.Size;
        var currentMeasure = skCircleView.CurrentMeasure;
        // Find the matching selected note
        var noteInChart = chart.Notes.FirstOrDefault(
            x => Math.Abs(x.BeatInfo.MeasureDecimal - currentMeasure) < 0.00001 && 
                 x.NoteType.ToLabel() == noteType &&
                 x.Position == position &&
                 x.Size == size
        );
        if (noteInChart == null)
            return;
        selectedNoteIndex = chart.Notes.IndexOf(noteInChart);
        UpdateNoteLabels();
    }
}