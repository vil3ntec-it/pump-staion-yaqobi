using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ورق ⇐ حسابِ قرض‌دار و مصارف ═════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «اون حسابِ طرف رو که توی ورق زدم با مشخصات نمیاد تو همون
/// اسمِ حساب… چرا اتومات نمی‌ره تو حساب‌اش؟ … مهم نبود اسمش اول باشه یا آخر،
/// اگه اسمش بزرگ بود یک بخش‌اش هم کافی بود — مثل محمد هارون، همون هارون رو هم
/// می‌زدم دقیق به حسابِ محمد هارون می‌رسید.»
///
/// این آزمون‌ها همان چیزی را می‌سنجند که سایت در ‎syncWaraqTxnsToPersons‎
/// می‌کرد — ولی روی منطقِ خودِ برنامه، نه روی نما.
/// </summary>
public class WaraqPostingTests
{
    private static readonly WaraqService Calc = new();

    /// <summary>ورقی با یک شیفتِ روزِ خالی، فیِ پطرول ۵۰ و دیزل ۶۰.</summary>
    private static WaraqEntry Sheet(params WaraqTransaction[] txns)
    {
        var w = new WaraqEntry { Id = 7, DateShamsi = "1405/06/18", DateKey = 14050618 };
        var day = new WaraqShift { Kind = ShiftKind.Day, PricePerLiter = 50m, PricePerLiterDiesel = 60m };
        var i = 0;
        foreach (var t in txns) { t.SortIndex = i++; day.Transactions.Add(t); }
        w.Shifts.Add(day);
        w.Shifts.Add(new WaraqShift { Kind = ShiftKind.Night, PricePerLiter = 50m, PricePerLiterDiesel = 60m });
        return w;
    }

    private static WaraqTransaction Txn(string name, decimal liters = 0m, decimal amount = 0m,
                                        WaraqTxnType type = WaraqTxnType.Debt,
                                        LedgerMode unit = LedgerMode.Fuel,
                                        FuelType fuel = FuelType.Petrol) =>
        new()
        {
            Name = name, Liters = liters, Amount = amount, Type = type, Unit = unit, Fuel = fuel,
            // مبلغِ نوشته‌شده دستی است، مگر آن‌که صفر باشد
            AmountAuto = amount == 0m ? null : false,
        };

    private static Debtor Person(string name, params string[] subs)
    {
        var p = new Debtor { Name = name, MainAccount = new DebtAccount { Name = name } };
        foreach (var s in subs)
            p.SubAccounts.Add(new DebtAccount { Name = s, LegacySubId = "s" + s });
        return p;
    }

    private static List<DebtRow> Fuel(Debtor p) => p.MainAccount.FuelRows;
    private static List<DebtRow> Money(Debtor p) => p.MainAccount.MoneyRows;

    // ══ نام ════════════════════════════════════════════════════════════════

    /// <summary>«هارون» باید به حسابِ «محمد هارون» برسد — خواستهٔ صریحِ صاحب ریپو.</summary>
    [Fact]
    public void A_short_piece_of_the_name_is_enough()
    {
        var p = Person("محمد هارون");
        var w = Sheet(Txn("هارون", liters: 10m));

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        var row = Assert.Single(Fuel(p));
        Assert.Equal(500m, row.Bardagi);      // ۱۰ لیتر × فیِ ۵۰
        Assert.Equal(10m, row.Liters);
        Assert.Equal(50m, row.PricePerLiter);
        Assert.Equal("هارون", row.Name);
    }

    /// <summary>و برعکس: نامِ بلندتر از نامِ حساب هم به همان حساب می‌رسد.</summary>
    [Fact]
    public void A_longer_written_name_still_finds_the_account()
    {
        var p = Person("محمد هارون");
        var w = Sheet(Txn("محمد هارون یعقوبی", liters: 4m));

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        Assert.Single(Fuel(p));
    }

    /// <summary>نامی که هیچ حسابی ندارد ⇒ هیچ حسابی هم ساخته نمی‌شود.</summary>
    [Fact]
    public void An_unknown_name_creates_nothing()
    {
        var p = Person("محمد هارون");
        var w = Sheet(Txn("کسی که حساب ندارد", liters: 3m));

        var outcome = WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        Assert.Empty(Fuel(p));
        Assert.Equal(1, outcome.Unmatched);
    }

    /// <summary>حسابِ فرعیِ هم‌نام برندهٔ حسابِ اصلی است.</summary>
    [Fact]
    public void A_sub_account_wins_when_its_own_name_is_written()
    {
        var p = Person("محمد هارون", "هارون تانکر");
        var w = Sheet(Txn("هارون تانکر", liters: 2m));

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        Assert.Empty(p.MainAccount.FuelRows);
        Assert.Single(p.SubAccounts[0].FuelRows);
    }

    // ══ نوع: قرض یا مصرف ═══════════════════════════════════════════════════

