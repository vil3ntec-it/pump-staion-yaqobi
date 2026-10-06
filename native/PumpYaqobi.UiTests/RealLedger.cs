using Microsoft.Data.Sqlite;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ دفترِ واقعی‌نما برای ‎monthshift all‎ و سنجهٔ برابری (۱۴۰۵/۰۷/۲۱) ══════════
///
/// ⚠️ این دفترِ خودِ صاحب ریپو نیست (آن در دسترس نیست)؛ ساختگی است ولی به شکلِ
/// دفترِ واقعیِ یک پمپ پس از دو سال، و بازساختنی (بذرِ ثابت):
///
///   • ‎YearsAudit.Seed‎ با ‎PUMP_YEARS=2‎: هر روز ورق با دو شیفت، پارچهٔ پطرول و
///     دیزل، ۶۰۰ قرض‌دار (اصلی + فرعی، دفترِ تیل)، شرکت‌ها، گاوصندوق، مصارف،
///     صرافی، چکنه، خرید و میله‌زنیِ مخزن، فاکتور، حاضری، امانت، آرشیو…
///   • این‌جا افزوده می‌شود: دفترِ <b>پول</b> و رسیدِ تیلِ چهل حساب در نُه ماهِ اخیر
///     (۱۴۰۴/۱۱ تا ماهِ جاری — از مرزِ سال می‌گذرد)، فاکتورهای در صفِ ماهِ اخیر،
///   • و ردِ نسخه‌های پیشین: بردگیِ گردنشده (فیِ اعشاری)، الباقیِ کهنه، ردیفِ «پولیِ»
///     خراب (پولی با بردگیِ صفر و لیتر)، رسیدِ کهنهٔ سربرگِ حسابِ مهاجرت‌کرده، و
///     ‎DateKey = 0‎ / ماهِ خالی روی ده درصدِ ردیف‌ها.
/// </summary>
internal static class RealLedger
{
    public sealed record Stats(int MoneyRows, int RasidFuelRows, int StaleRows, int ZeroKeys, int StaleHeads, int Pending);

