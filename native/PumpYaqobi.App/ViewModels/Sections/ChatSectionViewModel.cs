using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک پیامِ داخلِ گفت‌وگو.</summary>
public sealed partial class ChatMessageViewModel : ObservableObject
{
    public ChatMessageViewModel(string sender, string text, string time, bool mine, bool pending = false)
    {
        Sender = sender; Text = text; Time = time; Mine = mine;
        _isPending = pending;
    }

    public string Sender { get; }
    public string Text { get; }
    public string Time { get; }

    /// <summary>پیامِ خودم — حبابش سمتِ دیگر و رنگش تاکیدی است.</summary>
    public bool Mine { get; }

    /// <summary>هنوز نرفته (آفلاین). با رفتن، خودش خاموش می‌شود.</summary>
    [ObservableProperty] private bool _isPending;
}

/// <summary>یک گفت‌وگو — «کارمندان» یا یک قرض‌دار.</summary>
public sealed partial class ChatThreadViewModel : ObservableObject
{
    public ChatThreadViewModel(string id, string title)
    {
        Id = id; Title = title;
    }

    public string Id { get; }
    public string Title { get; }
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    [ObservableProperty] private int _unread;
    [ObservableProperty] private string _preview = "";

    public bool HasUnread => Unread > 0;
    partial void OnUnreadChanged(int v) => OnPropertyChanged(nameof(HasUnread));
}

/// <summary>
/// ══ پیام‌رسان — یک بخشِ جدا ═══════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «بخشِ چت هم نیست … یک بخشِ جداگانه تو برنامه باشه
/// نه داینامیک.» پس برخلافِ نسخهٔ وب (که داخلِ رباتِ دستیار است) این‌جا یک
/// بخشِ کاملِ خودش است، کنارِ بقیه در نوارِ بالا.
///
/// ⚠️ روی همان سرورِ خانگی سوار است و **هیچ سرویسِ بیرونی‌ای** ندارد. قاعده‌ها
/// مو‌به‌مو همان نسخهٔ وب‌اند تا پیامِ برنامه و پیامِ کیو‌آرِ همان حساب در یک
/// گفت‌وگو بنشینند:
///
///   • هر گفت‌وگو یک «موضوع» است: ‎staff‎ برای کارمندان، ‎p-…‎ برای هر حساب.
///   • نامِ فرستنده در ‎title‎ و شناسهٔ گفت‌وگو در ‎tags‎ — سرور فیلدِ اضافه
///     پاس نمی‌دهد.
///   • نوشتنِ آفلاین گم نمی‌شود: در صف می‌ماند و با وصل شدن خودش می‌رود.
///
/// تاریخچه کنارِ دیتابیس در یک فایلِ ساده می‌ماند، نه در جدول: چت دادهٔ حسابی
/// نیست و نباید در پشتیبان‌گیریِ حساب‌ها قاطی شود.
/// </summary>
public sealed partial class ChatSectionViewModel : SectionViewModel
{
    private const string StaffTopic = "staff";
    private const int Keep = 300;               // بیشترین پیامِ هر گفت‌وگو

    private readonly AppHost _host;
    private readonly HomeServer _server;
    private readonly CancellationTokenSource _life = new();
    private readonly List<(string Topic, string Text)> _queue = new();
    private readonly string _file;

    public ChatSectionViewModel(AppHost host) : base("chat", "chat", "پیام‌رسان")
    {
        _host = host;
        _server = new HomeServer(() => AppSettings.Load().ServerUrl);
        _file = Path.Combine(AppSettings.Dir, "chat.json");

        Threads.Add(new ChatThreadViewModel(StaffTopic, "کارمندان"));
        Current = Threads[0];

        Load();
        _ = ListenAsync();
    }

    public ObservableCollection<ChatThreadViewModel> Threads { get; } = new();

    [ObservableProperty] private ChatThreadViewModel? _current;
    [ObservableProperty] private string _draft = "";

    /// <summary>حالِ اتصال — همان چیزی که کاربر باید ببیند، نه پنهان بماند.</summary>
    [ObservableProperty] private string _state = "";

    partial void OnCurrentChanged(ChatThreadViewModel? v)
    {
        if (v is not null) v.Unread = 0;
    }

    // ── فرستادن ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = (Draft ?? "").Trim();
        if (text.Length == 0 || Current is not { } th) return;
        Draft = "";

        var me = _host.Session.UserName ?? "میرزا";
        var bubble = new ChatMessageViewModel(me, text, DateTime.Now.ToString("HH:mm"), mine: true, pending: true);
        th.Messages.Add(bubble);
        Trim(th);
        th.Preview = text;
        Save();

