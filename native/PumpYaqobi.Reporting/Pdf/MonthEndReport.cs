using PumpYaqobi.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <param name="ProfitLocked">
/// بی اجازهٔ «مفاد / ضرر»، «نتیجهٔ خالص» و «مفاد ثبت‌شده» قفل چاپ می‌شوند، نه
/// صفر. در نسخهٔ وب اصلاً سند ساخته نمی‌شد؛ این‌جا بقیهٔ گزارش — که قفلی
/// ندارد — چاپ می‌شود و فقط همان دو خانه قفل می‌ماند.
/// </param>
public sealed record MonthEndReportInput(
    string MonthLabel,
    MonthReport Current,
    MonthReport Previous,
    bool ProfitLocked,
    string Dates);

/// <summary>
/// ══ سندِ گزارش پایان ماه ════════════════════════════════════════════════════
/// بازسازیِ ‎pdfMonthReport()‎: کادرِ بزرگِ «نتیجهٔ خالص» و ده ردیفِ
/// «برچسب · عدد · توضیح».
/// </summary>
public sealed class MonthEndReport : IDocument
{
    private readonly MonthEndReportInput _in;

    public MonthEndReport(MonthEndReportInput input) => _in = input;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "🛢️ پمپ یعقوبی — گزارش پایان ماه",
                         _in.MonthLabel, _in.Dates, Body);

    private static string R(decimal v) =>
        PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    /// <summary>‎gtxt‎ — رشدِ نسبت به ماهِ قبل. ماهِ قبلِ صفر ⇒ «—»، نه ۱۰۰٪.</summary>
    private static string G(decimal cur, decimal prev)
    {
        var pct = MonthReportService.Growth(cur, prev);
        return pct is null ? "—" : (pct > 0 ? "+" : "") + PersianText.Num(pct.Value) + "٪";
    }

    private void Body(IContainer c) => c.Column(col =>
    {
        var cur = _in.Current;
        var prev = _in.Previous;
        var ok = cur.Net >= 0m;
        var netColor = _in.ProfitLocked ? DocStyle.FootFg : ok ? "#059669" : "#e11d48";

        col.Item().Border(2).BorderColor(netColor).Padding(14).AlignCenter()
           .Text(_in.ProfitLocked
                 ? "🔒 نتیجهٔ خالص — با اجازهٔ «مفاد / ضرر» باز می‌شود"
                 : "نتیجهٔ خالص: " + R(cur.Net) + " افغانی — " + (ok ? "مفاد" : "ضرر")
                   + " (نسبت به ماه قبل: " + G(cur.Net, prev.Net) + ")")
           .FontSize(DocStyle.BoxValue + 2).Bold().FontColor(netColor);

        col.Item().PaddingTop(10).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(2.0f);   // برچسب
                cd.RelativeColumn(2.2f);   // عدد
                cd.RelativeColumn(3.0f);   // توضیح
            });

            var i = 0;
            void Row(string label, string value, string sub, string? color = null)
            {
                var even = i++ % 2 == 1;
                DocStyle.TdText(t.Cell(), even, label);
                DocStyle.TdText(t.Cell(), even, value, color);
                DocStyle.TdText(t.Cell(), even, sub, DocStyle.Sub);
            }

            Row("💰 جمله فروش", R(cur.Sales) + " افغانی",
                R(cur.Liters) + " لیتر · رشد: " + G(cur.Sales, prev.Sales));
            Row("⛽ فروش پطرول", R(cur.Petrol.Amount) + " افغانی",
                R(cur.Petrol.Liters) + " لیتر · " + PersianText.Num(cur.Petrol.Parcha) + " پارچه",
                DocStyle.Petrol);
            Row("🟤 فروش دیزل", R(cur.Diesel.Amount) + " افغانی",
                R(cur.Diesel.Liters) + " لیتر · " + PersianText.Num(cur.Diesel.Parcha) + " پارچه",
                DocStyle.Diesel);
            Row("📈 مفاد ثبت‌شده",
                _in.ProfitLocked ? "🔒" : R(cur.Profit) + " افغانی",
                _in.ProfitLocked ? "با اجازهٔ مفاد/ضرر باز می‌شود"
                                 : "رشد: " + G(cur.Profit, prev.Profit));
            Row("🛒 درآمد اضافی", R(cur.Extra) + " افغانی", "خرید عمده از مشتری");
            Row("💸 مصارف", R(cur.Expenses) + " افغانی",
                "رشد: " + G(cur.Expenses, prev.Expenses), DocStyle.Danger);
            Row("🧾 رسید نقدی قرض‌داران", R(cur.Rasid) + " افغانی",
                PersianText.Num(cur.RasidCount) + " رسید", DocStyle.Money);
            Row("🛢️ خرید تیل", R(cur.BuyPetrol.Liters + cur.BuyDiesel.Liters) + " لیتر",
                "جمله " + R(cur.BuyPetrol.Amount + cur.BuyDiesel.Amount) + " افغانی");
            // ⚠️ برچسب عمداً «بردگی/ماندگی» است، نه «ورود/خروج»ی نسخهٔ وب:
            // آن‌جا ‎sin‎ روی بردگی می‌نشست و ‎sout‎ روی ماندگی، یعنی برچسب
            // برعکسِ خودِ بخشِ گاوصندوق درمی‌آمد. این‌جا با همان چیزی که روی
            // صفحه دیده می‌شود یکی است.
            Row("🔐 گاوصندوق (افغانی)", "بردگی " + R(cur.SafeBardagi),
                "ماندگی " + R(cur.SafeMandagi));
            Row("🚚 تخلیهٔ تانکر", PersianText.Num(cur.TankerCount) + " تخلیه",
                cur.TankerShort > 0m ? "جمله کم‌آمد " + R(cur.TankerShort) + " لیتر"
                                     : "کم‌آمدی ثبت نشده");
        });
    });
}
