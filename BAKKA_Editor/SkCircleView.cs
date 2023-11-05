using System;
using System.Drawing;
using System.Linq;
using BAKKA_Editor.Enums;
using BAKKA_Editor.Rendering;
using SkiaSharp;

namespace BAKKA_Editor;

internal struct SkArcInfo
{
    public float StartAngle;
    public float ArcLength;
    public SKRect Rect;
    public float NoteScale;
}

internal partial class SkCircleView
{
    private SKCanvas canvas;
    private Brushes Brushes;
    public int lastMousePos = -1;
    public bool Playing { get; set; } = false; // TODO: move out

    // Cursor
    public Cursor Cursor = new();


    public SkCircleView(UserSettings userSettings, SizeF size)
    {
        Brushes = new(userSettings);
        Update(size);
    }

    public SizeF PanelSize { get; private set; }
    public SKRect DrawRect { get; private set; }
    public PointF TopCorner { get; private set; }
    public PointF CenterPoint { get; private set; }
    public float Radius { get; private set; }
    /// <summary>
    /// The current measure at depth 0
    /// </summary>
    public float CurrentMeasure { get; set; }
    public uint BeatsPerMeasure { get; set; }
    public float Hispeed { get; set; } = 1.5f;
    public float BeatDivision { get; set; } = 2;
    public int GuideLineSelection { get; set; } = 0;
    public bool showHispeed { get; set; } = true;
    public bool renderNotesBeyondCircle { get; set; } = false; // temporary(?) - may be moved to settings since it's for the Settings UI, just wanted to put it here to put it in the code already.
    public float arrowMovementOffset;

    public void SetCanvas(SKCanvas canvas)
    {
        this.canvas = canvas;
    }

    public void Update(SizeF size)
    {
        PanelSize = size;
        var basePenWidth = PanelSize.Width * 4.0f / 600.0f;
        TopCorner = new PointF(basePenWidth * 4, basePenWidth * 4);
        DrawRect = new SKRect(
            TopCorner.X,
            TopCorner.Y,
            PanelSize.Width - basePenWidth * 8,
            PanelSize.Height - basePenWidth * 8);
        Radius = DrawRect.Width / 2.0f;
        CenterPoint = new PointF(TopCorner.X + Radius, TopCorner.Y + Radius);
        Brushes.UpdateBrushStrokeWidth(PanelSize.Width);
    }

    // lazy reduced allocation version
    private float GetTotalMeasureShowNotes2(Chart chart)
    {
        // Convert hispeed to frames
        var displayFrames = 73.0f - (Hispeed - 1.5f) * 10.0f;
        var tempTotalTime = displayFrames / 60.0f * 1000.0f;

        float currentTime = chart.GetTime(CurrentMeasure);

        var tempEndTime = currentTime + tempTotalTime;

        if (showHispeed)
        {
            var initialSpeed = chart.Gimmicks.LastOrDefault(x =>
                x.GimmickType == GimmickType.HiSpeedChange && x.Measure <= CurrentMeasure);

            if (initialSpeed == null)
            {
                initialSpeed = new Gimmick { HiSpeed = 1.0 };
            }

            float initialModifiedTime = tempTotalTime / (float)initialSpeed.HiSpeed;

            var gimmicksInTimeRange = chart.Gimmicks.Where(x =>
                x.Measure >= CurrentMeasure &&
                chart.GetTime(new BeatInfo(x.Measure)) <= currentTime &&
                x.GimmickType == GimmickType.HiSpeedChange).ToList();

            if (gimmicksInTimeRange.Count > 0)
            {
                for (var i = 0; i < gimmicksInTimeRange.Count; i++)
                {
                    float timeDiff;
                    float itemTime = chart.GetTime(gimmicksInTimeRange[i].BeatInfo);
                    float modifiedTime;

                    // hack: prevent divide by zero when hi-speed is 0
                    var clampedSpeed = (float) gimmicksInTimeRange[i].HiSpeed;
                    if (clampedSpeed == 0)
                        clampedSpeed = 0.01f;

                    if (itemTime <= tempTotalTime + currentTime)
                    {
                        if (i == 0)
                            itemTime = currentTime;

                        if (i != gimmicksInTimeRange.Count - 1)
                        {
                            var tempTestITimeDiff = currentTime + tempTotalTime - itemTime;
                            var tempTestIModifiedTime = tempTestITimeDiff / (float)gimmicksInTimeRange[i].HiSpeed;

                            if (currentTime + tempTotalTime - tempTestITimeDiff + tempTestIModifiedTime <
                                chart.GetTime(gimmicksInTimeRange[i + 1].BeatInfo))
                            {
                                timeDiff = currentTime + tempTotalTime - itemTime;
                                modifiedTime = timeDiff / clampedSpeed;
                            }
                            else
                            {
                                timeDiff = chart.GetTime(gimmicksInTimeRange[i + 1].BeatInfo) - itemTime;
                                modifiedTime = timeDiff / clampedSpeed;
                            }
                        }
                        else
                        {
                            timeDiff = currentTime + tempTotalTime - itemTime;
                            modifiedTime = timeDiff / clampedSpeed;
                        }

                        tempTotalTime = tempTotalTime - timeDiff + modifiedTime;
                    }
                }
            }
            else
            {
                tempTotalTime = initialModifiedTime;
            }

            tempEndTime = currentTime + tempTotalTime;
        }

        // Convert total time to total measure
        var endMeasure = chart.GetBeatFromMeasureDecimal(tempEndTime);
        var result = endMeasure - CurrentMeasure;

        // hack: make sure we don't get infinity
        // better to render nothing than to deadlock because we never finish rendering a frame
        if (float.IsInfinity(result))
        {
            return 0.001f;
        }

        return result;
    }

