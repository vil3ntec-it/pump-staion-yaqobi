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
            host.SafeData.AddAsync(new SafeEntry
            {
                DateShamsi = $"{month}/{i:00}",
                Kind = i % 3 == 0 ? SafeEntryKind.Bardagi : SafeEntryKind.Mandagi,
                Title = i % 3 == 0 ? "بردگیِ روزانه" : "ماندگیِ شیفتِ روز",
                Amount = 1000 * i + 250,
                Currency = i % 7 == 0 ? Currency.Usd : Currency.Afn,
                Note = i % 5 == 0 ? "با اجازهٔ مدیر" : "",
            }).GetAwaiter().GetResult();
        }
    }
}
