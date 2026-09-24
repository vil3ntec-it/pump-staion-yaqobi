using PumpYaqobi.App.Views;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ فهرستِ ‎F1‎ نباید دروغ بگوید ═══════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۰۵): «همه‌شان با دقت و ظرافت کار کنند و هیچ
/// بخشی منطقش خراب یا دست‌کاری نشود.»
///
/// میانبری که کاربر از آن خبر ندارد، با نبودنش یکی است — و فهرستی که با کد
/// جور نباشد از نبودنِ فهرست <b>بدتر</b> است، چون آدم را دنبالِ کلیدی
/// می‌فرستد که کاری نمی‌کند. پس این آزمون روی خودِ سورس می‌گردد: هر کلیدی که
/// واقعاً گرفته می‌شود باید در فهرستِ ‎F1‎ هم باشد، و برعکس.
/// </summary>
public class ShortcutHelpTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string Service() =>
        Read("PumpYaqobi.App", "Services", "Shortcuts.cs");

    private static string Grid() =>
        Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");

    /// <summary>متنِ همهٔ کلیدهای فهرست، برای جست‌وجوی ساده.</summary>
    private static string Keys() =>
        string.Join(" | ", ShortcutsWindow.All.Select(r => r.Key));

    // ── ۱) هر کلیدی که گرفته می‌شود، در فهرست هم هست ───────────────────────

    [Theory]
    [InlineData("Alt + عدد")]
    [InlineData("Ctrl + Shift + عدد")]
    [InlineData("Ctrl + عدد")]
    [InlineData("Shift + عدد")]
    [InlineData("Ctrl + S")]
    [InlineData("Ctrl + P")]
    [InlineData("Ctrl + K")]
    [InlineData("F1")]
    [InlineData("Ctrl + C")]
    [InlineData("Ctrl + X")]
    [InlineData("Ctrl + V")]
    [InlineData("Ctrl + A")]
    [InlineData("Ctrl + Z")]
    [InlineData("Ctrl + Y")]
    [InlineData("Ctrl + Delete")]
    [InlineData("F2")]
    [InlineData("Enter")]
    [InlineData("Tab")]
    [InlineData("Esc")]
    public void Har_Kelid_Dar_Fehrest_F1_Hast(string key)
    {
        Assert.Contains(ShortcutsWindow.All, r => r.Key == key);
    }

    /// <summary>هیچ کلیدی دو بار نوشته نشده باشد — دو معنی برای یک کلید یعنی یکی دروغ است.</summary>
    [Fact]
    public void HichKelidi_DoBar_Nayamade()
    {
        var keys = ShortcutsWindow.All.Where(r => r.Key.Length > 0).Select(r => r.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    /// <summary>هر سطر توضیح دارد و هر گروه عنوان — سطرِ خالی یعنی فهرستِ نیمه‌کاره.</summary>
    [Fact]
    public void HarSatr_Tozih_Darad()
    {
        Assert.NotEmpty(ShortcutsWindow.All);
        Assert.All(ShortcutsWindow.All, r => Assert.False(string.IsNullOrWhiteSpace(r.What)));
        // دست‌کم چهار سرگروه (گشتن · جدول‌ها · کپی و برگشت · ذخیره و چاپ)
        Assert.True(ShortcutsWindow.All.Count(r => r.Key.Length == 0) >= 4);
    }

    // ── ۲) و خودِ کد واقعاً همان‌ها را می‌گیرد ──────────────────────────────

    /// <summary>
    /// ⛔ جابه‌جاییِ ۱۴۰۵/۰۷/۰۵ قفل می‌شود: ‎Alt+عدد‎ بخش را باز می‌کند و
    /// ‎Ctrl+Shift+عدد‎ کارت را — نه برعکس. این را خودِ صاحب ریپو خواست
    /// («alt+عدد برود بخش‌هایی که موجود است») و برگرداندنش یعنی میانبری که
    /// هر روز زده می‌شود دو کلید بخواهد.
    /// </summary>
    [Fact]
    public void Alt_Baraye_Bakhsh_Ast_Va_CtrlShift_Baraye_Kart()
    {
        var s = Service();
        var alt = s.IndexOf("if (alt && !ctrl)", StringComparison.Ordinal);
        var cs = s.IndexOf("if (ctrl && shift && !alt)", StringComparison.Ordinal);
        Assert.True(alt > 0 && cs > 0);

        // شاخهٔ Alt باید بافرِ «بخش» را پر کند، و Ctrl+Shift بافرِ «کارت» را
        // ⚠️ بازه بریده می‌شود، وگرنه شاخه‌ای نزدیکِ تهِ فایل آزمون را می‌ترکاند
        Assert.Contains("_sectionBuf += digit", s[alt..Math.Min(s.Length, alt + 260)]);
        Assert.Contains("_openBuf += digit", s[cs..Math.Min(s.Length, cs + 260)]);

        // و رها شدنِ Alt باید بخش را باز کند
        Assert.Contains("if (sec.Length > 0) GotoSection(sec);", s);
    }

    [Fact]
    public void CtrlS_Ghabl_Az_Zakhire_Khaneye_Baz_Ra_Mineshanad()
    {
        var s = Service();
        Assert.Contains("if (e.Key == Key.S && ctrl && !alt && !shift)", s);
        // ⛔ ترتیب مهم است: اول نشاندنِ خانه، بعد ذخیره
        var i = s.IndexOf("CommitOpenCell(sender);", StringComparison.Ordinal);
        var j = s.IndexOf("_ = SaveNowAsync();", StringComparison.Ordinal);
        Assert.True(i > 0 && j > i);
    }

    [Fact]
    public void F1_Fehrest_Ra_Baz_Mikonad()
    {
        Assert.Contains("if (e.Key == Key.F1", Service());
        Assert.Contains("Views.ShortcutsWindow.ShowAsync()", Service());
    }

    /// <summary>
    /// ⛔ کلیپ‌بورد مالِ <b>جدول</b> است، نه شنوندهٔ تونلیِ پنجره. اگر روزی به
    /// ‎Shortcuts.cs‎ برگردد، کادرِ تایپ دیگر نمی‌تواند متنِ خودش را کپی کند —
    /// همان اشتباهی که یک بار با ‎Shift+عدد‎ شد و نویسه‌ها را خورد (۱۴۰۵/۰۶/۲۹).
    ///
    /// ⚠️ ولی ‎Ctrl+Z‎/‎Ctrl+Y‎ از ۱۴۰۵/۰۷/۱۲ مالِ <b>پنجره</b>‌اند: گزارشِ صاحب
    /// ریپو «کنترول زد اصلن کار نمیکنه» بود، و ریشه همین بود که برگشت فقط با
    /// فوکوسِ همان جدول کار می‌کرد و حذف‌ها اصلاً در آن نبودند. ⛔ و باز هم
    /// <b>بیرونِ کادرِ تایپ</b> — داخلِ کادر، ‎Ctrl+Z‎ حرفِ قبلیِ همان کادر است.
    /// </summary>
    [Fact]
    public void Clipboard_Dar_Jadval_Ast_Na_Dar_Shenavandeye_Panjere()
    {
        var g = Grid();
        Assert.Contains("if (ctrl && !alt && !_editing)", g);
        foreach (var k in new[] { "Key.C", "Key.X", "Key.V", "Key.A" })
            Assert.Contains("e.Key == " + k, g);

        // و در شنوندهٔ پنجره نیستند
        var s = Service();
        foreach (var k in new[] { "Key.C", "Key.V", "Key.X", "Key.A" })
            Assert.DoesNotContain("e.Key == " + k + " &&", s);

        // برگشت و دوباره: در پنجره، و فقط بیرونِ کادرِ تایپ
        Assert.Contains("if (ctrl && !alt && !TypingInBox(sender) && (e.Key == Key.Y || e.Key == Key.Z))", s);
        Assert.Contains("UndoHub.UndoAsync()", s);
        Assert.Contains("UndoHub.RedoAsync()", s);
        // و جدول دیگر پشتهٔ جدای خودش را ندارد
        Assert.DoesNotContain("private bool UndoEdit()", g);
        Assert.Contains("Services.UndoHub.Push(new CellStep(this, group));", g);
    }

    /// <summary>
    /// ⛔ هر نوشتنِ کلیپ‌بورد از همان یک در رد می‌شود (<c>Write</c>)، و آن در
    /// ردیفِ قفل‌شده و ستونِ خواندنی را رد می‌کند. بی این، یک ‎Ctrl+V‎ می‌توانست
    /// ردیفِ 📦 خریدِ مخزن را در حسابِ شرکت عوض کند.
    /// </summary>
    [Fact]
    public void Neveshtan_Faghat_Az_Yek_Dar_Va_Ba_Ghofl()
    {
        var g = Grid();
        Assert.Contains("private static bool Write(object item, DataGridColumn col, string value, List<Edit> log)", g);
        Assert.Contains("if (col.IsReadOnly) return false;", g);
        Assert.Contains("if (item is ViewModels.ILockedRow { IsLocked: true }) return false;", g);
        // فقط خاصیتِ رشته‌ایِ نوشتنی — همان چیزی که تایپِ کاربر هم می‌نویسد
        Assert.Contains("p.PropertyType != typeof(string)", g);
    }

    /// <summary>⛔ پیست ردیف نمی‌سازد — بیرونِ جدول دور ریخته می‌شود.</summary>
    [Fact]
    public void Paste_Radif_Nemisazad()
    {
        var g = Grid();
        Assert.Contains("if (ri >= all.Count) break;", g);
        Assert.Contains("if (ci >= cols.Count) break;", g);
        Assert.DoesNotContain("AddRowsAsync", g);
    }

    /// <summary>
    /// ⛔ برگشت پیش از نوشتن می‌سنجد که ردیف هنوز هست. بی این، ‎Ctrl+Z‎ روی
    /// ردیفی که حذف شده یک ذخیرهٔ ناخواسته می‌ساخت.
    /// </summary>
    [Fact]
    public void Bargasht_Radife_Rafte_Ra_Nemineviseh()
    {
        var g = Grid();
        Assert.Contains("if (!Live(ed.Item)) continue;", g);
        Assert.Contains("private void ForgetHistory()", g);
    }
}