    private float GetNoteScaleFromMeasure(Chart chart, float objectTime)
    {
        // Scale from 0-1
        float objectTimeAsTime = chart.GetTime(new BeatInfo(objectTime));
        float currentTime = chart.GetTime(new BeatInfo(CurrentMeasure));
        float endTimeShowNotes = chart.GetTime(new BeatInfo(CurrentMeasure + GetTotalMeasureShowNotes2(chart)));
        float notescaleInit;
        var latestHispeedChange = chart.Gimmicks
            .LastOrDefault(x => x.GimmickType == GimmickType.HiSpeedChange && CurrentMeasure >= x.Measure);
        if (latestHispeedChange?.HiSpeed < 0.0)
            //Reverse
            notescaleInit = (objectTimeAsTime - currentTime) / (endTimeShowNotes - currentTime);
        else
            //Normal
            notescaleInit = 1 - (objectTimeAsTime - currentTime) / (endTimeShowNotes - currentTime);
        //Scale math
        notescaleInit = 0.001f + (float) Math.Pow(notescaleInit, 3.0f) -
            0.501f * (float) Math.Pow(notescaleInit, 2.0f) + 0.5f * notescaleInit;
        return notescaleInit;
    }

    private float GetNoteScaleFromMeasure2(Chart chart, float objectTime)
    {
        // Scale from 0-1
        float objectTimeAsTime = chart.GetTime(objectTime);
        float currentTime = chart.GetTime(CurrentMeasure);
        float endTimeShowNotes = chart.GetTime(CurrentMeasure + GetTotalMeasureShowNotes2(chart));

        float notescaleInit;
        var latestHispeedChange = chart.Gimmicks
            .LastOrDefault(x => x.GimmickType == GimmickType.HiSpeedChange && CurrentMeasure >= x.Measure);
        if (latestHispeedChange?.HiSpeed < 0.0)
            //Reverse
            notescaleInit = (objectTimeAsTime - currentTime) / (endTimeShowNotes - currentTime);
        else
            //Normal
            notescaleInit = 1 - (objectTimeAsTime - currentTime) / (endTimeShowNotes - currentTime);
        //Scale math
        notescaleInit = 0.001f + (float) Math.Pow(notescaleInit, 3.0f) -
            0.501f * (float) Math.Pow(notescaleInit, 2.0f) + 0.5f * notescaleInit;

        return notescaleInit;
    }

    private SkArcInfo GetScaledRect(Chart chart, float objectTime, float scale = 1)
    {
        SkArcInfo info = new();
        info.NoteScale = GetNoteScaleFromMeasure2(chart, objectTime);
        var scaledRectSize = DrawRect.Width * info.NoteScale;
        var scaledRadius = scaledRectSize / 2.0f;
        info.Rect = SKRect.Create(
            CenterPoint.X - scaledRadius * scale,
            CenterPoint.Y - scaledRadius * scale,
            scaledRectSize * scale,
            scaledRectSize * scale);
        return info;
    }

    private SkArcInfo GetSkArcInfo(Chart chart, Note note, float scale = 1)
    {
        var info = GetScaledRect(chart, note.Measure, scale);
        info.StartAngle = -note.Position * 6;
        info.ArcLength = -note.Size * 6;

        if (note.Size == 60)
        {
            // hack hack hack HACK
            // skia's arcs cannot have a sweep angle of 360deg or something :(
            info.ArcLength = -359.999f;
        }
        else
        {
            info.StartAngle -= 6;
            info.ArcLength += 12;
        }

        return info;
    }

    private SKPoint GetPointOnArc(float x, float y, float radius, float angle)
    {
        var point = new SKPoint(
            (float)(radius * Math.Cos(Utils.DegToRad(angle)) + x),
            (float)(radius * Math.Sin(Utils.DegToRad(angle)) + y));

        return point;
    }

    /// <summary>
    ///     Creates the background brush with the passed in color if it's null.
    /// </summary>
    /// <param name="dark">Is dark mode enabled?</param>
    public void DrawBackground(bool dark)
    {
        canvas.Clear(Brushes.GetBackgroundColor(dark));
    }

