using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using SkiaSharp;
using System.Drawing;
using System.Reflection.PortableExecutable;
using System.IO;
using BAKKA_Editor.ViewModels;

namespace BAKKA_Editor
{
    internal struct SkArcInfo
    {
        public float StartAngle;
        public float ArcLength;
        public SKRect Rect;
        public float NoteScale;
    }

    internal class SkCircleView
    {
        public SizeF PanelSize { get; private set; }
        public SKRect DrawRect { get; private set; }
        public PointF TopCorner { get; private set; }
        public PointF CenterPoint { get; private set; }
        public float Radius { get; private set; }
        public float CurrentMeasure { get; set; }
        public float Hispeed { get; set; } = 1.5f;
        public bool showHispeed { get; set; } = true;

        // Pens and Brushes
        public SKPaint? BeatPen { get; set; }
        public SKPaint? TickMinorPen { get; set; }
        public SKPaint? TickMediumPen { get; set; }
        public SKPaint? TickMajorPen { get; set; }
        public SKPaint? HoldBrush { get; set; } = new SKPaint()
        {
            IsAntialias = true,
            Color = SKColors.Yellow.WithAlpha(170),
            Style = SKPaintStyle.Fill
        };
        public SKPaint MaskBrush { get; set; } = Utils.CreateFillBrush(SKColors.Black.WithAlpha(90), false);
        public SKPaint? BackgroundBrush { get; set; }
        public SKPaint? HighlightPen { get; set; }
        public SKPaint? FlairPen { get; set; }
        private int CursorTransparency = 110;
        private int SelectTransparency = 110;
        private int FlairTransparency = 75;

        // Graphics.
        // BufferedGraphics bufGraphics;
        SKCanvas canvas;

        // Mouse information. Public so other GUI elements can be updated with their values.
        public int mouseDownPos = -1;
        public Point mouseDownPt;
        public int lastMousePos = -1;
        public bool rolloverPos = false;
        public bool rolloverNeg = false;

        public SkCircleView(SizeF size)
        {
            Update(size);
        }

        public void SetCanvas(SKCanvas canvas)
        {
            this.canvas = canvas;
        }

        public void Update(SizeF size)
        {
            PanelSize = size;
            float basePenWidth = PanelSize.Width * 4.0f / 600.0f;
            TopCorner = new PointF(basePenWidth * 4, basePenWidth * 4);
            DrawRect = new SKRect(
                TopCorner.X,
                TopCorner.Y,
                PanelSize.Width - basePenWidth * 8,
                PanelSize.Height - basePenWidth * 8);
            Radius = DrawRect.Width / 2.0f;
            CenterPoint = new PointF(TopCorner.X + Radius, TopCorner.Y + Radius);

            // Pens
            BeatPen = Utils.CreateStrokeBrush(SKColors.White, PanelSize.Width * 1.0f / 600.0f);
            TickMinorPen = Utils.CreateStrokeBrush(SKColors.Black, PanelSize.Width * 2.0f / 600.0f);
            TickMediumPen = Utils.CreateStrokeBrush(SKColors.Black, PanelSize.Width * 4.0f / 600.0f);
            TickMajorPen = Utils.CreateStrokeBrush(SKColors.Black, PanelSize.Width * 7.0f / 600.0f);
            HighlightPen = Utils.CreateStrokeBrush(SKColors.LightPink.WithAlpha((byte)SelectTransparency), PanelSize.Width * 8.0f / 600.0f);
            FlairPen = Utils.CreateStrokeBrush(SKColors.Yellow.WithAlpha((byte)FlairTransparency), PanelSize.Width * 8.0f / 600.0f);
        }

