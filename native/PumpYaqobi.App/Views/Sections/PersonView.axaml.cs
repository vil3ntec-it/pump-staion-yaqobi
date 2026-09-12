using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

public partial class PersonView : UserControl
{
    public PersonView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => Follow();
        Follow();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ستون‌های زندهٔ جدول
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو: «دو تا رسید تو یکی.» سایت هر بار فقط یکی را نشان
    //  می‌دهد (‎_togglePmMoneyCols‎، خطِ ۳۴۳۴ی ‎index.html‎) و ستونِ «نوع تیل»
    //  را هم در حسابِ جداگانهٔ پطرول/دیزل برمی‌دارد (‎_togglePmFtColumn‎).
    //
    //  ⚠️ چرا کد و نه ‎Binding‎: ستونِ ‎DataGrid‎ عنصرِ دیداری نیست، در درختِ
    //  منطقی نمی‌نشیند و ‎DataContext‎ به آن ارث نمی‌رسد — پس
    //  ‎IsVisible="{Binding …}"‎ روی ستون هیچ‌وقت وصل نمی‌شود. این‌جا به همان
    //  خاصیت‌های ویومدل گوش می‌دهیم و خودمان می‌نشانیمشان.

    private PersonViewModel? _vm;
    private AccountViewModel? _acct;

    private void Follow()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnPersonChanged;
        _vm = DataContext as PersonViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnPersonChanged;
        FollowAccount();
    }

    private void OnPersonChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PersonViewModel.Current)) FollowAccount();
    }

    private void FollowAccount()
    {
        if (_acct is not null) _acct.PropertyChanged -= OnAccountChanged;
        _acct = _vm?.Current;
        if (_acct is not null) _acct.PropertyChanged += OnAccountChanged;
        ApplyColumns();
    }

    private void OnAccountChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AccountViewModel.ShowRasidColumn)
                           or nameof(AccountViewModel.ShowRasidFuelColumn)
                           or nameof(AccountViewModel.ShowFuelTypeColumn)
                           or nameof(AccountViewModel.IsMoney)
                           or nameof(AccountViewModel.RowFilter))
            ApplyColumns();
    }

    private void ApplyColumns()
    {
        if (_acct is null) return;
        // ⚠️ فقط جدولِ خودِ حساب — جدولِ آرشیو هم ستونِ «رسید» دارد و نباید
        // با آن قاطی شود.
        if (this.FindControl<DataGrid>("PersonGrid") is not { } grid) return;

        foreach (var col in grid.Columns)
        {
            var head = col.Header?.ToString()?.Trim();
            if (head == "رسید") col.IsVisible = _acct.ShowRasidColumn;
            else if (head == "رسید تیل") col.IsVisible = _acct.ShowRasidFuelColumn;
            else if (head == "نوع تیل") col.IsVisible = _acct.ShowFuelTypeColumn;
        }
    }

    /// <summary>
    /// ══ کادرِ «مقدار رسید»ِ سربرگ — همتای ‎personRasidFocus‎ی سایت ═══════════
    ///
    /// کادر جمعِ همهٔ رسیدهای دفتر را نشان می‌دهد. با دست خوردن خالی می‌شود،
    /// چون عددی که می‌نویسید «رسیدِ تازه» است نه ویرایشِ آن جمع. با بیرون رفتن
    /// (‎UpdateSourceTrigger=LostFocus‎) همان عدد در دفتر ثبت می‌شود و کادر
    /// دوباره جمعِ تازه را نشان می‌دهد.
    ///
    /// بی این، کاربر روی «۵٬۰۰۰» می‌نوشت «۲٬۰۰۰» و جمع ۷٬۰۰۰ می‌شد — همان
    /// چیزی که در سایت هم با پاک شدنِ کادر جلویش گرفته شده.
    /// </summary>
    private void RasidBoxFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox tb) tb.Text = "";
    }

    /// <summary>
    /// ‎Enter‎ همان‌جا ثبت کند — بی این، کاربر باید از کادر بیرون می‌رفت.
    /// (همتای ‎onkeydown=Enter ⇒ blur‎ی کادرِ رسیدِ سایت.)
    /// </summary>
    private void RasidBoxKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox tb) return;
        // فوکوس که برود، ‎LostFocus‎ همان لحظه عدد را در دفتر می‌نشاند
        TopLevel.GetTopLevel(tb)?.FocusManager?.ClearFocus();
        e.Handled = true;
    }
}
