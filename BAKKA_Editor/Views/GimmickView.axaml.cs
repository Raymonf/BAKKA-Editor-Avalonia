using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BAKKA_Editor.ViewModels;

namespace BAKKA_Editor.Views;

public partial class GimmickView : UserControl
{
    public GimmickView()
    {
        InitializeComponent();
    }

    internal void SetGimmick(
        Gimmick baseGimmick, GimmicksViewModel.FormReason reason, Gimmick? gim1 = null, Gimmick? gim2 = null)
    {
        var vm = (GimmicksViewModel?) DataContext;
        if (vm == null)
            throw new NullReferenceException(nameof(vm));

        var quant = Utils.GetQuantization(baseGimmick.BeatInfo.Beat, 12);

        vm.InGimmick = new Gimmick();
        var gimmick = vm.InGimmick;
        gimmick.BeatInfo = new BeatInfo(baseGimmick.BeatInfo);
        gimmick.GimmickType = baseGimmick.GimmickType;

        switch (gimmick.GimmickType)
        {
            case GimmickType.BpmChange:
                vm.StartMeasureInfo.Measure = gimmick.BeatInfo.Measure;
                vm.StartMeasureInfo.Beat2 = (decimal)quant.Item2;
                vm.StartMeasureInfo.Beat1 = (decimal)quant.Item1;
                if (reason == GimmicksViewModel.FormReason.Edit)
                    vm.Bpm = (decimal) baseGimmick.BPM;
                break;
            case GimmickType.TimeSignatureChange:
                vm.StartMeasureInfo.Measure = gimmick.BeatInfo.Measure;
                vm.StartMeasureInfo.Beat2 = (decimal)quant.Item2;
                vm.StartMeasureInfo.Beat1 = (decimal)quant.Item1;
                if (reason == GimmicksViewModel.FormReason.Edit)
                {
                    vm.TimeSig1 = baseGimmick.TimeSig.Upper;
                    vm.TimeSig2 = baseGimmick.TimeSig.Lower;
                }

                break;
            case GimmickType.HiSpeedChange:
                vm.StartMeasureInfo.Measure = gimmick.BeatInfo.Measure;
                vm.StartMeasureInfo.Beat2 = (decimal)quant.Item2;
                vm.StartMeasureInfo.Beat1 = (decimal)quant.Item1;
                if (reason == GimmicksViewModel.FormReason.Edit)
                    vm.HiSpeed = (decimal) baseGimmick.HiSpeed;
                break;
            case GimmickType.ReverseStart:
                vm.StartMeasureInfo.Measure = gimmick.BeatInfo.Measure;
                vm.StartMeasureInfo.Beat2 = (decimal)quant.Item2;
                vm.StartMeasureInfo.Beat1 = (decimal)quant.Item1;
                if (reason == GimmicksViewModel.FormReason.Edit && gim1 != null && gim2 != null)
                {
                    var quantMid1 = Utils.GetQuantization(gim1.BeatInfo.Beat, 12);
                    var quantEnd1 = Utils.GetQuantization(gim2.BeatInfo.Beat, 12);
                    vm.RevEnd1MeasureInfo.Measure = gim1.BeatInfo.Measure;
                    vm.RevEnd1MeasureInfo.Beat2 = (decimal)quantMid1.Item2;
                    vm.RevEnd1MeasureInfo.Beat1 = (decimal)quantMid1.Item1;
                    vm.RevEnd2MeasureInfo.Measure = gim2.BeatInfo.Measure;
                    vm.RevEnd2MeasureInfo.Beat2 = (decimal)quantEnd1.Item2;
                    vm.RevEnd2MeasureInfo.Beat1 = (decimal)quantEnd1.Item1;
                }

                break;
            case GimmickType.ReverseMiddle:
                var quantStart2 = Utils.GetQuantization(gim1.BeatInfo.Beat, 12);
                var quantEnd2 = Utils.GetQuantization(gim2.BeatInfo.Beat, 12);
                vm.StartMeasureInfo.Measure = gim1.BeatInfo.Measure;
                vm.StartMeasureInfo.Beat2 = (decimal)quantStart2.Item2;
                vm.StartMeasureInfo.Beat1 = (decimal)quantStart2.Item1;
                vm.RevEnd1MeasureInfo.Measure = gimmick.BeatInfo.Measure;
                vm.RevEnd1MeasureInfo.Beat2 = (decimal)quant.Item2;
                vm.RevEnd1MeasureInfo.Beat1 = (decimal)quant.Item1;
                vm.RevEnd2MeasureInfo.Measure = gim2.BeatInfo.Measure;
                vm.RevEnd2MeasureInfo.Beat2 = (decimal)quantEnd2.Item2;
                vm.RevEnd2MeasureInfo.Beat1 = (decimal)quantEnd2.Item1;
                break;
            case GimmickType.ReverseEnd:
                var quantStart3 = Utils.GetQuantization(gim1.BeatInfo.Beat, 12);
                var quantMid3 = Utils.GetQuantization(gim2.BeatInfo.Beat, 12);
                vm.StartMeasureInfo.Measure = gim1.BeatInfo.Measure;
                vm.StartMeasureInfo.Beat2 = (decimal)quantStart3.Item2;
                vm.StartMeasureInfo.Beat1 = (decimal)quantStart3.Item1;
                vm.RevEnd1MeasureInfo.Measure = gim2.BeatInfo.Measure;
                vm.RevEnd1MeasureInfo.Beat2 = (decimal)quantMid3.Item2;
                vm.RevEnd1MeasureInfo.Beat1 = (decimal)quantMid3.Item1;
                vm.RevEnd2MeasureInfo.Measure = gimmick.BeatInfo.Measure;
                vm.RevEnd2MeasureInfo.Beat2 = (decimal)quant.Item2;
                vm.RevEnd2MeasureInfo.Beat1 = (decimal)quant.Item1;
                break;
            case GimmickType.StopStart:
                vm.StartMeasureInfo.Measure = gimmick.BeatInfo.Measure;
                vm.StartMeasureInfo.Beat2 = (decimal)quant.Item2;
                vm.StartMeasureInfo.Beat1 = (decimal)quant.Item1;
                if (reason == GimmicksViewModel.FormReason.Edit)
                {
                    var stopEnd = Utils.GetQuantization(gim1.BeatInfo.Beat, 12);
                    vm.StopEndMeasureInfo.Measure = gim1.BeatInfo.Measure;
                    vm.StopEndMeasureInfo.Beat2 = (decimal)stopEnd.Item2;
                    vm.StopEndMeasureInfo.Beat1 = (decimal)stopEnd.Item1;
                }

                break;
            case GimmickType.StopEnd:
                var stopStart = Utils.GetQuantization(gim1.BeatInfo.Beat, 12);
                vm.StartMeasureInfo.Measure = gim1.BeatInfo.Measure;
                vm.StartMeasureInfo.Beat2 = (decimal)stopStart.Item2;
                vm.StartMeasureInfo.Beat1 = (decimal)stopStart.Item1;
                vm.StopEndMeasureInfo.Measure = gimmick.BeatInfo.Measure;
                vm.StopEndMeasureInfo.Beat2 = (decimal)quant.Item2;
                vm.StopEndMeasureInfo.Beat1 = (decimal)quant.Item1;
                break;
            default:
                break;
        }

        vm.BpmEnabled = gimmick.GimmickType == GimmickType.BpmChange;

        vm.TimeSig1Enabled = gimmick.GimmickType == GimmickType.TimeSignatureChange;
        vm.TimeSig2Enabled = gimmick.GimmickType == GimmickType.TimeSignatureChange;

        vm.HiSpeedEnabled = gimmick.GimmickType == GimmickType.HiSpeedChange;

        vm.StopEndMeasureEnabled = gimmick.IsStop;
        vm.StopEndBeat1Enabled = gimmick.IsStop;
        vm.StopEndBeat2Enabled = gimmick.IsStop;

        vm.RevEnd1MeasureEnabled = gimmick.IsReverse;
        vm.RevEnd1Beat1Enabled = gimmick.IsReverse;
        vm.RevEnd1Beat2Enabled = gimmick.IsReverse;
        vm.RevEnd2MeasureEnabled = gimmick.IsReverse;
        vm.RevEnd2Beat1Enabled = gimmick.IsReverse;
        vm.RevEnd2Beat2Enabled = gimmick.IsReverse;

        vm.OutGimmicks = new List<Gimmick>();
        vm.InsertGimmick = false;

        vm.InsertButtonText = reason switch
        {
            GimmicksViewModel.FormReason.New => "Insert Gimmick",
            GimmicksViewModel.FormReason.Edit => "Edit Gimmick"
        };
    }
    
