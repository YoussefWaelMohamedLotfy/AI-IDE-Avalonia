using System.Collections.Specialized;
using System.ComponentModel;
using AI_IDE_Avalonia.Models.Documents;
using AI_IDE_Avalonia.ViewModels.Documents;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace AI_IDE_Avalonia.Views.Documents;

public partial class DocumentView : UserControl
{
    public DocumentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private DocumentViewModel? _vm;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.Messages.CollectionChanged -= OnMessagesChanged;
        }

        _vm = DataContext as DocumentViewModel;

        if (_vm is not null)
        {
            _vm.Messages.CollectionChanged += OnMessagesChanged;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            // Watch the last assistant message for streaming content updates
            if (e.NewItems?[0] is ChatMessage { IsUser: false } assistantMsg)
                assistantMsg.PropertyChanged += OnLastMessageContentChanged;

            ScrollToBottom();
        }
    }

    private void OnLastMessageContentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatMessage.Content))
            ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        Dispatcher.UIThread.Post(
            () => MessagesScroller.Offset = MessagesScroller.Offset.WithY(double.MaxValue),
            DispatcherPriority.Render);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Enter
            && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && InputBox.IsFocused
            && _vm?.SendCommand.CanExecute(null) == true)
        {
            _vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
