using System;
using Avalonia.Headless;
using Xunit;

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Defines the xUnit collection that all headless Avalonia tests belong to.
/// A single <see cref="HeadlessTestFixture"/> instance is shared across every
/// test class in the collection, which ensures only one Avalonia session is
/// created for the entire test run.
/// </summary>
[CollectionDefinition(Name)]
public sealed class HeadlessTestsCollection : ICollectionFixture<HeadlessTestFixture>
{
    public const string Name = "HeadlessTests";
}

/// <summary>
/// xUnit collection fixture that owns the single
/// <see cref="HeadlessUnitTestSession"/> shared by all headless tests.
/// </summary>
public sealed class HeadlessTestFixture : IDisposable
{
    /// <summary>
    /// The single headless Avalonia session for this test run.
    /// Started once and reused by every test class in the
    /// <see cref="HeadlessTestsCollection"/> collection.
    /// </summary>
    public HeadlessUnitTestSession Session { get; } =
        HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));

    public void Dispose() => Session.Dispose();
}
