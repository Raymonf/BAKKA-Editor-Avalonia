using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace BAKKA_Editor.Controls;

public partial class SkiaCanvas : UserControl
{
    class SkiaDrawOp : ICustomDrawOperation
    {
        private Action<SKCanvas> renderFunc;

        public SkiaDrawOp(Rect bounds, Action<SKCanvas> render)
        {
            Bounds = bounds;
            renderFunc = render;
        }

        public void Dispose()
        {
        }

        public Rect Bounds { get; }
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                return;
            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            renderFunc.Invoke(canvas);
        }
    }

    public event Action<SKCanvas>? RenderSkia;

    public SkiaCanvas()
    {
    }

    public override void Render(DrawingContext context)
    {
        if (RenderSkia != null)
            context.Custom(new SkiaDrawOp(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height), RenderSkia));
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }
}