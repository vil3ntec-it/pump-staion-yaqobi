using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.UiTests;

/// <summary>
/// دادهٔ نمونه — فقط برای عکس‌گرفتن در همین ابزار. هرگز داخلِ خودِ برنامه
/// نمی‌رود (بندِ ۴۳: «هیچ داده ساختگی در نسخهٔ نهایی نباشد»).
/// </summary>
internal static class Seed
{
    public static void Fill(AppHost host)
    {
        var today = Shamsi.Today();
        var month = Shamsi.ThisMonth();

        for (var i = 1; i <= 24; i++)
        {
            host.SafeLedger.AddAsync(new SafeEntry
            {
                DateShamsi = $"{month}/{i:00}",
                Kind = i % 3 == 0 ? SafeEntryKind.Bardagi : SafeEntryKind.Mandagi,
                Title = i % 3 == 0 ? "بردگیِ روزانه" : "ماندگیِ شیفتِ روز",
                Amount = 1000 * i + 250,
                Currency = i % 7 == 0 ? Currency.Usd : Currency.Afn,
                Note = i % 5 == 0 ? "با اجازهٔ مدیر" : "",
            }).GetAwaiter().GetResult();
        }

        for (var i = 1; i <= 18; i++)
        {
            host.ExchangeLedger.AddAsync(new ExchangeRow
            {
                DateShamsi = $"{month}/{i:00}",
                Description = "حوالهٔ " + i,
                Amount = 100000 + i * 5000,
                Currency = i % 3 == 0 ? ExchangeCurrency.Kaldar : ExchangeCurrency.Toman,
                Rate = 70 + i % 5,
                Bardagi = i % 4 == 0 ? 300 : 0,
            }).GetAwaiter().GetResult();

            host.ExpenseLedger.AddAsync(new Expense
            {
                DateShamsi = $"{month}/{i:00}",
                Title = i % 2 == 0 ? "نانِ کارمندان" : "ترمیمِ پایه",
                Amount = 500 + i * 120,
                Note = "",
            }).GetAwaiter().GetResult();

            host.RetailLedger.AddAsync(new RetailRow
            {
                DateShamsi = $"{month}/{i:00}",
                Name = "چکنهٔ " + i,
                Fuel = i % 3 == 0 ? PumpYaqobi.Domain.Enums.FuelType.Diesel : PumpYaqobi.Domain.Enums.FuelType.Petrol,
                ByMoney = i % 5 == 0,
                Liters = i % 5 == 0 ? 0 : 10 + i,
                PricePerLiter = 62,
                Bardagi = i % 5 == 0 ? 1500 : 0,
                Rasid = i % 2 == 0 ? 400 : 0,
            }).GetAwaiter().GetResult();
        }

        // چند قرض‌دار با دفترِ تیل و دفترِ پول
        for (var p = 1; p <= 9; p++)
        {
            var d = host.Debtors.AddDebtorAsync("قرض‌دارِ شمارهٔ " + p, "070000000" + p, false)
                        .GetAwaiter().GetResult();
            var full = host.Debtors.LoadFullAsync(d.Id).GetAwaiter().GetResult()!;
            var acc = full.MainAccount!;
            acc.RasidFuelPetrol = 500 * p;
            acc.RasidMoneyPetrol = 20000;
            host.Debt.SetPercent(acc, PumpYaqobi.Domain.Enums.FuelType.Petrol, p % 3 == 0 ? 2 : null);
            host.Debtors.UpdateAccountAsync(acc).GetAwaiter().GetResult();

            for (var i = 1; i <= 6; i++)
            {
                host.Debtors.SaveRowAsync(new DebtRow
                {
                    FuelAccountId = acc.Id,
                    SortIndex = i,
                    DateShamsi = $"{month}/{i:00}",
                    Name = "بردگیِ " + i,
                    Hawala = "ح" + i,
                    Fuel = i % 3 == 0 ? PumpYaqobi.Domain.Enums.FuelType.Diesel
                                      : PumpYaqobi.Domain.Enums.FuelType.Petrol,
                    Liters = 20 * i,
                    PricePerLiter = 62,
                    Rasid = i % 2 == 0 ? 500 : 0,
                }).GetAwaiter().GetResult();
            }
        }

        // شرکت‌های تیل — دو دفترِ جدا
        for (var k = 1; k <= 5; k++)
        {
            var comp = host.Companies.AddAsync("شرکتِ تیلِ " + k).GetAwaiter().GetResult();
            for (var i = 1; i <= 6; i++)
            {
                host.Companies.SaveRowAsync(new CompanyRow
                {
                    CompanyId = comp.Id,
                    Fuel = i % 3 == 0 ? PumpYaqobi.Domain.Enums.FuelType.Diesel
                                      : PumpYaqobi.Domain.Enums.FuelType.Petrol,
                    SortIndex = i,
                    DateShamsi = $"{month}/{i:00}",
                    Name = i % 2 == 0 ? "خرید" : "رسید",
                    Kg = i % 2 == 0 ? 30000 : 0,
                    Usd = i % 2 == 0 ? 690 + i : 0,
                    Rate = 70,
                    Poul = i % 2 == 0 ? 0 : 400000,
                }).GetAwaiter().GetResult();
            }
        }

        // پارچه‌ها — روز و شب
        for (var i = 1; i <= 10; i++)
        {
            var rep = host.ParchaData.AddAsync(PumpYaqobi.Domain.Enums.FuelType.Petrol,
                          $"{month}/{i:00}").GetAwaiter().GetResult();
            host.ParchaData.SaveShiftAsync(rep, ShiftKind.Day, new ShiftData
            {
                Name = "کارمندِ روز " + i, PumpNum = 1,
                Start = 100000 + i * 1000, End = 100000 + i * 1000 + 1200,
                Price = 62, ProfitPer = 2, Debt = 3000,
            }).GetAwaiter().GetResult();
            host.ParchaData.SaveShiftAsync(rep, ShiftKind.Night, new ShiftData
            {
                Name = "کارمندِ شب " + i, PumpNum = 2,
                Start = 200000 + i * 900, End = 200000 + i * 900 + 800,
                Price = 62, ProfitPer = 2, Debt = 1500,
            }).GetAwaiter().GetResult();
        }

        // ورقِ روزانه
        for (var i = 1; i <= 4; i++)
        {
            var w = host.WaraqData.OpenOrCreateAsync($"{month}/{i:00}", "پمپ یعقوبی")
                        .GetAwaiter().GetResult();
            var day = w.Shifts.First(s2 => s2.Kind == ShiftKind.Day);
            day.WorkerName = "کارمندِ روز";
            day.FabricDebt = 12000;
            host.WaraqData.SaveShiftAsync(day).GetAwaiter().GetResult();
            for (var k = 1; k <= 3; k++)
            {
                host.WaraqData.SavePumpAsync(new WaraqPump
                {
                    ShiftId = day.Id, SortIndex = k, Num = k,
                    Fuel = k == 3 ? PumpYaqobi.Domain.Enums.FuelType.Diesel
                                  : PumpYaqobi.Domain.Enums.FuelType.Petrol,
                    Start = 10000 * k, End = 10000 * k + 900 + k * 30,
                    PricePerLiter = 62, Debt = 4000,
                }).GetAwaiter().GetResult();
            }
            var txns = day.Transactions.OrderBy(t => t.SortIndex).Take(4).ToList();
            for (var k = 0; k < txns.Count; k++)
            {
                txns[k].Name = k % 2 == 0 ? "قرضِ " + k : "مصرفِ " + k;
                txns[k].Liters = k % 2 == 0 ? 20 + k : 0;
                txns[k].Amount = k % 2 == 0 ? 0 : 900;
                txns[k].Type = k % 2 == 0 ? WaraqTxnType.Debt : WaraqTxnType.Expense;
                txns[k].AmountAuto = k % 2 == 0 ? true : false;
                host.WaraqData.SaveTxnAsync(txns[k]).GetAwaiter().GetResult();
            }
        }

        _ = today;
    }
}
