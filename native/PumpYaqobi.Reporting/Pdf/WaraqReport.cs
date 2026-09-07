using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

public sealed record WaraqReportInput(
    string Station,
    string DateShamsi,
    ShiftKind Kind,
    WaraqShift Shift,
    string Dates);

/// <summary>
/// ══ ورقِ روزانه ════════════════════════════════════════════════════════════
/// بازسازیِ ‎printWaraq()‎: جدولِ پایه‌ها، دو ستونِ «قرض/مصرف» کنارِ هم، و
/// نوارِ شش‌کادرهٔ جمع‌ها.
///
/// ⚠️ ردیف‌های «قرض/مصرف» عمداً دو ستونی‌اند: در ورقِ کاغذیِ خودِ پمپ هم دو
/// ستون است و یک‌ستونه کردنش ورق را دو برابر می‌کند.
///
/// ⚠️ «قرضِ هر کارمند» نوارِ بالای صفحه است، نه ردیفِ جدا — خواستهٔ صاحب ریپو
/// بود که جای خالیِ کنارِ نامِ کارمند استفاده شود تا ردیف‌های تراکنش کم نشوند.
/// </summary>
public sealed class WaraqReport : IDocument
{
    private const string Blue = "#2b6cb0";
    private const string Purple = "#805ad5";

    private readonly WaraqReportInput _in;
    private readonly WaraqService _calc;

    public WaraqReport(WaraqReportInput input, WaraqService calc) { _in = input; _calc = calc; }

