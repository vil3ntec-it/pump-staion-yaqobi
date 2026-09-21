using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک پیامِ داخلِ گفت‌وگو — متنی یا رسانه‌ای (عکس، ویدیو، صدا).</summary>
public sealed partial class ChatMessageViewModel : ObservableObject
{
    public ChatMessageViewModel(string sender, string text, string time, bool mine, bool pending = false,
                                string id = "", string kind = "text", string? mediaId = null, bool deleted = false)
    {
        Sender = sender; Text = text; Time = time; Mine = mine; Id = id; Kind = kind; MediaId = mediaId;
        _isPending = pending;
        _deleted = deleted;
    }

    /// <summary>شناسهٔ ابر — خالی برای پیام‌های شبکهٔ خانگی.</summary>
    public string Id { get; }
    public string Sender { get; }
    public string Text { get; }
    public string Time { get; }
    public string Kind { get; }
    public string? MediaId { get; }

    /// <summary>پیامِ خودم — حبابش سمتِ دیگر و رنگش تاکیدی است.</summary>
    public bool Mine { get; }

    /// <summary>هنوز نرفته (آفلاین). با رفتن، خودش خاموش می‌شود.</summary>
    [ObservableProperty] private bool _isPending;

    /// <summary>پاک شده — جایش می‌ماند، متنش نه.</summary>
    [ObservableProperty] private bool _deleted;

    /// <summary>عکس، وقتی از ابر رسید.</summary>
    [ObservableProperty] private Bitmap? _image;

    public bool IsText => Kind == "text" && !Deleted;
    public bool IsImage => Kind == "image" && !Deleted;
    public bool IsMedia => (Kind == "video" || Kind == "audio") && !Deleted;
    public bool CanDelete => Id.Length > 0 && !Deleted;
    public string MediaLabel => Kind == "video" ? "🎥 ویدیو — باز کردن" : Kind == "audio" ? "🎤 پیامِ صوتی — پخش" : "";

    partial void OnDeletedChanged(bool v)
    {
        foreach (var n in new[] { nameof(IsText), nameof(IsImage), nameof(IsMedia), nameof(CanDelete) })
            OnPropertyChanged(n);
    }
}

/// <summary>یک گفت‌وگو — «کارمندان» (شبکهٔ خانگی) یا یک مشتریِ کیو‌آر (ابر).</summary>
public sealed partial class ChatThreadViewModel : ObservableObject
{
    public ChatThreadViewModel(string id, string title, string? acct = null)
    {
        Id = id; _title = title; Acct = acct;
    }

    public string Id { get; }

    /// <summary>شناسهٔ حسابِ کیو‌آر (‎d12‎) — ‎null‎ یعنی گفت‌وگوی شبکهٔ خانگی.</summary>
    public string? Acct { get; }
    public bool IsSupport => Acct is not null;

    [ObservableProperty] private string _title;
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    [ObservableProperty] private int _unread;
    [ObservableProperty] private string _preview = "";
    [ObservableProperty] private bool _blocked;

    /// <summary>
    /// نامی که خودِ مشتری در صفحهٔ کیو‌آر نوشته.
    ///
    /// ⚠️ <b>عنوان نیست، زیرنویس است.</b> عنوان نامِ همان حسابی است که
    /// کیو‌آرش را اسکن کرده — خواستهٔ صریحِ صاحب ریپو. نامی که مشتری خودش
    /// تایپ می‌کند هر چیزی می‌تواند باشد و دو نفر از یک حساب هم می‌توانند
    /// بنویسند؛ حساب همان یکی است.
    /// </summary>
    [ObservableProperty] private string _who = "";

    public bool HasUnread => Unread > 0;

    /// <summary>حرفِ اولِ عنوان — دایرهٔ کنارِ هر گفت‌وگو.</summary>
    public string Avatar
    {
        get
        {
            var t = (Title ?? "").TrimStart();
            return t.Length == 0 ? "؟" : t[..1];
        }
    }

    public string Subtitle => IsSupport
        ? (Blocked ? "🚫 بلاک شده · " : "") + (Who.Length > 0 ? Who + " · " : "") + "مشتریِ کیو‌آر"
        : "شبکهٔ پمپ";

    public string BlockText => Blocked ? "رفعِ بلاک" : "بلاک";
    public bool HasPreview => !string.IsNullOrWhiteSpace(Preview);

