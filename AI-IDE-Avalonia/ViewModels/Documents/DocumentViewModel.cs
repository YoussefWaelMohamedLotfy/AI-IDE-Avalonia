using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.CommandBars;
using Dock.Model.Mvvm.Controls;

namespace AI_IDE_Avalonia.ViewModels.Documents;

public partial class DocumentViewModel : Document, IDockCommandBarProvider
{
    private int _renameCounter;
    private readonly RelayCommand _toggleModifiedCommand;
    private readonly RelayCommand _renameCommand;
    private readonly RelayCommand _closeCommand;

    public DocumentViewModel()
    {
        _toggleModifiedCommand = new RelayCommand(ToggleModified);
        _renameCommand = new RelayCommand(RenameDocument);
        _closeCommand = new RelayCommand(CloseDocument);
    }

    public event EventHandler? CommandBarsChanged;

    public IReadOnlyList<DockCommandBarDefinition> GetCommandBars()
    {
        var displayTitle = IsModified ? $"{Title}*" : Title;

        var menuItems = new List<DockCommandBarItem>
        {
            new("_Document")
            {
                Items =
                [
                    new($"Active: {displayTitle}") { Order = 0 },
                    new("_Toggle Modified") { Command = _toggleModifiedCommand, Order = 1 },
                    new("_Rename") { Command = _renameCommand, Order = 2 },
                    new(null) { IsSeparator = true, Order = 3 },
                    new("_Close") { Command = _closeCommand, Order = 4 }
                ]
            }
        };

        var toolItems = new List<DockCommandBarItem>
        {
            new("Toggle Modified") { Command = _toggleModifiedCommand, Order = 0 },
            new("Rename") { Command = _renameCommand, Order = 1 },
            new("Close") { Command = _closeCommand, Order = 2 }
        };

        return
        [
            new("DocumentMenu", DockCommandBarKind.Menu)
            {
                Order = 0,
                Items = menuItems
            },
            new("DocumentToolBar", DockCommandBarKind.ToolBar)
            {
                Order = 1,
                Items = toolItems
            }
        ];
    }

    private void ToggleModified()
    {
        IsModified = !IsModified;
        RaiseCommandBarsChanged();
    }

    private void RenameDocument()
    {
        _renameCounter++;
        Title = $"{Id} ({_renameCounter})";
        RaiseCommandBarsChanged();
    }

    private void CloseDocument()
    {
        if (!CanClose)
            return;

        Factory?.CloseDockable(this);
    }

    private void RaiseCommandBarsChanged()
    {
        CommandBarsChanged?.Invoke(this, EventArgs.Empty);
    }
}