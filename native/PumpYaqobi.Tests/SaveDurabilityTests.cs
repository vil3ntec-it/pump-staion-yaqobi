using System.Text.RegularExpressions;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «هیچ نوشته‌ای گم نشود» — با رفتار، نه با خواندنِ رشته ═══════════════════
///
/// گزارشِ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «من چندین ورق رو پر کردم اما با
/// اعتماد تو همه‌شون پاک شدن و هیچ چیزی ثبت نشده بودن… برنامه رو برای اسان
/// شدن کار ام خاستم ولی این داره دو برابر اش میکنه.»
///
/// ⛔ سه سوراخ بود و هر سه بی‌صدا — شرحشان در <see cref="SaveGuard"/>. این
/// فایل هر سه را قفل می‌کند، و <b>دندانش سنجیده شد</b>: با برگرداندن کدِ
/// پیش از اصلاح، بندهای ۱ و ۲ و ۳ سرخ می‌شوند.
/// </summary>
public class SaveDurabilityTests
{
    /// <summary>ردیفی که می‌شود گفت چند بار شکست بخورد.</summary>
    private sealed class Flaky : RowViewModel
    {
        private int _left;
        public int Saves;
        public int Applies;

        public Flaky(int failFirst) => _left = failFirst;

        public void Edit() => Touch();

        protected override void Apply() => Applies++;

        protected override Task SaveAsync()
        {
            Saves++;
            if (_left-- > 0) throw new InvalidOperationException("دیسک قفل است");
            return Task.CompletedTask;
        }
    }

    // ══ ۱) نوشتنِ شکست‌خورده دوباره تلاش می‌کند ══════════════════════════════

    [Fact]
    public async Task Neveshtane_ShekastKhorde_Dobare_Talash_Mikonad()
    {
        var row = new Flaky(failFirst: 2);
        row.Edit();
        Assert.True(row.IsDirty);

        await row.FlushAsync();

        Assert.False(row.IsDirty);
        Assert.Equal(3, row.Saves);      // دو شکست، بعد موفق
    }

    // ══ ۲) شکستِ کامل: ساکت نمی‌ماند، و ردیف را هم پاک نمی‌کند ═══════════════

    [Fact]
    public async Task ShekasteKamel_Sedaash_Darmiayad_Va_Radif_Kasif_Mimanad()
    {
        var said = new List<string>();
        void Listen(string w) => said.Add(w);
        SaveGuard.Failed += Listen;
        try
        {
            var row = new Flaky(failFirst: 99);
            row.Edit();

            //  ⛔ استثنا بیرون نمی‌دهد — وگرنه یک ردیفِ خراب جلوی نوشتنِ بقیه
            //  را می‌گرفت و بسته شدنِ برنامه هم می‌ماسید.
            await row.FlushAsync();

            Assert.True(row.IsDirty, "ردیفِ ننوشته نباید «نوشته شد» دیده شود");
            Assert.NotEmpty(said);
        }
        finally { SaveGuard.Failed -= Listen; }
    }

    // ══ ۳) بسته شدنِ برنامه: هر نوشتهٔ در صف می‌نشیند ════════════════════════

    [Fact]
    public async Task BastanE_Barname_Har_Neveshteye_DarSaf_Ra_Mineshanad()
    {
        //  ⚠️ عمداً هیچ‌کس ‎FlushAsync‎ِ این ردیف را نمی‌زند: کاربر تایپ کرده
        //  و همان لحظه ✕ را زده. پیش از این اصلاح، همین ردیف می‌رفت.
        var row = new Flaky(failFirst: 0);
        row.Edit();
        Assert.True(row.IsDirty);

        //  ⚠️ عددِ برگشتی سنجیده **نمی‌شود**: فهرستِ نگهبان استاتیک است و
        //  ردیفِ عمداً-خرابِ بندِ ۲ ممکن است هنوز در همان فهرست باشد. ادعا
        //  روی خودِ این ردیف است، که یکتا و قطعی است.
        await SaveGuard.FlushAllAsync(TimeSpan.FromSeconds(8));

        Assert.False(row.IsDirty);
        Assert.Equal(1, row.Saves);
        GC.KeepAlive(row);
    }

    [Fact]
    public async Task Radife_DastNakhorde_Hich_Neveshtani_Nadarad()
    {
        var row = new Flaky(failFirst: 0);
        await row.FlushAsync();
        Assert.Equal(0, row.Saves);      // قاعدهٔ «ردیفِ دست‌نخورده ذخیره نمی‌شود»
        GC.KeepAlive(row);
    }

    // ══ ۴) و سمتِ پوسته — روی خودِ سورس ══════════════════════════════════════

