using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ پیام‌رسانِ تمام‌صفحه — با پنجرهٔ واقعی (۱۴۰۵/۰۷/۱۳) ═══════════════════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- chatroom [پوشهٔ عکس]
///
/// دفترِ پیام‌ها (‎chat.db‎) پیش از بالا آمدنِ پنجره پر می‌شود — گروه، پشتیبانی
/// و یک مشتریِ کیو‌آر با حسابِ واقعی در دفتر — بعد سنجیده می‌شود:
/// <list type="bullet">
/// <item>تمام‌صفحه: سربرگ و نوارِ بخش‌ها پنهان، صفحه هم‌قدِ پنجره.</item>
/// <item>سه ستون کنارِ هم، هیچ‌کدام بیرون از قاب.</item>
/// <item>ستونِ مشخصات برای مشتری دفترهای همان حساب را دارد.</item>
/// <item>پیامِ پیرتر از ۱۵ روز در سطل است، نه در گفت‌وگو.</item>
/// </list>
/// </summary>
internal static class ChatProbe
{
    private static int _bad;
    private const long Day = 86_400_000L;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-chatroom");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-chatroom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();
        var acctId = SeedDebtor(AppHost.Current);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        //  ⚠️ پیش از ساختنِ پنجره: دفترِ پیام‌ها در سازندهٔ ویومدل باز می‌شود
        SeedChat(dir, acctId);
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Settle(win);

        var chat = vm.Chat;
        Wait(win, vm.GoAsync(chat));
        Check("پوستهٔ پنجره پنهان است (تمام‌صفحه)", !vm.IsChromeVisible);

