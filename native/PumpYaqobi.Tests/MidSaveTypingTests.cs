using System.Reflection;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ نوشتنی که وسطِ ذخیره رسید گم نمی‌شود (۱۴۰۵/۰۷/۲۲) ════════════════════════
///
/// گزارشِ صاحب ریپو: «توی ورق نام و مقدار و مبلغ را نوشتم؛ وقتی بیرون شدم عددها
/// نصفه یا پاک شده بودند.» ذخیره روی نخِ دیگر بود و ‎WriteAsync‎ پس از آن ردیف را
/// «پاک» می‌کرد، هر چه در این فاصله تایپ شده بود. این‌جا همان مسابقه بی دیتابیس.
/// </summary>
public class MidSaveTypingTests
{
    private sealed class Probe : RowViewModel
    {
        public string Typed = "";
        private string _applied = "";
        public readonly List<string> OnDisk = new();
        public TaskCompletionSource? Hold;
        public void Type(string s) { Typed = s; Touch(); }
        protected override void Apply() => _applied = Typed;
        protected override async Task SaveAsync()
        {
            var v = _applied;
            if (Hold is { } h) { Hold = null; await h.Task; }
            lock (OnDisk) OnDisk.Add(v);
        }
    }

    [Fact]
    public async Task KelidVasateZakhire_RuyeDiskMiresad()
    {
        var hold = new TaskCompletionSource();
        var r = new Probe { Hold = hold };
        r.Type("125");
        var flush = r.FlushAsync();             // ذخیرهٔ «125» شروع شد و وسطِ کار است
        await Task.Delay(30);
        r.Type("12500");                        // کاربر همان لحظه ادامه داد
        hold.SetResult();
        await flush;
        await r.FlushAsync();                   // بیرون رفتن از ورق
        Assert.Equal("12500", r.OnDisk[^1]);
        Assert.False(r.IsDirty);
    }

    [Fact]
    public async Task BiTaghireTaze_YekBarZakhireMishavad()
    {
        var r = new Probe();
        r.Type("7");
        await r.FlushAsync();
        Assert.Equal(new[] { "7" }, r.OnDisk);
        Assert.False(r.IsDirty);
    }

    [Fact]
    public void NoskheyeJoda_HameSotunhaRaDarad()
    {
        //  ⛔ ستونِ تازه‌ای که به ‎WaraqTransaction‎ اضافه شود و در ‎Detached‎ ننشیند،
        //  با هر ذخیرهٔ ورق روی دیسک به پیش‌فرض برمی‌گشت.
        var t = new WaraqTransaction();
        foreach (var p in Settable())
            p.SetValue(t, Sample(p.PropertyType));
        var c = WaraqPageViewModel.Detached(t);
        foreach (var p in Settable())
            Assert.True(Equals(p.GetValue(t), p.GetValue(c)), "ستونِ جامانده در Detached: " + p.Name);
        Assert.Null(c.Shift);
    }

    private static IEnumerable<PropertyInfo> Settable() =>
        typeof(WaraqTransaction).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.Name != nameof(WaraqTransaction.Shift));

    private static object Sample(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        if (u == typeof(string)) return "x";
        if (u == typeof(long)) return 42L;
        if (u == typeof(int)) return 7;
        if (u == typeof(decimal)) return 12.5m;
        if (u == typeof(bool)) return true;
        if (u == typeof(DateTime)) return new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        if (u.IsEnum) return Enum.GetValues(u).GetValue(Enum.GetValues(u).Length - 1)!;
        throw new InvalidOperationException("نوعِ ناشناخته: " + t);
    }
}
