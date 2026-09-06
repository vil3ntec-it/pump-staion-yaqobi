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

        _ = today;
    }
}