    partial void OnUnreadChanged(int v) => OnPropertyChanged(nameof(HasUnread));
    partial void OnPreviewChanged(string v) => OnPropertyChanged(nameof(HasPreview));
    partial void OnTitleChanged(string v) => OnPropertyChanged(nameof(Avatar));
    partial void OnWhoChanged(string v) => OnPropertyChanged(nameof(Subtitle));
    partial void OnBlockedChanged(bool v) { OnPropertyChanged(nameof(Subtitle)); OnPropertyChanged(nameof(BlockText)); }
}

/// <summary>
/// ══ پیام‌رسان — یک بخشِ جدا ═══════════════════════════════════════════════
///
/// دو نوع گفت‌وگو در یک صفحه:
///
///   • <b>کارمندان</b> (و حساب‌های سایتِ قدیم): روی سرورِ خانگی، مثلِ همیشه —
///     ‎staff‎ و ‎p-…‎، نامِ فرستنده در ‎title‎ و شناسهٔ گفت‌وگو در ‎tags‎.
///   • <b>پشتیبانیِ مشتری</b> (از ۱۴۰۵/۰۶/۲۶): مشتری که کیو‌آرِ حسابش را
///     اسکن کرده از همان صفحه می‌نویسد؛ پیام‌ها روی ابر می‌نشینند
///     (‎CloudLink.Chat*‎) و این‌جا هر چند ثانیه صندوق پرسیده می‌شود. «اسمش
///     برای من دیده بشه، پیام‌ها پاک بشن، عکس و ویدیو و صدا، بلاک» — همه
///     همین‌جا. حالتش در <see cref="SupportState"/> است تا بی صفحه آزموده شود.
///
/// ⚠️ پشتیبانی فقط وقتی هست که برنامه با کدِ شش‌رقمی به ابر فعال شده باشد؛
/// وگرنه همان پیام‌رسانِ شبکهٔ خانگی است و بس.
///
/// تاریخچهٔ خانگی کنارِ دیتابیس در یک فایلِ ساده می‌ماند، نه در جدول: چت دادهٔ
/// حسابی نیست و نباید در پشتیبان‌گیریِ حساب‌ها قاطی شود. تاریخچهٔ پشتیبانی
/// روی خودِ ابر است و با هر بار باز شدن دوباره می‌آید.
/// </summary>
public sealed partial class ChatSectionViewModel : SectionViewModel
{
    private const string StaffTopic = "staff";
    private const int Keep = 300;               // بیشترین پیامِ هر گفت‌وگو

    /// <summary>هر چند وقت صندوقِ ابر پرسیده شود.</summary>
    public static readonly TimeSpan CloudEvery = TimeSpan.FromSeconds(8);

    /// <summary>وقتی چت باز نیست: فقط برای شمارهٔ نخوانده، هر دقیقه.</summary>
    public static readonly TimeSpan CloudEveryIdle = TimeSpan.FromSeconds(60);

    private readonly AppHost _host;
    private readonly HomeServer _server;
    private readonly CancellationTokenSource _life = new();
    private readonly List<(string Topic, string Text)> _queue = new();
    private readonly string _file;
    private readonly SupportState _support = new();
    private CloudLink? _cloud;
    private DateTime _lastThreads = DateTime.MinValue;
    private WaveRecorder? _rec;

    public ChatSectionViewModel(AppHost host) : base("chat", "chat", "پیام‌رسان")
    {
        _host = host;
        _server = new HomeServer(() => AppSettings.Load().ServerUrl);
        _file = Path.Combine(AppSettings.Dir, "chat.json");

        Threads.Add(new ChatThreadViewModel(StaffTopic, "کارمندان"));
        Current = Threads[0];

        Load();
        _ = ListenAsync();
        _ = CloudLoopAsync();
    }

    public ObservableCollection<ChatThreadViewModel> Threads { get; } = new();

    [ObservableProperty] private ChatThreadViewModel? _current;
    [ObservableProperty] private string _draft = "";

    /// <summary>حالِ اتصال — همان چیزی که کاربر باید ببیند، نه پنهان بماند.</summary>
    [ObservableProperty] private string _state = "";

    /// <summary>پیام‌های نخواندهٔ مشتری‌ها — روی دکمهٔ «پشتیبانی» در سربرگ.</summary>
    [ObservableProperty] private int _supportUnread;

    /// <summary>در حالِ ضبطِ صدا.</summary>
    [ObservableProperty] private bool _recording;

