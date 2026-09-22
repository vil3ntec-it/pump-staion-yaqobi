using System.IO;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «تراز» — خواستهٔ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۰) ═══════════════════════
///
/// «بخشِ ورق‌ها: اون ایکونِ ماه و آفتاب رو حذف کن نباشن و کادرشون کمی تو
/// رفته.» · «هیچ کادرش برابر نیست و نامِ کارمندها از همه متفاوت‌تر است و
/// فاصله‌هاشو چرا رعایت نکردی؟ همه را با دقت درست کن و تراز.»
///
/// ⚠️ این‌ها **سنجهٔ ظاهر** نیستند (عکسِ پیکسلی جای دیگری است)؛ چیزی را
/// قفل می‌کنند که باعثِ آن ناهمواری شده بود: سه پهنا و سه قدِ متفاوت روی
/// یک کارت، و دو نشانِ تزئینی سرِ کارتِ ورق. اگر فردا کسی یکی از این‌ها را
/// برگرداند، همین‌جا سرخ می‌شود — نه در چشمِ صاحبِ سامانه.
/// </summary>
public class FormAlignTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App"))) d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(Root(), Path.Combine(parts)));

    /// <summary>⛔ کارتِ ورق دیگر نشانِ ☀️🌙 ندارد.</summary>
    [Fact]
    public void KarteVaraq_Neshane_MahOAftab_Nadarad()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "WaraqSectionViewModel.cs");
        var card = vm.Split("public sealed class WaraqCardViewModel")[1];
        //  فقط خطِ ساختِ عنوان — کامنت‌ها آزادند که تاریخچه را بگویند
        var line = card.Split("Title =")[1].Split(';')[0];
        Assert.DoesNotContain("☀", line);
        Assert.DoesNotContain("🌙", line);

        //  ⚠️ و ☀️/🌙ی خودِ **انتخابِ** روز/شب دست‌نخورده است: آن‌جا نشان
        //  یک تصمیم است، نه تزئین. برداشتنش یعنی کاربر نمی‌داند کدام را زد.
        var waraqPage = Read("PumpYaqobi.App", "Views", "Sections", "WaraqPageView.axaml");
        Assert.Contains("☀️ روز", waraqPage);
        Assert.Contains("🌙 شب", waraqPage);
    }

    /// <summary>
    /// ⛔ همهٔ خانه‌های فرمِ پارچه یک قالب دارند — یک پهنا، یک قد، یک فاصله.
    /// </summary>
    [Fact]
    public void FormeParcha_HameyeKadrha_Taraz_Ast()
    {
        var view = Read("PumpYaqobi.App", "Views", "Sections", "ParchaSectionView.axaml");
        var form = view.Split("نامِ کارمند + شمارهٔ پایه")[1].Split("Classes=\"saved-badge\"")[0];

        //  ⛔ پهنای ثابتِ کادرِ نام و بلندیِ دستی‌اش رفتند — همان دو چیزی که
        //  آن ردیف را «از همه متفاوت‌تر» کرده بود.
        Assert.DoesNotContain("ColumnDefinitions=\"210", form);
        Assert.DoesNotContain("MinHeight=\"48\"", form);
        Assert.DoesNotContain("Width=\"54\"", form);

        //  ⛔ **هیچ ردیفی ستونِ کناریِ خالی ندارد.** بندِ قبلی («روی هر
        //  ردیف رزرو می‌شود») در ۱۴۰۵/۰۷/۱۲ با عکسِ صاحب ریپو پس گرفته شد:
        //  «اون بغل‌ها خالی گذاشته شدن و کادرش به تمامِ ورق نمی‌رسه.»
        //
        //  ⚠️ و جایش خالی نماند: همان ادعا از درِ تازه گرفته شد — هر
        //  ‎Grid‎ی که آن قالب را دارد باید **واقعاً** خانهٔ دوم داشته باشد،
        //  وگرنه همان نوارِ خالی از درِ دیگر برمی‌گردد.
        var rows = form.Split("ColumnDefinitions=\"*,8,106\"").Length - 1;
        var seconds = form.Split("Grid.Column=\"2\"").Length - 1;
        Assert.Equal(rows, seconds);

        //  ⛔ و آن قالب فقط جایی است که دکمه یا خانهٔ دومِ واقعی هست:
        //  ردیفِ نام (برچسب‌ها و کادرها) و دو ردیفِ پایه.
        Assert.Equal(4, rows);
        Assert.Equal(2, form.Split("Classes=\"wside\"").Length - 1);

        //  ⛔ و هیچ کادری بیرون از آن قالب نمانده: هر TextBox و هر خانهٔ
        //  «خودکار» کلاسِ مشترک را دارد.
        var boxes = form.Split("<TextBox ").Length - 1;
        var wfields = form.Split("Classes=\"wfield\"").Length - 1;
        Assert.Equal(boxes, wfields);
        Assert.Equal(form.Split("Classes=\"calc").Length - 1,
                     form.Split("Classes=\"calc wfield\"").Length - 1);

        //  ⛔ و آن قد یک جا نوشته شده، نه در تک‌تکِ نماها
        var css = Read("PumpYaqobi.App", "Themes", "Controls.axaml");
        Assert.Contains("Selector=\"TextBox.wfield\"", css);
        Assert.Contains("Selector=\"Border.calc.wfield\"", css);
        Assert.Contains("Selector=\"Button.wside\"", css);

        //  ⛔ و دکمهٔ کناری رنگِ شیفتِ خودش را دارد، نه سطحِ کادرِ تایپ:
        //  «اون‌ها دکمه‌ستن، باید متفاوت باشن.» بی این، سبکِ پایهٔ دکمه
        //  (‎Pump.Input‎) دقیقاً هم‌رنگِ کادرِ کنارش می‌شد.
        Assert.Contains("Button.wside.day /template/", css);
        Assert.Contains("Button.wside.night /template/", css);
        //  ⚠️ و زیرِ ماوس هم همان می‌ماند — وگرنه به رنگِ پیش‌فرضِ آوالونیا
        //  برمی‌گشت و همان لحظه دوباره شبیهِ کادر می‌شد.
        Assert.Contains("Button.wside.day:pointerover /template/", css);
        Assert.Contains("Button.wside.night:pointerover /template/", css);
        //  ⚠️ رنگِ تازه‌ای به برنامه اضافه نشد: همان دو رنگِ سربرگِ شیفت.
        Assert.Contains("#f0952b", css);
        Assert.Contains("#7c4dbe", css);

        //  ⛔ و خودِ نما کلاسِ روز/شب را روی **هر دو** دکمه می‌گذارد.
        //  ⚠️ روی ‎form‎ شمرده می‌شود نه کلِ فایل: سربرگِ شیفت و دکمهٔ ذخیره
        //  هم همان کلاس را دارند و بیرونِ این بازه‌اند.
        Assert.Equal(2, form.Split("Classes.day=\"{Binding IsDay}\"").Length - 1);
        Assert.Equal(2, form.Split("Classes.night=\"{Binding IsNight}\"").Length - 1);
    }

    /// <summary>
    /// ⛔ جهتِ کلیدِ چپ/راست <b>سنجیده</b> می‌شود، نه فرض.
    ///
    /// گزارشِ صاحب ریپو دو بار برعکسِ هم بود («راست می‌زنم چپ می‌رود» و بعد
    /// «چپ به راست می‌رود و برعکسش») — یعنی جابه‌جاییِ ثابت جوابِ درستی
    /// نیست. حالا از خودِ کنترل پرسیده می‌شود که مختصات آینه‌شده هست یا نه.
    /// </summary>
    [Fact]
    public void JahateKelid_Sanjide_Mishavad_Na_Farz()
    {
        var src = Read("PumpYaqobi.App", "Services", "FieldNavigation.cs");
        Assert.Contains("private static bool Mirrored(", src);
        Assert.Contains("!Mirrored(from, vr)", src);
        //  ⛔ و جابه‌جایی همچنان فقط در چیدمانِ راست‌به‌چپ است
        Assert.Contains("IsRtl(from)", src);
    }

    /// <summary>
    /// ⛔ واحدِ رسید دو <b>کشویی</b> است، نه یک دکمهٔ چرخشی — و با «پول»
    /// کشوییِ تیل بسته است.
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۰): «چرا اون واحدِ رسید دکمه‌ای
    /// است؟ … یا رسیدِ پول هیچ‌کدوم لازم نیست، نه پطرول نه دیزل.»
    /// </summary>
    [Fact]
    public void VahedeRasid_DoKeshooyi_Ast_Na_Dokme()
    {
        var view = Read("PumpYaqobi.App", "Views", "Sections", "DebtReceiptSectionView.axaml");
        //  ⚠️ با تگِ **بستهٔ** سبک‌ها می‌بُریم: «WrapPanel.Styles» دو بار
        //  می‌آید و بریدن با خودش، تکهٔ اشتباه می‌دهد (همین‌جا یک بار داد).
        var form = view.Split("</WrapPanel.Styles>")[1].Split("</WrapPanel>")[0];

        //  ⛔ دکمهٔ چرخشی رفت
        Assert.DoesNotContain("ToggleUnitCommand", form);
        Assert.DoesNotContain("fuelchip", form);

        //  ⛔ و جایش دو کشویی نشست — دو تصمیمِ جدا، مثلِ دو ستونِ دفتر
        Assert.Contains("Binding UnitOptions", form);
        Assert.Contains("Binding FuelOptions", form);
        Assert.Contains("SelectedIndex=\"{Binding UnitIndex}\"", form);
        Assert.Contains("SelectedIndex=\"{Binding FuelIndex}\"", form);

        //  ⛔ رسیدِ پول ⇒ کشوییِ تیل بسته، و **پنهان نه** (ردیف جابه‌جا نشود)
        Assert.Contains("IsEnabled=\"{Binding IsFuel}\"", form);
        Assert.DoesNotContain("IsVisible=\"{Binding IsFuel}\"", form);
        //  ⛔ و قفل بی‌توضیح نیست
        Assert.Contains("Binding FuelHint", form);

        //  ⛔ تاریخ اولِ اول — نخستین فرزندِ WrapPanel، یعنی سمتِ راست
        var order = new[] { "تاریخ", "نام قرض‌دار", "واحد رسید", "نوع تیل" };
        var at = 0;
        foreach (var label in order)
        {
            var i = form.IndexOf("Text=\"" + label + "\"", System.StringComparison.Ordinal);
            Assert.True(i > at, $"ترتیبِ «{label}» درست نیست");
            at = i;
        }

        //  ⛔ و منطق عوض نشد: همان دو خاصیتِ دفتر پشتِ دو کشویی‌اند
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "DebtReceiptSectionViewModel.cs");
        Assert.Contains("Unit = value == 1 ? LedgerMode.Fuel : LedgerMode.Money", vm);
        Assert.Contains("Fuel = value == 1 ? FuelType.Diesel : FuelType.Petrol", vm);
        Assert.Contains("AddAsync(TypedName, amount, DateShamsi, Note, Unit, Fuel)", vm);
    }

    /// <summary>
    /// ⛔ گامِ پمپ دیوار نیست، و لوکیشن نمی‌خواهد.
    ///
    /// گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۰): «این بخش مانعِ ساختِ حساب
    /// می‌شود… این به سرور چه ربطی دارد؟ اون لوکیشن رو حذف کن لازم نیست،
    /// یارو همین که اسمِ پمپشو بزنه بسه… نمی‌خوام این مشکل پیش بیاد.»
    /// روی صفحه: «❌ خطای داخلی سرور».
    /// </summary>
    [Fact]
    public void GameParcheyePomp_Divar_Nist_Va_Location_Nemikhahad()
    {
        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");
        //  ⛔ کادرِ لوکیشن رفت و برنمی‌گردد
        Assert.DoesNotContain("Binding LoginLocation", xaml);
        Assert.Contains("Binding LoginPump", xaml);

        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        //  ⛔ و در ویومدل هم چیزی از آن نمانده
        Assert.DoesNotContain("LoginLocation", vm);

        //  ⛔ و ۵۰۰ی سرور دیگر کاربر را پشتِ این گام حبس نمی‌کند: پیش از
        //  «نشد»، از خودِ سرور پرسیده می‌شود که پمپ ساخته شده یا نه.
        var step = vm.Split("private Task FinishPumpAsync()")[1].Split("private void SkipPump")[0];
        Assert.Contains("HasStationAsync()", step);
        Assert.Contains("LoginStep = 4", step);

        //  ⛔ و پیامِ «خطای داخلی سرور» بی‌سرنخ نمی‌ماند
        Assert.Contains("PumpStepWhy(res)", step);
        Assert.Contains("کدِ پیگیری", vm);

        //  ⛔ و خودِ پرسش یک درخواستِ ساده است، نه منطقِ دوم
        var link = Read("PumpYaqobi.App", "Services", "CloudLink.cs");
        var has = link.Split("public async Task<bool> HasStationAsync(")[1].Split("\n    }")[0];
        Assert.Contains("/api/pump/me", has);
        //  و هیچ چیزی نمی‌سازد و هیچ چیزی را عوض نمی‌کند
        Assert.DoesNotContain("HttpMethod.Post", has);

        //  ⛔ و به سرور همچنان فقط نام می‌رود — لوکیشن هیچ‌وقت نمی‌رفت
        var ensure = link.Split("public async Task<CloudResult> EnsureStationAsync(")[1]
                         .Split("HasStationAsync")[0];
        Assert.Contains("new { name =", ensure);
        Assert.DoesNotContain("location", ensure);
    }
}