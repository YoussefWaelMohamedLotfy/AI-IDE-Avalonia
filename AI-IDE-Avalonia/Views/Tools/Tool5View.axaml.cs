using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AI_IDE_Avalonia.Models.Documents;
using AI_IDE_Avalonia.ViewModels.Tools;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace AI_IDE_Avalonia.Views.Tools;

public partial class Tool5View : UserControl
{
    private Tool5ViewModel? _vm;
    private readonly List<ChatMessage> _subscribedMessages = new();

    public Tool5View()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.Messages.CollectionChanged -= OnMessagesChanged;
            UnsubscribeAllMessages();
        }

        _vm = DataContext as Tool5ViewModel;

        if (_vm is not null)
        {
            _vm.Messages.CollectionChanged += OnMessagesChanged;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            if (e.NewItems?[0] is ChatMessage { IsUser: false } assistantMsg)
            {
                assistantMsg.PropertyChanged += OnLastMessageContentChanged;
                _subscribedMessages.Add(assistantMsg);
            }
            ScrollToBottom();
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            UnsubscribeAllMessages();
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ChatMessage>())
            {
                item.PropertyChanged -= OnLastMessageContentChanged;
                _subscribedMessages.Remove(item);
            }
        }
    }

    private void UnsubscribeAllMessages()
    {
        foreach (var msg in _subscribedMessages)
            msg.PropertyChanged -= OnLastMessageContentChanged;
        _subscribedMessages.Clear();
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

        if (!InputBox.IsFocused) return;

        if (e.Key == Key.Up)
        {
            _vm?.NavigateHistoryUp();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            _vm?.NavigateHistoryDown();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _vm!.InputText = string.Empty;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter
            && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && _vm?.SendCommand.CanExecute(null) == true)
        {
            _vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
