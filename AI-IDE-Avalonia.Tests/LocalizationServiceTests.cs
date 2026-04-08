using AI_IDE_Avalonia.Services;
using Avalonia.Media;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Unit tests for <see cref="LocalizationService"/>.
/// All tests are pure — they do not require the Avalonia UI thread.
/// </summary>
public class LocalizationServiceTests
{
    [Test]
    public async Task DefaultCulture_IsEnglish()
    {
        var svc = new LocalizationService();

        await Assert.That(svc.CurrentCulture.Name).IsEqualTo("en");
    }

    [Test]
    public async Task FlowDirection_DefaultIsLeftToRight()
    {
        var svc = new LocalizationService();

        await Assert.That(svc.FlowDirection).IsEqualTo(FlowDirection.LeftToRight);
    }

    [Test]
    public async Task SetCulture_Arabic_SwitchesToRightToLeft()
    {
        var svc = new LocalizationService();
        svc.SetCulture("ar");

        await Assert.That(svc.CurrentCulture.Name).IsEqualTo("ar");
        await Assert.That(svc.FlowDirection).IsEqualTo(FlowDirection.RightToLeft);
    }

    [Test]
    public async Task SetCulture_BackToEnglish_SwitchesToLeftToRight()
    {
        var svc = new LocalizationService();
        svc.SetCulture("ar");
        svc.SetCulture("en");

        await Assert.That(svc.FlowDirection).IsEqualTo(FlowDirection.LeftToRight);
    }

    [Test]
    public async Task Indexer_KnownKey_ReturnsNonEmptyString()
    {
        var svc = new LocalizationService();

        var value = svc["OpenFolder"];

        await Assert.That(value).IsNotNullOrEmpty();
    }

    [Test]
    public async Task Indexer_UnknownKey_ReturnsFallbackKey()
    {
        var svc = new LocalizationService();
        const string unknown = "ThisKeyDoesNotExist_XYZ";

        var value = svc[unknown];

        await Assert.That(value).IsEqualTo(unknown);
    }

    [Test]
    public async Task StringProperties_ReturnLocalisedValues()
    {
        var svc = new LocalizationService();

        // These properties must return non-null, non-empty strings in the default (en) culture.
        await Assert.That(svc.OpenFolder).IsNotNullOrEmpty();
        await Assert.That(svc.Recent).IsNotNullOrEmpty();
        await Assert.That(svc.ChatSend).IsNotNullOrEmpty();
        await Assert.That(svc.ExpandAll).IsNotNullOrEmpty();
        await Assert.That(svc.CollapseAll).IsNotNullOrEmpty();
    }

    [Test]
    public async Task SetCulture_RaisesPropertyChanged()
    {
        var svc = new LocalizationService();
        var raised = new List<string?>();
        svc.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        svc.SetCulture("ar");

        // Raising with "" (empty) signals that all properties changed.
        await Assert.That(raised.Count).IsGreaterThan(0);
        await Assert.That(raised.Contains(string.Empty)).IsTrue();
    }
}
