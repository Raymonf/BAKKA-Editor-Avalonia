using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using BAKKA_Editor.Enums;

namespace BAKKA_Editor.Rendering;

internal class RenderEngine
{
    private SKCanvas canvas;
    private Brushes brushes { get; set; }
    private readonly SkCircleView circleView;
    private UserSettings userSettings;

    public RenderEngine(SkCircleView skCircleView, UserSettings settings)
    {
        brushes = new Brushes(settings);
        circleView = skCircleView;
        userSettings = settings;
    }

    private SizeF PanelSize { get; set; }
    private SKRect DrawRect { get; set; }
    private PointF CanvasTopCorner { get; set; }
    public PointF CanvasCenterPoint { get; private set; }
    private float CanvasRadius { get; set; }

    public float CurrentMeasure { get; set; }
    private float ScaledCurrentMeasure { get; set; }
    public uint BeatsPerMeasure { get; set; }
    public float UserHiSpeed { get; set; } = 1.5f;
    public float BeatDivision { get; set; } = 2;
    public int GuideLineSelection { get; set; } = 0;
    public float ArrowMovementOffset { get; set; } = 0;
    public float VisibleMeasures { get; set; } = 1f;
    public bool Playing { get; set; } = false;

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
        brushes.SetHoldFill(new SKPoint(CanvasCenterPoint.X, CanvasCenterPoint.Y), CanvasRadius);
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
        var scaledPosition = userSettings.ViewSettings.ShowGimmickEffects
            ? chart.GetScaledMeasurePosition(position)
            : position;
        var scaledCurrentMeasure = userSettings.ViewSettings.ShowGimmickEffects ? ScaledCurrentMeasure : CurrentMeasure;
        var visionEndMeasure = scaledCurrentMeasure + VisibleMeasures;

        noteScale = 1 - (scaledPosition - scaledCurrentMeasure) / (visionEndMeasure - scaledCurrentMeasure);

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

