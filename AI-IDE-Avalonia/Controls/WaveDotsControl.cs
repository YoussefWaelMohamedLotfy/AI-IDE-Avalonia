using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using System;

namespace AI_IDE_Avalonia.Controls;

/// <summary>
/// Renders an animated 3-D wave of dots inspired by the Visual Studio 2019 splash screen.
/// Uses SkiaSharp through Avalonia's <see cref="ICustomDrawOperation"/> pipeline —
/// no additional NuGet packages required beyond Avalonia itself.
/// </summary>
public sealed class WaveDotsControl : Control
{
    private double _time;
    private DispatcherTimer? _timer;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 fps
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _time += 0.035;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Accept whatever space the parent offers
        return new Size(
            double.IsInfinity(availableSize.Width)  ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
    }

    public override void Render(DrawingContext context)
    {
        // Bounds includes parent-relative X,Y; the draw op must use local-space coords (origin = 0,0)
        context.Custom(new WaveDotsDrawOp(new Rect(Bounds.Size), _time));
    }
}

/// <summary>
/// Custom Skia draw operation that renders the wave-of-dots animation onto the canvas.
/// </summary>
internal sealed class WaveDotsDrawOp(Rect bounds, double time) : ICustomDrawOperation
{
    // Color palette: dark indigo → app accent violet (#7C3AED) → pale lavender
    // Top-end kept at a muted mid-violet so "foreground" dots don't flash white
    private static readonly SKColor[] Palette =
    [
        new(0x1E, 0x0A, 0x4A),  // near-black indigo
        new(0x3B, 0x0F, 0x82),  // deep violet
        new(0x5B, 0x21, 0xB6),  // indigo-600
        new(0x7C, 0x3A, 0xED),  // violet-600  (accent)
        new(0x8B, 0x5C, 0xF6),  // violet-500
        new(0xA7, 0x8B, 0xFA),  // violet-400
        new(0xC4, 0xB5, 0xFD),  // violet-300  (soft, not white)
    ];

    // Shared blur filter avoids recreating it every frame
    private static readonly SKMaskFilter Glow =
        SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f);

    public Rect Bounds { get; } = bounds;
    public bool HitTest(Point p) => false;
    public bool Equals(ICustomDrawOperation? other) => false;
    public void Dispose() { }

    public void Render(ImmediateDrawingContext context)
    {
        if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } skiaFeature)
            return;

        using var lease = skiaFeature.Lease();
        DrawWaveDots(lease.SkCanvas);
    }

    private void DrawWaveDots(SKCanvas canvas)
    {
        const int cols = 42;
        const int rows = 12;

        float w = (float)Bounds.Width;
        float h = (float)Bounds.Height;

        float cellW = w / (cols + 1);
        float cellH = h / (rows + 1);
        float baseRadius = MathF.Min(cellW, cellH) * 0.22f;

        // The curtain fold: each column is mirrored around the vertical centre.
        // mc = 0 at both left/right edges, halfCols at the centre column.
        double halfCols = (cols - 1) / 2.0;

        using var dotPaint  = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var glowPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, MaskFilter = Glow };

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float cx = (col + 1) * cellW;
                float cy = (row + 1) * cellH;

                // Fold the column coordinate so both halves are mirror images —
                // gives the curtain-fold symmetry along the vertical centre line.
                double mc = halfCols - Math.Abs(col - halfCols);  // 0 at edges → halfCols at centre

                // Diagonal phase: mc (folded horizontal) + row (vertical) combined so
                // wave crests travel diagonally from the outer edges inward and downward.
                double phase = mc * 0.42 + row * 0.62;

                // Three layered waves on that diagonal phase for organic depth
                double z = Math.Sin(phase - time * 1.30)
                         + 0.50 * Math.Sin(phase * 0.68 - time * 0.85)
                         + 0.28 * Math.Cos(phase * 0.38 + time * 0.55);

                // Normalise z → [0, 1]  (raw range ≈ [-1.78, +1.78])
                double t = Math.Clamp((z + 1.78) / 3.56, 0.0, 1.0);

                // Dot size: "closer" (higher t) → larger
                float radius = baseRadius * (0.20f + 0.80f * (float)t);

                // Y displacement — stronger near the centre fold, flatter at the edges,
                // mimicking how a gathered curtain drapes more deeply at the fold.
                float foldWeight = (float)(mc / halfCols);          // 0 at edges, 1 at centre
                cy += (float)(z * cellH * (0.12 + 0.28 * foldWeight));

                // Colour
                double colorPos = t * (Palette.Length - 1);
                int    idx      = Math.Clamp((int)colorPos, 0, Palette.Length - 2);
                double frac     = colorPos - idx;

                SKColor c0 = Palette[idx];
                SKColor c1 = Palette[idx + 1];
                byte r = (byte)(c0.Red   + frac * (c1.Red   - c0.Red));
                byte g = (byte)(c0.Green + frac * (c1.Green - c0.Green));
                byte b = (byte)(c0.Blue  + frac * (c1.Blue  - c0.Blue));

                // Faint glow only on the very closest dots
                if (t > 0.80)
                {
                    glowPaint.Color = new SKColor(r, g, b, (byte)(8 * t));
                    canvas.DrawCircle(cx, cy, radius * 1.8f, glowPaint);
                }

                // Max alpha ~90, min ~12 — deliberately dim
                dotPaint.Color = new SKColor(r, g, b, (byte)(12 + 78 * t));
                canvas.DrawCircle(cx, cy, radius, dotPaint);
            }
        }
    }
}
