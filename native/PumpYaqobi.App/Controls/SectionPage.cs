using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ قالبِ مشترکِ بخش‌ها ═════════════════════════════════════════════════════
/// هر بخشِ برنامه همین چیدمان را دارد: عنوان، نوارِ ابزار، نوارِ فیلتر،
/// بدنه (معمولاً جدول) و نوارِ جمع‌ها. یک‌جا تعریف می‌شود تا چهل‌ودو بخش
/// مو‌به‌مو یک‌شکل بمانند و تغییرِ ظاهری در همه‌شان با هم اعمال شود.
/// </summary>
public class SectionPage : TemplatedControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SectionPage, string?>(nameof(Header));

    public static readonly StyledProperty<string?> SubHeaderProperty =
        AvaloniaProperty.Register<SectionPage, string?>(nameof(SubHeader));

    public static readonly StyledProperty<object?> ToolbarProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Toolbar));

    public static readonly StyledProperty<object?> FiltersProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Filters));

    /// <summary>کادرهایی که بعد از کارت‌های زیربخش در همان نوار می‌نشینند (جست‌وجوی کوچک).</summary>
    public static readonly StyledProperty<object?> InlineProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Inline));

    /// <summary>
    /// ══ «☰ کارها» — یک کادرِ کشویی برای کارهای کم‌کاربردِ بخش (۱۴۰۵/۰۷/۱۶) ══
    /// خواستهٔ صاحب ریپو: «این‌ها را توی یک کادرِ کشویی بگذار که برنامه را شلوغ
    /// نکند» — تاریخچه، ماهِ جدید، PDF و کارت‌های زیربخش.
    ///
    /// ⛔ **فقط جای دکمه‌ها عوض می‌شود**، نه فرمانشان: هر دکمهٔ داخلِ این کشویی
    /// همان ‎Command‎ی را دارد که در نوار داشت.
    /// </summary>
    public static readonly StyledProperty<object?> MoreProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(More));

    public static readonly StyledProperty<object?> SummaryProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Summary));

    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Body));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Footer));


    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public string? SubHeader { get => GetValue(SubHeaderProperty); set => SetValue(SubHeaderProperty, value); }
    public object? Toolbar { get => GetValue(ToolbarProperty); set => SetValue(ToolbarProperty, value); }
    public object? Filters { get => GetValue(FiltersProperty); set => SetValue(FiltersProperty, value); }
    public object? Inline { get => GetValue(InlineProperty); set => SetValue(InlineProperty, value); }
    public object? More { get => GetValue(MoreProperty); set => SetValue(MoreProperty, value); }
    public object? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    [Content]
    public object? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    // ══ کشوییِ «☰ کارها» ═══════════════════════════════════════════════════
    //
    // ⚠️ در کد ساخته می‌شود، نه در قالب: محتوای ‎Flyout‎ در پنجرهٔ بازشوی جدا
    // می‌نشیند و ‎TemplateBinding‎ آن‌جا به ‎SectionPage‎ نمی‌رسد. پس محتوا
    // ‎DataContext‎ِ خودِ بخش را صریح می‌گیرد، و هر کلیکِ دکمه‌ای در آن
    // کشویی را می‌بندد.

    private Button? _moreButton;
    private readonly Flyout _moreFlyout = new() { Placement = PlacementMode.BottomEdgeAlignedLeft };
    private readonly StackPanel _moreBody = new() { Spacing = 4, MinWidth = 230 };
    private readonly ContentControl _moreHost = new();
    private readonly StackPanel _subItems = new() { Spacing = 4 };

    /// <summary>برای سنجه‌ها: کشویی از چه دکمه‌هایی ساخته شده است.</summary>
    public StackPanel MoreBody => _moreBody;

    public SectionPage()
    {
        _moreBody.Children.Add(_subItems);
        _moreBody.Children.Add(_moreHost);
        _moreFlyout.Content = _moreBody;
        _moreBody.AddHandler(Button.ClickEvent, (_, _) => _moreFlyout.Hide(), RoutingStrategies.Bubble, true);
        _moreFlyout.Opening += (_, _) => FillMenu();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _moreButton = e.NameScope.Find<Button>("PART_More");
        if (_moreButton is not null) _moreButton.Flyout = _moreFlyout;
        UpdateMore();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MoreProperty)
        {
            // ⚠️ محتوا یک بار در میزبانِ کشویی می‌نشیند — نه در قالب.
            _moreHost.Content = More;
            UpdateMore();
        }
        else if (change.Property == DataContextProperty)
        {
            if (change.OldValue is SectionViewModel o) o.PropertyChanged -= OnVmChanged;
            if (change.NewValue is SectionViewModel n) n.PropertyChanged += OnVmChanged;
            _moreBody.DataContext = DataContext;
            UpdateMore();
        }
    }

    private void OnVmChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SectionViewModel.ShowSubMenuItems)) UpdateMore();
    }

    private SectionViewModel? Vm => DataContext as SectionViewModel;

    private void UpdateMore()
    {
        if (_moreButton is null) return;
        _moreButton.IsVisible = More is not null || Vm?.ShowSubMenuItems == true;
    }

    /// <summary>کارت‌های زیربخش — هر بار که کشویی باز می‌شود، از خودِ بخش.</summary>
    private void FillMenu()
    {
        _moreBody.DataContext = DataContext;
        _subItems.Children.Clear();
        var vm = Vm;
        if (vm is null || !vm.ShowSubMenuItems) { _subItems.IsVisible = false; return; }
        foreach (var sub in vm.SubSections)
        {
            var b = new Button
            {
                Content = sub.LinkTitle,
                Command = vm.ShowSubCommand,
                CommandParameter = sub,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            b.Classes.Add("ghost");
            b.Classes.Add("menu-item");
            _subItems.Children.Add(b);
        }
        _subItems.IsVisible = _subItems.Children.Count > 0;
    }
}
