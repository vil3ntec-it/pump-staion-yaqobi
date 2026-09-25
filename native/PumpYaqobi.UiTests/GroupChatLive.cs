using PumpYaqobi.App.Services;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ گروهِ کارکنان با سرورِ خانگیِ واقعی — دستی، نه در CI ═════════════════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- groupchat &lt;نشانی&gt; &lt;کد&gt; &lt;رمزِ برنامه&gt;
///
/// همان ‎StationChat‎ی برنامه با سرورِ واقعی می‌فرستد و می‌خواند؛ پیامی که
/// اپِ کارمندان (با رمزِ خواندن) فرستاده باید این‌جا دیده شود.
/// </summary>
internal static class GroupChatLive
{
    public static int Run(string[] args)
    {
        if (args.Length < 4) { Console.WriteLine("groupchat <url> <code> <token>"); return 2; }
        var s = new AppSettings { ServerUrl = args[1], StationCode = args[2], ServerToken = args[3] };
        var chat = new StationChat(() => s);
        var bad = 0;
        void Check(string what, bool ok, string d = "")
        { Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (d.Length > 0 ? " — " + d : "")); if (!ok) bad++; }

        var cid = "pc" + Guid.NewGuid().ToString("N")[..10];
        var sent = chat.PostAsync(cid, "میرزا", "mirza", "سلام از کامپیوتر").GetAwaiter().GetResult();
        Check("کامپیوتر فرستاد", sent.Ok && sent.Message is { Seq: > 0 }, sent.Why);
        var again = chat.PostAsync(cid, "میرزا", "mirza", "سلام از کامپیوتر").GetAwaiter().GetResult();
        Check("تلاشِ دوباره با همان cid پیامِ دوم نمی‌سازد", again.Ok && again.Message?.Seq == sent.Message?.Seq);

        var (ok, list, why, last) = chat.FetchAsync(0).GetAwaiter().GetResult();
        Check("کامپیوتر خواند", ok, why);
        Check("پیامِ گوشی (نقشِ کارمند) این‌جا هست", list.Any(m => m.Role == "staff" && m.Text.Contains("گوشی")),
            string.Join(" | ", list.Select(m => m.From + ":" + m.Text)));
        Check("last با بزرگ‌ترین شماره می‌خواند", last == list.Max(m => m.Seq), last.ToString());

        var bogus = new StationChat(() => new AppSettings { ServerUrl = args[1], StationCode = args[2], ServerToken = "wrong-key-xyz" });
        var (bOk, _, bWhy, _) = bogus.FetchAsync(0).GetAwaiter().GetResult();
        Check("رمزِ غلط ⇒ نه، با جملهٔ آدمیزاد", !bOk && bWhy.Contains("کهنه"), bWhy);

        Console.WriteLine(bad == 0 ? "✅ همهٔ بندها سبزند" : $"❌ {bad} ایراد");
        return bad == 0 ? 0 : 1;
    }
}