        private float GetTotalMeasureShowNotes(Chart chart)
        {
            //Convert hispeed to frames
            float displayFrames = 73.0f - ((Hispeed - 1.5f) * 10.0f);
            float tempTotalTime = ((displayFrames / 60.0f) * 1000.0f);
            float currentTime = chart.GetTime(new BeatInfo(CurrentMeasure));
            float tempEndTime = currentTime + tempTotalTime;
            if (showHispeed)
            {
                //Account for hispeed gimmick
                List<Gimmick> HispeedChanges = new List<Gimmick>();
                Gimmick? InitialSpeed = chart.Gimmicks.LastOrDefault(x => x.GimmickType == GimmickType.HiSpeedChange && CurrentMeasure > x.Measure);
                //Add initial hispeed to list
                if (InitialSpeed == null)
                {
                    InitialSpeed = new Gimmick();
                    InitialSpeed.HiSpeed = 1.0;
                }
                HispeedChanges.Add(InitialSpeed);
                //Random todos that im too lazy to put in applicable locations
                //TODO: add "time" to notes so we can compare against it every time instead of always calculating what time a note is at
                //TODO: add function to reevaluate the time of every note when bpm or TS is added/removed.

                //add all hispeed changes to list that happen within the current total time to show notes
                HispeedChanges.AddRange(chart.Gimmicks.Where(
                    x => x.Measure >= CurrentMeasure
                    && chart.GetTime(new BeatInfo(x.Measure)) < tempEndTime
                    && x.GimmickType == GimmickType.HiSpeedChange).ToList());
                if (HispeedChanges.Count > 1)
                {
                    for (int i = 0; i < HispeedChanges.Count; i++)
                    {
                        float timeDiff;
                        float itemTime = chart.GetTime(HispeedChanges[i].BeatInfo);
                        float modifiedTime;
                        if (itemTime <= (tempTotalTime + currentTime))
                        {
                            if (i == 0)
                                itemTime = currentTime;

                            if (i != HispeedChanges.Count - 1)
                            {
                                float tempTestITimeDiff = (currentTime + tempTotalTime) - itemTime;
                                float tempTestIModifiedTime = (tempTestITimeDiff) / (float)(HispeedChanges[i].HiSpeed);
                                if ((currentTime + tempTotalTime - tempTestITimeDiff + tempTestIModifiedTime) < chart.GetTime(HispeedChanges[i + 1].BeatInfo))
                                {
                                    timeDiff = (currentTime + tempTotalTime) - itemTime;
                                    modifiedTime = timeDiff / (float)HispeedChanges[i].HiSpeed;
                                }
                                else
                                {
                                    timeDiff = chart.GetTime(HispeedChanges[i + 1].BeatInfo) - itemTime;
                                    modifiedTime = timeDiff / (float)HispeedChanges[i].HiSpeed;
                                }
                            }
                            else
                            {
                                timeDiff = (currentTime + tempTotalTime) - itemTime;
                                modifiedTime = timeDiff / (float)HispeedChanges[i].HiSpeed;
                            }
                            tempTotalTime = tempTotalTime - timeDiff + modifiedTime;
                        }
                    }
                }
                else
                {
                    tempTotalTime /= (float)HispeedChanges[0].HiSpeed;
                }
                tempEndTime = currentTime + tempTotalTime;
            }
            //convert total time to total measure
            BeatInfo EndMeasure = chart.GetBeat(tempEndTime);
            return EndMeasure.MeasureDecimal - CurrentMeasure;
        }

        private float GetNoteScaleFromMeasure(Chart chart, float objectTime)
        {
            // Scale from 0-1
            float objectTimeAsTime      = chart.GetTime(new BeatInfo(objectTime));
            float currentTime           = chart.GetTime(new BeatInfo(CurrentMeasure));
            float EndTimeShowNotes    = chart.GetTime(new BeatInfo(CurrentMeasure + GetTotalMeasureShowNotes(chart)));
            float notescaleInit;
            var LatestHispeedChange = chart.Gimmicks.Where(x => x.GimmickType == GimmickType.HiSpeedChange && CurrentMeasure >= x.Measure).LastOrDefault();
            if (LatestHispeedChange != null && LatestHispeedChange.HiSpeed < 0.0)
            {
                //Reverse
                notescaleInit = (objectTimeAsTime - currentTime) / (EndTimeShowNotes - currentTime);
            }
            else
            {
                //Normal
                notescaleInit = 1 - ((objectTimeAsTime - currentTime) / (EndTimeShowNotes - currentTime));
            }
            //Scale math
            notescaleInit = 0.001f + (float)Math.Pow(notescaleInit, 3.0f) - (0.501f * (float)Math.Pow(notescaleInit, 2.0f)) + (0.5f * notescaleInit);
            return notescaleInit;
        }
        
        private SkArcInfo GetScaledRect(Chart chart, float objectTime)
        {
            SkArcInfo info = new();
            float notescaleInit = GetNoteScaleFromMeasure(chart, objectTime);
            info.NoteScale = GetNoteScaleFromMeasure(chart, objectTime);
            float scaledRectSize = DrawRect.Width * info.NoteScale;
            float scaledRadius = scaledRectSize / 2.0f;
            info.Rect = SKRect.Create(
                    CenterPoint.X - scaledRadius,
                    CenterPoint.Y - scaledRadius,
                    scaledRectSize,
                    scaledRectSize);
            return info;
        }

