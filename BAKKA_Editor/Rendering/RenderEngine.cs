using SkiaSharp;
using System;
using System.Drawing;
using System.Linq;
using BAKKA_Editor.Enums;

namespace BAKKA_Editor.Rendering;

internal class RenderEngine
{
    private SKCanvas canvas;
    private Brushes brushes { get; set; }

    public RenderEngine(UserSettings userSettings)
    {
        brushes = new Brushes(userSettings);
    }

    public SizeF PanelSize { get; private set; }
    public SKRect DrawRect { get; private set; }
    public PointF CanvasTopCorner { get; private set; }
    public PointF CanvasCenterPoint { get; private set; }
    public float CanvasRadius { get; private set; }

    public float CurrentMeasure { get; set; }
    public float ScaledCurrentMeasure { get; private set; }
    public float UserHiSpeed { get; set; } = 1.5f;
    public float BeatDivision { get; set; } = 2;
    public int GuideLineSelection { get; set; } = 0;
    public bool ShowHiSpeed { get; set; } = true;
    public float ArrowMovementOffset { get; set; } = 0;
    public float VisibleMeasures { get; set; } = 1f;
    
    // ================ Setup & Updates ================
    public void SetCanvas(SKCanvas canvas)
    {
        this.canvas = canvas;
    }

    public void UpdateHiSpeed(Chart chart, float hiSpeed)
    {
        var clampedHiSpeed = hiSpeed == 0 ? 0.01f : hiSpeed;
        var visibleTime = (73.0f - (clampedHiSpeed - 1.5f) * 10.0f) / 60.0f * 1000.0f;

        UserHiSpeed = clampedHiSpeed;
        VisibleMeasures = chart.GetMeasureDecimalFromTime(visibleTime);
    }

    public void UpdateScaledCurrentMeasure(Chart chart)
    {
        ScaledCurrentMeasure = chart.GetScaledMeasurePosition(CurrentMeasure);
    }

    public void UpdateCanvasSize(SizeF size)
    {
        PanelSize = size;
        var marginWidth = PanelSize.Width * 16.0f / 600.0f;
        CanvasTopCorner = new PointF(marginWidth, marginWidth);
        DrawRect = new SKRect(
            CanvasTopCorner.X,
            CanvasTopCorner.Y,
            PanelSize.Width - marginWidth * 2,
            PanelSize.Height - marginWidth * 2);
        CanvasRadius = DrawRect.Width / 2.0f;
        CanvasCenterPoint = new PointF(CanvasTopCorner.X + CanvasRadius, CanvasTopCorner.Y + CanvasRadius);
        brushes.UpdateBrushStrokeWidth(PanelSize.Width);
    }

    // ================ Helpers ================
    /// <summary>
    /// Returns the visual scale of an object from it's position
    /// </summary>
    /// <param name="position">Position in MeasureDecimals</param>
    /// <returns></returns>
    private float GetNoteScale(Chart chart, float position)
    {
        float noteScale;

        // get scaled note and cursor position if ShowHiSpeed is true,
        // otherwise default to original positions.
        var scaledPosition = ShowHiSpeed ? chart.GetScaledMeasurePosition(position) : position;
        var scaledCurrentMeasure = ShowHiSpeed ? ScaledCurrentMeasure : CurrentMeasure;
        var visionEndMeasure = scaledCurrentMeasure + VisibleMeasures;

        var latestHiSpeedChange = chart.Gimmicks.LastOrDefault(x =>
            x.GimmickType == GimmickType.HiSpeedChange
            && CurrentMeasure >= x.Measure) ?? new Gimmick { HiSpeed = 1.0 };

        if (latestHiSpeedChange.HiSpeed < 0)
        {
            // reverse
            noteScale = (scaledPosition - scaledCurrentMeasure) / (visionEndMeasure - scaledCurrentMeasure);
        }
        else
        {
            // normal
            noteScale = 1 - (scaledPosition - scaledCurrentMeasure) / (visionEndMeasure - scaledCurrentMeasure);
        }

        // cool scale math. it's just a curve for "perspective"
        // to not make the note scaling linear. I'm not smart enough to actually
        // describe any of this in detail. Look at it on desmos if you want to.
        // https://desmos.com/calculator/o07na4lswa
        noteScale = 0.001f + MathF.Pow(noteScale, 3.0f) - 0.501f * MathF.Pow(noteScale, 2) + 0.5f * noteScale;
        return noteScale;
    }