        // ⚠️ برچسبِ گفت‌وگو همیشه می‌رود، حتی روی موضوعِ خودش: طرفِ مقابل از
        // همین برچسب می‌فهمد پیام به کدام گفت‌وگو تعلق دارد.
        var ok = await _server.SendAsync(th.Id, me, text, new[] { th.Id }, _life.Token);
        if (ok) { bubble.IsPending = false; State = ""; }
        else
        {
            _queue.Add((th.Id, text));
            State = "آفلاین — " + Shamsi.Money(_queue.Count) + " پیام در صف";
        }
    }

    /// <summary>صف را خالی کن — با هر پیامِ رسیده و هر بار وصل شدن صدا می‌شود.</summary>
    private async Task FlushAsync()
    {
        if (_queue.Count == 0) return;
        var me = _host.Session.UserName ?? "میرزا";
        for (var i = _queue.Count - 1; i >= 0; i--)
        {
            var (topic, text) = _queue[i];
            if (!await _server.SendAsync(topic, me, text, new[] { topic }, _life.Token)) break;
            _queue.RemoveAt(i);
        }
        State = _queue.Count == 0 ? "" : "آفلاین — " + Shamsi.Money(_queue.Count) + " پیام در صف";
        if (_queue.Count == 0) MarkAllSent();
    }

    private void MarkAllSent()
    {
        foreach (var t in Threads)
            foreach (var m in t.Messages)
                m.IsPending = false;
    }

    // ── گرفتن ───────────────────────────────────────────────────────────────

    private async Task ListenAsync()
    {
        if (!_server.Configured)
        {
            State = "نشانیِ سرور در «تنظیمات» خالی است";
            return;
        }

        await _server.ListenAsync(
            Threads.Select(t => t.Id).ToList(),
            m => Dispatcher.UIThread.Post(() => Arrived(m)),
            _life.Token);
    }

    private void Arrived(HomeMessage m)
    {
        var id = HomeServer.ThreadOf(m);
        var th = Threads.FirstOrDefault(t => t.Id == id);
        if (th is null)
        {
            th = new ChatThreadViewModel(id, id == StaffTopic ? "کارمندان" : id);
            Threads.Add(th);
        }

        var me = _host.Session.UserName ?? "میرزا";
        var mine = string.Equals(m.Title, me, StringComparison.Ordinal);

        // ⚠️ پیامِ خودم دو بار می‌رسد (یک‌بار همان لحظه، یک‌بار از سرور).
        // با مقایسهٔ متن و فرستنده یکی‌شان کنار گذاشته می‌شود — همان کاری که
        // نسخهٔ وب هم می‌کند.
        if (mine && th.Messages.Count > 0 &&
            th.Messages[^1].Text == m.Message && th.Messages[^1].Mine) return;

        th.Messages.Add(new ChatMessageViewModel(m.Title, m.Message, DateTime.Now.ToString("HH:mm"), mine));
        Trim(th);
        th.Preview = m.Message;
        if (!ReferenceEquals(th, Current) && !mine) th.Unread++;

        Save();
        _ = FlushAsync();
    }

    private static void Trim(ChatThreadViewModel th)
    {
        while (th.Messages.Count > Keep) th.Messages.RemoveAt(0);
    }

    // ── تاریخچهٔ محلی ───────────────────────────────────────────────────────

    private sealed record Saved(string Id, string Title, List<string[]> Rows);

    private void Save()
    {
        try
        {
            var data = Threads.Select(t => new Saved(t.Id, t.Title,
                t.Messages.Select(m => new[] { m.Sender, m.Text, m.Time, m.Mine ? "1" : "0" })
                          .ToList())).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(data));
        }
        catch { /* تاریخچه رفاه است، نه داده — نبودش برنامه را نمی‌خواباند */ }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var data = JsonSerializer.Deserialize<List<Saved>>(File.ReadAllText(_file));
            if (data is null) return;

            foreach (var s in data)
            {
                var th = Threads.FirstOrDefault(t => t.Id == s.Id);
                if (th is null) { th = new ChatThreadViewModel(s.Id, s.Title); Threads.Add(th); }
                foreach (var r in s.Rows)
                    if (r.Length >= 4)
                        th.Messages.Add(new ChatMessageViewModel(r[0], r[1], r[2], r[3] == "1"));
                if (th.Messages.Count > 0) th.Preview = th.Messages[^1].Text;
            }
        }
        catch { /* فایلِ خراب = تاریخچهٔ خالی، نه سقوط */ }
    }
}