    private void startMeasureNumeric_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        var vm = (GimmicksViewModel?) DataContext;
        if (vm == null)
            throw new NullReferenceException(nameof(vm));
        if (vm.StopEndMeasureEnabled && vm.StopEndMeasure < vm.StartMeasure)
        {
            vm.StopEndMeasureInfo.Measure = vm.StartMeasureInfo.Measure + 1;
        }
        if (vm.RevEnd1MeasureEnabled && vm.RevEnd1Measure < vm.StartMeasure)
        {
            vm.RevEnd1MeasureInfo.Measure = vm.StartMeasureInfo.Measure + 1;
        }
    }

    private void startBeat1Numeric_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        var vm = (GimmicksViewModel?) DataContext;
        if (vm == null)
            throw new NullReferenceException(nameof(vm));
        if (vm.StartMeasureInfo.Beat1 >= vm.StartMeasureInfo.Beat2)
        {
            vm.StartMeasureInfo.Measure++;
            vm.StartMeasureInfo.Beat1 = 0;
        }
        else if (vm.StartMeasureInfo.Beat1 < 0)
        {
            if (vm.StartMeasureInfo.Measure > 0)
            {
                vm.StartMeasureInfo.Measure--;
                vm.StartMeasureInfo.Beat1 = vm.StartMeasureInfo.Beat2 - 1;
            }
            else if (vm.StartMeasureInfo.Measure == 0)
            {
                vm.StartMeasureInfo.Beat1 = 0;
            }
        }
    }

    private void stopEndMeasureNumeric_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        var vm = (GimmicksViewModel?) DataContext;
        if (vm == null)
            throw new NullReferenceException(nameof(vm));
        if (vm.StopEndMeasure < vm.StartMeasure)
        {
            vm.StopEndMeasureInfo.Measure = vm.StartMeasureInfo.Measure;
            vm.StopEndMeasureInfo.Beat1 = vm.StartMeasureInfo.Beat1;
            vm.StopEndMeasureInfo.Beat2 = vm.StartMeasureInfo.Beat2;
            return;
        }
    }

    private void stopEndBeat1Numeric_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        var vm = (GimmicksViewModel?) DataContext;
        if (vm == null)
            throw new NullReferenceException(nameof(vm));
        if (vm.StopEndMeasure < vm.StartMeasure)
        {
            vm.StopEndMeasureInfo.Measure = vm.StartMeasureInfo.Measure;
            vm.StopEndMeasureInfo.Beat1 = vm.StartMeasureInfo.Beat1;
            vm.StopEndMeasureInfo.Beat2 = vm.StartMeasureInfo.Beat2;
            return;
        }

        if (vm.StopEndMeasureInfo.Beat1 >= vm.StopEndMeasureInfo.Beat2)
        {
            vm.StopEndMeasureInfo.Measure++;
            vm.StopEndMeasureInfo.Beat1 = 0;
        }
        else if (vm.StopEndMeasureInfo.Beat1 < 0)
        {
            if (vm.StopEndMeasureInfo.Measure > 0)
            {
                vm.StopEndMeasureInfo.Measure--;
                vm.StopEndMeasureInfo.Beat1 = vm.StopEndMeasureInfo.Beat2 - 1;
            }
            else if (vm.StopEndMeasureInfo.Measure == 0)
            {
                vm.StopEndMeasureInfo.Beat1 = 0;
            }
        }
    }

    private void revEnd1MeasureNumeric_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        var vm = (GimmicksViewModel?) DataContext;
        if (vm == null)
            throw new NullReferenceException(nameof(vm));
        if (vm.RevEnd1Measure < vm.StartMeasure)
        {
            vm.RevEnd1MeasureInfo.Measure = vm.StartMeasureInfo.Measure;
            vm.RevEnd1MeasureInfo.Beat1 = vm.StartMeasureInfo.Beat1;
            vm.RevEnd1MeasureInfo.Beat2 = vm.StartMeasureInfo.Beat2;
        }
        if (vm.RevEnd2Measure < vm.RevEnd1Measure)
        {
            vm.RevEnd2MeasureInfo.Measure = vm.RevEnd1MeasureInfo.Measure;
            vm.RevEnd2MeasureInfo.Beat1 = vm.RevEnd1MeasureInfo.Beat1;
            vm.RevEnd2MeasureInfo.Beat2 = vm.RevEnd1MeasureInfo.Beat2;
        }
    }        

    private void revEnd1Beat1Numeric_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        var vm = (GimmicksViewModel?) DataContext;
        if (vm == null)
            throw new NullReferenceException(nameof(vm));
        if (vm.RevEnd1Measure < vm.StartMeasure)
        {
            vm.RevEnd1MeasureInfo.Measure = vm.StartMeasureInfo.Measure;
            vm.RevEnd1MeasureInfo.Beat1 = vm.StartMeasureInfo.Beat1;
            vm.RevEnd1MeasureInfo.Beat2 = vm.StartMeasureInfo.Beat2;
            return;
        }

        if (vm.RevEnd1MeasureInfo.Beat1 >= vm.RevEnd1MeasureInfo.Beat2)
        {
            vm.RevEnd1MeasureInfo.Measure++;
            vm.RevEnd1MeasureInfo.Beat1 = 0;
        }
        else if (vm.RevEnd1MeasureInfo.Beat1 < 0)
        {
            if (vm.RevEnd1MeasureInfo.Measure > 0)
            {
                vm.RevEnd1MeasureInfo.Measure--;
                vm.RevEnd1MeasureInfo.Beat1 = vm.RevEnd1MeasureInfo.Beat2 - 1;
            }
            else if (vm.RevEnd1MeasureInfo.Measure == 0)
            {
                vm.RevEnd1MeasureInfo.Beat1 = 0;
            }
        }
    }

    private void revEnd2MeasureNumeric_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        var vm = (GimmicksViewModel?) DataContext;
        if (vm == null)
            throw new NullReferenceException(nameof(vm));
        if (vm.RevEnd2Measure < vm.RevEnd1Measure)
        {
            vm.RevEnd2MeasureInfo.Measure = vm.RevEnd1MeasureInfo.Measure;
            vm.RevEnd2MeasureInfo.Beat1 = vm.RevEnd1MeasureInfo.Beat1;
            vm.RevEnd2MeasureInfo.Beat2 = vm.RevEnd1MeasureInfo.Beat2;
            return;
        }
    }

    private void revEnd2Beat1Numeric_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        var vm = (GimmicksViewModel?) DataContext;
        if (vm == null)
            throw new NullReferenceException(nameof(vm));
        if (vm.RevEnd2Measure < vm.RevEnd1Measure)
        {
            vm.RevEnd2MeasureInfo.Measure = vm.RevEnd1MeasureInfo.Measure;
            vm.RevEnd2MeasureInfo.Beat1 = vm.RevEnd1MeasureInfo.Beat1;
            vm.RevEnd2MeasureInfo.Beat2 = vm.RevEnd1MeasureInfo.Beat2;
            return;
        }

        if (vm.RevEnd2MeasureInfo.Beat1 >= vm.RevEnd2MeasureInfo.Beat2)
        {
            vm.RevEnd2MeasureInfo.Measure++;
            vm.RevEnd2MeasureInfo.Beat1 = 0;
        }
        else if (vm.RevEnd2MeasureInfo.Beat1 < 0)
        {
            if (vm.RevEnd2MeasureInfo.Measure > 0)
            {
                vm.RevEnd2MeasureInfo.Measure--;
                vm.RevEnd2MeasureInfo.Beat1 = vm.RevEnd2MeasureInfo.Beat2 - 1;
            }
            else if (vm.RevEnd2MeasureInfo.Measure == 0)
            {
                vm.RevEnd2MeasureInfo.Beat1 = 0;
            }
        }
    }
}