    /// <summary>
    /// Calculates a position around the ring given mouse coordinates relative to the center of the circle.
    /// </summary>
    /// <param name="xCen">Horizontal pixel location of the mouse relative to the center of the circle. 
    /// Negative numbers are to the left of the center of the circle.</param>
    /// <param name="yCen">Vertical pixel location of the mouse relative to the center of the circle.
    /// Negative numbers are below the center of the circle.</param>
    /// <returns>A position around the circle from 0 to 59.</returns>
    public int CalculateTheta(float xCen, float yCen)
    {
        var thetaCalc = (float) (Math.Atan2(yCen, xCen) * 180.0f / Math.PI);
        if (thetaCalc < 0)
            thetaCalc += 360.0f;
        var theta = (int) (thetaCalc / 6.0f);
        return theta;
    }

    /*private void FillPie(SKPaint paint, SKRect rect, float startAngle, float sweepAngle)
    {
        var path = new SKPath();
        path.AddArc(rect, startAngle, sweepAngle);
        canvas.DrawPath(path, paint);
    }*/
    private void FillPie(SKPaint paint, SKRect rect, float startAngle, float sweepAngle)
    {
        canvas.DrawArc(rect, -startAngle, -sweepAngle, true, paint);
    }

    public void DrawMasks(Chart chart, bool darkMode)
    {
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
                        FillPie(Brushes.MaskFill, DrawRect, mask.Position * 6.0f, mask.Size * 6.0f);
                    break;
                }
                // Explicitly draw MaskRemove for edge cases
                case NoteType.MaskRemove:
                    FillPie(Brushes.GetBackgroundFill(darkMode), DrawRect, mask.Position * 6.0f, mask.Size * 6.0f);
                    break;
            }
        }
    }

    public void DrawCircleWithDividers(Chart chart)
    {
        // Draw measure circles
        var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        for (var measure = (float)Math.Ceiling(CurrentMeasure - 1);
             measure - CurrentMeasure < totalMeasureShowNotes;
             measure += 1.0f)
        {
            var measureArcInfo = GetScaledRect(chart, measure);
            if (measureArcInfo.Rect.Width >= 1 && GetObjectVisibility(measure))
                canvas.DrawOval(measureArcInfo.Rect, Brushes.MeasurePen);
        }

        // Draw beat divider circle
        if (BeatDivision > 1)
        {
            for (var beat = (float)Math.Floor(CurrentMeasure / BeatDivision) * BeatDivision;
             beat - CurrentMeasure < totalMeasureShowNotes;
             beat += 1 / BeatDivision)
            {
                var beatArcInfo = GetScaledRect(chart, beat);
                if (beatArcInfo.Rect.Width >= 1 && beat % 1 > 0.0001 && GetObjectVisibility(beat))
                    canvas.DrawOval(beatArcInfo.Rect, Brushes.BeatPen);
            }
        }
    }
    
    public void DrawDegreeCircle()
    {
        // Draw base circle
        canvas.DrawOval(DrawRect, Brushes.DegreeCircleMediumPen);

        for (var i = 0; i < 360; i += 6)
        {
            var startPoint = new SKPoint(
                (float) (Radius * Math.Cos(Utils.DegToRad(i)) + CenterPoint.X),
                (float) (Radius * Math.Sin(Utils.DegToRad(i)) + CenterPoint.Y));
            var tickLength = PanelSize.Width * 10.0f / 285.0f;
            var innerRad = Radius - tickLength;
            SKPaint activePen;
            if (i % 90 == 0)
            {
                innerRad = Radius - tickLength * 3.5f;
                activePen = Brushes.DegreeCircleMajorPen;
            }
            else if (i % 30 == 0)
            {
                innerRad = Radius - tickLength * 2.5f;
                activePen = Brushes.DegreeCircleMediumPen;
            }
            else
            {
                activePen = Brushes.DegreeCircleMinorPen;
            }

            var endPoint = new SKPoint(
                (float) (innerRad * Math.Cos(Utils.DegToRad(i)) + CenterPoint.X),
                (float) (innerRad * Math.Sin(Utils.DegToRad(i)) + CenterPoint.Y));

            canvas.DrawLine(startPoint, endPoint, activePen);
        }
    }

    public void DrawGuideLines()
    {
        // none - offset   0 - interval 00
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
                var innerRad = Radius - tickLength;

                var startPoint = GetPointOnArc(CenterPoint.X, CenterPoint.Y, Radius, i);
                var endPoint = GetPointOnArc(CenterPoint.X, CenterPoint.Y, innerRad, i);

                canvas.DrawLine(startPoint, endPoint, Brushes.GetGuidelinePen(startPoint, endPoint));
            }
        }
    }

    public void DrawGimmicksWithVisibilityCheck(Chart chart, bool showGimmicks, int selectedGimmickIndex, float noteScaleMultiplier)
    {
        if (showGimmicks)
        {
            var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
            var drawGimmicks = chart.Gimmicks.Where(
                x => x.Measure >= CurrentMeasure - 1
                     && x.Measure <= CurrentMeasure + totalMeasureShowNotes);

            foreach (var gimmick in drawGimmicks)
            {
                var info = GetScaledRect(chart, gimmick.Measure);

                if (info.Rect.Width >= 1 && GetObjectVisibility(gimmick.Measure))
                    canvas.DrawOval(info.Rect, Brushes.GetGimmickPen(gimmick, info.NoteScale * noteScaleMultiplier));
            }
        }
    }

    /*public void DrawHolds(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
    {
        var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        var currentInfo = GetScaledRect(chart, CurrentMeasure);
        var endInfo = GetScaledRect(chart, CurrentMeasure + totalMeasureShowNotes);

        // First, draw holes that start before the viewpoint and have nodes that end after
        var holdNotes = chart.Notes.Where(
            x => x.Measure < CurrentMeasure
                 && x.NextNote != null
                 && x.NextNote.Measure > CurrentMeasure + totalMeasureShowNotes
                 && x.IsHold).ToList();
        foreach (var note in holdNotes)
        {
            var info = GetSkArcInfo(chart, note);
            var nextInfo = GetSkArcInfo(chart, note.NextNote);

            var ratio = (currentInfo.Rect.Width - nextInfo.Rect.Width) / (info.Rect.Width - nextInfo.Rect.Width);
            var startNoteAngle = nextInfo.StartAngle;
            var endNoteAngle = info.StartAngle;
            if (nextInfo.StartAngle > info.StartAngle && Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180)
                startNoteAngle -= 360;
            else if (info.StartAngle > nextInfo.StartAngle && Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180)
                endNoteAngle -= 360;
            var startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
            var endAngle = ratio * (endNoteAngle - info.ArcLength - (startNoteAngle - nextInfo.ArcLength)) +
                           (startNoteAngle - nextInfo.ArcLength);
            var arcLength = startAngle - endAngle;

            var ratio2 = (endInfo.Rect.Width - nextInfo.Rect.Width) / (info.Rect.Width - nextInfo.Rect.Width);
            var startNoteAngle2 = nextInfo.StartAngle;
            var endNoteAngle2 = info.StartAngle;
            if (nextInfo.StartAngle > info.StartAngle && Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180)
                startNoteAngle2 -= 360;
            else if (info.StartAngle > nextInfo.StartAngle && Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180)
                endNoteAngle2 -= 360;
            var startAngle2 = ratio2 * (endNoteAngle2 - startNoteAngle2) + startNoteAngle2;
            var endAngle2 = ratio2 * (endNoteAngle2 - info.ArcLength - (startNoteAngle2 - nextInfo.ArcLength)) +
                            (startNoteAngle2 - nextInfo.ArcLength);
            var arcLength2 = startAngle2 - endAngle2;

            var p = new SKPath();
            p.ArcTo(currentInfo.Rect, startAngle, arcLength, true);
            p.ArcTo(endInfo.Rect, startAngle2 + arcLength2, -arcLength2, false);
            canvas.DrawPath(p, Brushes.GetHoldFill(new SKPoint(CenterPoint.X, CenterPoint.Y), Radius));
        }

        // Second, draw all the notes on-screen
        holdNotes = chart.Notes.Where(
            x => x.Measure >= CurrentMeasure
                 && x.Measure <= CurrentMeasure + totalMeasureShowNotes
                 && x.IsHold).ToList();
        foreach (var note in holdNotes)
        {
            var info = GetSkArcInfo(chart, note);

            // If the previous note is off-screen, this case handles that
            if (note.PrevNote?.Measure < CurrentMeasure)
            {
                var prevInfo = GetSkArcInfo(chart, (Note) note.PrevNote);
                var ratio = (currentInfo.Rect.Width - info.Rect.Width) / (prevInfo.Rect.Width - info.Rect.Width);
                var startNoteAngle = info.StartAngle;
                var endNoteAngle = prevInfo.StartAngle;
                if (info.StartAngle > prevInfo.StartAngle && Math.Abs(info.StartAngle - prevInfo.StartAngle) > 180)
                    startNoteAngle -= 360;
                else if (prevInfo.StartAngle > info.StartAngle && Math.Abs(info.StartAngle - prevInfo.StartAngle) > 180)
                    endNoteAngle -= 360;
                var startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
                var endAngle = ratio * (endNoteAngle - prevInfo.ArcLength - (startNoteAngle - info.ArcLength)) +
                               (startNoteAngle - info.ArcLength);
                var arcLength = startAngle - endAngle;

                var p = new SKPath();
                p.ArcTo(info.Rect, info.StartAngle, info.ArcLength, true);
                p.ArcTo(currentInfo.Rect, startAngle + arcLength, -arcLength, false);
                canvas.DrawPath(p, Brushes.GetHoldFill(new SKPoint(CenterPoint.X, CenterPoint.Y), Radius));
            }

            // If the next note is on-screen, this case handles that
            if (note.NextNote != null && note.NextNote.Measure <= CurrentMeasure + totalMeasureShowNotes)
            {
                var nextInfo = GetSkArcInfo(chart, note.NextNote);
                var p = new SKPath();
                p.ArcTo(info.Rect, info.StartAngle, info.ArcLength, true);
                p.ArcTo(nextInfo.Rect, nextInfo.StartAngle + nextInfo.ArcLength, -nextInfo.ArcLength, false);
                canvas.DrawPath(p, Brushes.GetHoldFill(new SKPoint(CenterPoint.X, CenterPoint.Y), Radius));
            }

            // If the next note is off-screen, this case handles that
            if (note.NextNote != null && note.NextNote.Measure > CurrentMeasure + totalMeasureShowNotes)
            {
                var nextInfo = GetSkArcInfo(chart, note.NextNote);
                var ratio = (endInfo.Rect.Width - nextInfo.Rect.Width) / (info.Rect.Width - nextInfo.Rect.Width);
                var startNoteAngle = nextInfo.StartAngle;
                var endNoteAngle = info.StartAngle;
                if (nextInfo.StartAngle > info.StartAngle && Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180)
                    startNoteAngle -= 360;
                else if (info.StartAngle > nextInfo.StartAngle && Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180)
                    endNoteAngle -= 360;
                var startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
                var endAngle = ratio * (endNoteAngle - info.ArcLength - (startNoteAngle - nextInfo.ArcLength)) +
                               (startNoteAngle - nextInfo.ArcLength);
                var arcLength = startAngle - endAngle;

                var p = new SKPath();
                p.ArcTo(endInfo.Rect, startAngle, arcLength, true);
                p.ArcTo(info.Rect, info.StartAngle + info.ArcLength, -info.ArcLength, false);
                canvas.DrawPath(p, Brushes.GetHoldFill(new SKPoint(CenterPoint.X, CenterPoint.Y), Radius));
            }

            // Draw note
            if (info.Rect.Width >= 1)
            {
                canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false, Brushes.GetNotePen(note, info.NoteScale * NoteScaleMultiplier));

                // Draw flair
                if (note.IsFlair)
                    canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, Brushes.GetFlairPen(info.NoteScale * NoteScaleMultiplier));

                // Plot highlighted
                if (highlightSelectedNote)
                    if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, Brushes.GetHighlightPen(info.NoteScale * NoteScaleMultiplier, false));
            }
        }
    }*/

    public void DrawHoldsSingle(Chart chart, bool highlightSelectedNote, int selectedNoteIndex, float noteScaleMultiplier)
    {
        var currentInfo = GetScaledRect(chart, CurrentMeasure);
        var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        var endInfo = GetScaledRect(chart, CurrentMeasure + totalMeasureShowNotes);
        var endMeasure = CurrentMeasure + totalMeasureShowNotes;

        // Draw all the notes on-screen
        var holdNotes = chart.Notes
            .Where(x => x.IsHold && (
                (x.Measure < CurrentMeasure && x.NextReferencedNote?.Measure > endMeasure) ||
                (x.Measure >= CurrentMeasure && x.Measure <= endMeasure)));
        // .ToList();
        foreach (var note in holdNotes)
        {
            var info = GetSkArcInfo(chart, note);

            // If the previous note is off-screen, this case handles that
            if (note.PrevReferencedNote?.Measure < CurrentMeasure)
            {
                var prevInfo = GetSkArcInfo(chart, (Note) note.PrevReferencedNote);
                var ratio = (currentInfo.Rect.Width - info.Rect.Width) / (prevInfo.Rect.Width - info.Rect.Width);
                var startNoteAngle = info.StartAngle;
                var endNoteAngle = prevInfo.StartAngle;
                if (info.StartAngle > prevInfo.StartAngle && Math.Abs(info.StartAngle - prevInfo.StartAngle) > 180)
                    startNoteAngle -= 360;
                else if (prevInfo.StartAngle > info.StartAngle && Math.Abs(info.StartAngle - prevInfo.StartAngle) > 180)
                    endNoteAngle -= 360;
                var startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
                var endAngle = ratio * (endNoteAngle - prevInfo.ArcLength - (startNoteAngle - info.ArcLength)) +
                               (startNoteAngle - info.ArcLength);
                var arcLength = startAngle - endAngle;

                var arc1StartAngle = note.Size == 60 ? info.StartAngle : info.StartAngle + 1.5f;
                var arc1ArcLength = note.Size == 60 ? info.ArcLength : info.ArcLength - 3.0f;

                var arc2StartAngle = note.Size == 60 ? startAngle + arcLength : startAngle + arcLength - 1.5f;
                var arc2ArcLength = note.Size == 60 ? -arcLength : -arcLength + 3.0f;

                var p = new SKPath();
                p.ArcTo(info.Rect, arc1StartAngle, arc1ArcLength, true);
                p.ArcTo(currentInfo.Rect, arc2StartAngle, arc2ArcLength, false);
                canvas.DrawPath(p, Brushes.GetHoldFill(new SKPoint(CenterPoint.X, CenterPoint.Y), Radius));
            }

            // If the next note is on-screen, this case handles that
            if (note.NextReferencedNote != null && note.NextReferencedNote.Measure <= CurrentMeasure + totalMeasureShowNotes)
            {
                var nextInfo = GetSkArcInfo(chart, note.NextReferencedNote);

                var arc1StartAngle = note.Size == 60 ? info.StartAngle : info.StartAngle + 1.5f;
                var arc1ArcLength = note.Size == 60 ? info.ArcLength : info.ArcLength - 3.0f;

                var arc2StartAngle = note.Size == 60 ? nextInfo.StartAngle + nextInfo.ArcLength : nextInfo.StartAngle + nextInfo.ArcLength - 1.5f;
                var arc2ArcLength = note.Size == 60 ? -nextInfo.ArcLength : -nextInfo.ArcLength + 3.0f;

                var p = new SKPath();
                p.ArcTo(info.Rect, arc1StartAngle, arc1ArcLength, true);
                p.ArcTo(nextInfo.Rect, arc2StartAngle, arc2ArcLength, false);
                canvas.DrawPath(p, Brushes.GetHoldFill(new SKPoint(CenterPoint.X, CenterPoint.Y), Radius));
            }

            // If the next note is off-screen, this case handles that
            if (note.NextReferencedNote != null && note.NextReferencedNote.Measure > CurrentMeasure + totalMeasureShowNotes)
            {
                var nextInfo = GetSkArcInfo(chart, note.NextReferencedNote);
                var ratio = (endInfo.Rect.Width - nextInfo.Rect.Width) / (info.Rect.Width - nextInfo.Rect.Width);
                var startNoteAngle = nextInfo.StartAngle;
                var endNoteAngle = info.StartAngle;
                if (nextInfo.StartAngle > info.StartAngle && Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180)
                    startNoteAngle -= 360;
                else if (info.StartAngle > nextInfo.StartAngle && Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180)
                    endNoteAngle -= 360;
                var startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
                var endAngle = ratio * (endNoteAngle - info.ArcLength - (startNoteAngle - nextInfo.ArcLength)) +
                               (startNoteAngle - nextInfo.ArcLength);
                var arcLength = startAngle - endAngle;

                // slightly hacky fix to stop hold notes from overflowing the circle
                var limitedRect = info.Rect.Width < DrawRect.Width ? info.Rect : DrawRect;

                var arc1StartAngle = note.Size == 60 ? startAngle : startAngle + 1.5f;
                var arc1ArcLength = note.Size == 60 ? arcLength : arcLength - 3.0f;

                var arc2StartAngle = note.Size == 60 ? info.StartAngle + info.ArcLength : info.StartAngle + info.ArcLength - 1.5f;
                var arc2ArcLength = note.Size == 60 ? -info.ArcLength : -info.ArcLength + 3.0f;

                var p = new SKPath();
                p.ArcTo(endInfo.Rect, arc1StartAngle, arc1ArcLength, true);
                p.ArcTo(limitedRect, arc2StartAngle, arc2ArcLength, false);
                canvas.DrawPath(p, Brushes.GetHoldFill(new SKPoint(CenterPoint.X, CenterPoint.Y), Radius));
            }

            // Draw note
            if (info.Rect.Width >= 1 && GetObjectVisibility(note.Measure))
            {
                // draw hold start notes with end caps
                if (note.NoteType is NoteType.HoldStartNoBonus or NoteType.HoldStartBonusFlair)
                {
                    if (note.Size != 60)
                        DrawEndCaps(info.Rect, info.StartAngle, info.ArcLength, info.NoteScale * noteScaleMultiplier);

                    canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false, Brushes.GetNotePen(note, info.NoteScale * noteScaleMultiplier));
                }

                // draw hold joint and end notes if paused
                if (!Playing && note.NoteType is NoteType.HoldJoint or NoteType.HoldEnd)
                {
                    canvas.DrawArc(info.Rect, info.StartAngle + 1.5f, info.ArcLength - 3.0f, false, Brushes.GetNotePen(note, info.NoteScale * noteScaleMultiplier));
                }

                // draw thin hold end notes only while playing
                if (Playing && note.NoteType is NoteType.HoldEnd)
                {
                    canvas.DrawArc(info.Rect, info.StartAngle + 1.5f, info.ArcLength - 3.0f, false, Brushes.GetEndHoldPen(note, info.NoteScale * noteScaleMultiplier));
                }


                // Draw bonus
                if (note.IsFlair)
                {
                    canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, Brushes.GetFlairPen(info.NoteScale * noteScaleMultiplier));
                }

                // Plot highlighted
                if (highlightSelectedNote)
                    if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, Brushes.GetHighlightPen(info.NoteScale * noteScaleMultiplier, false));
            }
        }
    }

    public void DrawNotes(Chart chart, bool highlightSelectedNote, int selectedNoteIndex, float noteScaleMultiplier)
    {
        var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        var drawNotes = chart.Notes.Where(
            x => x.Measure >= CurrentMeasure - 1
                 && x.Measure <= CurrentMeasure + totalMeasureShowNotes
                 && !x.IsHold && !x.IsMask); //.ToList();
        foreach (var note in drawNotes)
        {
            var info = GetSkArcInfo(chart, note);

            if (info.Rect.Width >= 1 && GetObjectVisibility(note.Measure))
            {
                if (note.IsBonus)
                    canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false, Brushes.GetBonusPen(info.NoteScale * noteScaleMultiplier));
                
                if (note.IsFlair)
                    canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false, Brushes.GetFlairPen(info.NoteScale * noteScaleMultiplier));

                if (note.Size != 60)
                    DrawEndCaps(info.Rect, info.StartAngle, info.ArcLength, info.NoteScale * noteScaleMultiplier);

                canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false, Brushes.GetNotePen(note, info.NoteScale * noteScaleMultiplier));
                
                if (highlightSelectedNote && (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex]))
                    canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false, Brushes.GetHighlightPen(info.NoteScale * noteScaleMultiplier, false));
            }
        }
    }

    public void DrawEndCaps(SKRect rect, float start, float length, float noteScale)
    {
        var arc1Start = start - 0.1f;
        var arc2Start = start + length - 1.5f;
        const float arcLength = 1.6f;
        canvas.DrawArc(rect, arc1Start, arcLength, false, Brushes.GetEndCapPen(noteScale));
        canvas.DrawArc(rect, arc2Start, arcLength, false, Brushes.GetEndCapPen(noteScale));
    }

    public void DrawNoteLinks(Chart chart, float noteScaleMultiplier)
    {
        // leaving this here for when it's actually implemented.
        // A nested foreach loop is a stupid solution.
    }

    public void DrawSlideArrows(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
    {
        var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        var drawNotes = chart.Notes.Where(
            x => x.Measure >= CurrentMeasure - 1
                 && x.Measure <= CurrentMeasure + totalMeasureShowNotes
                 && x.IsSlide);
        foreach (var note in drawNotes)
        {
            var info = GetSkArcInfo(chart, note);

            int arrowDirection;
            switch (note.NoteType)
            {
                case NoteType.SlideOrangeNoBonus:
                case NoteType.SlideOrangeBonus:
                case NoteType.SlideOrangeBonusFlair:
                    arrowDirection = 1;
                    break;
                case NoteType.SlideGreenNoBonus:
                case NoteType.SlideGreenBonus:
                case NoteType.SlideGreenBonusFlair:
                    arrowDirection = -1;
                    break;
                default:
                    arrowDirection = 0;
                    break;
            }

            var radius = info.Rect.Width * 0.53f;

            const float radiusOffset = 0.79f;
            const float arrowMinWidth = 0.02f;
            const float arrowMaxWidth = 0.07f;
            const float arrowLength = 3.5f;

            var startPoint = info.StartAngle - 12 - (6 * arrowDirection);
            var endPoint = startPoint + info.ArcLength + 18 + (6 * arrowDirection);

            const int interval = 12;

            if (arrowMovementOffset > 12)
                arrowMovementOffset -= 12;

            for (var i = startPoint + arrowMovementOffset % 12 * arrowDirection; i > endPoint; i -= interval)
            {
                var progress = arrowDirection != 1 ? (i - startPoint) / (endPoint - startPoint) : 1 - (i - startPoint) / (endPoint - startPoint);
                var scaledArrowWidth = (1 - progress) * arrowMinWidth + arrowMaxWidth * progress;

                var p1 = GetPointOnArc(CenterPoint.X, CenterPoint.Y, radius * (radiusOffset + scaledArrowWidth), i - arrowLength * arrowDirection * 0.5f);
                var p2 = GetPointOnArc(CenterPoint.X, CenterPoint.Y, radius * radiusOffset, i + arrowLength * arrowDirection);
                var p3 = GetPointOnArc(CenterPoint.X, CenterPoint.Y, radius * (radiusOffset - scaledArrowWidth), i - arrowLength * arrowDirection * 0.5f);

                if (info.Rect.Width >= 1 && GetObjectVisibility(note.Measure))
                {
                    canvas.DrawLine(p1, p2, Brushes.GetArrowPen(note, info.NoteScale));
                    canvas.DrawLine(p2, p3, Brushes.GetArrowPen(note, info.NoteScale));

                    if (highlightSelectedNote)
                        if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        {
                            canvas.DrawLine(p1, p2, Brushes.GetHighlightPen(info.NoteScale, true));
                            canvas.DrawLine(p2, p3, Brushes.GetHighlightPen(info.NoteScale, true));
                        }
                }
            }
        }
    }

    public void DrawSnapArrows(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
    {
        var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        var drawNotes = chart.Notes.Where(
            x => x.Measure >= CurrentMeasure - 1
                 && x.Measure <= CurrentMeasure + totalMeasureShowNotes
                 && x.IsSnap);
        foreach (var note in drawNotes)
        {
            var info = GetSkArcInfo(chart, note);

            int arrowDirection;
            switch (note.NoteType)
            {
                case NoteType.SnapRedNoBonus:
                case NoteType.SnapRedBonusFlair:
                    arrowDirection = 1;
                    break;
                case NoteType.SnapBlueNoBonus:
                case NoteType.SnapBlueBonusFlair:
                    arrowDirection = -1;
                    break;
                default:
                    arrowDirection = 0;
                    break;
            }

            var radius = info.Rect.Width * 0.53f;
            var radiusOffset = arrowDirection == 1 ? 0.8f : 0.7f;

            // arrow settings
            // offset = how far apart the two rows of arrows are
            // length = how long arrows are
            // width = how wide arrows are
            var arrowOffset = info.Rect.Width * 0.045f;
            var arrowLength = 0.1f;
            var arrowWidth = 3f;

            var startPoint = (float) -note.Position * 6;
            var endPoint = startPoint + (float) -note.Size * 6;

            var arrowCount = MathF.Floor((note.Size / 3));
            var interval = (endPoint - startPoint) / arrowCount;
            var offset = interval / 2;

            for (var i = startPoint + offset; i > endPoint; i += interval)
            {
                var p1 = GetPointOnArc(CenterPoint.X, CenterPoint.Y, radius * radiusOffset, i + arrowWidth);
                var p2 = GetPointOnArc(CenterPoint.X, CenterPoint.Y, radius * (radiusOffset - arrowLength * arrowDirection), i);
                var p3 = GetPointOnArc(CenterPoint.X, CenterPoint.Y, radius * radiusOffset, i - arrowWidth);

                var p4 = GetPointOnArc(CenterPoint.X, CenterPoint.Y, arrowOffset + radius * radiusOffset, i + arrowWidth);
                var p5 = GetPointOnArc(CenterPoint.X, CenterPoint.Y, arrowOffset + radius * (radiusOffset - arrowLength * arrowDirection), i);
                var p6 = GetPointOnArc(CenterPoint.X, CenterPoint.Y, arrowOffset + radius * radiusOffset, i - arrowWidth);

                if (info.Rect.Width >= 1 && GetObjectVisibility(note.Measure))
                {
                    canvas.DrawLine(p1, p2, Brushes.GetArrowPen(note, info.NoteScale));
                    canvas.DrawLine(p2, p3, Brushes.GetArrowPen(note, info.NoteScale));

                    canvas.DrawLine(p4, p5, Brushes.GetArrowPen(note, info.NoteScale));
                    canvas.DrawLine(p5, p6, Brushes.GetArrowPen(note, info.NoteScale));

                    if (highlightSelectedNote)
                        if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        {
                            canvas.DrawLine(p1, p2, Brushes.GetHighlightPen(info.NoteScale, true));
                            canvas.DrawLine(p2, p3, Brushes.GetHighlightPen(info.NoteScale, true));

                            canvas.DrawLine(p4, p5, Brushes.GetHighlightPen(info.NoteScale, true));
                            canvas.DrawLine(p5, p6, Brushes.GetHighlightPen(info.NoteScale, true));
                        }
                }
            }
        }
    }

    private float DepthToTime(uint depth)
    {
        return (float)depth / (float)BeatsPerMeasure;
    }

    public float DepthToMeasure(uint depth)
    {
        float measure = CurrentMeasure;
        measure += DepthToTime(depth);
        return measure;
    }

    public uint CalculateMaximumDepth(Chart chart)
    {
        float totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        float startBeat = CurrentMeasure;
        float endBeat = CurrentMeasure + totalMeasureShowNotes;
        float stepSize = DepthToMeasure(1) - DepthToMeasure(0);

        // Just in case, to avoid dividing by 0.
        if (stepSize == 0)
        {
            return 0;
        }

        return (uint)Math.Floor((endBeat - startBeat) / stepSize);
    }

    public void DrawCursor(Chart chart, NoteType noteType, float startAngle, float sweepAngle, uint depth)
    {
        float measure = DepthToMeasure(depth);
        var measureArcInfo = GetScaledRect(chart, measure);
        canvas.DrawArc(measureArcInfo.Rect, -startAngle * 6.0f,
            -sweepAngle * 6.0f,
            false,
            Brushes.GetCursorPen(noteType)
        );
    }

    /// <summary>
    /// Draws a transluscent ring at the current cursor beat position.
    /// </summary>
    /// <param name="depth">An index representing depth into the circle view. 0 is the outermost circle. Higher values go deeper into the view.</param>
    public void DrawCursorBeatIndicator(Chart chart, uint depth)
    {
        float measure = DepthToMeasure(depth);
        var measureArcInfo = GetScaledRect(chart, measure);
        canvas.DrawOval(measureArcInfo.Rect, CursorBeatIndicatorPen);
    }
    
    public void DrawMirrorAxis(int axis)
    {
        var axisAngle = -axis * 3;

        var startPoint = new SKPoint(
                (float)(Radius * Math.Cos(Utils.DegToRad(axisAngle)) + CenterPoint.X),
                (float)(Radius * Math.Sin(Utils.DegToRad(axisAngle)) + CenterPoint.Y));

        var endPoint = new SKPoint(
                (float)(Radius * Math.Cos(Utils.DegToRad(axisAngle + 180)) + CenterPoint.X),
                (float)(Radius * Math.Sin(Utils.DegToRad(axisAngle + 180)) + CenterPoint.Y));

        canvas.DrawLine(startPoint, endPoint, Brushes.MirrorAxisPen);
    }

    private bool GetObjectVisibility(float noteTime)
    {
        return (noteTime >= CurrentMeasure && !renderNotesBeyondCircle) || (noteTime > CurrentMeasure - 1 && renderNotesBeyondCircle);
    }
}