using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using System;
using System.Collections.Generic;

namespace AI_IDE_Avalonia.Controls;

/// <summary>
/// A clickable gutter margin that renders breakpoint dots, similar to the Visual Studio breakpoint margin.
/// </summary>
public sealed class BreakpointMargin : AbstractMargin
{
    private IBrush _backgroundBrush = new ImmutableSolidColorBrush(new Color(255, 51, 51, 51));
    private readonly IBrush _defaultBackgroundBrush = Brushes.Transparent;
    private readonly IBrush _hoverBrush = new ImmutableSolidColorBrush(new Color(192, 80, 80, 80));
    private readonly IPen _hoverPen = new ImmutablePen(new ImmutableSolidColorBrush(new Color(192, 37, 37, 37)), 1);
    private readonly IBrush _breakpointBrush = new ImmutableSolidColorBrush(new Color(255, 195, 81, 92));
    private readonly IPen _breakpointPen = new ImmutablePen(new ImmutableSolidColorBrush(new Color(255, 240, 92, 104)), 1);

    private readonly List<int> _breakpointLines = [];
    private int _hoverLine = -1;

    /// <summary>Raised whenever the set of breakpoint line numbers changes.</summary>
    public event EventHandler? BreakpointsChanged;

    /// <summary>Gets the current set of breakpoint line numbers (1-based).</summary>
    public IReadOnlyList<int> BreakpointLines => _breakpointLines;

    public IBrush BackgroundBrush
    {
        get => _backgroundBrush;
        set { _backgroundBrush = value; InvalidateVisual(); }
    }

    public void UseDefaultBackground()
    {
        _backgroundBrush = _defaultBackgroundBrush;
        InvalidateVisual();
    }

    public BreakpointMargin()
    {
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
    {
        if (oldTextView != null)
        {
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
            oldTextView.DocumentChanged -= OnDocumentChanged;
        }

        if (newTextView != null)
        {
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
            newTextView.DocumentChanged += OnDocumentChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        _breakpointLines.Clear();
        BreakpointsChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(28, 0);

    private int GetLineAt(PointerEventArgs e)
    {
        if (TextView is null) return -1;
        // Use the margin's own Y coordinate — the margin and TextView share the same vertical
        // origin in the TextArea layout, so no inter-control transform is needed.
        double visualY = e.GetPosition(this).Y + TextView.VerticalOffset;
        if (!TextView.VisualLinesValid) return -1;
        VisualLine? line = TextView.GetVisualLineFromVisualTop(visualY);
        return line?.FirstDocumentLine.LineNumber ?? -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        int newHover = GetLineAt(e);
        if (_hoverLine != newHover)
        {
            _hoverLine = newHover;
            InvalidateVisual();
        }
        base.OnPointerMoved(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        _hoverLine = -1;
        InvalidateVisual();
        base.OnPointerExited(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        int line = GetLineAt(e);
        _hoverLine = line;

        if (line >= 1)
        {
            if (!_breakpointLines.Remove(line))
                _breakpointLines.Add(line);

            _breakpointLines.Sort();
            BreakpointsChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        e.Handled = true;
        base.OnPointerPressed(e);
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(_backgroundBrush, null, Bounds);

        // Subtle separator on the right edge to visually detach gutter from line numbers / text.
        var separatorBrush = new ImmutableSolidColorBrush(new Color(60, 128, 128, 128));
        context.DrawLine(new ImmutablePen(separatorBrush, 1),
            new Point(Bounds.Width - 1, 0),
            new Point(Bounds.Width - 1, Bounds.Height));

        if (TextView?.VisualLinesValid == true)
        {
            foreach (VisualLine visualLine in TextView.VisualLines)
            {
                double cy = visualLine.VisualTop - TextView.VerticalOffset + visualLine.Height / 2;
                int lineNumber = visualLine.FirstDocumentLine.LineNumber;

                if (_breakpointLines.Contains(lineNumber))
                    context.DrawEllipse(_breakpointBrush, _breakpointPen, new Point(12, cy), 7, 7);
                else if (_hoverLine == lineNumber)
                    context.DrawEllipse(_hoverBrush, _hoverPen, new Point(12, cy), 7, 7);
            }
        }

        base.Render(context);
    }
}
