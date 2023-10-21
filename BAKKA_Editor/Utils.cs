using System;
using System.Drawing;
using Avalonia.Controls;
using Avalonia.Input;
using BAKKA_Editor.Enums;
using SkiaSharp;

namespace BAKKA_Editor;

internal static class Utils
{
    public static Point ToSystemDrawing(this Avalonia.Point p)
    {
        return new Point((int) p.X, (int) p.Y);
    }

    internal static string? GetTag(string input, string tag)
    {
        if (input.Contains(tag)) return input.Substring(input.IndexOf(tag, StringComparison.Ordinal) + tag.Length);

        return null;
    }

    internal static double DegToRad(double deg)
    {
        return deg * Math.PI / 180.0f;
    }

    internal static SKColor GimmickTypeToColor(GimmickType type)
    {
        switch (type)
        {
            case GimmickType.NoGimmick:
                return SKColors.Transparent;
            case GimmickType.BpmChange:
                return new SKColor(200, 0, 255, 255);
            case GimmickType.TimeSignatureChange:
                return new SKColor(200, 160, 255, 160);
            case GimmickType.HiSpeedChange:
                return new SKColor(200, 0, 255, 0);
            case GimmickType.ReverseStart:
            case GimmickType.ReverseMiddle:
            case GimmickType.ReverseEnd:
                return new SKColor(200, 255, 255, 0);
            case GimmickType.StopStart:
            case GimmickType.StopEnd:
                return new SKColor(200, 255, 0, 0);
            default:
                return SKColors.Transparent;
        }
    }

    internal static SKColor NoteTypeToColor(NoteType type)
    {
        switch (type)
        {
            case NoteType.TouchNoBonus:
            case NoteType.TouchBonus:
            case NoteType.TouchBonusFlair:
                return SKColors.Fuchsia;
            case NoteType.SnapRedNoBonus:
            case NoteType.SnapRedBonusFlair:
                return SKColors.Red;
            case NoteType.SnapBlueNoBonus:
            case NoteType.SnapBlueBonusFlair:
                return SKColors.Aqua;
            case NoteType.SlideOrangeNoBonus:
            case NoteType.SlideOrangeBonus:
            case NoteType.SlideOrangeBonusFlair:
                return new SKColor(255, 128, 0);
            case NoteType.SlideGreenNoBonus:
            case NoteType.SlideGreenBonus:
            case NoteType.SlideGreenBonusFlair:
                return SKColors.LimeGreen;
            case NoteType.HoldStartNoBonus:
            case NoteType.HoldJoint:
            case NoteType.HoldEnd:
            case NoteType.HoldStartBonusFlair:
                return SKColors.Yellow;
            case NoteType.Chain:
            case NoteType.ChainBonusFlair:
                return new SKColor(204, 190, 45);
            default:
                return SKColors.Transparent;
        }
    }

    internal static int FindGcd(int a, int b)
    {
        if (b == 0)
            return a;
        return FindGcd(b, a & b);
    }

    internal static string ToLabel(this NoteType type)
    {
        switch (type)
        {
            case NoteType.TouchNoBonus:
                return "Touch";
            case NoteType.TouchBonus:
                return "Touch [Bonus]";
            case NoteType.SnapRedNoBonus:
                return "Snap (R)";
            case NoteType.SnapBlueNoBonus:
                return "Snap (B)";
            case NoteType.SlideOrangeNoBonus:
                return "Slide (O)";
            case NoteType.SlideOrangeBonus:
                return "Slide (O) [Bonus]";
            case NoteType.SlideGreenNoBonus:
                return "Slide (G)";
            case NoteType.SlideGreenBonus:
                return "Slide (G) [Bonus]";
            case NoteType.HoldStartNoBonus:
                return "Hold Start";
            case NoteType.HoldJoint:
                return "Hold Joint";
            case NoteType.HoldEnd:
                return "Hold End";
            case NoteType.MaskAdd:
                return "Mask Add";
            case NoteType.MaskRemove:
                return "Mask Remove";
            case NoteType.EndOfChart:
                return "End of Chart";
            case NoteType.Chain:
                return "Chain";
            case NoteType.TouchBonusFlair:
                return "Touch [R Note]";
            case NoteType.SnapRedBonusFlair:
                return "Snap (R) [R Note]";
            case NoteType.SnapBlueBonusFlair:
                return "Snap (B) [R Note]";
            case NoteType.SlideOrangeBonusFlair:
                return "Slide (O) [R Note]";
            case NoteType.SlideGreenBonusFlair:
                return "Slide (G) [R Note]";
            case NoteType.HoldStartBonusFlair:
                return "Hold Start [R Note]";
            case NoteType.ChainBonusFlair:
                return "Chain [R Note]";
            default:
                return "Undefined Note Type";
        }
    }

    internal static string ToLabel(this GimmickType type)
    {
        switch (type)
        {
            case GimmickType.BpmChange:
                return "BPM Change";
            case GimmickType.TimeSignatureChange:
                return "Time Signature Change";
            case GimmickType.HiSpeedChange:
                return "Hi-Speed Change";
            case GimmickType.ReverseStart:
                return "Reverse Start";
            case GimmickType.ReverseMiddle:
                return "Reverse Middle";
            case GimmickType.ReverseEnd:
                return "Reverse Stop";
            case GimmickType.StopStart:
                return "Stop Start";
            case GimmickType.StopEnd:
                return "Stop End";
            default:
                return "Undefined Gimmick";
        }
    }

    private static bool HasDecimal(double num)
    {
        return Math.Abs(Math.Ceiling(num) - Math.Floor(num)) < 0.0001;
    }

    internal static Tuple<int, int> GetQuantization(int val, int min)
    {
        var numerator = val;
        var denominator = 1920;

        var gcd = GetGcd(numerator, denominator);

        numerator /= gcd;
        denominator /= gcd;

        while (denominator < min)
        {
            numerator *= 2;
            denominator *= 2;
        }

        return Tuple.Create(numerator, denominator);
    }

    private static int GetGcd(int a, int b)
    {
        while (true)
        {
            if (b == 0) return a;
            var a1 = a;
            a = b;
            b = a1 % b;
        }
    }

    internal static float GetDist(Point a, Point b)
    {
        return (float) Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    }

    private static string KeyIntToString(int key)
    {
        // Gets the key name from the keycode
        var val = ((Key) key).ToString();
        // Converts digit keycode into number instead of key name
        if (key is >= 34 and <= 43) val = $"{key - 34}";
        return val;
    }

    internal static void AppendHotkey(this Button button, int key)
    {
        button.Content += $" ({KeyIntToString(key)})";
    }

    internal static SKPaint CreateStrokeBrush(SKColor color, float strokeWidth)
    {
        return new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth
        };
    }

    internal static SKPaint CreateFillBrush(SKColor color, bool antialiased = true)
    {
        return new SKPaint
        {
            IsAntialias = antialiased,
            Color = color,
            Style = SKPaintStyle.Fill
        };
    }
}