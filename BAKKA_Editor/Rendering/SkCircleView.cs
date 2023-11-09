using System;
using System.Data;
using System.Drawing;
using System.Linq;
using BAKKA_Editor.Enums;
using BAKKA_Editor.Rendering;
using SkiaSharp;

namespace BAKKA_Editor.Rendering;

internal struct SkArcInfo
{
    public float StartAngle;
    public float ArcAngle;
    public SKRect Rect;
    public float NoteScale;
}

internal enum RolloverState
{
    None,
    Counterclockwise,
    Clockwise
}

internal class SkCircleView
{
    public RenderEngine RenderEngine;
    public Cursor Cursor = new();

    public bool IsPlaying { get; set; } = false; // TODO: move out.  // to where? - yasu

    // Mouse info. Public so UI can be updated with the values.
    public int lastMousePosition = -1;
    public int mouseDownPosition = -1;
    public Point mouseDownPoint;
    public RolloverState rolloverState = RolloverState.None;
    public int relativeMouseDragPosition = 0;
    public int mouseDownSize = -1;

    public SkCircleView(UserSettings userSettings, SizeF size)
    {
        RenderEngine = new(this, userSettings);
        RenderEngine.UpdateCanvasSize(size);
    }

    public void UpdateMouseDown(float xCenter, float yCenter, Point mousePoint, int size)
    {
        var theta = (float)(Math.Atan2(yCenter, xCenter) * 180.0f / Math.PI);
        if (theta < 0)
            theta += 360.0f;

        mouseDownPosition = (int)(theta / 6.0f);
        mouseDownPoint = mousePoint;
        lastMousePosition = mouseDownPosition;
        rolloverState = RolloverState.None;
        relativeMouseDragPosition = 0;
        mouseDownSize = size;
    }

    public void UpdateMouseUp()
    {
        if (mouseDownPosition <= -1) return;

        // reset position and point
        mouseDownPosition = -1;
        mouseDownPoint = new Point();
    }

    public void UpdateMouseMove(int mousePosition, int relativeMousePosition, int minimumCursorSize, RolloverState state)
    {
        lastMousePosition = mousePosition;
        relativeMouseDragPosition = relativeMousePosition;
        mouseDownSize = minimumCursorSize;
        rolloverState = state;
    }

    public int CalculateTheta(float x, float y)
    {
        var thetaCalc = (float)(Math.Atan2(y, x) * 180.0f / Math.PI);
        if (thetaCalc < 0)
            thetaCalc += 360.0f;
        var theta = (int)(thetaCalc / 6.0f);
        return theta;
    }
}

