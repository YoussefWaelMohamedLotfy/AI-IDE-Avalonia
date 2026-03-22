// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Avalonia;
using Dock.Serializer.SystemTextJson;
using Dock.Settings;

[assembly: DockJsonSourceGeneration]
[assembly: DockJsonSerializable(typeof(AI_IDE_Avalonia.ViewModels.Docks.CustomDocumentDock))]
[assembly: DockJsonSerializable(typeof(AI_IDE_Avalonia.ViewModels.Documents.DocumentViewModel))]
[assembly: DockJsonSerializable(typeof(AI_IDE_Avalonia.ViewModels.Tools.Tool1ViewModel))]
[assembly: DockJsonSerializable(typeof(AI_IDE_Avalonia.ViewModels.Tools.Tool2ViewModel))]
[assembly: DockJsonSerializable(typeof(AI_IDE_Avalonia.ViewModels.Tools.Tool3ViewModel))]
[assembly: DockJsonSerializable(typeof(AI_IDE_Avalonia.ViewModels.Tools.Tool4ViewModel))]
[assembly: DockJsonSerializable(typeof(AI_IDE_Avalonia.ViewModels.Views.DashboardViewModel))]
[assembly: DockJsonSerializable(typeof(AI_IDE_Avalonia.ViewModels.Views.HomeViewModel))]

namespace AI_IDE_Avalonia;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .WithDockSettings(new DockSettingsOptions
            {
                CommandBarMergingEnabled = true,
                CommandBarMergingScope = DockCommandBarMergingScope.ActiveDocument
            })
            .LogToTrace();
}
