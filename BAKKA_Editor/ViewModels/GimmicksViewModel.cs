using System;
using System.Collections.Generic;
using Avalonia.Controls;
using BAKKA_Editor.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;

namespace BAKKA_Editor.ViewModels;

public partial class GimmicksViewModel : ObservableObject
{
    public enum FormReason
    {
        New,
        Edit
    }

    [ObservableProperty] private decimal bpm = 120;

    // Enable State
    [ObservableProperty] private bool bpmEnabled;

    [ObservableProperty] private bool dialogSuccess;
    [ObservableProperty] private decimal hiSpeed = 1;
    [ObservableProperty] private bool hiSpeedEnabled;

    // Button Text
    [ObservableProperty] private string insertButtonText = "Insert Gimmick";

    [ObservableProperty] private bool insertGimmick;
    [ObservableProperty] private bool revEnd1Beat1Enabled;
    [ObservableProperty] private bool revEnd1Beat2Enabled;
    [ObservableProperty] private bool revEnd1MeasureEnabled;
    [ObservableProperty] private bool revEnd2Beat1Enabled;
    [ObservableProperty] private bool revEnd2Beat2Enabled;
    [ObservableProperty] private bool revEnd2MeasureEnabled;
    [ObservableProperty] private bool stopEndBeat1Enabled;
    [ObservableProperty] private bool stopEndBeat2Enabled;
    [ObservableProperty] private bool stopEndMeasureEnabled;

    // Properties
    [ObservableProperty] private decimal timeSig1 = 4;
    [ObservableProperty] private bool timeSig1Enabled;
    [ObservableProperty] private decimal timeSig2 = 4;
    [ObservableProperty] private bool timeSig2Enabled;

    public GimmicksViewModel()
    {
        OkCommand = new RelayCommand<UserControl>(OnOk);
    }

    // Gimmick State
    internal Gimmick? InGimmick { get; set; }
    internal List<Gimmick> OutGimmicks { get; set; }

    public GimmickMeasureInputViewModel StartMeasureInfo { get; set; } = new();
    public GimmickMeasureInputViewModel StopEndMeasureInfo { get; set; } = new();

    public GimmickMeasureInputViewModel RevEnd1MeasureInfo { get; set; } = new();
    public GimmickMeasureInputViewModel RevEnd2MeasureInfo { get; set; } = new();

    public float StartMeasure => (float) StartMeasureInfo.Measure +
                                 (float) StartMeasureInfo.Beat1 / (float) StartMeasureInfo.Beat2;

    public float StopEndMeasure => (float) StopEndMeasureInfo.Measure +
                                   (float) StopEndMeasureInfo.Beat1 / (float) StopEndMeasureInfo.Beat2;

    public float RevEnd1Measure => (float) RevEnd1MeasureInfo.Measure +
                                   (float) RevEnd1MeasureInfo.Beat1 / (float) RevEnd1MeasureInfo.Beat2;

    public float RevEnd2Measure => (float) RevEnd2MeasureInfo.Measure +
                                   (float) RevEnd2MeasureInfo.Beat1 / (float) RevEnd2MeasureInfo.Beat2;

    public ContentDialog? Dialog { get; set; }
    public RelayCommand<UserControl> OkCommand { get; }

    private void OnOk(UserControl? userControl)
    {
        var gimmick = InGimmick;
        if (gimmick == null)
            throw new NullReferenceException(nameof(gimmick));

        switch (gimmick.GimmickType)
        {
            case GimmickType.BpmChange:
                gimmick.BeatInfo = new BeatInfo((int) StartMeasureInfo.Measure,
                    (int) StartMeasureInfo.Beat1 * 1920 / (int) StartMeasureInfo.Beat2);
                gimmick.BPM = (double) Bpm;
                OutGimmicks.Add(gimmick);
                break;
            case GimmickType.TimeSignatureChange:
                gimmick.BeatInfo = new BeatInfo((int) StartMeasureInfo.Measure,
                    (int) StartMeasureInfo.Beat1 * 1920 / (int) StartMeasureInfo.Beat2);
                gimmick.TimeSig.Upper = (int) TimeSig1;
                gimmick.TimeSig.Lower = (int) TimeSig2;
                OutGimmicks.Add(gimmick);
                break;
            case GimmickType.HiSpeedChange:
                gimmick.BeatInfo = new BeatInfo((int) StartMeasureInfo.Measure,
                    (int) StartMeasureInfo.Beat1 * 1920 / (int) StartMeasureInfo.Beat2);
                gimmick.HiSpeed = (double) HiSpeed;
                OutGimmicks.Add(gimmick);
                break;
            case GimmickType.ReverseStart:
            case GimmickType.ReverseMiddle:
            case GimmickType.ReverseEnd:
                OutGimmicks.Add(new Gimmick
                {
                    BeatInfo = new BeatInfo((int) StartMeasureInfo.Measure,
                        (int) StartMeasureInfo.Beat1 * 1920 / (int) StartMeasureInfo.Beat2),
                    GimmickType = GimmickType.ReverseStart
                });
                OutGimmicks.Add(new Gimmick
                {
                    BeatInfo = new BeatInfo((int) RevEnd1MeasureInfo.Measure,
                        (int) RevEnd1MeasureInfo.Beat1 * 1920 / (int) RevEnd1MeasureInfo.Beat2),
                    GimmickType = GimmickType.ReverseMiddle
                });
                OutGimmicks.Add(new Gimmick
                {
                    BeatInfo = new BeatInfo((int) RevEnd2MeasureInfo.Measure,
                        (int) RevEnd2MeasureInfo.Beat1 * 1920 / (int) RevEnd2MeasureInfo.Beat2),
                    GimmickType = GimmickType.ReverseEnd
                });
                break;
            case GimmickType.StopStart:
            case GimmickType.StopEnd:
                OutGimmicks.Add(new Gimmick
                {
                    BeatInfo = new BeatInfo((int) StartMeasureInfo.Measure,
                        (int) StartMeasureInfo.Beat1 * 1920 / (int) StartMeasureInfo.Beat2),
                    GimmickType = GimmickType.StopStart
                });
                OutGimmicks.Add(new Gimmick
                {
                    BeatInfo = new BeatInfo((int) StopEndMeasureInfo.Measure,
                        (int) StopEndMeasureInfo.Beat1 * 1920 / (int) StopEndMeasureInfo.Beat2),
                    GimmickType = GimmickType.StopEnd
                });
                break;
        }

        DialogSuccess = true;

        if (Dialog != null)
            Dialog.Hide();
    }
}