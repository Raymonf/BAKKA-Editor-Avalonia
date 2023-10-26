using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using BAKKA_Editor.Enums;
using SkiaSharp;

namespace BAKKA_Editor;

internal struct SkArcInfo
{
    public float StartAngle;
    public float ArcLength;
    public SKRect Rect;
    public float NoteScale;
}

internal class SkCircleView
{
    private SKCanvas canvas;
    private readonly int CursorTransparency = 110;
    private readonly int FlairTransparency = 75;
    public int lastMousePos = -1;

    // Mouse information. Public so other GUI elements can be updated with their values.
    public int mouseDownPos = -1;
    public Point mouseDownPt;
    public bool rolloverNeg;
    public bool rolloverPos;
    private readonly int SelectTransparency = 110;

    public SkCircleView(SizeF size)
    {
        Update(size);
    }

    public SizeF PanelSize { get; private set; }
    public SKRect DrawRect { get; private set; }
    public PointF TopCorner { get; private set; }
    public PointF CenterPoint { get; private set; }
    public float Radius { get; private set; }
    public float CurrentMeasure { get; set; }
    public float Hispeed { get; set; } = 1.5f;
    public float BeatDivision { get; set; } = 2;
    public string GuideLineSelection { get; set; } = "None";
    public bool showHispeed { get; set; } = true;
    public bool renderNotesBeyondCircle { get; set; } = false; // temporary(?) - may be moved to settings since it's for the Settings UI, just wanted to put it here to put it in the code already.

    // Pens and Brushes
    public SKPaint? MeasurePen { get; set; }
    public SKPaint? BeatPen { get; set; }
    public SKPaint? TickMinorPen { get; set; }
    public SKPaint? TickMediumPen { get; set; }
    public SKPaint? TickMajorPen { get; set; }

    public SKPaint? HoldBrush { get; set; } = new()
    {
        IsAntialias = true,
        Color = SKColors.Yellow.WithAlpha(170),
        Style = SKPaintStyle.Fill
    };

    public SKPaint MaskBrush { get; set; } = Utils.CreateFillBrush(SKColors.Black.WithAlpha(90), false);
    public SKPaint? BackgroundBrush { get; set; }
    public SKPaint? HighlightPen { get; set; }
    public SKPaint? FlairPen { get; set; }

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