    /// <summary>«مصرف» به بخشِ مصارف می‌رود، نه به حسابِ کسی.</summary>
    [Fact]
    public void An_expense_row_goes_to_the_expenses_section()
    {
        var p = Person("محمد هارون");
        var expenses = new List<Expense>();
        var w = Sheet(Txn("چای و نان", amount: 250m, type: WaraqTxnType.Expense));

        WaraqPostingService.Apply(w, new List<Debtor> { p }, expenses, Calc);

        var e = Assert.Single(expenses);
        Assert.Equal("چای و نان", e.Title);
        Assert.Equal(250m, e.Amount);
        Assert.Equal("1405/06/18", e.DateShamsi);
        Assert.Empty(Fuel(p));
    }

    /// <summary>
    /// نوع که از «مصرف» به «قرض» عوض شود، مصرفِ قبلی برداشته می‌شود — وگرنه
    /// همان پول دو جا شمرده می‌شد.
    /// </summary>
    [Fact]
    public void Switching_expense_to_debt_takes_the_expense_back()
    {
        var p = Person("محمد هارون");
        var expenses = new List<Expense>();
        var t = Txn("محمد هارون", amount: 250m, type: WaraqTxnType.Expense);
        var w = Sheet(t);

        WaraqPostingService.Apply(w, new List<Debtor> { p }, expenses, Calc);
        Assert.Single(expenses);

        t.Type = WaraqTxnType.Debt;
        WaraqPostingService.Apply(w, new List<Debtor> { p }, expenses, Calc);

        Assert.Empty(expenses);
        Assert.Single(Fuel(p));
    }

    /// <summary>و برعکس: «قرض» که مصرف شود، از حسابِ طرف برداشته می‌شود.</summary>
    [Fact]
    public void Switching_debt_to_expense_takes_the_row_out_of_the_account()
    {
        var p = Person("محمد هارون");
        var expenses = new List<Expense>();
        var t = Txn("محمد هارون", amount: 250m);
        var w = Sheet(t);

        WaraqPostingService.Apply(w, new List<Debtor> { p }, expenses, Calc);
        Assert.Single(Money(p).Concat(Fuel(p)));

        t.Type = WaraqTxnType.Expense;
        WaraqPostingService.Apply(w, new List<Debtor> { p }, expenses, Calc);

        Assert.Empty(Fuel(p));
        Assert.Empty(Money(p));
        Assert.Single(expenses);
    }

    // ══ واحد: تیل یا پول ════════════════════════════════════════════════════

    /// <summary>«واحد پول» به دفترِ پول می‌رود، نه دفترِ تیل.</summary>
    [Fact]
    public void The_money_unit_lands_in_the_money_ledger()
    {
        var p = Person("محمد هارون");
        var w = Sheet(Txn("محمد هارون", amount: 800m, unit: LedgerMode.Money));

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        Assert.Empty(Fuel(p));
        var row = Assert.Single(Money(p));
        Assert.Equal(800m, row.Bardagi);
        Assert.Equal(0m, row.Liters);          // پول است، لیتر از تقسیم ساخته نمی‌شود
        Assert.True(row.ByMoney);
    }

    /// <summary>واحد که عوض شود، ردیف جابه‌جا می‌شود — نه این‌که دو تا شود.</summary>
    [Fact]
    public void Changing_the_unit_moves_the_row_instead_of_doubling_it()
    {
        var p = Person("محمد هارون");
        var t = Txn("محمد هارون", liters: 10m);
        var w = Sheet(t);

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);
        Assert.Single(Fuel(p));

