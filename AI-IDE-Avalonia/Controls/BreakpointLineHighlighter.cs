using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Rendering;
using System.Collections.Generic;
using System.Linq;

namespace AI_IDE_Avalonia.Controls;

/// <summary>
/// Background renderer that paints a semi-transparent red highlight behind every line
/// that has a breakpoint set in the associated <see cref="BreakpointMargin"/>.
/// </summary>
public sealed class BreakpointLineHighlighter : IBackgroundRenderer
{
    private static readonly IBrush HighlightBrush =
        new ImmutableSolidColorBrush(new Color(60, 220, 50, 60));

    private readonly IReadOnlyList<int> _breakpointLines;

    public KnownLayer Layer => KnownLayer.Background;

    public BreakpointLineHighlighter(IReadOnlyList<int> breakpointLines)
    {
        _breakpointLines = breakpointLines;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid || _breakpointLines.Count == 0)
            return;

        foreach (VisualLine visualLine in textView.VisualLines)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;
            if (!_breakpointLines.Contains(lineNumber))
                continue;

            double top    = visualLine.VisualTop - textView.VerticalOffset;
            double height = visualLine.Height;
            double width  = textView.Bounds.Width;

            drawingContext.DrawRectangle(HighlightBrush, null,
                new Rect(0, top, width, height));
        }
    }
}