    /// <summary>
    /// Returns an SkArcInfo struct describing
    /// </summary>
    /// <param name="chart"></param>
    /// <param name="position"></param>
    /// <param name="scale"></param>
    /// <returns></returns>
    private SkArcInfo GetRect(Chart chart, float position, float scale = 1)
    {
        SkArcInfo info = new();
        info.NoteScale = GetNoteScale(chart, position);
        var scaledRectSize = DrawRect.Width * info.NoteScale;
        var scaledRadius = scaledRectSize / 2.0f;
        info.Rect = SKRect.Create(
            CanvasCenterPoint.X - scaledRadius * scale,
            CanvasCenterPoint.Y - scaledRadius * scale,
            scaledRectSize * scale,
            scaledRectSize * scale);
        return info;
    }

    private SkArcInfo GetArc(Chart chart, Note note, float scale = 1)
    {
        var arc = GetRect(chart, note.Measure, scale);
        arc.StartAngle = -note.Position * 6;
        arc.ArcAngle = -note.Size * 6;

        if (note.Size == 60)
        {
            // avoid skia bug. arcs cannot have sweep angles of 360deg.
            // check for this elsewhere and draw ovals instead maybe?
            arc.ArcAngle = -359.9999f;
        }
        else
        {
            // make note smaller by 1 unit on
            // each side for game accuracy
            arc.StartAngle -= 6;
            arc.ArcAngle += 12;
        }

        return arc;
    }

    /// <summary>
    /// Returns an SKPoint on an arc described by the centerpoint (x,y), radius and angle.
    /// </summary>
    /// <param name="x">X coordinate of centerpoint</param>
    /// <param name="y">Y coordinate of centerpoint</param>
    /// <param name="radius">Radius of the arc</param>
    /// <param name="angle">Angle on the arc in degrees</param>
    /// <returns></returns>
    private SKPoint GetPointOnArc(float x, float y, float radius, float angle)
    {
        var point = new SKPoint(
            (float)(radius * Math.Cos(Utils.DegToRad(angle)) + x),
            (float)(radius * Math.Sin(Utils.DegToRad(angle)) + y));

        return point;
    }

    private void FillPie(SKPaint paint, SKRect rect, float startAngle, float sweepAngle)
    {
        canvas.DrawArc(rect, -startAngle, -sweepAngle, true, paint);
    }

    // ================ Rendering ================
    public void DrawBackground(bool isDarkMode)
    {
        canvas.Clear(Brushes.GetBackgroundColor(isDarkMode));
    }

    public void DrawMasks(Chart chart, bool darkMode)
    {
        // TODO: i undid this thing because it was so slow
        // but it might not be drawing masks right
        return;
        // var masks = chart.Notes.Where(x => x.Measure <= CurrentMeasure && x.IsMask).ToList();
        var notes = chart.Notes;
        for (var i = 0; i < notes.Count; i++)
        {
            var mask = notes[i];
            if (!mask.IsMask || mask.Measure > CurrentMeasure)
                continue;

            switch (mask.NoteType)
            {
                case NoteType.MaskAdd:
                {
                    var shouldDraw = true;
                    for (var j = 0; j < chart.Notes.Count; j++)
                    {
                        var rem = notes[j];
                        if (rem.NoteType == NoteType.MaskRemove && rem.Position == mask.Position &&
                            rem.Size == mask.Size)
                            if (rem.Measure >= mask.Measure)
                            {
                                shouldDraw = false;
                                break;
                            }
                    }

                    if (shouldDraw)
                        FillPie(brushes.MaskFill, DrawRect, mask.Position * 6.0f, mask.Size * 6.0f);
                    break;
                }
                // Explicitly draw MaskRemove for edge cases
                case NoteType.MaskRemove:
                    FillPie(brushes.GetBackgroundFill(darkMode), DrawRect, mask.Position * 6.0f, mask.Size * 6.0f);
                    break;
            }
        }
    }

    public void DrawDegreeCircle()
    {
        canvas.DrawOval(DrawRect, brushes.DegreeCircleMediumPen);

        for (int i = 0; i < 360; i += 6)
        {
            var startPoint = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, CanvasRadius, i);
            var tickLength = PanelSize.Width / 28.5f;
            var innerRadius = CanvasRadius - tickLength;
            SKPaint currentPen;

            if (i % 90 == 0)
            {
                innerRadius = CanvasRadius - tickLength * 3.5f;
                currentPen = brushes.DegreeCircleMajorPen;
            }
            else if (i % 60 == 0)
            {
                innerRadius = CanvasRadius - tickLength * 2.5f;
                currentPen = brushes.DegreeCircleMediumPen;
            }
            else
            {
                currentPen = brushes.DegreeCircleMinorPen;
            }

            var endPoint = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, innerRadius, i);

