using BAKKA_Editor.Enums;
using BAKKA_Editor.ViewModels;
using SkiaSharp;

namespace BAKKA_Editor.Rendering;

internal class Brushes
{
    public UserSettings UserSettings;

    public Brushes(UserSettings userSettings)
    {
        UserSettings = userSettings;
    }

    // A Brush can be either a pen or a fill.
    // A Pen is a brush that creates lines of a color.
    // A Fill is a brush that creates areas of a color.

    private static readonly SKColor MeasurePenColor = SKColors.White;
    private const float MeasurePenStrokeWidth = 1.0f;

    private static readonly SKColor BeatPenColor = SKColors.White.WithAlpha(0x80);
    private const float BeatPenStrokeWidth = 0.5f;

    private static readonly SKColor DegreeCircleColor = SKColors.Black;
    private const float DegreeCircleMajorPenStrokeWidth = 7.0f;
    private const float DegreeCircleMediumPenStrokeWidth = 4.0f;
    private const float DegreeCircleMinorPenStrokeWidth = 2.0f;

    private static readonly SKColor MirrorAxisPenColor = SKColors.Cyan;
    private const float MirrorAxisPenStrokeWidth = 0.5f;

    private const float CursorPenStrokeWidth = 24.0f;

    private static readonly SKColor[] GuidelinePenColors =
    {
        SKColors.White.WithAlpha(0x20),
        SKColors.White.WithAlpha(0x00)
    };

    private const float GuidelinePenStrokeWidth = 1.0f;

    private static readonly SKColor BackgroundFillColorDark = new(68, 68, 68);
    private static readonly SKColor BackgroundFillColorLight = new(243, 243, 243);

    private static readonly SKColor MaskFillColor = SKColors.Black.WithAlpha(90);

    private const float NotePenStrokeWidth = 8.0f;

    private const float ArrowPenStrokeWidth = 2.0f;

    private const float GimmickPenStrokeWidth = 5.0f;

    private static readonly SKColor BonusPenColor = SKColors.LightSkyBlue.WithAlpha(0x60);
    private const float BonusPenStrokeWidth = 15.0f;

    private static readonly SKColor FlairPenColor = SKColors.Khaki.WithAlpha(0xCC);
    private const float FlairPenStrokeWidth = 18.0f;

    private static readonly SKColor HighlightPenColor = SKColors.LightPink.WithAlpha(0x80);
    private const float HighlightPenStrokeWidth = 20.0f;

    private static readonly SKColor LinkPenColor = SKColors.DeepSkyBlue.WithAlpha(0xDD);
    private const float LinkPenStrokeWidth = 3.0f;

    private static readonly SKColor EndcapPenColor = SKColors.DeepSkyBlue;
    private const float EndcapPenStrokeWidth = 8.0f;

    private const float HoldEndPenStrokeWidth = 4.0f;

    public SKPaint ArrowPen = new()
    {
        StrokeWidth = ArrowPenStrokeWidth,
        StrokeCap = SKStrokeCap.Round,
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };

    public SKPaint BackgroundBrush = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false
    };

    public SKPaint BeatPen = new()
    {
        Color = BeatPenColor,
        StrokeWidth = BeatPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint BonusPen = new()
    {
        Color = BonusPenColor,
        StrokeWidth = BonusPenStrokeWidth,
        StrokeCap = SKStrokeCap.Square,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint CursorPen = new()
    {
        StrokeWidth = CursorPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint DegreeCircleMajorPen = new()
    {
        Color = DegreeCircleColor,
        StrokeWidth = DegreeCircleMajorPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint DegreeCircleMediumPen = new()
    {
        Color = DegreeCircleColor,
        StrokeWidth = DegreeCircleMediumPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint DegreeCircleMinorPen = new()
    {
        Color = DegreeCircleColor,
        StrokeWidth = DegreeCircleMinorPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint EndCapPen = new()
    {
        Color = EndcapPenColor,
        StrokeWidth = EndcapPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint FlairPen = new()
    {
        Color = FlairPenColor,
        StrokeWidth = FlairPenStrokeWidth,
        StrokeCap = SKStrokeCap.Square,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint GimmickPen = new()
    {
        StrokeWidth = GimmickPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint GuideLinePen = new()
    {
        StrokeWidth = GuidelinePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint HighlightPen = new()
    {
        Color = HighlightPenColor,
        StrokeWidth = HighlightPenStrokeWidth,
        StrokeCap = SKStrokeCap.Square,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint HoldEndPen = new()
    {
        StrokeWidth = HoldEndPenStrokeWidth,
        StrokeCap = SKStrokeCap.Round,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint HoldFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false
    };

    public SKPaint LinkPen = new()
    {
        Color = LinkPenColor,
        StrokeWidth = LinkPenStrokeWidth,
        StrokeCap = SKStrokeCap.Square,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint MaskFill = new()
    {
        Color = MaskFillColor,
        Style = SKPaintStyle.Fill,
        IsAntialias = false
    };

    public SKPaint MeasurePen = new()
    {
        Color = MeasurePenColor,
        StrokeWidth = MeasurePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint MirrorAxisPen = new()
    {
        Color = MirrorAxisPenColor,
        StrokeWidth = MirrorAxisPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public SKPaint NotePen = new()
    {
        StrokeWidth = NotePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    private float strokeWidthMultiplier = 1.0183333f;

    public void UpdateBrushStrokeWidth(float panelSize)
    {
        // update window size multiplier
        strokeWidthMultiplier = panelSize / 600.0f;

        // update "static" pens here when window is resized
        MeasurePen.StrokeWidth = MeasurePenStrokeWidth * strokeWidthMultiplier;
        BeatPen.StrokeWidth = BeatPenStrokeWidth * strokeWidthMultiplier;
        DegreeCircleMajorPen.StrokeWidth = DegreeCircleMajorPenStrokeWidth * strokeWidthMultiplier;
        DegreeCircleMediumPen.StrokeWidth = DegreeCircleMediumPenStrokeWidth * strokeWidthMultiplier;
        DegreeCircleMinorPen.StrokeWidth = DegreeCircleMinorPenStrokeWidth * strokeWidthMultiplier;
        MirrorAxisPen.StrokeWidth = MirrorAxisPenStrokeWidth * strokeWidthMultiplier;
        CursorPen.StrokeWidth = CursorPenStrokeWidth * strokeWidthMultiplier;
    }

    public SKPaint GetCursorPen(NoteType noteType)
    {
        CursorPen.Color = NoteTypeToColor(noteType).WithAlpha(0x80);
        return CursorPen;
    }

    public SKPaint GetGuidelinePen(SKPoint startPoint, SKPoint endPoint)
    {
        var shader = SKShader.CreateLinearGradient(startPoint, endPoint, GuidelinePenColors, SKShaderTileMode.Clamp);
        GuideLinePen.Shader = shader;
        return GuideLinePen;
    }

    public SKPaint GetNotePen(Note note, float noteScaleMultiplier)
    {
        NotePen.Color = NoteTypeToColor(note.NoteType);
        NotePen.StrokeWidth = NotePenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;

        return NotePen;
    }

    public SKPaint GetArrowPen(Note note)
    {
        ArrowPen.Color = NoteTypeToColor(note.NoteType);
        return ArrowPen;
    }

    public SKPaint GetGimmickPen(Gimmick gimmick, float noteScaleMultiplier)
    {
        GimmickPen.Color = Utils.GimmickTypeToColor(gimmick.GimmickType);
        GimmickPen.StrokeWidth = GimmickPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
        return GimmickPen;
    }

    public SKPaint GetBonusPen(float noteScaleMultiplier)
    {
        BonusPen.StrokeWidth = BonusPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
        return BonusPen;
    }

    public SKPaint GetFlairPen(float noteScaleMultiplier)
    {
        FlairPen.StrokeWidth = FlairPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
        return FlairPen;
    }

    public SKPaint GetHighlightPen(float noteScaleMultiplier)
    {
        HighlightPen.StrokeWidth = HighlightPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
        return HighlightPen;
    }

    public SKPaint GetLinkPen(float noteScaleMultiplier)
    {
        LinkPen.StrokeWidth = LinkPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
        return LinkPen;
    }

    public SKPaint GetEndCapPen(float noteScaleMultiplier)
    {
        EndCapPen.StrokeWidth = EndcapPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
        return EndCapPen;
    }

    public SKPaint GetHoldEndPen(Note note, float noteScaleMultiplier)
    {
        HoldEndPen.Color = NoteTypeToColor(note.NoteType);
        HoldEndPen.StrokeWidth = HoldEndPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
        return HoldEndPen;
    }

    public void SetHoldFill(SKPoint center, float radius)
    {
        var gradientColor0 = SKColor.Parse(UserSettings.ColorSettings.ColorNoteHoldGradient0);
        var gradientColor1 = SKColor.Parse(UserSettings.ColorSettings.ColorNoteHoldGradient1);
        SKColor[] holdColors = {gradientColor0, gradientColor1};
        var shader = SKShader.CreateRadialGradient(center, radius, holdColors, SKShaderTileMode.Clamp);
        HoldFill.Shader = shader;
    }

    public SKPaint GetBackgroundFill(bool dark)
    {
        BackgroundBrush.Color = dark ? BackgroundFillColorDark : BackgroundFillColorLight;
        return BackgroundBrush;
    }

    public static SKColor GetBackgroundColor(bool dark)
    {
        return dark ? BackgroundFillColorDark : BackgroundFillColorLight;
    }

    private SKColor NoteTypeToColor(NoteType type)
    {
        if (UserSettings == null)
            return SKColors.Black;

        switch (type)
        {
            case NoteType.TouchNoBonus:
            case NoteType.TouchBonus:
            case NoteType.TouchBonusFlair:
                return SKColor.Parse(UserSettings.ColorSettings.ColorNoteTap);
            case NoteType.SnapRedNoBonus:
            case NoteType.SnapRedBonusFlair:
                return SKColor.Parse(UserSettings.ColorSettings.ColorNoteSnapFw);
            case NoteType.SnapBlueNoBonus:
            case NoteType.SnapBlueBonusFlair:
                return SKColor.Parse(UserSettings.ColorSettings.ColorNoteSnapBw);
            case NoteType.SlideOrangeNoBonus:
            case NoteType.SlideOrangeBonus:
            case NoteType.SlideOrangeBonusFlair:
                return SKColor.Parse(UserSettings.ColorSettings.ColorNoteSlideCw);
            case NoteType.SlideGreenNoBonus:
            case NoteType.SlideGreenBonus:
            case NoteType.SlideGreenBonusFlair:
                return SKColor.Parse(UserSettings.ColorSettings.ColorNoteSlideCcw);
            case NoteType.HoldStartNoBonus:
            case NoteType.HoldStartBonusFlair:
                return SKColor.Parse(UserSettings.ColorSettings.ColorNoteHoldStart);
            case NoteType.HoldJoint:
            case NoteType.HoldEnd:
                return SKColor.Parse(UserSettings.ColorSettings.ColorNoteHoldSegment);
            case NoteType.Chain:
            case NoteType.ChainBonusFlair:
                return SKColor.Parse(UserSettings.ColorSettings.ColorNoteChain);
            case NoteType.MaskAdd:
                return SKColors.Black.WithAlpha(0x80);
            case NoteType.MaskRemove:
                return SKColors.Gray.WithAlpha(0x80);
            default:
                return SKColors.Transparent;
        }
    }
}