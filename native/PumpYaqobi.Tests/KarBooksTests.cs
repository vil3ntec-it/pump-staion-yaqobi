using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// «نمی‌شود واحدِ تیل یا پول را عوض کرد» (صاحب ریپو، ۱۴۰۵/۰۷/۱۶): عکسِ گوشی
/// فقط دفترِ فعالِ هر حساب را داشت. حالا ‎books‎ هر دو دفتر را می‌برد، و نسخهٔ
/// سرورِ حساب ردیف‌های هر دو را هم بر‌می‌دارد تا بار دو برابر نشود.
/// </summary>
public class KarBooksTests
{
    [Fact]
    public void Aks_HarDoDaftar_RaMibarad_VaAbr_RadifHayeHarDo_RaBarmidarad()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Services", "StationSnapshot.cs"));
        Assert.Contains("[\"books\"] = snap.Books.Select(", src);
        Assert.Contains("[\"on\"] = (b.Unit == \"افغانی\") == a.Mode.IsMoney()", src);
        Assert.Contains("[\"r\"] = withRows ? b.Rows : new List<string[]>()", src);
        //  ⛔ نسخهٔ سرورِ حساب: ردیف‌های ‎books‎ هم خالی می‌شوند
        Assert.Contains("new Dictionary<string, object?>(b) { [\"r\"] = new List<string[]>() }", src);
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }
}
