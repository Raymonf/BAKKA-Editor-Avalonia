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
    private UserSettings userSettings;

    public RenderEngine(UserSettings settings)
    {
        brushes = new Brushes(settings);
        userSettings = settings;
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
        var scaledPosition = userSettings.ViewSettings.ShowGimmickEffects ? chart.GetScaledMeasurePosition(position) : position;
        var scaledCurrentMeasure = userSettings.ViewSettings.ShowGimmickEffects ? ScaledCurrentMeasure : CurrentMeasure;
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
            (float)(radius * Math.Cos(Utils.DegToRad(angle)) + x),
            (float)(radius * Math.Sin(Utils.DegToRad(angle)) + y));

        return point;
    }

    private void FillPie(SKPaint paint, SKRect rect, float startAngle, float sweepAngle)
    {
        canvas.DrawArc(rect, -startAngle, -sweepAngle, true, paint);
    }

    // ================ Rendering ================
    public void Render(Chart chart, NoteType currentNoteType, int selectedNoteIndex, int selectedGimmickIndex, bool isHoveringOverMirrorAxis, bool showCursor, int cursorStartAngle, int cursorArcAngle, int axis)
    {
        DrawBackground(userSettings.ViewSettings.DarkMode);
        DrawMaskEffect(chart, userSettings.ViewSettings.DarkMode);
        DrawGuideLines();
        DrawCircleDividers(chart);
        DrawDegreeCircle();
        if (isHoveringOverMirrorAxis) DrawMirrorAxis(axis);
        if (userSettings.ViewSettings.ShowGimmicks) DrawGimmickNotes(chart, userSettings.ViewSettings.HighlightViewedNote, selectedGimmickIndex);
        if (userSettings.ViewSettings.ShowMaskNotes) DrawMaskNotes(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex);
        DrawHolds(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex, userSettings.ViewSettings.NoteScaleMultiplier);
        if (userSettings.ViewSettings.ShowNoteLinks) DrawNoteLinks(chart);
        DrawNotes(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex, userSettings.ViewSettings.NoteScaleMultiplier);
        if (userSettings.ViewSettings.ShowSlideSnapArrows) DrawArrows(chart, userSettings.ViewSettings.HighlightViewedNote, selectedNoteIndex);
        if (showCursor) DrawCursor(currentNoteType, cursorStartAngle, cursorArcAngle);
    }

    // ==== UI
    public void DrawBackground(bool isDarkMode)
    {
        canvas.Clear(Brushes.GetBackgroundColor(isDarkMode));
    }

    public void DrawMaskEffect(Chart chart, bool darkMode)
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

    public void DrawCircleDividers(Chart chart)
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

    public void DrawMirrorAxis(int axis)
    {
        var axisAngle = -axis * 3;

        var startPoint = new SKPoint(
                (float)(CanvasRadius * Math.Cos(Utils.DegToRad(axisAngle)) + CanvasCenterPoint.X),
                (float)(CanvasRadius * Math.Sin(Utils.DegToRad(axisAngle)) + CanvasCenterPoint.Y));

        var endPoint = new SKPoint(
                (float)(CanvasRadius * Math.Cos(Utils.DegToRad(axisAngle + 180)) + CanvasCenterPoint.X),
                (float)(CanvasRadius * Math.Sin(Utils.DegToRad(axisAngle + 180)) + CanvasCenterPoint.Y));

        canvas.DrawLine(startPoint, endPoint, brushes.MirrorAxisPen);
    }

    public void DrawCursor(NoteType currentNoteType, float startAngle, float arcAngle)
    {
        canvas.DrawArc(DrawRect, -startAngle * 6.0f, -arcAngle * 6.0f, false, brushes.GetCursorPen(currentNoteType));
    }

    // ==== NOTES
    public void DrawGimmickNotes(Chart chart, bool highlightSelectedNote, int selectedGimmickIndex)
    {
        var visibleGimmicks = userSettings.ViewSettings.ShowGimmickEffects ?
            chart.Gimmicks.Where(x => x.Measure >= CurrentMeasure && chart.GetScaledMeasurePosition(x.Measure) <= ScaledCurrentMeasure + VisibleMeasures) :
            chart.Gimmicks.Where(x => x.Measure >= CurrentMeasure && x.Measure <= CurrentMeasure + VisibleMeasures);

        foreach (var gimmick in visibleGimmicks)
        {
            var info = GetRect(chart, gimmick.Measure);
            if (info.Rect.Width < 1) continue;

            canvas.DrawOval(info.Rect, brushes.GetGimmickPen(gimmick, 1));

            if (highlightSelectedNote && selectedGimmickIndex != -1 && gimmick == chart.Gimmicks[selectedGimmickIndex])
                canvas.DrawOval(info.Rect, brushes.GetHighlightPen(1));
        }
    }

    public void DrawMaskNotes(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
    {

    }

    public void DrawNotes(Chart chart, bool highlightSelectedNote, int selectedNoteIndex, float noteScaleMultiplier)
    {
        var visibleNotes = userSettings.ViewSettings.ShowGimmickEffects ?
            chart.Notes.Where(x => !x.IsHold && x.Measure >= CurrentMeasure && chart.GetScaledMeasurePosition(x.Measure) <= ScaledCurrentMeasure + VisibleMeasures) :
            chart.Notes.Where(x => !x.IsHold && x.Measure >= CurrentMeasure && x.Measure <= CurrentMeasure + VisibleMeasures);

        foreach (var note in visibleNotes)
        {
            var info = GetArc(chart, note);
            var fullStartAngle = info.StartAngle + 1.5f;
            var fullArcAngle = info.ArcAngle - 3.0f;

            if (info.Rect.Width < 1) continue;

            // save another note list lookup and just draw masks here
            if (note.IsMask)
            {
                canvas.DrawArc(info.Rect, info.StartAngle + 6, info.ArcAngle - 12, false, brushes.GetNotePen(note, info.NoteScale * noteScaleMultiplier));
                if (highlightSelectedNote && selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                    canvas.DrawArc(info.Rect, info.StartAngle + 6, info.ArcAngle - 12, false, brushes.GetHighlightPen(info.NoteScale * noteScaleMultiplier));

                continue;
            }

            if (note.IsBonus)
                canvas.DrawArc(info.Rect, fullStartAngle, fullArcAngle, false, brushes.GetBonusPen(info.NoteScale * noteScaleMultiplier));

            if (note.IsFlair)
                canvas.DrawArc(info.Rect, fullStartAngle, fullArcAngle, false, brushes.GetFlairPen(info.NoteScale * noteScaleMultiplier));

            if (note.Size != 60)
            {
                DrawEndCaps(info.Rect, info.StartAngle, info.ArcAngle, info.NoteScale * noteScaleMultiplier);
                canvas.DrawArc(info.Rect, info.StartAngle, info.ArcAngle, false, brushes.GetNotePen(note, info.NoteScale * noteScaleMultiplier));
            }
            else
            {
                canvas.DrawOval(info.Rect, brushes.GetNotePen(note, info.NoteScale * noteScaleMultiplier));
            }

            if (highlightSelectedNote && selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                canvas.DrawArc(info.Rect, fullStartAngle, fullArcAngle, false, brushes.GetHighlightPen(info.NoteScale * noteScaleMultiplier));
        }
    }
    
    public void DrawHolds(Chart chart, bool highlightSelectedNote, int selectedNoteIndex, float noteScaleMultiplier)
    {

    }

    public void DrawEndCaps(SKRect rect, float start, float length, float noteScale)
    {
        var arc1Start = start - 0.1f;
        var arc2Start = start + length - 1.5f;
        const float arcLength = 1.6f;
        canvas.DrawArc(rect, arc1Start, arcLength, false, brushes.GetEndCapPen(noteScale));
        canvas.DrawArc(rect, arc2Start, arcLength, false, brushes.GetEndCapPen(noteScale));
    }

    public void DrawArrows(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
    {
        var visibleNotes = userSettings.ViewSettings.ShowGimmickEffects ?
            chart.Notes.Where(x => (x.IsSlide || x.IsSnap) && x.Measure >= CurrentMeasure && chart.GetScaledMeasurePosition(x.Measure) <= ScaledCurrentMeasure + VisibleMeasures) :
            chart.Notes.Where(x => (x.IsSlide || x.IsSnap) && x.Measure >= CurrentMeasure && x.Measure <= CurrentMeasure + VisibleMeasures);

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
                const float slideArrowMinWidth = 0.04f;
                const float slideArrowMaxWidth = 0.075f;
                const float slideArrowLength = 3.5f;
                const float slideArrowExtrude = 5.0f;

                var slideStartPoint = info.StartAngle - 9;
                var slideEndPoint = slideStartPoint + info.ArcAngle + 12;

                const int interval = 12;

                for (var i = slideStartPoint; i > slideEndPoint; i -= interval)
                {
                    var progress = arrowDirection != 1 ? (i - slideStartPoint) / (slideEndPoint - slideStartPoint) : 1 - (i - slideStartPoint) / (slideEndPoint - slideStartPoint);
                    var scaledArrowWidth = (1 - progress) * slideArrowMinWidth + progress * slideArrowMaxWidth;

                    var p1 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * (slideRadiusOffset + scaledArrowWidth),
                        ArrowMovementOffset * arrowDirection + i - slideArrowLength * arrowDirection * 0.5f);

                    var p2 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * slideRadiusOffset,
                        ArrowMovementOffset * arrowDirection + i + slideArrowLength * arrowDirection);

                    var p3 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * (slideRadiusOffset - scaledArrowWidth),
                        ArrowMovementOffset * arrowDirection + i - slideArrowLength * arrowDirection * 0.5f);

                    var p4 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * (slideRadiusOffset - scaledArrowWidth),
                        ArrowMovementOffset * arrowDirection + i - slideArrowLength * arrowDirection * 0.5f - slideArrowExtrude * arrowDirection);

                    var p5 = GetPointOnArc(
                        CanvasCenterPoint.X, CanvasCenterPoint.Y,
                        radius * slideRadiusOffset,
                        ArrowMovementOffset * arrowDirection + i + slideArrowLength * arrowDirection - slideArrowExtrude * arrowDirection);
                    
                    var p6 = GetPointOnArc(
                        CanvasCenterPoint.X,CanvasCenterPoint.Y,
                        radius * (slideRadiusOffset + scaledArrowWidth),
                        ArrowMovementOffset * arrowDirection + i - slideArrowLength * arrowDirection * 0.5f - slideArrowExtrude * arrowDirection);

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

                var snapStartPoint = (float)-note.Position * 6;
                var snapEndPoint = snapStartPoint + (float)-note.Size * 6;

                var snapArrowCount = MathF.Floor(note.Size / 3);
                var snapArrowInterval = (snapEndPoint - snapStartPoint) / snapArrowCount;
                var snapRadialOffset = snapArrowInterval * 0.5f;

                for (var i = snapStartPoint + snapRadialOffset; i > snapEndPoint; i += snapArrowInterval)
                {
                    var p1 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, radius * snapRadiusOffset, i + snapArrowWidth);
                    var p2 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                    var p3 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, radius * snapRadiusOffset, i - snapArrowWidth);
                                                                
                    var p4 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, snapRowOffset + radius * snapRadiusOffset, i + snapArrowWidth);
                    var p5 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, snapRowOffset + radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                    var p6 = GetPointOnArc(CanvasCenterPoint.X, CanvasCenterPoint.Y, snapRowOffset + radius * snapRadiusOffset, i - snapArrowWidth);

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

    public void DrawNoteLinks(Chart chart)
    {

    }
}
