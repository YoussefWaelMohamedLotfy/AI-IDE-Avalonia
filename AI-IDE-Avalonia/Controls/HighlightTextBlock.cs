using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AI_IDE_Avalonia.Controls;

/// <summary>
/// A TextBlock that renders a substring highlight (yellow bg, black fg) over the matched portion.
/// Bind <see cref="DisplayText"/> to the full string and <see cref="HighlightText"/> to the filter.
/// </summary>
public class HighlightTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> DisplayTextProperty =
        AvaloniaProperty.Register<HighlightTextBlock, string?>(nameof(DisplayText));

    public static readonly StyledProperty<string?> HighlightTextProperty =
        AvaloniaProperty.Register<HighlightTextBlock, string?>(nameof(HighlightText));

    public string? DisplayText
    {
        get => GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public string? HighlightText
    {
        get => GetValue(HighlightTextProperty);
        set => SetValue(HighlightTextProperty, value);
    }

    static HighlightTextBlock()
    {
        DisplayTextProperty.Changed.AddClassHandler<HighlightTextBlock>((x, _) => x.UpdateInlines());
        HighlightTextProperty.Changed.AddClassHandler<HighlightTextBlock>((x, _) => x.UpdateInlines());
    }

    private void UpdateInlines()
    {
        Inlines ??= [];
        Inlines.Clear();

        var text = DisplayText ?? string.Empty;
        var filter = HighlightText?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(text))
            return;

        if (string.IsNullOrEmpty(filter))
        {
            Inlines.Add(new Run(text));
            return;
        }

        int start = 0;
        while (start < text.Length)
        {
            int idx = text.IndexOf(filter, start, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                Inlines.Add(new Run(text[start..]));
                break;
            }

            if (idx > start)
                Inlines.Add(new Run(text[start..idx]));

            Inlines.Add(new Run(text[idx..(idx + filter.Length)])
            {
                Background = Brushes.Yellow,
                Foreground = Brushes.Black,
            });

            start = idx + filter.Length;
        }
    }
}