    private bool IsNight => _in.Kind == ShiftKind.Night;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    private static string R(decimal v) =>
        PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    private static string Dash0(decimal v) =>
        v == 0m ? "—" : PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container,
            (IsNight ? "🌙 ورق شب" : "☀️ ورق روز") + " — "
            + (string.IsNullOrWhiteSpace(_in.Station) ? "پمپ" : _in.Station),
            "تاریخ: " + DocStyle.Dash(_in.DateShamsi)
            + (string.IsNullOrWhiteSpace(_in.Shift.WorkerName) ? "" : "  ·  👷 " + _in.Shift.WorkerName),
            _in.Dates, Body, landscape: true, titleColor: Blue);

    private void Body(IContainer c) => c.Column(col =>
    {
        var sd = _in.Shift;
        var t = _calc.ShiftTotals(sd);
        var q = _calc.Shortage(t);

        // ── نوارِ «قرضِ هر کارمند» ─────────────────────────────────────────
        var perWorker = new List<(string Name, decimal Debt)>();
        var i0 = 0;
        foreach (var p in sd.Pumps)
        {
            i0++;
            if (p.Debt == 0m) continue;
            var nm = string.IsNullOrWhiteSpace(p.Worker)
                ? "پایهٔ " + PersianText.Num(p.Num > 0 ? p.Num : i0)
                : p.Worker!.Trim();
            var at = perWorker.FindIndex(x => x.Name == nm);
            if (at >= 0) perWorker[at] = (nm, perWorker[at].Debt + p.Debt);
            else perWorker.Add((nm, p.Debt));
        }

        if (perWorker.Count > 0)
            col.Item().PaddingBottom(5).Text(
                "👷 قرضِ کارمندان:  " + string.Join("   ·   ",
                    perWorker.OrderByDescending(x => x.Debt).Select(x => x.Name + ": " + R(x.Debt))))
               .FontSize(DocStyle.BoxLabel).Bold().FontColor(DocStyle.Danger);

        col.Item().Element(Pumps);
        col.Item().PaddingTop(6).Element(Transactions);
        col.Item().PaddingTop(6).Element(x => Summary(x, t, q));
    });

    /// <summary>جدولِ پایه‌ها — همان ده ستونِ ورقِ کاغذی.</summary>
    private void Pumps(IContainer c) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.7f);   // بایه
            cd.RelativeColumn(2.0f);   // نام
            cd.RelativeColumn(1.0f);   // نوع
            cd.RelativeColumn(2.0f);   // توضیح/ساعت
            cd.RelativeColumn(1.3f);   // شروع
            cd.RelativeColumn(1.3f);   // ختم
            cd.RelativeColumn(1.0f);   // فی لیتر
            cd.RelativeColumn(1.1f);   // لیتر
            cd.RelativeColumn(1.3f);   // مبلغ
            cd.RelativeColumn(1.2f);   // جمله قرض
        });

        t.Header(h =>
        {
            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("بایه"); Th("نام"); Th("نوع"); Th("توضیح/ساعت"); Th("شروع"); Th("ختم");
            Th("فی لیتر"); Th("لیتر"); Th("مبلغ"); Th("جمله قرض");
        });

        var i = 0;
        foreach (var p in _in.Shift.Pumps)
        {
            var even = i % 2 == 1;
            i++;
            // ⚠️ لیترِ منفی در ورق دیده نمی‌شود — همان ‎Math.max(0, end−start)‎
            var liters = Math.Max(0m, p.End - p.Start);
            var amount = liters * p.PricePerLiter;
            void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);

            Td(PersianText.Num(p.Num > 0 ? p.Num : i), DocStyle.Index);
            Td(DocStyle.Dash(p.Worker));
            Td(p.Fuel == FuelType.Diesel ? "دیزل" : "بطرول");
            Td(DocStyle.Dash(p.Note), DocStyle.Sub);
            Td(R(p.Start)); Td(R(p.End));
            Td(R(p.PricePerLiter));
            Td(R(liters), DocStyle.Fuel);
            Td(R(amount), Blue);
            Td(Dash0(p.Debt), DocStyle.Danger);
        }
    });

    /// <summary>«قرض/مصرف» — دو ستونِ کنارِ هم، نیمه‌نیمه، مثلِ ورقِ کاغذی.</summary>
    private void Transactions(IContainer c)
    {
        var sd = _in.Shift;
        var rows = sd.Transactions
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) || _calc.TxnAmount(sd, x) != 0m)
            .ToList();

        var mid = (rows.Count + 1) / 2;      // ‎Math.ceil(n/2)‎
        var left = rows.Take(mid).ToList();
        var right = rows.Skip(mid).ToList();

        c.Row(row =>
        {
            row.RelativeItem().PaddingLeft(6).Element(x => TxnTable(x, left, 0));
            row.RelativeItem().Element(x => TxnTable(x, right, mid));
        });
    }

    private void TxnTable(IContainer c, List<WaraqTransaction> rows, int offset) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.6f);   // 📋
            cd.RelativeColumn(2.4f);   // نام
            cd.RelativeColumn(1.2f);   // مقدار تیل
            cd.RelativeColumn(1.4f);   // مبلغ
            cd.RelativeColumn(1.0f);   // نوع
        });

        t.Header(h =>
        {
            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("📋"); Th("نام"); Th("مقدار تیل"); Th("مبلغ"); Th("نوع");
        });

        if (rows.Count == 0)
        {
            t.Cell().ColumnSpan(5).Element(x => DocStyle.TdText(x, false, "—", DocStyle.FootFg));
            return;
        }

        var i = 0;
        foreach (var x in rows)
        {
            var even = i % 2 == 1;
            var n = offset + ++i;
            var debt = x.Type == WaraqTxnType.Debt;
            void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(x.Name));
            Td(Dash0(x.Liters), DocStyle.Fuel);
            Td(Dash0(_calc.TxnAmount(_in.Shift, x)), debt ? DocStyle.Danger : Purple);
            Td(debt ? "قرض" : "مصرف", DocStyle.Sub);
        }
    });

    private void Summary(IContainer c, WaraqShiftTotals t, WaraqShortage q) => c.Row(row =>
    {
        var label = q.Shortage > 0m ? "⚠️ کمبودی" : q.Excess > 0m ? "✅ اضافی" : "✅ بدون کمبود";
        var value = q.Shortage > 0m ? q.Shortage : q.Excess > 0m ? q.Excess : 0m;
        var color = q.Shortage > 0m ? "#d97706" : DocStyle.Money;

        void Box(string l, string v, string cl, bool last = false)
        {
            var it = last ? row.RelativeItem() : row.RelativeItem().PaddingLeft(6);
            it.Element(x => DocStyle.SumBox(x, l, v, cl));
        }

        Box("⛽ بطرول", R(t.PetrolLiters) + " لیتر", DocStyle.Petrol);
        Box("🟤 دیزل", R(t.DieselLiters) + " لیتر", DocStyle.Diesel);
        Box("🟣 مصرف", R(t.Expenses), Purple);
        Box("💳 قرض", R(t.Debt), DocStyle.Danger);
        Box(label, R(value), color);
        Box("📊 فروش", R(t.Sales), Blue, last: true);
    });
}