        private SkArcInfo GetSkArcInfo(Chart chart, Note note)
        {
            SkArcInfo info = GetScaledRect(chart,note.Measure);
            info.StartAngle = -note.Position * 6;
            info.ArcLength = -note.Size * 6;
            if (Math.Abs(info.ArcLength - (-360)) > 0.00001)
            {
                info.StartAngle -= 2;
                info.ArcLength += 4;
            }
            return info;
        }

        // Updates the mouse down position within the circle, and returns the new position.
        public void UpdateMouseDown(float xCen, float yCen, Point mousePt)
        {
            float theta = (float)(Math.Atan2(yCen, xCen) * 180.0f / Math.PI);
            if (theta < 0)
                theta += 360.0f;
            // Left click moves the cursor
            mouseDownPos = (int)(theta / 6.0f);
            mouseDownPt = mousePt;
            lastMousePos = -1;
            rolloverPos = false;
            rolloverNeg = false;
        }

        public void UpdateMouseUp()
        {
            if (mouseDownPos <= -1)
            {
                return;
            }

            // Reset position and point.
            mouseDownPos = -1;
            mouseDownPt = new Point();
        }

        /// <summary>
        /// Creates the background brush with the passed in color if it's null.
        /// </summary>
        /// <param name="color">SKColor</param>
        public void DrawBackground(SKColor color)
        {
            if (BackgroundBrush == null)
                // Remember background color for drawing later
                BackgroundBrush = Utils.CreateFillBrush(color, false);
            canvas.Clear(color);
        }
        
