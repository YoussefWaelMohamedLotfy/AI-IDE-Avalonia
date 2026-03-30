using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Xunit;

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Headless tests that exercise basic Window lifecycle behaviour.
/// </summary>
[Collection(HeadlessTestsCollection.Name)]
public class MainWindowTests(HeadlessTestFixture fixture)
{
    private readonly HeadlessUnitTestSession _session = fixture.Session;

    [Fact]
    public Task Window_Should_Open_And_Be_Visible() =>
        _session.Dispatch(() =>
        {
            var window = new Window();
            window.Show();

            Assert.True(window.IsVisible);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Window_Should_Have_Correct_Title() =>
        _session.Dispatch(() =>
        {
            var window = new Window { Title = "AI IDE" };
            window.Show();

            Assert.Equal("AI IDE", window.Title);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Window_Should_Not_Be_Visible_Before_Show() =>
        _session.Dispatch(() =>
        {
            var window = new Window();

            Assert.False(window.IsVisible);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Window_Should_Close_Correctly() =>
        _session.Dispatch(() =>
        {
            var window = new Window();
            window.Show();
            Assert.True(window.IsVisible);

            window.Close();

            Assert.False(window.IsVisible);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Window_Should_Respect_Initial_Size() =>
        _session.Dispatch(() =>
        {
            var window = new Window { Width = 1200, Height = 680 };
            window.Show();

            Assert.Equal(1200, window.Width);
            Assert.Equal(680, window.Height);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Window_Content_Should_Be_Rendered() =>
        _session.Dispatch(() =>
        {
            var window = new Window
            {
                Content = new TextBlock { Text = "Hello, Headless!" }
            };
            window.Show();

            var textBlock = window.Content as TextBlock;
            Assert.NotNull(textBlock);
            Assert.Equal("Hello, Headless!", textBlock.Text);
        }, TestContext.Current.CancellationToken);
}