        var page = win.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "ChatPage");
        Check("صفحهٔ چت در درخت و دیدنی است", page is { IsEffectivelyVisible: true });
        if (page is not null)
            Check("صفحه هم‌قدِ پنجره است", Math.Abs(page.Bounds.Height - win.ClientSize.Height) < 2,
                $"{page.Bounds.Height:0} در برابرِ {win.ClientSize.Height:0}");

        var cols = win.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Classes.Contains("chatcol") && b.IsEffectivelyVisible).ToList();
        Check("سه ستون", cols.Count == 3, cols.Count.ToString());
        foreach (var c in cols)
        {
            var tl = c.TranslatePoint(new Point(0, 0), win) ?? default;
            var br = c.TranslatePoint(new Point(c.Bounds.Width, c.Bounds.Height), win) ?? default;
            var l = Math.Min(tl.X, br.X); var r = Math.Max(tl.X, br.X);
            Check($"ستونی به پهنای {c.Bounds.Width:0} درونِ قاب", l >= -1 && r <= win.ClientSize.Width + 1 && br.Y <= win.ClientSize.Height + 1);
        }

        // گروه
        chat.Current = chat.Threads.First(t => t.IsGroup);
        Settle(win);
        Check("گروه: سه پیام در گفت‌وگو", chat.Current.Messages.Count == 3, chat.Current.Messages.Count.ToString());
        Check("گروه: پیامِ پیر در سطل است", chat.TrashCount == 1, chat.TrashCount.ToString());
        Check("گروه: نقشِ فرستنده دیده می‌شود", chat.Current.Messages.Any(m => m.HasRole));
        Check("گروه: اعضا در ستونِ مشخصات", chat.HasMembers);
        Shot(win, Path.Combine(shots, "chat-group.png"));

        chat.ToggleTrashCommand.Execute(null);
        Settle(win);
        Check("سطل: پیامِ پیر با «روز مانده»", chat.Current!.Messages.Count == 1 && chat.Current.Messages[0].Trashed);
        Shot(win, Path.Combine(shots, "chat-trash.png"));
        chat.ToggleTrashCommand.Execute(null);
        Settle(win);

        // پشتیبانی
        chat.Current = chat.Threads.First(t => t.IsSupportDesk);
        Settle(win);
        Check("پشتیبانی: دو پیام", chat.Current.Messages.Count == 2);
        Shot(win, Path.Combine(shots, "chat-support.png"));

        // مشتری
        var cust = chat.Threads.FirstOrDefault(t => t.IsCustomer);
        Check("گفت‌وگوی مشتری از دفترِ همین کامپیوتر آمد", cust is not null);
        if (cust is not null)
        {
            chat.Current = cust;
            for (var i = 0; i < 100 && !(chat.HasInfoBooks && chat.Current.Messages.Any(m => m.Image is not null)); i++)
            { Pump(win); Thread.Sleep(10); }
            Settle(win);
            Check("مشتری: دفترهای حساب در ستونِ مشخصات", chat.HasInfoBooks, chat.InfoBooks.Count.ToString());
            Check("مشتری: حساب‌های دیگرِ همان شخص", chat.InfoAccounts.Count == 2, chat.InfoAccounts.Count.ToString());
            Check("مشتری: شمارهٔ تلفن", chat.InfoPhone == "0799123456");
            Check("مشتری: یک عکسِ فرستاده‌شده", chat.ImageCount == 1);
            Check("مشتری: عکس از فایلِ همین کامپیوتر نشست", chat.Current.Messages.Any(m => m.Image is not null));
            Shot(win, Path.Combine(shots, "chat-customer.png"));

            ThemeManager.Apply(PumpTheme.Gold);
            Settle(win);
            Shot(win, Path.Combine(shots, "chat-customer-dark.png"));
            ThemeManager.Apply(PumpTheme.Blue);
            Settle(win);
        }

        Wait(win, chat.BackCommand.ExecuteAsync(null));
        Check("‹ برگشت پوسته را برمی‌گرداند", vm.IsChromeVisible);

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ همهٔ بندها سبزند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static long SeedDebtor(AppHost h)
    {
        using var db = h.Db.Create();
        var today = Shamsi.Today();
        var d = new Debtor
        {
            Name = "احمد رحیمی", Phone = "0799123456",
            MainAccount = new DebtAccount { Mode = LedgerMode.Fuel, QrKey = "k1" },
        };
        d.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = today, DateKey = Shamsi.Key(today), Name = "بارِ اول",
            Fuel = FuelType.Diesel, Liters = 400, PricePerLiter = 70, Bardagi = 28000 });
        d.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = today, DateKey = Shamsi.Key(today), Name = "رسید",
            Fuel = FuelType.Diesel, Rasid = 10000, SortIndex = 1 });
        d.SubAccounts.Add(new DebtAccount { Name = "دکان", LegacySubId = "s1", Mode = LedgerMode.Money });
        db.Debtors.Add(d);
        db.SaveChanges();
        return d.MainAccount.Id;
    }

    private static void SeedChat(string dir, long acctId)
    {
        var s = new ChatStore(dir);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ChatRow G(string key, string from, string text, long at, bool mine = false) =>
            new(key, "group", 0, from, text, "text", null, mine, at);
        s.Upsert(G("grp:1", "کریم", "پیامِ هفدهِ روزِ پیش", now - 17 * Day));
        s.Upsert(G("grp:2", "کریم", "سلام، تیلِ دیزل پایه دو کم شده", now - 3 * 3600_000));
        s.Upsert(G("grp:3", "میرزا", "بسیار خوب، فردا تانکر می‌آید", now - 2 * 3600_000, mine: true));
        s.Upsert(G("grp:4", "نوید", "پارچهٔ شب ثبت شد ✔", now - 600_000));
        s.Sweep(now);

        s.Upsert(new ChatRow("sup:1", "support", 1, "میرزا", "چاپِ ورق کج می‌آید، چه کنم؟", "text", null, true, now - 5 * 3600_000));
        s.Upsert(new ChatRow("sup:2", "support", 2, "پشتیبانی", "سلام، تنظیمِ ورق را روی A4 بگذارید.", "text", null, false, now - 4 * 3600_000));

        var th = "acct:d" + acctId;
        s.Upsert(new ChatRow("cloud:c1", th, 1, "احمد", "سلام، الباقیِ حسابم چقدر است؟", "text", null, false, now - 90 * 60_000));
        s.Upsert(new ChatRow("cloud:c2", th, 2, "پمپ یعقوبی", "سلام، ۱۸٬۰۰۰ افغانی.", "text", null, true, now - 80 * 60_000));
        s.SaveMedia("img1", "image/png", Receipt());
        s.Upsert(new ChatRow("cloud:c3", th, 3, "احمد", "", "image", "img1", false, now - 70 * 60_000));
        s.Upsert(new ChatRow("cloud:c4", th, 4, "احمد", "رسیدِ دیروز را فرستادم", "text", null, false, now - 60 * 60_000));
    }

    /// <summary>عکسِ یک «رسید» برای سنجه — با خودِ آوالونیا کشیده می‌شود.</summary>
    private static byte[] Receipt()
    {
        var card = new Border
        {
            Width = 360, Height = 240, CornerRadius = new CornerRadius(14),
            Background = new Avalonia.Media.LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new(Avalonia.Media.Color.Parse("#fef3c7"), 0), new(Avalonia.Media.Color.Parse("#fdba74"), 1) },
            },
            Child = new TextBlock { Text = "🧾 رسید ۱۰٬۰۰۰ افغانی", FontSize = 22, FontWeight = Avalonia.Media.FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
        };
        card.Measure(new Size(360, 240));
        card.Arrange(new Rect(0, 0, 360, 240));
        using var rt = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(360, 240));
        rt.Render(card);
        using var ms = new MemoryStream();
        rt.Save(ms);
        return ms.ToArray();
    }

    private static void Shot(Window w, string path)
    {
        Settle(w);
        using var frame = w.CaptureRenderedFrame();
        frame?.Save(path);
        Console.WriteLine("  📷 " + path);
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 600 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