    public static Stats Build(string file)
    {
        YearsAudit.Seed(file);
        var rnd = new Random(1405);
        var today = Shamsi.Today();
        var todayKey = Shamsi.Key(today);
        var y = int.Parse(today[..4]);
        var m = int.Parse(today[5..7]);
        //  نُه ماهِ اخیر، از ۱۴۰۴/۱۱ تا ماهِ جاری
        var months = new List<string>();
        for (var k = 8; k >= 0; k--)
        {
            var mm = m - k; var yy = y;
            while (mm <= 0) { mm += 12; yy--; }
            months.Add($"{yy}/{mm:00}");
        }
        string Day(int i)
        {
            var mo = months[i % months.Count];
            var d = $"{mo}/{1 + (i * 7) % 28:00}";
            return Shamsi.Key(d) > todayKey ? $"{mo}/01" : d;
        }

        using var c = new SqliteConnection("Data Source=" + file);
        c.Open();
        using var tx = c.BeginTransaction();
        SqliteCommand Cmd(string sql)
        {
            var x = c.CreateCommand(); x.Transaction = tx; x.CommandText = sql; return x;
        }
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var accts = new List<long>();
        using (var q = Cmd("SELECT Id FROM DebtAccounts ORDER BY Id LIMIT 40"))
        using (var r = q.ExecuteReader()) while (r.Read()) accts.Add(r.GetInt64(0));

        int money = 0, rf = 0;
        foreach (var (a, ai) in accts.Select((a, i) => (a, i)))
        {
            for (var k = 0; k < (ai == 0 ? 60 : 18); k++)
            {
                var b = 3000 + rnd.Next(0, 40) * 250;
                var rs = k % 3 == 0 ? rnd.Next(1, 8) * 500 : 0;
                using var ins = Cmd("INSERT INTO DebtRows (MoneyAccountId, SortIndex, DateShamsi, DateKey, Name, Fuel, Liters, Bardagi, Rasid, RasidFuel, Albaqi, ByMoney, CreatedAt, UpdatedAt) " +
                                    "VALUES ($a,$s,$d,$k,$n,$f,'0',$b,$r,'0',$al,1,$t,$t)");
                var d = Day(k + ai);
                ins.Parameters.AddWithValue("$a", a); ins.Parameters.AddWithValue("$s", 1000 + k);
                ins.Parameters.AddWithValue("$d", d); ins.Parameters.AddWithValue("$k", Shamsi.Key(d));
                ins.Parameters.AddWithValue("$n", "بردگیِ پول " + k); ins.Parameters.AddWithValue("$f", k % 3 == 0 ? 2 : 1);
                ins.Parameters.AddWithValue("$b", b.ToString()); ins.Parameters.AddWithValue("$r", rs.ToString());
                ins.Parameters.AddWithValue("$al", (b - rs).ToString()); ins.Parameters.AddWithValue("$t", now);
                ins.ExecuteNonQuery(); money++;
            }
            for (var k = 0; k < 6; k++)
            {
                using var ins = Cmd("INSERT INTO DebtRows (FuelAccountId, SortIndex, DateShamsi, DateKey, Name, Fuel, Liters, Bardagi, Rasid, RasidFuel, Albaqi, ByMoney, CreatedAt, UpdatedAt) " +
                                    "VALUES ($a,$s,$d,$k,'رسیدِ تیل',$f,'0','0','0',$rf,'0',0,$t,$t)");
                var d = Day(k * 3 + ai);
                ins.Parameters.AddWithValue("$a", a); ins.Parameters.AddWithValue("$s", 2000 + k);
                ins.Parameters.AddWithValue("$d", d); ins.Parameters.AddWithValue("$k", Shamsi.Key(d));
                ins.Parameters.AddWithValue("$f", k % 2 == 0 ? 1 : 2); ins.Parameters.AddWithValue("$rf", (20 + k * 5).ToString());
                ins.Parameters.AddWithValue("$t", now);
                ins.ExecuteNonQuery(); rf++;
            }
        }

        //  حسابِ مهاجرت‌کرده: چهار رسیدِ سربرگ = جمعِ ردیف‌ها (همان حالِ درستِ امروز)
        foreach (var a in accts)
        {
            decimal fp = 0, fd = 0, mp = 0, md = 0;
            using (var q = Cmd("SELECT FuelAccountId, Fuel, RasidFuel, Rasid FROM DebtRows WHERE FuelAccountId=$a OR MoneyAccountId=$a"))
            {
                q.Parameters.AddWithValue("$a", a);
                using var r = q.ExecuteReader();
                while (r.Read())
                {
                    var fuelBook = !r.IsDBNull(0);
                    var diesel = r.GetInt32(1) == 2;
                    var rfv = decimal.Parse(Convert.ToString(r.GetValue(2), System.Globalization.CultureInfo.InvariantCulture)!, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                    var rsv = decimal.Parse(Convert.ToString(r.GetValue(3), System.Globalization.CultureInfo.InvariantCulture)!, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                    if (fuelBook) { if (diesel) fd += rfv; else fp += rfv; }
                    else { if (diesel) md += rsv; else mp += rsv; }
                }
            }
            using var up = Cmd("UPDATE DebtAccounts SET ReceiptsMigrated=1, RasidFuelPetrol=$fp, RasidFuelDiesel=$fd, RasidMoneyPetrol=$mp, RasidMoneyDiesel=$md WHERE Id=$a");
            up.Parameters.AddWithValue("$fp", fp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            up.Parameters.AddWithValue("$fd", fd.ToString(System.Globalization.CultureInfo.InvariantCulture));
            up.Parameters.AddWithValue("$mp", mp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            up.Parameters.AddWithValue("$md", md.ToString(System.Globalization.CultureInfo.InvariantCulture));
            up.Parameters.AddWithValue("$a", a);
            up.ExecuteNonQuery();
        }

        //  ── ردِ نسخه‌های پیشین ──
        var first = accts[0];
        int Exec(string sql) { using var x = Cmd(sql); return x.ExecuteNonQuery(); }
        //  فیِ اعشاری با بردگیِ گردنشده و الباقیِ کهنه (نسخهٔ وب گرد نمی‌کرد)
        var stale = Exec($"UPDATE DebtRows SET PricePerLiter='62.35', Bardagi=CAST(Liters AS REAL)*62.35, Albaqi=CAST(Liters AS REAL)*62.35 " +
                         $"WHERE FuelAccountId={first} AND ByMoney=0 AND CAST(Liters AS REAL) > 0 AND Id % 3 = 0");
        stale += Exec($"UPDATE DebtRows SET Albaqi=Bardagi WHERE MoneyAccountId IN ({string.Join(",", accts)}) AND Rasid <> '0' AND Id % 2 = 0");
        //  ردیفِ «پولیِ» خراب
        stale += Exec($"UPDATE DebtRows SET ByMoney=1, Bardagi='0' WHERE FuelAccountId IN ({string.Join(",", accts.Take(10))}) AND ByMoney=0 AND CAST(Liters AS REAL) > 0 AND Id % 13 = 0");
        //  رسیدِ کهنهٔ سربرگ
        var heads = Exec($"UPDATE DebtAccounts SET RasidMoneyPetrol=RasidMoneyPetrol+7000, RasidFuelPetrol=RasidFuelPetrol+40 WHERE Id IN ({string.Join(",", accts.Where((_, i) => i % 5 == 0))})");
        //  کلیدِ تاریخِ صفر و ماهِ خالی
        var zero = Exec("UPDATE DebtRows SET DateKey=0 WHERE Id % 10 = 0");
        zero += Exec("UPDATE SafeEntries SET DateKey=0, MonthKey=NULL WHERE Id % 10 = 0");
        zero += Exec("UPDATE Expenses SET DateKey=0, MonthKey='' WHERE Id % 10 = 0");
        //  فاکتورهای ماهِ اخیر در صف
        var pending = Exec($"UPDATE Invoices SET Status=1 WHERE DateKey >= {Shamsi.Key(months[^2] + "/01")} AND Id % 2 = 0");
        tx.Commit();
        return new Stats(money, rf, stale, zero, heads, pending);
    }
}
