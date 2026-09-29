using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

/// <summary>
/// ══ پیام‌رسانِ تمام‌صفحه ═════════════════════════════════════════════════════
///
/// ⚠️ «تمام‌صفحه» با XAML تنها نمی‌شود: صفحه‌ها داخلِ ScrollViewerِ پوسته
/// می‌نشینند و بلندیشان خودکار است. بلندی از <see cref="TopLevel.ClientSize"/>
/// می‌آید — همان راهِ <see cref="AccountSectionView"/> — و **بی هیچ شنوندهٔ
/// چیدمانی** (قاعدهٔ سرعت): فقط با عوض شدنِ اندازهٔ پنجره.
///
/// ⚠️ ‎InitializeComponent()‎ و نه ‎AvaloniaXamlLoader.Load(this)‎ — فیلدهای
/// ‎x:Name‎ را فقط اولی پر می‌کند.
/// </summary>
public partial class ChatSectionView : UserControl
{
    private TopLevel? _top;
    private ChatThreadViewModel? _watched;

    public ChatSectionView()
    {
        InitializeComponent();
        //  ⛔ Enter می‌فرستد، Shift+Enter خطِ تازه — تونلی، پیش از خودِ کادر
        //  (کادر ‎AcceptsReturn‎ دارد و Enter را خودش می‌بلعید).
        DraftBox.AddHandler(KeyDownEvent, OnDraftKey, RoutingStrategies.Tunnel);
    }

    private void OnDraftKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return)) return;
        if ((e.KeyModifiers & (KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Alt)) != 0) return;
        e.Handled = true;
        if (DataContext is ChatSectionViewModel vm && vm.SendCommand.CanExecute(null))
            vm.SendCommand.Execute(null);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (_top is not null) _top.PropertyChanged += OnTopChanged;
        FillHeight();
        if (DataContext is ChatSectionViewModel vm)
        {
            vm.PropertyChanged -= OnVmChanged;
            vm.PropertyChanged += OnVmChanged;
            Watch(vm.Current);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_top is not null) _top.PropertyChanged -= OnTopChanged;
        _top = null;
        if (DataContext is ChatSectionViewModel vm) vm.PropertyChanged -= OnVmChanged;
        Watch(null);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTopChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ClientSizeProperty) FillHeight();
    }

    private void FillHeight()
    {
        if (_top is null) return;
        var h = _top.ClientSize.Height;
        if (h > 0) ChatPage.Height = Math.Max(h, 520);
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatSectionViewModel.Current) && sender is ChatSectionViewModel vm)
            Watch(vm.Current);
    }

    /// <summary>پیامِ تازه ⇒ پایینِ گفت‌وگو. فقط گفت‌وگوی جلوی چشم شنیده می‌شود.</summary>
    private void Watch(ChatThreadViewModel? th)
    {
        if (_watched is not null) _watched.Messages.CollectionChanged -= OnMessages;
        _watched = th;
        if (th is null) return;
        th.Messages.CollectionChanged += OnMessages;
        ScrollDown();
    }

    private void OnMessages(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset) ScrollDown();
    }

    private void ScrollDown() =>
        Dispatcher.UIThread.Post(() => Bubbles?.ScrollToEnd(), DispatcherPriority.Background);
}