    /// <summary>
    /// Returns an arc based on the properties of a Note
    /// </summary>
    /// <param name="chart"></param>
    /// <param name="note"></param>
    /// <param name="scale"></param>
    /// <returns></returns>
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
            (float) (radius * Math.Cos(Utils.DegToRad(angle)) + x),
            (float) (radius * Math.Sin(Utils.DegToRad(angle)) + y));

        return point;
    }

    private void FillPie(SKPaint paint, SKRect rect, float startAngle, float sweepAngle)
    {
        canvas.DrawArc(rect, -startAngle, -sweepAngle, true, paint);
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
        // float totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        float startBeat = CurrentMeasure;
        float endBeat = CurrentMeasure + VisibleMeasures;
        float stepSize = DepthToMeasure(1) - DepthToMeasure(0);

        // Just in case, to avoid dividing by 0.
        if (stepSize == 0)
        {
            return 0;
        }

        return (uint)Math.Floor((endBeat - startBeat) / stepSize);
    }

    // ================ Rendering ================
    public void Render(Chart chart, NoteType currentNoteType, int selectedNoteIndex, int selectedGimmickIndex,
        bool isHoveringOverMirrorAxis, bool showCursor, int cursorStartAngle, int cursorArcAngle, int axis, List<Note> multiSelectNotes)
    {
        DrawBackground(userSettings.ViewSettings.DarkMode);
        if (userSettings.ViewSettings.ShowMaskEffects)
            DrawMaskEffect(chart, userSettings.ViewSettings.DarkMode);
        DrawGuideLines();
        DrawCircleDividers(chart);
        DrawDegreeCircle();
        if (isHoveringOverMirrorAxis)
            DrawMirrorAxis(axis);
        if ((!Playing && userSettings.ViewSettings.ShowGimmicks) || (Playing && userSettings.ViewSettings.ShowGimmicksDuringPlayback))
            DrawGimmickNotes(chart, userSettings.ViewSettings.HighlightViewedNote, selectedGimmickIndex);
        DrawHolds(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex,
            userSettings.ViewSettings.NoteScaleMultiplier, multiSelectNotes);
        DrawNotes(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex,
            userSettings.ViewSettings.NoteScaleMultiplier, multiSelectNotes);
        if (userSettings.ViewSettings.ShowSlideSnapArrows)
            DrawArrows(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex);
        if (showCursor)
        {
            DrawCursor(chart, currentNoteType, cursorStartAngle, cursorArcAngle, circleView.Cursor.Depth);
            if (userSettings.ViewSettings.ShowCursorDepth)
                DrawCursorBeatIndicator(chart, circleView.Cursor.Depth);
        }
    }

    // ==== UI
    private void DrawBackground(bool isDarkMode)
    {
        canvas.Clear(Brushes.GetBackgroundColor(isDarkMode));
    }

    private bool[] maskState = new bool[60];

    private void DrawMaskEffect(Chart chart, bool darkMode)
    {
        Array.Clear(maskState, 0, maskState.Length);

        foreach (var note in chart.Notes)
        {
            if (note.Measure > CurrentMeasure)
                break;

            if (!note.IsMask)
                continue;

            for (var i = 0; i < note.Size; i++)
            {
                var index = (note.Position + i) % 60;
                maskState[index] = note.NoteType == NoteType.MaskAdd;
            }
        }

        // attempt to "batch" draw the masks that are sequential
        var start = -1;
        var sweepAngle = 0;

        for (var i = 0; i < maskState.Length; i++)
        {
            if (maskState[i] && start == -1)
            {
                start = i;
                sweepAngle = 6;
            }
            else if (maskState[i] && start != -1)
            {
                sweepAngle += 6;
            }
            else if (!maskState[i] && start != -1)
            {
                FillPie(brushes.MaskFill, DrawRect, start * 6, sweepAngle);
                start = -1;
            }
        }

        // draw remaining mask if any
        if (start != -1)
        {
            FillPie(brushes.MaskFill, DrawRect, start * 6, sweepAngle);
        }
    }

    private void DrawDegreeCircle()
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
            else if (i % 30 == 0)
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

    private void DrawCircleDividers(Chart chart)
    {
        var start = MathF.Ceiling(CurrentMeasure * BeatDivision) * (1 / BeatDivision);
        var end = ScaledCurrentMeasure + VisibleMeasures;

        for (var i = start; chart.GetScaledMeasurePosition(i) < end; i += 1 / BeatDivision)
        {
            var beatArc = GetRect(chart, i);

            if (beatArc.Rect.Width < 1) continue;

            // floating point errors yippie. catches both of these cases:
            // i % 1 = 0.000000012345 <- i slightly more than an integer
            // i % 1 = 0.999999969420 <- i slightly less than an integer
            var remainder = i % 1;
            var isMeasure = remainder < 0.0001 || (remainder > 0.99999 && remainder < 1);

            if (isMeasure)
                canvas.DrawOval(beatArc.Rect, brushes.MeasurePen);
            else
                canvas.DrawOval(beatArc.Rect, brushes.BeatPen);
        }
    }

    private void DrawGuideLines()
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

        if (interval < 1) return;

        var tickLength = PanelSize.Width * 110.0f / 285.0f;
        var innerRadius = CanvasRadius - tickLength;

        for (var i = 0 + offset; i < 360 + offset; i += interval)
        {
            var startPoint = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, CanvasRadius, i);
            var endPoint = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, innerRadius, i);

            canvas.DrawLine(startPoint, endPoint, brushes.GetGuidelinePen(startPoint, endPoint));
        }
    }

    private void DrawMirrorAxis(int axis)
    {
        var axisAngle = -axis * 3;

        var startPoint = new SKPoint(
            (float) (CanvasRadius * Math.Cos(Utils.DegToRad(axisAngle)) + CanvasCenterPoint.X),
            (float) (CanvasRadius * Math.Sin(Utils.DegToRad(axisAngle)) + CanvasCenterPoint.Y));

        var endPoint = new SKPoint(
            (float) (CanvasRadius * Math.Cos(Utils.DegToRad(axisAngle + 180)) + CanvasCenterPoint.X),
            (float) (CanvasRadius * Math.Sin(Utils.DegToRad(axisAngle + 180)) + CanvasCenterPoint.Y));

        canvas.DrawLine(startPoint, endPoint, brushes.MirrorAxisPen);
    }

    private void DrawCursor(Chart chart, NoteType currentNoteType, float startAngle, float sweepAngle, uint depth)
    {
        float measure = DepthToMeasure(depth);
        var scale = GetNoteScale(chart, measure);
        var measureArcInfo = GetRect(chart, measure);
        canvas.DrawArc(measureArcInfo.Rect, -startAngle * 6.0f,
            -sweepAngle * 6.0f,
            false,
            brushes.GetCursorPen(currentNoteType, scale)
        );
    }

    /// <summary>
    /// Draws a transluscent ring at the current cursor beat position.
    /// </summary>
    /// <param name="depth">An index representing depth into the circle view. 0 is the outermost circle. Higher values go deeper into the view.</param>
    public void DrawCursorBeatIndicator(Chart chart, uint depth)
    {
        float measure = DepthToMeasure(depth);
        var scale = GetNoteScale(chart, measure);
        var measureArcInfo = GetRect(chart, measure);
        canvas.DrawOval(measureArcInfo.Rect, brushes.GetCursorMeasurePen(scale));
    }

    // ==== NOTES
    private void DrawGimmickNotes(Chart chart, bool highlightSelectedNote, int selectedGimmickIndex)
    {
        var visibleGimmicks = userSettings.ViewSettings.ShowGimmickEffects
            ? chart.Gimmicks.Where(x =>
                x.Measure >= CurrentMeasure &&
                chart.GetScaledMeasurePosition(x.Measure) <= ScaledCurrentMeasure + VisibleMeasures)
            : chart.Gimmicks.Where(x => x.Measure >= CurrentMeasure && x.Measure <= CurrentMeasure + VisibleMeasures);

        foreach (var gimmick in visibleGimmicks)
        {
            var info = GetRect(chart, gimmick.Measure);
            if (info.Rect.Width < 1) continue;

            canvas.DrawOval(info.Rect, brushes.GetGimmickPen(gimmick, 1));

            if (highlightSelectedNote && selectedGimmickIndex != -1 && gimmick == chart.Gimmicks[selectedGimmickIndex])
                canvas.DrawOval(info.Rect, brushes.GetHighlightPen(1));
        }
    }

    private void DrawNotes(Chart chart, bool highlightSelectedNote, int selectedNoteIndex, float noteScaleMultiplier, List<Note> multiSelectNotes)
    {
        var visibleNotes = userSettings.ViewSettings.ShowGimmickEffects
            ? chart.Notes.Where(x =>
                !x.IsHold && x.Measure >= CurrentMeasure && chart.GetScaledMeasurePosition(x.Measure) <=
                ScaledCurrentMeasure + VisibleMeasures)
            : chart.Notes.Where(x =>
                !x.IsHold && x.Measure >= CurrentMeasure && x.Measure <= CurrentMeasure + VisibleMeasures);

        foreach (var note in visibleNotes)
        {
            var info = GetArc(chart, note);
            var fullStartAngle = info.StartAngle + 1.5f;
            var fullArcAngle = info.ArcAngle - 3.0f;

            if (info.Rect.Width < 1) continue;

            // save another note list lookup and just draw masks here
            if (note.IsMask)
            {
                // draw mask notes only if the relevant setting to draw these notes is enabled
                switch (Playing)
                {
                    case false when !userSettings.ViewSettings.ShowMaskNotes:
                    case true when !userSettings.ViewSettings.ShowMaskNotesDuringPlayback:
                        continue;
                }

                canvas.DrawArc(info.Rect, info.StartAngle + 6, info.ArcAngle - 12, false,
                    brushes.GetNotePen(note, info.NoteScale * noteScaleMultiplier));
                if (highlightSelectedNote && selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                    canvas.DrawArc(info.Rect, info.StartAngle + 6, info.ArcAngle - 12, false,
                        brushes.GetHighlightPen(info.NoteScale * noteScaleMultiplier));

                if (multiSelectNotes.Count != 0 && multiSelectNotes.Contains(note))
                    canvas.DrawArc(info.Rect, fullStartAngle, fullArcAngle, false, brushes.GetMultiSelectPen(info.NoteScale * noteScaleMultiplier));

                continue;
            }

            if (note.IsBonus)
                canvas.DrawArc(info.Rect, fullStartAngle, fullArcAngle, false,
                    brushes.GetBonusPen(info.NoteScale * noteScaleMultiplier));

            if (note.IsFlair)
                canvas.DrawArc(info.Rect, fullStartAngle, fullArcAngle, false,
                    brushes.GetFlairPen(info.NoteScale * noteScaleMultiplier));

            if (note.Size != 60)
            {
                DrawEndCaps(info.Rect, info.StartAngle, info.ArcAngle, info.NoteScale * noteScaleMultiplier);
                canvas.DrawArc(info.Rect, info.StartAngle, info.ArcAngle, false,
                    brushes.GetNotePen(note, info.NoteScale * noteScaleMultiplier));
            }
            else
            {
                canvas.DrawOval(info.Rect, brushes.GetNotePen(note, info.NoteScale * noteScaleMultiplier));
            }

            if (highlightSelectedNote && selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                canvas.DrawArc(info.Rect, fullStartAngle, fullArcAngle, false, brushes.GetHighlightPen(info.NoteScale * noteScaleMultiplier));

            if (multiSelectNotes.Count != 0 && multiSelectNotes.Contains(note))
                canvas.DrawArc(info.Rect, fullStartAngle, fullArcAngle, false, brushes.GetMultiSelectPen(info.NoteScale * noteScaleMultiplier));
        }
    }

    private void DrawHolds(Chart chart, bool highlightSelectedNote, int selectedNoteIndex, float noteScaleMultiplier, List<Note> multiSelectNotes)
    {
        // awards for longest line(s) of code go to...
        // ray, feel free to write this differently.

        // ray says: i don't want to,,,,,
        IEnumerable<Note> visibleNotes;
        if (userSettings.ViewSettings.ShowGimmickEffects)
        {
            visibleNotes = chart.Notes.Where(x =>
                x.IsHold &&
                ((x.Measure >= CurrentMeasure &&
                  chart.GetScaledMeasurePosition(x.Measure) <= ScaledCurrentMeasure + VisibleMeasures) ||
                 (x.NextReferencedNote != null && x.Measure < CurrentMeasure &&
                  chart.GetScaledMeasurePosition(x.NextReferencedNote.Measure) >
                  ScaledCurrentMeasure + VisibleMeasures)));
        }
        else
        {
            visibleNotes = chart.Notes.Where(x =>
                x.IsHold && ((x.Measure >= CurrentMeasure && x.Measure <= CurrentMeasure + VisibleMeasures) ||
                             (x.Measure < CurrentMeasure &&
                              x.NextReferencedNote?.Measure > CurrentMeasure + VisibleMeasures)));
        }

        /*var endInfo = userSettings.ViewSettings.ShowGimmickEffects
            ? GetRect(chart, ScaledCurrentMeasure + VisibleMeasures)
            : GetRect(chart, CurrentMeasure + VisibleMeasures);*/

        var centerPoint = new SKPoint(CanvasCenterPoint.X, CanvasCenterPoint.Y);

        foreach (var note in visibleNotes)
        {
            var currentInfo = GetArc(chart, note);

            var currentNoteVisible = note.Measure >= CurrentMeasure;
            var nextNoteVisible = note.NextReferencedNote?.Measure < CurrentMeasure + VisibleMeasures;
            var prevNoteVisible = note.PrevReferencedNote?.Measure >= CurrentMeasure;

            if (userSettings.ViewSettings.ShowGimmickEffects && note.NextReferencedNote != null)
                nextNoteVisible = chart.GetScaledMeasurePosition(note.NextReferencedNote.Measure) <
                                  ScaledCurrentMeasure + VisibleMeasures;

            // current note on-screen + next note on-screen
            if (currentNoteVisible && nextNoteVisible && note.NextReferencedNote != null)
            {
                var nextNote = note.NextReferencedNote;
                var nextInfo = GetArc(chart, nextNote);

                // create arcs
                var arc1StartAngle = currentInfo.StartAngle;
                var arc1ArcLength = currentInfo.ArcAngle;
                var arc2StartAngle = nextInfo.StartAngle + nextInfo.ArcAngle;
                var arc2ArcLength = -nextInfo.ArcAngle;

                // crop arcs
                if (note.Size != 60)
                {
                    arc1StartAngle += 1.5f;
                    arc1ArcLength -= 3.0f;
                }
                if (note.NextReferencedNote.Size != 60)
                {
                    arc2StartAngle -= 1.5f;
                    arc2ArcLength += 3.0f;
                }

                var path = new SKPath();
                path.ArcTo(currentInfo.Rect, arc1StartAngle, arc1ArcLength, true);
                path.ArcTo(nextInfo.Rect, arc2StartAngle, arc2ArcLength, false);

                canvas.DrawPath(path, brushes.HoldFill);
            }

            // current note on-screen + previous note off-screen
            if (currentNoteVisible && !prevNoteVisible && note.PrevReferencedNote != null)
            {
                var previousInfo = GetArc(chart, note.PrevReferencedNote);

                // ratio between current note size and previous note
                var scaleRatio = (DrawRect.Width - currentInfo.Rect.Width) /
                                 (previousInfo.Rect.Width - currentInfo.Rect.Width);

                var currentStartAngle = currentInfo.StartAngle;
                var previousStartAngle = previousInfo.StartAngle;
                var previousArcAngle = previousInfo.ArcAngle;

                // crop off-screen arc
                if (note.PrevReferencedNote.Size != 60)
                {
                    previousStartAngle += 1.5f;
                    previousArcAngle -= 3.0f;
                }

                // handle angles rolling over
                if (Math.Abs(currentStartAngle - previousStartAngle) > 180)
                {
                    if (currentStartAngle > previousStartAngle)
                        currentStartAngle -= 360;
                    else
                        previousStartAngle -= 360;
                }

                // calculate new startAngle and arcAngle for intermediate arc
                var currentEndAngle = currentStartAngle - currentInfo.ArcAngle;
                var newEndAngle = scaleRatio * (previousStartAngle - previousArcAngle - (currentEndAngle)) +
                                  (currentEndAngle);

                var newStartAngle = scaleRatio * (previousStartAngle - currentStartAngle) + currentStartAngle;
                var newArcLength = newStartAngle - newEndAngle;

                // create arcs
                var arc1StartAngle = currentInfo.StartAngle;
                var arc1ArcLength = currentInfo.ArcAngle;
                var arc2StartAngle = newStartAngle + newArcLength;
                var arc2ArcLength = -newArcLength;

                // crop visible arc
                if (note.Size != 60)
                {
                    arc1StartAngle += 1.5f;
                    arc1ArcLength -= 3.0f;
                }

                var path = new SKPath();
                path.ArcTo(currentInfo.Rect, arc1StartAngle, arc1ArcLength, true);
                path.ArcTo(DrawRect, arc2StartAngle, arc2ArcLength, false);

                canvas.DrawPath(path, brushes.HoldFill);
            }

            // current note on-screen + next note off-screen
            if (currentNoteVisible && !nextNoteVisible && note.NextReferencedNote != null)
            {
                // create arcs
                var arc1StartAngle = currentInfo.StartAngle;
                var arc1ArcLength = currentInfo.ArcAngle;

                // crop arcs
                if (note.Size != 60)
                {
                    arc1StartAngle += 1.5f;
                    arc1ArcLength -= 3.0f;
                }

                var path = new SKPath();
                path.MoveTo(centerPoint);
                path.ArcTo(currentInfo.Rect, arc1StartAngle, arc1ArcLength, false);

                canvas.DrawPath(path, brushes.HoldFill);
            }

            // current note off-screen + next note off-screen
            if (!currentNoteVisible && !nextNoteVisible && note.NextReferencedNote != null)
            {
                var nextInfo = GetArc(chart, note.NextReferencedNote);

                // ratio between current note size and previous note
                var scaleRatio = (DrawRect.Width - nextInfo.Rect.Width) /
                                 (currentInfo.Rect.Width - nextInfo.Rect.Width);

                var currentStartAngle = nextInfo.StartAngle;
                var previousStartAngle = currentInfo.StartAngle;

                // handle angles rolling over
                if (Math.Abs(currentStartAngle - previousStartAngle) > 180)
                {
                    if (currentStartAngle > previousStartAngle)
                        currentStartAngle -= 360;
                    else
                        previousStartAngle -= 360;
                }

                // calculate new startAngle and arcAngle for intermediate arc
                var currentEndAngle = currentStartAngle - nextInfo.ArcAngle;
                var newEndAngle = scaleRatio * (previousStartAngle - currentInfo.ArcAngle - (currentEndAngle)) +
                                  (currentEndAngle);

                var newStartAngle = scaleRatio * (previousStartAngle - currentStartAngle) + currentStartAngle;
                var newArcLength = newStartAngle - newEndAngle;

                // create arcs
                var arc1StartAngle = newStartAngle + newArcLength;
                var arc1ArcLength = -newArcLength;

                // crop arcs to the right size
                if (note.Size != 60)
                {
                    arc1StartAngle -= 1.5f;
                    arc1ArcLength += 3.0f;
                }

                var path = new SKPath();
                path.MoveTo(centerPoint);
                path.ArcTo(DrawRect, arc1StartAngle, arc1ArcLength, false);

                canvas.DrawPath(path, brushes.HoldFill);
            }

            if (currentInfo.Rect.Width < 1 || note.Measure < CurrentMeasure) continue;

            var noteScale = currentInfo.NoteScale * noteScaleMultiplier;
            var fullStartAngle = currentInfo.StartAngle + 1.5f;
            var fullArcAngle = currentInfo.ArcAngle - 3.0f;

            // hold start notes
            if (note.NoteType is NoteType.HoldStartNoBonus or NoteType.HoldStartBonusFlair &&
                note.Measure >= CurrentMeasure)
            {
                if (note.IsFlair)
                    canvas.DrawArc(currentInfo.Rect, fullStartAngle, fullArcAngle, false,
                        brushes.GetFlairPen(noteScale));

                if (note.Size != 60)
                    DrawEndCaps(currentInfo.Rect, currentInfo.StartAngle, currentInfo.ArcAngle, noteScale);

                canvas.DrawArc(currentInfo.Rect, currentInfo.StartAngle, currentInfo.ArcAngle, false,
                    brushes.GetNotePen(note, noteScale));
            }

            // hold segments
            if (note.NoteType is NoteType.HoldJoint && !Playing)
            {
                canvas.DrawArc(currentInfo.Rect, fullStartAngle, fullArcAngle, false,
                    brushes.GetNotePen(note, noteScale));
            }

            // hold end notes
            if (note.NoteType is NoteType.HoldEnd)
            {
                var brush = Playing
                    ? brushes.GetHoldEndPen(note, noteScale)
                    : brushes.GetNotePen(note, noteScale);
                canvas.DrawArc(currentInfo.Rect, fullStartAngle, fullArcAngle, false,
                    brush);
            }

            // highlight selection
            if (highlightSelectedNote && selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                canvas.DrawArc(currentInfo.Rect, fullStartAngle, fullArcAngle, false,
                    brushes.GetHighlightPen(noteScale));

            if (multiSelectNotes.Count != 0 && multiSelectNotes.Contains(note))
                canvas.DrawArc(currentInfo.Rect, fullStartAngle, fullArcAngle, false, brushes.GetMultiSelectPen(currentInfo.NoteScale * noteScaleMultiplier));
        }
    }

    private void DrawEndCaps(SKRect rect, float start, float length, float noteScale)
    {
        var arc1Start = start - 0.1f;
        var arc2Start = start + length - 1.5f;
        const float arcLength = 1.6f;
        canvas.DrawArc(rect, arc1Start, arcLength, false, brushes.GetEndCapPen(noteScale));
        canvas.DrawArc(rect, arc2Start, arcLength, false, brushes.GetEndCapPen(noteScale));
    }

    private void DrawArrows(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
    {
        IEnumerable<Note> visibleNotes;
        if (userSettings.ViewSettings.ShowGimmickEffects)
        {
            visibleNotes = chart.Notes.Where(x =>
                (x.IsSlide || x.IsSnap) && x.Measure >= CurrentMeasure && chart.GetScaledMeasurePosition(x.Measure) <=
                ScaledCurrentMeasure + VisibleMeasures);
        }
        else
        {
            visibleNotes = chart.Notes.Where(x =>
                (x.IsSlide || x.IsSnap) && x.Measure >= CurrentMeasure &&
                x.Measure <= CurrentMeasure + VisibleMeasures);
        }

        ArrowMovementOffset %= 12;

        foreach (var note in visibleNotes)
        {
            var info = GetArc(chart, note);
            if (info.Rect.Width < 1) continue;

            int arrowDirection;
            switch (note.NoteType)
            {
                case NoteType.SlideOrangeNoBonus:
                case NoteType.SlideOrangeBonus:
                case NoteType.SlideOrangeBonusFlair:
                case NoteType.SnapRedNoBonus:
                case NoteType.SnapRedBonusFlair:
                    arrowDirection = 1;
                    break;
                case NoteType.SlideGreenNoBonus:
                case NoteType.SlideGreenBonus:
                case NoteType.SlideGreenBonusFlair:
                case NoteType.SnapBlueNoBonus:
                case NoteType.SnapBlueBonusFlair:
                    arrowDirection = -1;
                    break;
                default:
                    arrowDirection = 0;
                    break;
            }

            var radius = info.Rect.Width * 0.53f;

            // ======== Slide
            if (note.IsSlide)
            {
                const float slideRadiusOffset = 0.79f;
                const float slideArrowMinWidth = 0.03f;
                const float slideArrowMaxWidth = 0.075f;
                const float slideArrowMinLength = 1.75f;
                const float slideArrowMaxLength = 3.5f;
                const float slideArrowExtrude = 5.0f;

                var slideStartPoint = info.StartAngle - 12;
                var slideEndPoint = slideStartPoint + info.ArcAngle + 12;

                const int interval = 12;

                for (var i = slideStartPoint; i > slideEndPoint; i -= interval)
                {
                    var progress = arrowDirection != 1
                        ? (i - slideStartPoint) / (slideEndPoint - slideStartPoint)
                        : 1 - (i - slideStartPoint) / (slideEndPoint - slideStartPoint);
                    var scaledSlideArrowWidth = (1 - progress) * slideArrowMinWidth + progress * slideArrowMaxWidth;
                    var scaledSlideArrowLength = (1 - progress) * slideArrowMinLength + progress * slideArrowMaxLength;

                    var p1 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * (slideRadiusOffset + scaledSlideArrowWidth),
                        ArrowMovementOffset * arrowDirection + i - scaledSlideArrowLength * arrowDirection * 0.5f);

                    var p2 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * slideRadiusOffset,
                        ArrowMovementOffset * arrowDirection + i + scaledSlideArrowLength * arrowDirection);

                    var p3 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * (slideRadiusOffset - scaledSlideArrowWidth),
                        ArrowMovementOffset * arrowDirection + i - scaledSlideArrowLength * arrowDirection * 0.5f);

                    var p4 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * (slideRadiusOffset - scaledSlideArrowWidth),
                        ArrowMovementOffset * arrowDirection + i - scaledSlideArrowLength * arrowDirection * 0.5f -
                        slideArrowExtrude * arrowDirection);

                    var p5 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * slideRadiusOffset,
                        ArrowMovementOffset * arrowDirection + i + scaledSlideArrowLength * arrowDirection -
                        slideArrowExtrude * arrowDirection);

                    var p6 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * (slideRadiusOffset + scaledSlideArrowWidth),
                        ArrowMovementOffset * arrowDirection + i - scaledSlideArrowLength * arrowDirection * 0.5f -
                        slideArrowExtrude * arrowDirection);

                    var path = new SKPath();
                    path.MoveTo(p1);
                    path.LineTo(p2);
                    path.LineTo(p3);
                    path.LineTo(p4);
                    path.LineTo(p5);
                    path.LineTo(p6);
                    path.Close();

                    canvas.DrawPath(path, brushes.GetArrowPen(note));


                    if (highlightSelectedNote && selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                    {
                        canvas.DrawPath(path, brushes.GetHighlightPen(info.NoteScale));
                    }
                }
            }

            // ======== Snap
            if (note.IsSnap)
            {
                var snapRadiusOffset = arrowDirection == 1 ? 0.8f : 0.7f;
                var snapRowOffset = info.Rect.Width * 0.045f;
                const float snapArrowLength = 0.1f;
                const float snapArrowWidth = 3.0f;

                var snapStartPoint = (float) -note.Position * 6;
                var snapEndPoint = snapStartPoint + (float) -note.Size * 6;

                var snapArrowCount = MathF.Floor(note.Size / 3);
                var snapArrowInterval = (snapEndPoint - snapStartPoint) / snapArrowCount;
                var snapRadialOffset = snapArrowInterval * 0.5f;

                for (var i = snapStartPoint + snapRadialOffset; i > snapEndPoint; i += snapArrowInterval)
                {
                    var p1 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, radius * snapRadiusOffset,
                        i + snapArrowWidth);
                    var p2 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                    var p3 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, radius * snapRadiusOffset,
                        i - snapArrowWidth);

                    var p4 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        snapRowOffset + radius * snapRadiusOffset, i + snapArrowWidth);
                    var p5 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        snapRowOffset + radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                    var p6 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        snapRowOffset + radius * snapRadiusOffset, i - snapArrowWidth);

                    var path1 = new SKPath();
                    path1.MoveTo(p1);
                    path1.LineTo(p2);
                    path1.LineTo(p3);

                    var path2 = new SKPath();
                    path2.MoveTo(p4);
                    path2.LineTo(p5);
                    path2.LineTo(p6);

                    canvas.DrawPath(path1, brushes.GetNotePen(note, info.NoteScale));
                    canvas.DrawPath(path2, brushes.GetNotePen(note, info.NoteScale));

                    if (highlightSelectedNote && selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                    {
                        canvas.DrawPath(path1, brushes.GetHighlightPen(info.NoteScale));
                        canvas.DrawPath(path2, brushes.GetHighlightPen(info.NoteScale));
                    }
                }
            }
        }
    }
}