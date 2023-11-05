using System;
using System.Linq;

namespace BAKKA_Editor;

internal partial class SkCircleView
{
    public void DrawCircle(Chart chart)
    {
        // Draw measure circle
        var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
        for (var meas = (float) Math.Ceiling(CurrentMeasure);
             meas - CurrentMeasure < totalMeasureShowNotes;
             meas += 1.0f)
        {
            var info = GetScaledRect(chart, meas);
            if (info.Rect.Width >= 1) canvas.DrawOval(info.Rect, Brushes.BeatPen);
        }
    }

    public void DrawGimmicks(Chart chart, bool showGimmicks, int selectedGimmickIndex)
    {
        if (showGimmicks)
        {
            var totalMeasureShowNotes = GetTotalMeasureShowNotes2(chart);
            var drawGimmicks = chart.Gimmicks.Where(
                x => x.Measure >= CurrentMeasure
                     && x.Measure <= CurrentMeasure + totalMeasureShowNotes);

            foreach (var gimmick in drawGimmicks)
            {
                var info = GetScaledRect(chart, gimmick.Measure);

                if (info.Rect.Width >= 1) canvas.DrawOval(info.Rect, Brushes.GetGimmickPen(gimmick, info.NoteScale));
            }
        }
    }
}