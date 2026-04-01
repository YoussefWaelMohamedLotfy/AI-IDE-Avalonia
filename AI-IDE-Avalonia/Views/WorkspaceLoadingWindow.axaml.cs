using System;
using Avalonia.Controls;
using AI_IDE_Avalonia.ViewModels;

namespace AI_IDE_Avalonia.Views;

public partial class WorkspaceLoadingWindow : Window
{
    private TextBox? _statusTextBox;

    public WorkspaceLoadingWindow()
    {
        InitializeComponent();
        _statusTextBox = this.FindControl<TextBox>("StatusTextBox");
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is WorkspaceLoadingViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        base.OnDataContextChanged(e);

        if (DataContext is WorkspaceLoadingViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceLoadingViewModel.StatusLog))
            ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        if (_statusTextBox is null) return;
        var len = _statusTextBox.Text?.Length ?? 0;
        if (len > 0)
            _statusTextBox.CaretIndex = len;
    }
}