    public bool CanRecord => WaveRecorder.Available;
    public string RecordText => Recording ? "⏹ پایان و فرستادن" : "🎤 ضبطِ صدا";
    partial void OnRecordingChanged(bool v) => OnPropertyChanged(nameof(RecordText));

    public bool HasSupportUnread => SupportUnread > 0;
    partial void OnSupportUnreadChanged(int v) => OnPropertyChanged(nameof(HasSupportUnread));

    /// <summary>ابر — فقط وقتی فعال شده.</summary>
    private CloudLink? Cloud
    {
        get
        {
            var file = AppSettings.Load();
            if (string.IsNullOrWhiteSpace(file.CloudDeviceToken)) return null;
            return _cloud ??= new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });
        }
    }

    /// <summary>نامی که مشتری از صاحبِ پمپ می‌بیند.</summary>
    private string OwnerName
    {
        get
        {
            try { var n = HomeLink.StationName(_host); return n.Length > 0 ? n : "پمپ"; }
            catch { return "پمپ"; }
        }
    }

    /// <summary>گفت‌وگویی باز است؟ — برای حالتِ «هیچ گفت‌وگویی انتخاب نشده».</summary>
    public bool HasCurrent => Current is not null;

    partial void OnCurrentChanged(ChatThreadViewModel? v)
    {
        OnPropertyChanged(nameof(HasCurrent));
        if (v is null) return;
        v.Unread = 0;
        if (v.IsSupport) _ = MarkSeenAsync(v);
    }

    // ── فرستادن ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = (Draft ?? "").Trim();
        if (text.Length == 0 || Current is not { } th) return;
        Draft = "";

        if (th.IsSupport) { await SendSupportAsync(th, text); return; }

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

    private async Task SendSupportAsync(ChatThreadViewModel th, string text, string kind = "", string? mediaId = null)
    {
        var cloud = Cloud;
        if (cloud is null || th.Acct is null) { State = "برنامه به سرورِ حساب وصل نیست"; return; }
        State = "در حالِ فرستادن…";
        var (ok, msg, why) = await cloud.ChatSendAsync(th.Acct, OwnerName, text, kind, mediaId, _life.Token);
        if (!ok || msg is null) { State = "نرفت: " + why; _host.Toast("پیام فرستاده نشد: " + why, ToastKind.Error); return; }
        State = "";
        _support.Merge(new[] { msg });
        Refresh(th);
    }

    /// <summary>📎 عکس یا ویدیو از روی دیسک — بالا می‌رود و به مشتری می‌رسد.</summary>
    [RelayCommand]
    private Task AttachAsync() => CrashGuard.RunAsync("فرستادنِ فایل", async () =>
    {
        if (Current is not { IsSupport: true } th) return;
        var path = await Dialogs.PickFileAsync("عکس یا ویدیو", "عکس و ویدیو",
            new[] { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.gif", "*.mp4", "*.webm", "*.mov", "*.m4a", "*.mp3", "*.wav", "*.ogg" });
        if (string.IsNullOrEmpty(path)) return;
        var mime = MimeOf(path);
        if (mime.Length == 0) { _host.Toast("این نوعِ فایل پشتیبانی نمی‌شود", ToastKind.Error); return; }
        var bytes = await File.ReadAllBytesAsync(path, _life.Token);
        await UploadAndSendAsync(th, bytes, mime);
    });

    private async Task UploadAndSendAsync(ChatThreadViewModel th, byte[] bytes, string mime)
    {
        var cloud = Cloud;
        if (cloud is null || th.Acct is null) { State = "برنامه به سرورِ حساب وصل نیست"; return; }
        var kind = mime.StartsWith("image/") ? "image" : mime.StartsWith("video/") ? "video" : "audio";
        State = "در حالِ بالا بردن…";
        var (ok, mediaId, why) = await cloud.ChatUploadAsync(th.Acct, bytes, mime, _life.Token);
        if (!ok) { State = "نرفت: " + why; _host.Toast("فایل بالا نرفت: " + why, ToastKind.Error); return; }
        await SendSupportAsync(th, "", kind, mediaId);
    }

    /// <summary>🎤 یک ضربه شروع، ضربهٔ بعد پایان و فرستادن.</summary>
    [RelayCommand]
    private Task RecordAsync() => CrashGuard.RunAsync("ضبطِ صدا", async () =>
    {
        if (!CanRecord || Current is not { IsSupport: true } th) return;
        if (_rec is null)
        {
            _rec = new WaveRecorder();
            if (!_rec.Start()) { _rec = null; _host.Toast("میکروفون پیدا نشد", ToastKind.Error); return; }
            Recording = true;
            return;
        }
        var wav = _rec.Stop();
        _rec.Dispose();
        _rec = null;
        Recording = false;
        if (wav.Length < 2000) { _host.Toast("چیزی ضبط نشد", ToastKind.Warn); return; }
        await UploadAndSendAsync(th, wav, "audio/wav");
    });

    /// <summary>🗑 پاک کردنِ یک پیام — جایش می‌ماند، متنش نه (هر دو طرف می‌بینند).</summary>
    [RelayCommand]
    private Task DeleteMessageAsync(ChatMessageViewModel? m) => CrashGuard.RunAsync("پاک کردنِ پیام", async () =>
    {
        if (m is null || !m.CanDelete || Current is not { IsSupport: true } th) return;
        var cloud = Cloud;
        if (cloud is null) return;
        var r = await cloud.ChatDeleteAsync(m.Id, _life.Token);
        if (!r.Ok) { _host.Toast("پاک نشد: " + r.Why, ToastKind.Error); return; }
        _support.MarkDeleted(m.Id);
        m.Deleted = true;
        Refresh(th);
    });

    /// <summary>🚫 بلاک / رفعِ بلاکِ مشتری — دیگر نمی‌تواند بنویسد، می‌تواند بخواند.</summary>
    [RelayCommand]
    private Task ToggleBlockAsync() => CrashGuard.RunAsync("بلاک", async () =>
    {
        if (Current is not { IsSupport: true, Acct: { } acct } th) return;
        var cloud = Cloud;
        if (cloud is null) return;
        var r = await cloud.ChatBlockAsync(acct, !th.Blocked, _life.Token);
        if (!r.Ok) { _host.Toast("نشد: " + r.Why, ToastKind.Error); return; }
        th.Blocked = !th.Blocked;
        _support.Get(acct).Blocked = th.Blocked;
        _host.Toast(th.Blocked ? "مشتری بلاک شد" : "بلاک برداشته شد", ToastKind.Info);
    });

    /// <summary>▶ ویدیو یا صدا — با برنامهٔ خودِ ویندوز باز می‌شود.</summary>
    [RelayCommand]
    private Task OpenMediaAsync(ChatMessageViewModel? m) => CrashGuard.RunAsync("باز کردنِ رسانه", async () =>
    {
        if (m is null || m.MediaId is null || Cloud is not { } cloud) return;
        var got = await cloud.ChatMediaAsync(m.MediaId, _life.Token);
        if (got is null) { _host.Toast("رسانه نرسید", ToastKind.Error); return; }
        var ext = ExtOf(got.Value.Mime);
        var path = Path.Combine(Path.GetTempPath(), "pump-chat-" + m.MediaId + ext);
        await File.WriteAllBytesAsync(path, got.Value.Bytes, _life.Token);
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { _host.Toast("برنامه‌ای برای باز کردنِ این فایل نیست", ToastKind.Error); }
    });

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

    // ── گرفتن — شبکهٔ خانگی ─────────────────────────────────────────────────

    private async Task ListenAsync()
    {
        if (!_server.Configured)
        {
            State = Cloud is null ? "نشانیِ سرور در «تنظیمات» خالی است" : "";
            return;
        }

        await _server.ListenAsync(
            Threads.Where(t => !t.IsSupport).Select(t => t.Id).ToList(),
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

    // ── گرفتن — ابر (پشتیبانیِ مشتری) ──────────────────────────────────────

    private async Task CloudLoopAsync()
    {
        while (!_life.IsCancellationRequested)
        {
            try { await PollCloudAsync(_life.Token); }
            catch (OperationCanceledException) { return; }
            catch { /* ابر نرسید — دورِ بعد */ }
            // ⚠️ فقط وقتی خودِ چت باز است هر ۸ ثانیه؛ وگرنه هر دقیقه — بخشِ بسته
            // نباید هیچ کاری بکند (خواستهٔ صاحب ریپو). شمارهٔ نخواندهٔ سربرگ
            // با همان دورِ یک‌دقیقه‌ای زنده می‌ماند.
            try { await Task.Delay(IsActive ? CloudEvery : CloudEveryIdle, _life.Token); } catch { return; }
        }
    }

    /// <summary>یک دور: فهرستِ گفت‌وگوها (هر دقیقه) و صندوقِ پیام‌های تازه (هر بار).</summary>
    public async Task PollCloudAsync(CancellationToken ct = default)
    {
        var cloud = Cloud;
        if (cloud is null) return;

        if (DateTime.UtcNow - _lastThreads > TimeSpan.FromMinutes(1))
        {
            var (ok, threads, _) = await cloud.ChatThreadsAsync(ct);
            if (ok)
            {
                _lastThreads = DateTime.UtcNow;
                _support.ApplyThreads(threads);
            }
        }

        var (ok2, msgs, _) = await cloud.ChatInboxAsync(_support.LastSeq, ct);
        if (!ok2) return;
        var fresh = _support.Merge(msgs);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var t in _support.Threads) Refresh(Bind(t));
            foreach (var m in fresh)
            {
                var th = Bind(_support.Get(m.Acct));
                if (ReferenceEquals(th, Current) && IsActive) _ = MarkSeenAsync(th);
                else _host.Toast("💬 " + (m.Name.Length > 0 ? m.Name : "مشتری") + ": " + Preview(m), ToastKind.Info);
            }
            SupportUnread = _support.TotalUnread;
        });
    }

    /// <summary>گفت‌وگوی ابر ⇒ ویومدلِ همان (ساخته می‌شود اگر نباشد).</summary>
    private ChatThreadViewModel Bind(SupportState.Thread t)
    {
        var id = "acct:" + t.Acct;
        var th = Threads.FirstOrDefault(x => x.Id == id);
        if (th is null)
        {
            th = new ChatThreadViewModel(id, TitleOf(t), t.Acct) { Who = t.Name };
            Threads.Add(th);
        }
        WantName(t.Acct);
        return th;
    }

    /// <summary>
    /// عنوانِ گفت‌وگو — <b>نامِ همان حسابی که کیو‌آرش اسکن شده</b>.
    ///
    /// تا امروز «کریم · d12» بود: هم شناسهٔ خام را نشان می‌داد و هم نمی‌گفت
    /// پیام از کدام حساب است. حالا «محمد هارون» است (یا «محمد هارون › دکان»
    /// برای حسابِ فرعی) و نامِ خودِ مشتری به زیرنویس رفت.
    ///
    /// ⚠️ تا وقتی نامِ حساب از دفتر نیامده، همان نامِ مشتری عنوان است — نه
    /// یک عنوانِ خالی. پیدا کردنِ نام یک پرس‌وجوی جداست و نباید فهرست را
    /// نگه دارد.
    /// </summary>
    private string TitleOf(SupportState.Thread t)
    {
        if (_acctNames.TryGetValue(t.Acct, out var n) && n.Length > 0) return n;
        return t.Name.Length > 0 ? t.Name : "مشتری · " + t.Acct;
    }

    /// <summary>نامِ حسابِ هر کیو‌آر — یک بار پرسیده و نگه داشته می‌شود.</summary>
    private readonly Dictionary<string, string> _acctNames = new();
    private readonly HashSet<string> _naming = new();

    private void WantName(string? acct)
    {
        if (string.IsNullOrEmpty(acct)) return;
        if (_acctNames.ContainsKey(acct) || !_naming.Add(acct)) return;
        _ = ResolveNameAsync(acct);
    }

    /// <summary>
    /// ‎d12‎ ⇒ نامِ حسابِ قرض‌دار · ‎c3‎ ⇒ نامِ شرکت.
    ///
    /// ⚠️ همه‌اش پشتِ ‎try‎ است و هیچ‌وقت چت را نمی‌شکند: نامِ حساب رفاه
    /// است، خودِ پیام اصل.
    /// </summary>
    private async Task ResolveNameAsync(string acct)
    {
        try
        {
            var label = "";
            if (acct.Length > 1 && long.TryParse(acct[1..], out var id) && id > 0)
            {
                if (acct[0] == 'd')
                    label = await _host.Debtors.AccountLabelAsync(id, _life.Token);
                else if (acct[0] == 'c')
                    label = (await _host.Companies.LoadAsync(id, _life.Token))?.Name ?? "";
            }
            if (string.IsNullOrWhiteSpace(label)) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _acctNames[acct] = label.Trim();
                foreach (var th in Threads.Where(x => x.Acct == acct)) th.Title = label.Trim();
            });
        }
        catch { /* نامِ حساب رفاه است، نه اصل */ }
    }

    /// <summary>پیام‌های ویومدل را با حالتِ ابر یکی می‌کند.</summary>
    private void Refresh(ChatThreadViewModel th)
    {
        if (th.Acct is null || !_support.TryGet(th.Acct, out var t)) return;
        th.Title = TitleOf(t);
        th.Who = t.Name;
        th.Blocked = t.Blocked;
        th.Unread = ReferenceEquals(th, Current) ? 0 : t.Unread;

        // فقط تفاوت‌ها: پیامِ تازه اضافه، پاک‌شده علامت — نه ساختنِ دوبارهٔ همه
        var known = th.Messages.ToDictionary(m => m.Id, m => m);
        var cloudFirst = th.Messages.Count == 0 || th.Messages.All(m => m.Id.Length > 0);
        if (!cloudFirst) th.Messages.Clear();
        foreach (var m in t.Messages)
        {
            if (known.TryGetValue(m.Id, out var vm))
            {
                if (m.Deleted && !vm.Deleted) vm.Deleted = true;
                continue;
            }
            var bubble = new ChatMessageViewModel(
                m.FromCustomer ? (m.Name.Length > 0 ? m.Name : "مشتری") : (m.Name.Length > 0 ? m.Name : OwnerName),
                m.Text, TimeOf(m.At), mine: !m.FromCustomer, id: m.Id, kind: m.Kind, mediaId: m.MediaId, deleted: m.Deleted);
            th.Messages.Add(bubble);
            if (bubble.IsImage) _ = LoadImageAsync(bubble);
        }
        if (t.Messages.Count > 0) th.Preview = Preview(t.Messages[^1]);
        SupportUnread = _support.TotalUnread;
    }

    private async Task LoadImageAsync(ChatMessageViewModel m)
    {
        try
        {
            if (Cloud is not { } cloud || m.MediaId is null) return;
            var got = await cloud.ChatMediaAsync(m.MediaId, _life.Token);
            if (got is null) return;
            using var ms = new MemoryStream(got.Value.Bytes);
            var bmp = new Bitmap(ms);
            await Dispatcher.UIThread.InvokeAsync(() => m.Image = bmp);
        }
        catch { /* عکسِ خراب — حباب بی‌عکس می‌ماند */ }
    }

    private async Task MarkSeenAsync(ChatThreadViewModel th)
    {
        if (th.Acct is null || !_support.TryGet(th.Acct, out var t)) return;
        var last = t.LastSeq;
        _support.Seen(th.Acct);
        th.Unread = 0;
        SupportUnread = _support.TotalUnread;
        if (Cloud is { } cloud && last > 0) await cloud.ChatSeenAsync(th.Acct, last, _life.Token);
    }

    public override async Task OnActivatedAsync()
    {
        if (Current is { IsSupport: true } th) await MarkSeenAsync(th);
    }

    private static string Preview(CloudChatMessage m) =>
        m.Deleted ? "🚫 پاک شد" : m.Kind switch
        {
            "image" => "📷 عکس", "video" => "🎥 ویدیو", "audio" => "🎤 پیامِ صوتی", _ => m.Text,
        };

    private static string TimeOf(long ms)
    {
        if (ms <= 0) return "";
        var local = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime();
        return local.Date == DateTime.Today ? local.ToString("HH:mm") : Shamsi.Of(local.DateTime) + " " + local.ToString("HH:mm");
    }

    private static string MimeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif",
        ".mp4" or ".m4v" => "video/mp4", ".webm" => "video/webm", ".mov" => "video/quicktime",
        ".m4a" => "audio/mp4", ".mp3" => "audio/mpeg", ".wav" => "audio/wav", ".ogg" => "audio/ogg",
        _ => "",
    };

    private static string ExtOf(string mime) => mime.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", "image/gif" => ".gif",
        "video/mp4" => ".mp4", "video/webm" => ".webm", "video/quicktime" => ".mov",
        "audio/mp4" => ".m4a", "audio/mpeg" => ".mp3", "audio/wav" or "audio/x-wav" => ".wav",
        "audio/ogg" => ".ogg", "audio/webm" => ".weba", _ => ".bin",
    };

    private static void Trim(ChatThreadViewModel th)
    {
        while (th.Messages.Count > Keep) th.Messages.RemoveAt(0);
    }

    // ── تاریخچهٔ محلی (فقط شبکهٔ خانگی) ────────────────────────────────────

    private sealed record Saved(string Id, string Title, List<string[]> Rows);

    private void Save()
    {
        try
        {
            var data = Threads.Where(t => !t.IsSupport).Select(t => new Saved(t.Id, t.Title,
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
                if (s.Id.StartsWith("acct:", StringComparison.Ordinal)) continue;
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