        // Updates the mouse position and returns the new position in degrees.
        public int UpdateMouseMove(float xCen, float yCen)
        {
            float thetaCalc = (float)(Math.Atan2(yCen, xCen) * 180.0f / Math.PI);
            if (thetaCalc < 0)
                thetaCalc += 360.0f;
            int theta = (int)(thetaCalc / 6.0f);

            int delta = theta - lastMousePos;
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
                            if (rem.NoteType == NoteType.MaskRemove && rem.Position == mask.Position && rem.Size == mask.Size)
                            {
                                if (rem.Measure >= mask.Measure)
                                {
                                    shouldDraw = false;
                                    break;
                                }
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
            // Draw measure circle
            for (float meas = (float)Math.Ceiling(CurrentMeasure); (meas - CurrentMeasure) < GetTotalMeasureShowNotes(chart); meas += 1.0f)
            {
                var info = GetScaledRect(chart,meas);
                if (info.Rect.Width >= 1)
                {
                    canvas.DrawOval(info.Rect, BeatPen);
                }
            }

            // Draw base circle
            canvas.DrawOval(DrawRect, TickMediumPen);
        }

        public void DrawDegreeLines()
        {
            for (int i = 0; i < 360; i += 6)
            {
                SKPoint startPoint = new SKPoint(
                   (float) (Radius * Math.Cos(Utils.DegToRad(i)) + CenterPoint.X),
                   (float) (Radius * Math.Sin(Utils.DegToRad(i)) + CenterPoint.Y));
                float tickLength = PanelSize.Width * 10.0f / 285.0f;
                float innerRad = Radius - tickLength;
                SKPaint activePen;
                if (i % 90 == 0)
                {
                    innerRad = Radius - (tickLength * 3.5f);
                    activePen = TickMajorPen;
                }
                else if (i % 30 == 0)
                {
                    innerRad = Radius - (tickLength * 2.5f);
                    activePen = TickMediumPen;
                }
                else
                {
                    activePen = TickMinorPen;
                }
                SKPoint endPoint = new SKPoint(
                    (float) (innerRad * Math.Cos(Utils.DegToRad(i)) + CenterPoint.X),
                   (float) (innerRad * Math.Sin(Utils.DegToRad(i)) + CenterPoint.Y));

                canvas.DrawLine(startPoint, endPoint, activePen);
            }
        }

        public void DrawGimmicks(Chart chart, bool showGimmicks, int selectedGimmickIndex)
        {
            if (showGimmicks)
            {
                List<Gimmick> drawGimmicks = chart.Gimmicks.Where(
                    x => x.Measure >= CurrentMeasure
                         && x.Measure <= (CurrentMeasure + GetTotalMeasureShowNotes(chart))).ToList();

                foreach (var gimmick in drawGimmicks)
                {
                    var info = GetScaledRect(chart,gimmick.Measure);

                    if (info.Rect.Width >= 1)
                    {
                        canvas.DrawOval(info.Rect, GetNotePaint(gimmick));
                    }
                }
            }
        }
        
        public void DrawHolds(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
        {
            SkArcInfo currentInfo = GetScaledRect(chart, CurrentMeasure);
            SkArcInfo endInfo = GetScaledRect(chart, CurrentMeasure + GetTotalMeasureShowNotes(chart));

            // First, draw holes that start before the viewpoint and have nodes that end after
            List<Note> holdNotes = chart.Notes.Where(
                x => x.Measure < CurrentMeasure
                && x.NextNote != null
                && x.NextNote.Measure > (CurrentMeasure + GetTotalMeasureShowNotes(chart))
                && x.IsHold).ToList();
            foreach (var note in holdNotes)
            {
                SkArcInfo info = GetSkArcInfo(chart,note);
                SkArcInfo nextInfo = GetSkArcInfo(chart, (Note)note.NextNote);
                //GraphicsPath path = new GraphicsPath();
                //path.AddArc(endInfo.Rect, info.StartAngle, info.ArcLength);
                //path.AddArc(currentInfo.Rect, info.StartAngle + info.ArcLength, -info.ArcLength);
                //bufGraphics.Graphics.FillPath(HoldBrush, path);

                float ratio = (currentInfo.Rect.Width - nextInfo.Rect.Width) / (info.Rect.Width - nextInfo.Rect.Width);
                float startNoteAngle = nextInfo.StartAngle;
                float endNoteAngle = info.StartAngle;
                if (nextInfo.StartAngle > info.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                {
                    startNoteAngle -= 360;
                }
                else if (info.StartAngle > nextInfo.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                {
                    endNoteAngle -= 360;
                }
                float startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
                float endAngle = ratio * ((endNoteAngle - info.ArcLength) - (startNoteAngle - nextInfo.ArcLength)) +
                    (startNoteAngle - nextInfo.ArcLength);
                float arcLength = startAngle - endAngle;

                float ratio2 = (endInfo.Rect.Width - nextInfo.Rect.Width) / (info.Rect.Width - nextInfo.Rect.Width);
                float startNoteAngle2 = nextInfo.StartAngle;
                float endNoteAngle2 = info.StartAngle;
                if (nextInfo.StartAngle > info.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                {
                    startNoteAngle2 -= 360;
                }
                else if (info.StartAngle > nextInfo.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                {
                    endNoteAngle2 -= 360;
                }
                float startAngle2 = ratio2 * (endNoteAngle2 - startNoteAngle2) + startNoteAngle2;
                float endAngle2 = ratio2 * ((endNoteAngle2 - info.ArcLength) - (startNoteAngle2 - nextInfo.ArcLength)) +
                    (startNoteAngle2 - nextInfo.ArcLength);
                float arcLength2 = startAngle2 - endAngle2;

                var p = new SKPath();
                p.ArcTo(currentInfo.Rect, startAngle, arcLength, true);
                p.ArcTo(endInfo.Rect, startAngle2 + arcLength2, -arcLength2, false);
                canvas.DrawPath(p, HoldBrush);

                /*GraphicsPath path = new GraphicsPath();
                path.AddArc(currentInfo.Rect, startAngle, arcLength);
                path.AddArc(endInfo.Rect, startAngle2 + arcLength2, -arcLength2);
                draw.FillPath(HoldBrush, path);*/
            }

            // Second, draw all the notes on-screen
            holdNotes = chart.Notes.Where(
            x => x.Measure >= CurrentMeasure 
                 && x.Measure <= (CurrentMeasure + GetTotalMeasureShowNotes(chart))
                 && x.IsHold).ToList();
            foreach (var note in holdNotes)
            {
                SkArcInfo info = GetSkArcInfo(chart, note);

                // If the previous note is off-screen, this case handles that
                if (note.PrevNote != null && note.PrevNote.Measure < CurrentMeasure)
                {
                    SkArcInfo prevInfo = GetSkArcInfo(chart, (Note)note.PrevNote);
                    float ratio = (currentInfo.Rect.Width - info.Rect.Width) / (prevInfo.Rect.Width - info.Rect.Width);
                    float startNoteAngle = info.StartAngle;
                    float endNoteAngle = prevInfo.StartAngle;
                    if (info.StartAngle > prevInfo.StartAngle && (Math.Abs(info.StartAngle - prevInfo.StartAngle) > 180))
                    {
                        startNoteAngle -= 360;
                    }
                    else if (prevInfo.StartAngle > info.StartAngle && (Math.Abs(info.StartAngle - prevInfo.StartAngle) > 180))
                    {
                        endNoteAngle -= 360;
                    }
                    float startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
                    float endAngle = ratio * ((endNoteAngle - prevInfo.ArcLength) - (startNoteAngle - info.ArcLength)) +
                        (startNoteAngle - info.ArcLength);
                    float arcLength = startAngle - endAngle;

                    var p = new SKPath();
                    p.ArcTo(info.Rect, info.StartAngle, info.ArcLength, true);
                    p.ArcTo(currentInfo.Rect, startAngle + arcLength, -arcLength, false);
                    canvas.DrawPath(p, HoldBrush);
                }

                // If the next note is on-screen, this case handles that
                if (note.NextNote != null && note.NextNote.Measure <= (CurrentMeasure + GetTotalMeasureShowNotes(chart)))
                {
                    SkArcInfo nextInfo = GetSkArcInfo(chart, note.NextNote);
                    var p = new SKPath();
                    p.ArcTo(info.Rect, info.StartAngle, info.ArcLength, true);
                    p.ArcTo(nextInfo.Rect, nextInfo.StartAngle + nextInfo.ArcLength, -nextInfo.ArcLength, false);
                    canvas.DrawPath(p, HoldBrush);
                }

                // If the next note is off-screen, this case handles that
                if (note.NextNote != null && note.NextNote.Measure > (CurrentMeasure + GetTotalMeasureShowNotes(chart)))
                {
                    SkArcInfo nextInfo = GetSkArcInfo(chart, note.NextNote);
                    float ratio = (endInfo.Rect.Width - nextInfo.Rect.Width) / (info.Rect.Width - nextInfo.Rect.Width);
                    float startNoteAngle = nextInfo.StartAngle;
                    float endNoteAngle = info.StartAngle;
                    if (nextInfo.StartAngle > info.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                    {
                        startNoteAngle -= 360;
                    }
                    else if (info.StartAngle > nextInfo.StartAngle && (Math.Abs(nextInfo.StartAngle - info.StartAngle) > 180))
                    {
                        endNoteAngle -= 360;
                    }
                    float startAngle = ratio * (endNoteAngle - startNoteAngle) + startNoteAngle;
                    float endAngle = ratio * ((endNoteAngle - info.ArcLength) - (startNoteAngle - nextInfo.ArcLength)) +
                        (startNoteAngle - nextInfo.ArcLength);
                    float arcLength = startAngle - endAngle;

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
                    {
                        if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        {
                            canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, HighlightPen);
                        }
                    }
                }
            }
        }

        public void DrawNotes(Chart chart, bool highlightSelectedNote, int selectedNoteIndex)
        {
            List<Note> drawNotes = chart.Notes.Where(
            x => x.Measure >= CurrentMeasure
            && x.Measure <= (CurrentMeasure + GetTotalMeasureShowNotes(chart))
            && !x.IsHold && !x.IsMask).ToList();
            foreach (var note in drawNotes)
            {
                SkArcInfo info = GetSkArcInfo(chart,note);

                if (info.Rect.Width >= 1)
                {
                    canvas.DrawArc(info.Rect, info.StartAngle, info.ArcLength, false, GetNotePaint(note, info.NoteScale));
                    if (note.IsFlair)
                    {
                        canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, FlairPen);
                    }
                    // Plot highlighted
                    if (highlightSelectedNote)
                    {
                        if (selectedNoteIndex != -1 && note == chart.Notes[selectedNoteIndex])
                        {
                            canvas.DrawArc(info.Rect, info.StartAngle + 2, info.ArcLength - 4, false, HighlightPen);
                        }
                    }
                }
            }
        }

        public void DrawCursor(NoteType noteType, float startAngle, float sweepAngle)
        {
            canvas.DrawArc(DrawRect, -(float)startAngle * 6.0f,
                -(float)sweepAngle * 6.0f,
                false,
                GetNotePaint(noteType)
            );
        }

        private SKPaint GetNotePaint(Note note, float noteScale = 1.0f)
        {
            return new SKPaint
            {
                IsAntialias = true,
                Color = note.Color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 8.0f * noteScale / 600.0f
            };
        }

        private SKPaint GetNotePaint(Gimmick gimmick, float noteScale = 1.0f)
        {
            return new SKPaint
            {
                IsAntialias = true,
                Color = Utils.GimmickTypeToColor(gimmick.GimmickType),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 8.0f * noteScale / 600.0f
            };
        }

        private SKPaint GetNotePaint(NoteType noteType)
        {
            var color = Utils.NoteTypeToColor(noteType);
            return new SKPaint
            {
                IsAntialias = true,
                Color = color.WithAlpha((byte)CursorTransparency),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = PanelSize.Width * 24.0f / 600.0f
            };
        }
    }
}