            canvas.DrawLine(startPoint, endPoint, currentPen);
        }
    }

    public void DrawCircleDividers(Chart chart)
    {
        // Measures
        var measureStartValue = (float)Math.Ceiling(CurrentMeasure);
        var endValue = ScaledCurrentMeasure + VisibleMeasures;

        for (var i = measureStartValue; chart.GetScaledMeasurePosition(i) < endValue; i++)
        {
            var measureArc = GetRect(chart, i);
            if (measureArc.Rect.Width >= 1)
                canvas.DrawOval(measureArc.Rect, brushes.MeasurePen);
        }

        // Beats
        if (BeatDivision > 1)
        {
            var beatStartValue = MathF.Floor(CurrentMeasure / BeatDivision) * BeatDivision;

            // reuse endValue because it's the same as above
            for (var i = beatStartValue; chart.GetScaledMeasurePosition(i) < endValue; i += 1 / BeatDivision)
            {
                var beatArc = GetRect(chart, i);
                if (beatArc.Rect.Width >= 1 && i % 1 > 0.0001)
                    canvas.DrawOval(beatArc.Rect, brushes.BeatPen);
            }
        }
    }

    public void DrawGuideLines()
    {
        // 0 - offset   0 - interval 00
        // A - offset   0 - interval 06
        // B - offset +06 - interval 12
        // C - offset   0 - interval 18
        // D - offset +06 - interval 24
        // E - offset   0 - interval 30
        // F - offset +30 - interval 60
        // G - offset   0 - interval 90

        float offset;
        float interval;

        switch (GuideLineSelection)
        {
            default:
                offset = 0;
                interval = 0;
                break;

            case 1:
                offset = 0;
                interval = 6;
                break;

            case 2:
                offset = 6;
                interval = 12;
                break;

            case 3:
                offset = 0;
                interval = 18;
                break;

            case 4:
                offset = 6;
                interval = 24;
                break;

            case 5:
                offset = 0;
                interval = 30;
                break;

            case 6:
                offset = 30;
                interval = 60;
                break;

            case 7:
                offset = 0;
                interval = 90;
                break;
        }

        if (interval > 0)
        {
            for (var i = 0 + offset; i < 360 + offset; i += interval)
            {
                var tickLength = PanelSize.Width * 110.0f / 285.0f;
                var innerRadius = CanvasRadius - tickLength;

                var startPoint = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, CanvasRadius, i);
                var endPoint = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, innerRadius, i);

                canvas.DrawLine(startPoint, endPoint, brushes.GetGuidelinePen(startPoint, endPoint));
            }
        }
    }

    public void DrawNotes(Chart chart, bool highlightSelectedNote, int selectedNoteIndex, float noteScaleMultiplier)
    {
        var visibleNotes = chart.Notes.Where(x =>
            x.Measure >= CurrentMeasure
            && chart.GetScaledMeasurePosition(x.Measure) <= ScaledCurrentMeasure + VisibleMeasures);

        foreach (var note in visibleNotes)
        {
            var test = chart.GetScaledMeasurePosition(note.Measure);
            var info = GetArc(chart, note);

            if (info.Rect.Width >= 1)
            {
                if (note.IsBonus)
                    canvas.DrawArc(info.Rect, info.StartAngle, info.ArcAngle, false, brushes.GetBonusPen(info.NoteScale * noteScaleMultiplier));

                if (note.IsFlair)
                    canvas.DrawArc(info.Rect, info.StartAngle, info.ArcAngle, false, brushes.GetFlairPen(info.NoteScale * noteScaleMultiplier));

                if (note.Size != 60)
                    DrawEndCaps(info.Rect, info.StartAngle, info.ArcAngle, info.NoteScale * noteScaleMultiplier);

                canvas.DrawArc(info.Rect, info.StartAngle, info.ArcAngle, false, brushes.GetNotePen(note, info.NoteScale * noteScaleMultiplier));

                if (highlightSelectedNote && selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                    canvas.DrawArc(info.Rect, info.StartAngle, info.ArcAngle, false, brushes.GetHighlightPen(info.NoteScale * noteScaleMultiplier, false));
            }
        }
    }

    public void DrawEndCaps(SKRect rect, float start, float length, float noteScale)
    {

    }

    public void DrawGimmickNotes(Chart chart, bool showGimmicks, bool highlightSelectedNote, int selectedGimmickIndex)
    {
        if (!showGimmicks)
            return;

        var visibleGimmicks = chart.Gimmicks.Where(x =>
            x.Measure >= CurrentMeasure
            && chart.GetScaledMeasurePosition(x.Measure) <= CurrentMeasure + VisibleMeasures);

        foreach (var gimmick in visibleGimmicks)
        {
            var info = GetRect(chart, gimmick.Measure);
            if (info.Rect.Width >= 1)
            {
                canvas.DrawOval(info.Rect, brushes.GetGimmickPen(gimmick, 1));

                if (highlightSelectedNote && selectedGimmickIndex != -1 && gimmick == chart.Gimmicks[selectedGimmickIndex])
                    canvas.DrawOval(info.Rect, brushes.GetHighlightPen(1, false));
            }
        }
    }
}