        t.Unit = LedgerMode.Money;
        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        Assert.Empty(Fuel(p));
        Assert.Single(Money(p));
    }

    // ══ کلیدِ منبع ══════════════════════════════════════════════════════════

    /// <summary>کلید مو‌به‌مو همان کلیدِ سایت است: ‎&lt;ورق&gt;|&lt;شیفت&gt;|&lt;شماره&gt;‎.</summary>
    [Fact]
    public void The_source_key_is_the_same_one_the_site_used()
    {
        var w = new WaraqEntry { Id = 7 };
        Assert.Equal("id7|day|0", WaraqPostingService.SrcKeyOf(w, ShiftKind.Day, 0));
        Assert.Equal("id7|night|3", WaraqPostingService.SrcKeyOf(w, ShiftKind.Night, 3));

        w.LegacyId = "wq1758";
        Assert.Equal("wq1758|day|2", WaraqPostingService.SrcKeyOf(w, ShiftKind.Day, 2));
    }

    /// <summary>همگام‌سازیِ دوباره همان ردیف را تازه می‌کند، نه ردیفِ دوم.</summary>
    [Fact]
    public void Running_twice_updates_the_same_row()
    {
        var p = Person("محمد هارون");
        var t = Txn("محمد هارون", liters: 10m);
        var w = Sheet(t);

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);
        t.Liters = 20m;
        t.AmountAuto = true;
        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        var row = Assert.Single(Fuel(p));
        Assert.Equal(1000m, row.Bardagi);
    }

    /// <summary>رسیدِ دستیِ کاربر روی ردیفِ ورق حفظ می‌شود.</summary>
    [Fact]
    public void A_hand_typed_receipt_survives_the_next_sync()
    {
        var p = Person("محمد هارون");
        var t = Txn("محمد هارون", liters: 10m);
        var w = Sheet(t);

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);
        Fuel(p)[0].Rasid = 200m;

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        Assert.Equal(200m, Fuel(p)[0].Rasid);
    }

    /// <summary>ردیفِ خالی‌شده از همه‌جا پاک می‌شود.</summary>
    [Fact]
    public void An_emptied_row_disappears_from_the_account()
    {
        var p = Person("محمد هارون");
        var t = Txn("محمد هارون", liters: 10m);
        var w = Sheet(t);

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);
        Assert.Single(Fuel(p));

        t.Name = "";
        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        Assert.Empty(Fuel(p));
    }

    /// <summary>
    /// ردیفی که از ورق حذف شود، ثبتش هم می‌رود — این را سایت نداشت: شماره‌ها
    /// جلو می‌آمدند و کلیدِ آخر بی‌صاحب در حساب می‌ماند.
    /// </summary>
    [Fact]
    public void Deleting_the_last_row_does_not_leave_an_orphan()
    {
        var p = Person("محمد هارون");
        var a = Txn("محمد هارون", liters: 10m);
        var b = Txn("محمد هارون", liters: 5m);
        var w = Sheet(a, b);

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);
        Assert.Equal(2, Fuel(p).Count);

        w.Shifts[0].Transactions.Remove(b);
        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        var row = Assert.Single(Fuel(p));
        Assert.Equal(500m, row.Bardagi);
    }

    // ══ جزئیاتِ ردیف ════════════════════════════════════════════════════════

    /// <summary>شمارهٔ حواله از دلِ جمله بیرون می‌آید و در ستونِ خودش می‌نشیند.</summary>
    [Fact]
    public void The_hawala_number_is_split_out_of_the_name()
    {
        var p = Person("محمد هارون");
        var w = Sheet(Txn("محمد هارون حواله 42", liters: 10m));

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        var row = Assert.Single(Fuel(p));
        Assert.Equal("42", row.Hawala);
        Assert.DoesNotContain("حواله", row.Name!);
    }

    /// <summary>متن که «دیزل» بگوید، ردیف دیزل می‌شود و کلمه از نام برداشته می‌شود.</summary>
    [Fact]
    public void The_text_decides_the_fuel_when_it_says_so()
    {
        var p = Person("محمد هارون");
        var w = Sheet(Txn("محمد هارون دیزل", liters: 10m, fuel: FuelType.Diesel));

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        var row = Assert.Single(Fuel(p));
        Assert.Equal(FuelType.Diesel, row.Fuel);
        Assert.DoesNotContain("دیزل", row.Name!);
        Assert.Equal(600m, row.Bardagi);      // ۱۰ لیتر × فیِ دیزلِ ۶۰
    }

    /// <summary>
    /// و وقتی متن چیزی نگفته، ستونِ «نوع تیل»ِ همان ردیف معتبر است — سایت
    /// این را نادیده می‌گرفت و همه را پطرول می‌نوشت.
    /// </summary>
    [Fact]
    public void The_fuel_column_is_used_when_the_text_is_silent()
    {
        var p = Person("محمد هارون");
        var w = Sheet(Txn("محمد هارون", liters: 10m, fuel: FuelType.Diesel));

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        Assert.Equal(FuelType.Diesel, Assert.Single(Fuel(p)).Fuel);
    }

    /// <summary>هر ردیف کلیدِ منبعِ خودش را می‌گیرد و «از ورق» علامت می‌خورد.</summary>
    [Fact]
    public void Every_posted_row_carries_its_source()
    {
        var p = Person("محمد هارون");
        var w = Sheet(Txn("محمد هارون", liters: 1m));

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        var row = Assert.Single(Fuel(p));
        Assert.Equal("waraq", row.Src);
        Assert.Equal("id7|day|0", row.SrcKey);
    }

    /// <summary>شیفتِ شب دفترِ خودش را دارد و کلیدش هم جداست.</summary>
    [Fact]
    public void The_night_shift_has_its_own_keys()
    {
        var p = Person("محمد هارون");
        var w = Sheet(Txn("محمد هارون", liters: 1m));
        var night = w.Shifts[1];
        night.Transactions.Add(new WaraqTransaction { SortIndex = 0, Name = "محمد هارون", Liters = 2m });

        WaraqPostingService.Apply(w, new List<Debtor> { p }, new List<Expense>(), Calc);

        Assert.Equal(2, Fuel(p).Count);
        Assert.Contains(Fuel(p), r => r.SrcKey == "id7|day|0");
        Assert.Contains(Fuel(p), r => r.SrcKey == "id7|night|0");
    }
}