        // Pens
        MeasurePen = Utils.CreateStrokeBrush(SKColors.White, PanelSize.Width * 1.0f / 600.0f);
        BeatPen = Utils.CreateStrokeBrush(SKColors.White.WithAlpha(0x80), PanelSize.Width * 0.5f / 600.0f);
        TickMinorPen = Utils.CreateStrokeBrush(SKColors.Black, PanelSize.Width * 2.0f / 600.0f);
        TickMediumPen = Utils.CreateStrokeBrush(SKColors.Black, PanelSize.Width * 4.0f / 600.0f);
        TickMajorPen = Utils.CreateStrokeBrush(SKColors.Black, PanelSize.Width * 7.0f / 600.0f);
        HighlightPen = Utils.CreateStrokeBrush(SKColors.LightPink.WithAlpha((byte) SelectTransparency),
            PanelSize.Width * 8.0f / 600.0f);
        FlairPen = Utils.CreateStrokeBrush(SKColors.Yellow.WithAlpha((byte) FlairTransparency),
            PanelSize.Width * 8.0f / 600.0f);
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
                x.GimmickType == GimmickType.HiSpeedChange && CurrentMeasure > x.Measure);

            if (initialSpeed == null)
            {
                initialSpeed = new Gimmick { HiSpeed = 1.0 };
            }

            float initialModifiedTime = tempTotalTime / (float)initialSpeed.HiSpeed;

            var gimmicksInTimeRange = chart.Gimmicks.Where(x =>
                x.Measure >= CurrentMeasure &&
                chart.GetTime(new BeatInfo(x.Measure)) < tempEndTime &&
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

    /*private float GetTotalMeasureShowNotes(Chart chart)
    {
        //Convert hispeed to frames
        var displayFrames = 73.0f - (Hispeed - 1.5f) * 10.0f;
        var tempTotalTime = displayFrames / 60.0f * 1000.0f;

        float currentTime = chart.GetTime(CurrentMeasure);

        var tempEndTime = currentTime + tempTotalTime;
        if (showHispeed)
        {
            //Account for hispeed gimmick
            var hiSpeedChanges = new List<Gimmick>();
            var initialSpeed = chart.Gimmicks.LastOrDefault(x =>
                x.GimmickType == GimmickType.HiSpeedChange && CurrentMeasure > x.Measure);
            //Add initial hispeed to list
            if (initialSpeed == null)
            {
                initialSpeed = new Gimmick();
                initialSpeed.HiSpeed = 1.0;
            }

            hiSpeedChanges.Add(initialSpeed);
            //Random todos that im too lazy to put in applicable locations
            //TODO: add "time" to notes so we can compare against it every time instead of always calculating what time a note is at
            //TODO: add function to reevaluate the time of every note when bpm or TS is added/removed.

            //add all hispeed changes to list that happen within the current total time to show notes
            hiSpeedChanges.AddRange(chart.Gimmicks.Where(
                x => x.Measure >= CurrentMeasure
                     && chart.GetTime(new BeatInfo(x.Measure)) < tempEndTime
                     && x.GimmickType == GimmickType.HiSpeedChange));
            if (hiSpeedChanges.Count > 1)
                for (var i = 0; i < hiSpeedChanges.Count; i++)
                {
                    float timeDiff;
                    float itemTime = chart.GetTime(hiSpeedChanges[i].BeatInfo);
                    float modifiedTime;
                    if (itemTime <= tempTotalTime + currentTime)
                    {
                        if (i == 0)
                            itemTime = currentTime;

                        if (i != hiSpeedChanges.Count - 1)
                        {
                            var tempTestITimeDiff = currentTime + tempTotalTime - itemTime;
                            var tempTestIModifiedTime = tempTestITimeDiff / (float) hiSpeedChanges[i].HiSpeed;
                            if (currentTime + tempTotalTime - tempTestITimeDiff + tempTestIModifiedTime <
                                chart.GetTime(hiSpeedChanges[i + 1].BeatInfo))
                            {
                                timeDiff = currentTime + tempTotalTime - itemTime;
                                modifiedTime = timeDiff / (float) hiSpeedChanges[i].HiSpeed;
                            }
                            else
                            {
                                timeDiff = chart.GetTime(hiSpeedChanges[i + 1].BeatInfo) - itemTime;
                                modifiedTime = timeDiff / (float) hiSpeedChanges[i].HiSpeed;
                            }
                        }
                        else
                        {
                            timeDiff = currentTime + tempTotalTime - itemTime;
                            modifiedTime = timeDiff / (float) hiSpeedChanges[i].HiSpeed;
                        }

                        tempTotalTime = tempTotalTime - timeDiff + modifiedTime;
                    }
                }
            else
                tempTotalTime /= (float) hiSpeedChanges[0].HiSpeed;

            tempEndTime = currentTime + tempTotalTime;
        }

        //convert total time to total measure
        var endMeasure = chart.GetBeatMeasureDecimal(tempEndTime);
        return endMeasure - CurrentMeasure;
    }*/

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

    private SkArcInfo GetScaledRect(Chart chart, float objectTime)
    {
        SkArcInfo info = new();
        info.NoteScale = GetNoteScaleFromMeasure2(chart, objectTime);
        var scaledRectSize = DrawRect.Width * info.NoteScale;
        var scaledRadius = scaledRectSize / 2.0f;
        info.Rect = SKRect.Create(
            CenterPoint.X - scaledRadius,
            CenterPoint.Y - scaledRadius,
            scaledRectSize,
            scaledRectSize);
        return info;
    }

    private SkArcInfo GetSkArcInfo(Chart chart, Note note)
    {
        var info = GetScaledRect(chart, note.Measure);
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
            info.StartAngle -= 2;
            info.ArcLength += 4;
        }

        return info;
    }

    // Updates the mouse down position within the circle, and returns the new position.
    public void UpdateMouseDown(float xCen, float yCen, Point mousePt)
    {
        var theta = (float) (Math.Atan2(yCen, xCen) * 180.0f / Math.PI);
        if (theta < 0)
            theta += 360.0f;
        // Left click moves the cursor
        mouseDownPos = (int) (theta / 6.0f);
        mouseDownPt = mousePt;
        lastMousePos = -1;
        rolloverPos = false;
        rolloverNeg = false;
    }

    public void UpdateMouseUp()
    {
        if (mouseDownPos <= -1) return;

        // Reset position and point.
        mouseDownPos = -1;
        mouseDownPt = new Point();
    }

    /// <summary>
    ///     Creates the background brush with the passed in color if it's null.
    /// </summary>
    /// <param name="color">SKColor</param>
    public void DrawBackground(SKColor color, bool setBrush = false, bool isDarkMode = false)
    {
        if (setBrush || BackgroundBrush == null)
            // Remember background color for drawing later
            BackgroundBrush = Utils.CreateFillBrush(color, false);

        canvas.Clear(color);
    }

    // Updates the mouse position and returns the new position in degrees.
    public int UpdateMouseMove(float xCen, float yCen)
    {
        var thetaCalc = (float) (Math.Atan2(yCen, xCen) * 180.0f / Math.PI);
        if (thetaCalc < 0)
            thetaCalc += 360.0f;
        var theta = (int) (thetaCalc / 6.0f);

        var delta = theta - lastMousePos;
        // Handle rollover
        if (delta == -59)
        {
            if (rolloverNeg)
                rolloverNeg = false;
            else
                rolloverPos = true;
        }
        else if (delta == 59)
        {
            if (rolloverPos)
                rolloverPos = false;
            else
                rolloverNeg = true;
        }

        lastMousePos = theta;

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

    public void DrawMasks(Chart chart)
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
                        FillPie(MaskBrush, DrawRect, mask.Position * 6.0f, mask.Size * 6.0f);
                    break;
                }
                // Explicitly draw MaskRemove for edge cases
                case NoteType.MaskRemove:
                    FillPie(BackgroundBrush, DrawRect, mask.Position * 6.0f, mask.Size * 6.0f);
                    break;
            }
        }
    }

    public void DrawCircle(Chart chart)
    {
        // Draw measure circles
        var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        for (var measure = (float)Math.Ceiling(CurrentMeasure - 1);
             measure - CurrentMeasure < totalMeasureShowNotes;
             measure += 1.0f)
        {
            var measureArcInfo = GetScaledRect(chart, measure);
            if (measureArcInfo.Rect.Width >= 1 && GetObjectVisibility(measure))
                canvas.DrawOval(measureArcInfo.Rect, MeasurePen);
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
                    canvas.DrawOval(beatArcInfo.Rect, BeatPen);
            }
        }

        // Draw base circle
        canvas.DrawOval(DrawRect, TickMediumPen);
    }
    
    public void DrawDegreeLines()
    {
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
                activePen = TickMajorPen;
            }
            else if (i % 30 == 0)
            {
                innerRad = Radius - tickLength * 2.5f;
                activePen = TickMediumPen;
            }
            else
            {
                activePen = TickMinorPen;
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
            case "None":
                offset = 0;
                interval = 0;
                break;

            case "A":
                offset = 0;
                interval = 6;
                break;

            case "B":
                offset = 6;
                interval = 12;
                break;

            case "C":
                offset = 0;
                interval = 18;
                break;

            case "D":
                offset = 6;
                interval = 24;
                break;

            case "E":
                offset = 0;
                interval = 30;
                break;

            case "F":
                offset = 30;
                interval = 60;
                break;

            case "G":
                offset = 0;
                interval = 90;
                break;

            default: 
                offset = 0;
                interval = 0;
                break;
        }

        if (interval > 0)
        {
            var colors = new[]
             {
                SKColors.White.WithAlpha(0x20),
                SKColors.White.WithAlpha(0x00)
            };

            for (var i = 0 + offset; i < 360 + offset; i += interval)
            {
                var tickLength = PanelSize.Width * 110.0f / 285.0f;
                var innerRad = Radius - tickLength;

                var startPoint = new SKPoint(
                    (float)(Radius * Math.Cos(Utils.DegToRad(i)) + CenterPoint.X),
                    (float)(Radius * Math.Sin(Utils.DegToRad(i)) + CenterPoint.Y));

                var endPoint = new SKPoint(
                    (float)(innerRad * Math.Cos(Utils.DegToRad(i)) + CenterPoint.X),
                    (float)(innerRad * Math.Sin(Utils.DegToRad(i)) + CenterPoint.Y));

                var shader = SKShader.CreateLinearGradient(
                    new SKPoint(startPoint.X, startPoint.Y),
                    new SKPoint(endPoint.X, endPoint.Y),
                    colors,
                    null,
                    SKShaderTileMode.Clamp);

                var GuideLinePaint = new SKPaint { Shader = shader };

                canvas.DrawLine(startPoint, endPoint, GuideLinePaint);
            }
        }
    }

    public void DrawGimmicks(Chart chart, bool showGimmicks, int selectedGimmickIndex)
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
                    canvas.DrawOval(info.Rect, GetNotePaint(gimmick));
            }
        }
    }

    public void DrawHolds(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
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
            canvas.DrawPath(p, HoldBrush);
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
                canvas.DrawPath(p, HoldBrush);
            }

            // If the next note is on-screen, this case handles that
            if (note.NextNote != null && note.NextNote.Measure <= CurrentMeasure + totalMeasureShowNotes)
            {
                var nextInfo = GetSkArcInfo(chart, note.NextNote);
                var p = new SKPath();
                p.ArcTo(info.Rect, info.StartAngle, info.ArcLength, true);
                p.ArcTo(nextInfo.Rect, nextInfo.StartAngle + nextInfo.ArcLength, -nextInfo.ArcLength, false);
                canvas.DrawPath(p, HoldBrush);
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
                canvas.DrawPath(p, HoldBrush);
            }

            // Draw note
            if (info.Rect.Width >= 1)
            {
                canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false, GetNotePaint(note));

                // Draw bonus
                if (note.IsFlair)
                    canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, HighlightPen);

                // Plot highlighted
                if (highlightSelectedNote)
                    if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, HighlightPen);
            }
        }
    }

    public void DrawHoldsSingle(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
    {
        var currentInfo = GetScaledRect(chart, CurrentMeasure);
        var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        var endInfo = GetScaledRect(chart, CurrentMeasure + totalMeasureShowNotes);
        var endMeasure = CurrentMeasure + totalMeasureShowNotes;

        // Draw all the notes on-screen
        var holdNotes = chart.Notes
            .Where(x => x.IsHold && (
                (x.Measure < CurrentMeasure && x.NextNote?.Measure > endMeasure) ||
                (x.Measure >= CurrentMeasure && x.Measure <= endMeasure)));
            // .ToList();
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
                canvas.DrawPath(p, HoldBrush);
            }

            // If the next note is on-screen, this case handles that
            if (note.NextNote != null && note.NextNote.Measure <= CurrentMeasure + totalMeasureShowNotes)
            {
                var nextInfo = GetSkArcInfo(chart, note.NextNote);
                var p = new SKPath();
                p.ArcTo(info.Rect, info.StartAngle, info.ArcLength, true);
                p.ArcTo(nextInfo.Rect, nextInfo.StartAngle + nextInfo.ArcLength, -nextInfo.ArcLength, false);
                canvas.DrawPath(p, HoldBrush);
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
                canvas.DrawPath(p, HoldBrush);
            }

            // Draw note
            if (info.Rect.Width >= 1)
            {
                canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false, GetNotePaint(note));

                // Draw bonus
                if (note.IsFlair)
                    canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, HighlightPen);

                // Plot highlighted
                if (highlightSelectedNote)
                    if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, HighlightPen);
            }
        }
    }

    public void DrawNotes(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
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
                canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false,
                    GetNotePaint(note, info.NoteScale));

                if (note.IsFlair)
                    canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, FlairPen);
                // Plot highlighted
                if (highlightSelectedNote)
                    if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, HighlightPen);
                //if (note.Measure < CurrentMeasure)
                    //canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false, HitPen);
            }
        }
    }

    public void DrawCursor(NoteType noteType, float startAngle, float sweepAngle)
    {
        canvas.DrawArc(DrawRect, -startAngle * 6.0f,
            -sweepAngle * 6.0f,
            false,
            GetNotePaint(noteType)
        );
    }

    private SKPaint GetNotePaint(Note note, float noteScale = 1.0f)
    {
        var color = note.Measure >= CurrentMeasure ? note.Color : note.Color.WithAlpha(0x30);

        return new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = PanelSize.Width * 8.0f * noteScale / 600.0f
        };
    }

    private SKPaint GetNotePaint(Gimmick gimmick, float noteScale = 1.0f)
    {
        var color = gimmick.Measure >= CurrentMeasure ? Utils.GimmickTypeToColor(gimmick.GimmickType) : Utils.GimmickTypeToColor(gimmick.GimmickType).WithAlpha(0x30);

        return new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = PanelSize.Width * 5.0f * noteScale / 600.0f
        };
    }

    private SKPaint GetNotePaint(NoteType noteType)
    {
        var color = Utils.NoteTypeToColor(noteType);
        return new SKPaint
        {
            IsAntialias = true,
            Color = color.WithAlpha((byte) CursorTransparency),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = PanelSize.Width * 24.0f / 600.0f
        };
    }

    private bool GetObjectVisibility(float noteTime)
    {
        return (noteTime >= CurrentMeasure && !renderNotesBeyondCircle) || (noteTime > CurrentMeasure - 1 && renderNotesBeyondCircle);
    }
}