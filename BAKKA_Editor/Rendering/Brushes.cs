using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;
using BAKKA_Editor.Enums;
using SkiaSharp;

namespace BAKKA_Editor.Rendering
{
    internal class Brushes
    {
        // measure
        // beat
        // degree circle
        // mirroraxis
        // guide lines
        // background
        // mask

        // cursor

        // note
        // arrow
        // gimmick

        // bonus
        // flair
        // highlight

        // link
        // endcaps

        // hold area
        // hold end

        // A Brush can be either a pen or a fill.
        // A Pen is a brush that creates lines of a color.
        // A Fill is a brush that creates areas of a color.

        public UserSettings? userSettings;
        private float strokeWidthMultiplier = 1.0183333f;

        // ======= STATIC =======

        private static SKColor measurePenColor = SKColors.White;
        private static float measurePenStrokeWidth = 1.0f;
        public SKPaint MeasurePen = new SKPaint { Color = measurePenColor, StrokeWidth = measurePenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static SKColor beatPenColor = SKColors.White.WithAlpha(0x80);
        private static float beatPenStrokeWidth = 0.5f;
        public SKPaint BeatPen = new SKPaint { Color = beatPenColor, StrokeWidth = beatPenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static SKColor degreeCircleColor = SKColors.Black;
        private static float degreeCircleMajorPenStrokeWidth = 7.0f;
        private static float degreeCircleMediumPenStrokeWidth = 4.0f;
        private static float degreeCircleMinorPenStrokeWidth = 2.0f;
        public SKPaint DegreeCircleMajorPen = new SKPaint { Color = degreeCircleColor, StrokeWidth = degreeCircleMajorPenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };
        public SKPaint DegreeCircleMediumPen = new SKPaint { Color = degreeCircleColor, StrokeWidth = degreeCircleMediumPenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };
        public SKPaint DegreeCircleMinorPen = new SKPaint { Color = degreeCircleColor, StrokeWidth = degreeCircleMinorPenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static SKColor mirrorAxisPenColor = SKColors.Cyan;
        private static float mirrorAxisPenStrokeWidth = 0.5f;
        public SKPaint MirrorAxisPen = new SKPaint { Color = mirrorAxisPenColor, StrokeWidth = mirrorAxisPenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static float cursorPenStrokeWidth = 24.0f;
        public SKPaint CursorPen = new SKPaint { StrokeWidth = cursorPenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static SKColor[] guidelinePenColors = { SKColors.White.WithAlpha(0x20), SKColors.White.WithAlpha(0x00) };
        private static float guidelinePenStrokeWidth = 1.0f;
        public SKPaint GuideLinePen = new SKPaint { StrokeWidth = guidelinePenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static SKColor backgroundFillColorDark = new SKColor(68, 68, 68);
        private static SKColor backgroundFillColorLight = new SKColor(243, 243, 243);
        public SKPaint BackgroundBrush = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };

        private static SKColor maskFillColor = SKColors.Black.WithAlpha(90);
        public SKPaint MaskFill = new SKPaint { Color = maskFillColor, Style = SKPaintStyle.Fill, IsAntialias = false };

        // ======== NON-STATIC ========

        private static float notePenStrokeWidth = 8.0f;
        public SKPaint NotePen = new SKPaint { StrokeWidth = notePenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static float arrowPenStrokeWidth = 8.0f;
        public SKPaint ArrowPen = new SKPaint { StrokeWidth = arrowPenStrokeWidth, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static float gimmickPenStrokeWidth = 5.0f;
        public SKPaint GimmickPen = new SKPaint { StrokeWidth = gimmickPenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static SKColor bonusPenColor = SKColors.LightSkyBlue.WithAlpha(0x60);
        private static float bonusPenStrokeWidth = 15.0f;
        public SKPaint BonusPen = new SKPaint { Color = bonusPenColor, StrokeWidth = bonusPenStrokeWidth, StrokeCap = SKStrokeCap.Square, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static SKColor flairPenColor = SKColors.Khaki.WithAlpha(0xCC);
        private static float flairPenStrokeWidth = 18.0f;
        public SKPaint FlairPen = new SKPaint { Color = flairPenColor, StrokeWidth = flairPenStrokeWidth, StrokeCap = SKStrokeCap.Square, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static SKColor highlightPenColor = SKColors.LightPink.WithAlpha(0x80);
        private static float highlightPenStrokeWidth = 20.0f;
        public SKPaint HighlightPen = new SKPaint { Color = highlightPenColor, StrokeWidth = highlightPenStrokeWidth, StrokeCap = SKStrokeCap.Square, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static SKColor linkPenColor = SKColors.DeepSkyBlue.WithAlpha(0xDD);
        private static float linkPenStrokeWidth = 10.0f;
        public SKPaint LinkPen = new SKPaint { Color = linkPenColor, StrokeWidth = linkPenStrokeWidth, StrokeCap = SKStrokeCap.Square, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static SKColor endcapPenColor = SKColors.DeepSkyBlue;
        private static float endcapPenStrokeWidth = 8.0f;
        public SKPaint EndcapPen = new SKPaint { Color = endcapPenColor, StrokeWidth = endcapPenStrokeWidth, Style = SKPaintStyle.Stroke, IsAntialias = true };

        private static float holdEndPenStrokeWidth = 4.0f;
        public SKPaint HoldEndPen = new SKPaint { StrokeWidth = holdEndPenStrokeWidth, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke, IsAntialias = true };

        public SKPaint HoldFill = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };

        public void UpdateBrushStrokeWidth(float panelSize)
        {
            // update window size multiplier
            strokeWidthMultiplier = panelSize / 600.0f;

            // update "static" pens here when window is resized
            MeasurePen.StrokeWidth = measurePenStrokeWidth * strokeWidthMultiplier;
            BeatPen.StrokeWidth = beatPenStrokeWidth * strokeWidthMultiplier;
            DegreeCircleMajorPen.StrokeWidth = degreeCircleMajorPenStrokeWidth * strokeWidthMultiplier;
            DegreeCircleMediumPen.StrokeWidth = degreeCircleMediumPenStrokeWidth * strokeWidthMultiplier;
            DegreeCircleMinorPen.StrokeWidth = degreeCircleMinorPenStrokeWidth * strokeWidthMultiplier;
            MirrorAxisPen.StrokeWidth = mirrorAxisPenStrokeWidth * strokeWidthMultiplier;
            CursorPen.StrokeWidth = cursorPenStrokeWidth * strokeWidthMultiplier;
        }

        /*public void ModifyBrush(this SKPaint brush, SKColor? newColor, float? newStrokeWidth, SKStrokeCap? newStrokeCap)
        {
            brush.Color = newColor ?? brush.Color;
            brush.StrokeWidth = (newStrokeWidth ?? brush.StrokeWidth) * strokeWidthMultiplier;
            brush.StrokeCap = newStrokeCap ?? brush.StrokeCap;
        }*/

        // ======== STATIC ========
        public SKPaint GetCursorPen(NoteType noteType)
        {
            CursorPen.Color = NoteTypeToColor(noteType).WithAlpha(0x80);
            return CursorPen;
        }

        public SKPaint GetGuidelinePen(SKPoint startPoint, SKPoint endPoint)
        {
            var shader = SKShader.CreateLinearGradient(startPoint, endPoint, guidelinePenColors, SKShaderTileMode.Clamp);
            GuideLinePen.Shader = shader;
            return GuideLinePen;
        }

        // ======== NON-STATIC ========
        public SKPaint GetNotePen(Note note, float noteScaleMultiplier)
        {
            NotePen.Color = NoteTypeToColor(note.NoteType);
            NotePen.StrokeWidth = notePenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;

            return NotePen;
        }

        public SKPaint GetArrowPen(Note note, float noteScaleMultiplier)
        {
            ArrowPen.Color = NoteTypeToColor(note.NoteType);
            ArrowPen.StrokeWidth = arrowPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
            return ArrowPen;
        }

        public SKPaint GetGimmickPen(Gimmick gimmick, float noteScaleMultiplier)
        {
            GimmickPen.Color = Utils.GimmickTypeToColor(gimmick.GimmickType);
            GimmickPen.StrokeWidth = gimmickPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
            return GimmickPen;
        }
        
        public SKPaint GetBonusPen(float noteScaleMultiplier)
        {
            BonusPen.StrokeWidth = bonusPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
            return BonusPen;
        }

        public SKPaint GetFlairPen(float noteScaleMultiplier)
        {
            FlairPen.StrokeWidth = flairPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
            return FlairPen;
        }

        public SKPaint GetHighlightPen(float noteScaleMultiplier, bool round = false)
        {
            HighlightPen.StrokeWidth = highlightPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
            HighlightPen.StrokeCap = round ? SKStrokeCap.Round : SKStrokeCap.Square;
            return HighlightPen;
        }

        public SKPaint GetLinkPen(float noteScaleMultiplier)
        {
            LinkPen.StrokeWidth = linkPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
            return LinkPen;
        }

        public SKPaint GetEndcapPen(float noteScaleMultiplier)
        {
            EndcapPen.StrokeWidth = endcapPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
            return EndcapPen;
        }

        public SKPaint GetEndHoldPen(Note note, float noteScaleMultiplier)
        {
            HoldEndPen.Color = NoteTypeToColor(note.NoteType);
            HoldEndPen.StrokeWidth = holdEndPenStrokeWidth * strokeWidthMultiplier * noteScaleMultiplier;
            return HoldEndPen;
        }

        public SKPaint GetHoldFill(SKPoint center, float radius)
        {
            var gradientColor0 = userSettings != null ? SKColor.Parse(userSettings.ColorSettings.colorNoteHoldGradient0) : SKColors.Transparent;
            var gradientColor1 = userSettings != null ? SKColor.Parse(userSettings.ColorSettings.colorNoteHoldGradient1) : SKColors.Transparent;

            SKColor[] holdColors = { gradientColor0, gradientColor1 };

            var shader = SKShader.CreateRadialGradient(center, radius, holdColors, SKShaderTileMode.Clamp);
            HoldFill.Shader = shader;
            return HoldFill;
        }

        public SKPaint GetBackgroundFill(bool dark)
        {
            BackgroundBrush.Color = dark ? backgroundFillColorDark : backgroundFillColorLight;
            return BackgroundBrush;
        }

        public SKColor GetBackgroundColor(bool dark)
        {
            return dark ? backgroundFillColorDark : backgroundFillColorLight;
        }

        private SKColor NoteTypeToColor(NoteType type)
        {
            if (userSettings == null)
                return SKColors.Black;

            switch (type)
            {
                case NoteType.TouchNoBonus:
                case NoteType.TouchBonus:
                case NoteType.TouchBonusFlair:
                    return SKColor.Parse(userSettings.ColorSettings.colorNoteTap);
                case NoteType.SnapRedNoBonus:
                case NoteType.SnapRedBonusFlair:
                    return SKColor.Parse(userSettings.ColorSettings.colorNoteSnapFW);
                case NoteType.SnapBlueNoBonus:
                case NoteType.SnapBlueBonusFlair:
                    return SKColor.Parse(userSettings.ColorSettings.colorNoteSnapBW);
                case NoteType.SlideOrangeNoBonus:
                case NoteType.SlideOrangeBonus:
                case NoteType.SlideOrangeBonusFlair:
                    return SKColor.Parse(userSettings.ColorSettings.colorNoteSlideCW);
                case NoteType.SlideGreenNoBonus:
                case NoteType.SlideGreenBonus:
                case NoteType.SlideGreenBonusFlair:
                    return SKColor.Parse(userSettings.ColorSettings.colorNoteSlideCCW);
                case NoteType.HoldStartNoBonus:
                case NoteType.HoldStartBonusFlair:
                    return SKColor.Parse(userSettings.ColorSettings.colorNoteHoldStart);
                case NoteType.HoldJoint:
                case NoteType.HoldEnd:
                    return SKColor.Parse(userSettings.ColorSettings.colorNoteHoldSegment);
                case NoteType.Chain:
                case NoteType.ChainBonusFlair:
                    return SKColor.Parse(userSettings.ColorSettings.colorNoteChain);
                default:
                    return SKColors.Transparent;
            }
        }

    }
}
