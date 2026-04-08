using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Headless UI tests that exercise Avalonia's rendering pipeline without a real display.
/// Each test dispatches work to the Avalonia UI thread via <see cref="AvaloniaDispatch"/>.
/// </summary>
public class HeadlessWindowTests
{
    [Test]
    public async Task Window_CanBeCreatedAndShown()
    {
        var isVisible = await AvaloniaDispatch.RunAsync(() =>
        {
            var window = new Window
            {
                Width  = 800,
                Height = 600,
                Title  = "HeadlessTest",
            };

            window.Show();
            var v = window.IsVisible;
            window.Close();
            return v;
        });

        await Assert.That(isVisible).IsTrue();
    }

    [Test]
    public async Task Window_HasCorrectDimensions()
    {
        var size = await AvaloniaDispatch.RunAsync(() =>
        {
            var window = new Window
            {
                Width  = 400,
                Height = 300,
            };

            window.Show();
            var s = window.ClientSize;
            window.Close();
            return s;
        });

        await Assert.That(size.Width).IsEqualTo(400);
        await Assert.That(size.Height).IsEqualTo(300);
    }

    [Test]
    public async Task TextBlock_RendersText()
    {
        var text = await AvaloniaDispatch.RunAsync(() =>
        {
            var tb = new TextBlock { Text = "Hello, Headless!" };
            var window = new Window
            {
                Width   = 400,
                Height  = 200,
                Content = tb,
            };

            window.Show();
            var captured = tb.Text;
            window.Close();
            return captured;
        });

        await Assert.That(text).IsEqualTo("Hello, Headless!");
    }

    [Test]
    public async Task Button_Click_FiresEvent()
    {
        var clicked = await AvaloniaDispatch.RunAsync(() =>
        {
            var count = 0;
            var button = new Button { Content = "Click me" };
            button.Click += (_, _) => count++;

            var window = new Window
            {
                Width   = 400,
                Height  = 200,
                Content = button,
            };

            window.Show();
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.Close();
            return count;
        });

        await Assert.That(clicked).IsEqualTo(1);
    }

    [Test]
    public async Task StackPanel_ContainsChildren()
    {
        var childCount = await AvaloniaDispatch.RunAsync(() =>
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "One" });
            panel.Children.Add(new TextBlock { Text = "Two" });
            panel.Children.Add(new Button { Content = "Three" });

            var window = new Window
            {
                Width   = 400,
                Height  = 400,
                Content = panel,
            };

            window.Show();
            var count = panel.Children.Count;
            window.Close();
            return count;
        });

        await Assert.That(childCount).IsEqualTo(3);
    }

    [Test]
    public async Task Window_CaptureRenderedFrame_IsNotNull()
    {
        // Verifies that the headless renderer can produce a frame bitmap.
        var frameIsNotNull = await AvaloniaDispatch.RunAsync(() =>
        {
            var window = new Window
            {
                Width   = 200,
                Height  = 200,
                Content = new TextBlock { Text = "Frame test" },
            };

            window.Show();
            // Force the render timer so the headless platform produces at least one frame.
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var frame = window.CaptureRenderedFrame();
            window.Close();
            return frame is not null;
        });

        await Assert.That(frameIsNotNull).IsTrue();
    }

    [Test]
    public async Task TextBox_AcceptsInput()
    {
        var value = await AvaloniaDispatch.RunAsync(() =>
        {
            var textBox = new TextBox();
            var window  = new Window
            {
                Width   = 400,
                Height  = 200,
                Content = textBox,
            };

            window.Show();
            textBox.Text = "typed text";
            var captured = textBox.Text;
            window.Close();
            return captured;
        });

        await Assert.That(value).IsEqualTo("typed text");
    }

    [Test]
    public async Task ProgressBar_ValueIsCorrect()
    {
        var actual = await AvaloniaDispatch.RunAsync(() =>
        {
            var pb = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value   = 42,
            };

            var window = new Window { Width = 400, Height = 200, Content = pb };
            window.Show();
            var v = pb.Value;
            window.Close();
            return v;
        });

        await Assert.That(actual).IsEqualTo(42.0);
    }

    [Test]
    public async Task CheckBox_IsChecked_CanBeToggled()
    {
        var isChecked = await AvaloniaDispatch.RunAsync(() =>
        {
            var cb = new CheckBox { IsChecked = false };
            var window = new Window { Width = 400, Height = 200, Content = cb };
            window.Show();

            cb.IsChecked = true;
            var v = cb.IsChecked;

            window.Close();
            return v;
        });

        await Assert.That(isChecked).IsTrue();
    }
}
