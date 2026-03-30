using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Xunit;

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Headless tests that exercise text input and keyboard interactions.
/// </summary>
[Collection(HeadlessTestsCollection.Name)]
public class TextInputTests(HeadlessTestFixture fixture)
{
    private readonly HeadlessUnitTestSession _session = fixture.Session;

    [Fact]
    public Task TextBox_Should_Accept_Text_Input() =>
        _session.Dispatch(() =>
        {
            var window = new Window();
            var textBox = new TextBox();
            window.Content = textBox;
            window.Show();

            textBox.Focus();
            window.KeyTextInput("Hello");

            Assert.Equal("Hello", textBox.Text);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TextBox_Should_Clear_Text_With_Ctrl_A_And_Delete() =>
        _session.Dispatch(() =>
        {
            var window = new Window();
            var textBox = new TextBox { Text = "Initial text" };
            window.Content = textBox;
            window.Show();
            window.RunJobs();

            textBox.Focus();
            window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);

            Assert.True(string.IsNullOrEmpty(textBox.Text));
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TextBox_Should_Append_Multiple_Inputs() =>
        _session.Dispatch(() =>
        {
            var window = new Window();
            var textBox = new TextBox();
            window.Content = textBox;
            window.Show();

            textBox.Focus();
            window.KeyTextInput("Hello");
            window.KeyTextInput(", World");

            Assert.Equal("Hello, World", textBox.Text);
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("foo")]
    [InlineData("bar")]
    [InlineData("baz")]
    public Task TextBox_Should_Accept_Various_Inputs(string value) =>
        _session.Dispatch(() =>
        {
            var window = new Window();
            var textBox = new TextBox();
            window.Content = textBox;
            window.Show();

            textBox.Focus();
            window.KeyTextInput(value);

            Assert.Equal(value, textBox.Text);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Button_Click_Via_Mouse_Should_Execute() =>
        _session.Dispatch(() =>
        {
            var window = new Window { Width = 300, Height = 200 };
            var clickCount = 0;
            var button = new Button { Content = "Click me" };
            button.Click += (_, _) => clickCount++;
            window.Content = button;
            window.Show();

            var pos = button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2),
                window)!.Value;
            window.MouseDown(pos, MouseButton.Left);
            window.MouseUp(pos, MouseButton.Left);

            Assert.Equal(1, clickCount);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TextBox_Should_Be_Empty_By_Default() =>
        _session.Dispatch(() =>
        {
            var window = new Window();
            var textBox = new TextBox();
            window.Content = textBox;
            window.Show();

            Assert.True(string.IsNullOrEmpty(textBox.Text));
        }, TestContext.Current.CancellationToken);
}