    private static readonly string App =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                                      "PumpYaqobi.App"));

    private static string Src(params string[] parts) =>
        File.ReadAllText(Path.Combine(App, Path.Combine(parts)));

    /// <summary>
    /// ⛔ تا ۳.۱.۱۵۷ در کلِ برنامه <b>یک</b> شنوندهٔ <c>Closing</c> نبود
    /// (گشته شد: صفر)، پس زدنِ ✕ وسطِ تایپ یعنی نوشتهٔ رفته. این بند همان
    /// را نگه می‌دارد.
    /// </summary>
    [Fact]
    public void Bastane_Panjere_Aval_Minevisad_Bad_Mibandad()
    {
        var w = Src("Views", "MainWindow.axaml.cs");
        Assert.Contains("Closing +=", w);
        //  ترتیب: خانهٔ باز ⇒ نوشتن ⇒ بستن
        var commit = w.IndexOf("CommitFocused", StringComparison.Ordinal);
        var flush = w.IndexOf("FlushEverythingAsync", StringComparison.Ordinal);
        var close = w.IndexOf("Close();", StringComparison.Ordinal);
        Assert.True(commit > 0 && flush > commit,
            "خانهٔ بازِ ویرایش باید **پیش از** نوشتن بسته شود");
        Assert.True(close > flush, "پنجره پیش از نوشتن بسته می‌شود");
    }

    /// <summary>
    /// ⛔ ذخیره دیگر با <c>_ = …</c> رها نمی‌شود: استثنای «مشاهده‌نشده» یعنی
    /// ردیفی که تا ابد کثیف می‌ماند و کاربر هیچ نمی‌بیند.
    /// </summary>
    [Fact]
    public void Zakhire_Raha_Nemishavad()
    {
        var row = Src("ViewModels", "RowViewModel.cs");
        Assert.DoesNotContain("_ = SaveAsync()", row);
        Assert.Contains("SaveGuard.Track(this)", row);
        Assert.Contains("IPendingWrite", row);
    }

    /// <summary>
    /// ⛔ و بخشی که نتوانست فهرستش را بخواند، «خالی» نشان داده نمی‌شود:
    /// کاربر یک فهرستِ خالی را «همه‌چیز پاک شد» می‌خواند و حق هم دارد.
    /// </summary>
    [Fact]
    public void Khandane_Fehrest_Ke_Nashod_Sakete_Nemimanad()
    {
        foreach (var f in new[] { "WaraqSectionViewModel.cs", "ParchaSectionViewModel.cs",
                                  "StorageSectionViewModel.cs", "AttendanceSectionViewModel.cs",
                                  "DebtReceiptSectionViewModel.cs", "OldLoansSectionViewModel.cs" })
        {
            var s = Src("ViewModels", "Sections", f);
            var bare = Regex.Matches(s, @"_ = (Reload|Load|Refresh)Async\(\)").Count;
            Assert.True(bare == 0, f + " هنوز فهرستش را بی‌صدا می‌خواند");
        }
    }

    // ══ ۵) نوشتن‌های تکی (سربرگِ ورق، حسابِ قرض‌دار، امانت…) ═════════════════
    //
    //  ⛔ گشتنِ دوباره پس از اصلاحِ ردیف‌ها نشان داد نُه نوشتنِ دیگر هنوز با
    //  ‎_ = …‎ رها می‌شدند — از جمله سربرگِ ورق (نامِ کارمند و قرضِ پارچه).
    //  بستنِ برنامه منتظرشان نمی‌ماند و شکستشان هیچ‌جا دیده نمی‌شد.

    [Fact]
    public async Task Bastane_Barname_Montazere_Neveshtane_Taki_Mimanad()
    {
        var tcs = new TaskCompletionSource();
        SaveGuard.Watch(tcs.Task, "آزمونِ نگهبان");

        var flush = SaveGuard.FlushAllAsync(TimeSpan.FromSeconds(8));
        //  ⚠️ قطعی است نه زمانی: تا این نوشته تمام نشده، ‎FlushAllAsync‎
        //  نمی‌تواند تمام شود.
        Assert.False(flush.IsCompleted, "بستنِ برنامه منتظرِ نوشتنِ در جریان نماند");

        tcs.SetResult();
        await flush;
        Assert.True(flush.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Neveshtane_Taki_Ke_Nashod_Sedaash_Darmiayad()
    {
        var heard = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Listen(string w) { if (w.Contains("آزمونِ شکست")) heard.TrySetResult(w); }
        SaveGuard.Failed += Listen;
        try
        {
            SaveGuard.Watch(Task.FromException(new IOException("دیسک قفل است")), "آزمونِ شکست");
            var said = await heard.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains("ذخیره نشد", said);
            Assert.Contains("IOException", said);
        }
        finally { SaveGuard.Failed -= Listen; }
    }

    /// <summary>
    /// ⛔ و هیچ‌کدام از آن نُه جا به ‎_ = …‎ برنمی‌گردد. ⚠️ بی تلاشِ دوباره،
    /// عمداً: افزودنِ رسید تکرارپذیر نیست و تلاشِ دوباره یعنی رسیدِ دوتایی.
    /// </summary>
    [Fact]
    public void Neveshtanhaye_Taki_Raha_Nemishavand()
    {
        var cases = new (string File, string Bare, int Watched)[]
        {
            ("WaraqSectionViewModel.cs", "_ = _host.WaraqData.SaveShiftAsync(", 1),
            ("AmanatSectionViewModel.cs", "_ = _host.Amanat.UpdateAccountAsync(", 1),
            ("PersonViewModel.cs", "_ = _host.Debtors.Update", 4),
            ("PersonViewModel.cs", "_ = AddHeadReceiptAsync(", 1),
            ("DebtArchivePageViewModel.cs", "_ = PersistAsync();", 4),
        };
        foreach (var (file, bare, watched) in cases)
        {
            var s = Src("ViewModels", "Sections", file);
            Assert.DoesNotContain(bare, s);
            Assert.True(Regex.Matches(s, @"SaveGuard\.Watch\(").Count >= watched,
                file + " نوشتنِ تکی‌اش را به نگهبان نسپرده");
        }
        var notes = Src("ViewModels", "SectionNotesViewModel.cs");
        Assert.DoesNotContain("_ = _svc.SaveDraftAsync(", notes);
        Assert.Contains("SaveGuard.Watch(_svc.SaveDraftAsync(", notes);
    }
}
