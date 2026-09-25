using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>سه نوعِ گفت‌وگو — و هر کدام درِ خودش را دارد.</summary>
public enum ChatKind
{
    /// <summary>گروهِ کارکنانِ همین پمپ (سرورِ خانگی، پوشهٔ همین پمپ).</summary>
    Group,
    /// <summary>صاحبِ پمپ ↔ پشتیبانیِ برنامه (سرورِ حساب).</summary>
    Support,
    /// <summary>مشتریِ کیو‌آر ↔ صاحبِ پمپ (سرورِ حساب).</summary>
    Customer,
}

/// <summary>یک پیامِ داخلِ گفت‌وگو — متنی یا رسانه‌ای (عکس، ویدیو، صدا).</summary>
public sealed partial class ChatMessageViewModel : ObservableObject
{
    public ChatMessageViewModel(string sender, string text, string time, bool mine, bool pending = false,
                                string id = "", string kind = "text", string? mediaId = null, bool deleted = false,
                                string key = "", string role = "", bool trashed = false, string daysLeft = "")
    {
        Sender = sender; Text = text; Time = time; Mine = mine; Id = id; Kind = kind; MediaId = mediaId;
        Key = key; Role = role; Trashed = trashed; DaysLeft = daysLeft;
        _isPending = pending;
        _deleted = deleted;
    }

    /// <summary>کلیدِ همین پیام در ‎ChatStore‎.</summary>
    public string Key { get; }

    /// <summary>شناسهٔ سرورِ حساب — خالی برای گروه و پشتیبانی.</summary>
    public string Id { get; }
    public string Sender { get; }
    public string Text { get; }
    public string Time { get; }
    public string Kind { get; }
    public string? MediaId { get; }

    /// <summary>نقشِ فرستنده در گروه («مدیر»، «میرزا»، «کارمند») — خالی برای بقیه.</summary>
    public string Role { get; }
    public bool HasRole => Role.Length > 0;

    /// <summary>در سطل است — فقط در نمای سطل دیده می‌شود.</summary>
    public bool Trashed { get; }

    /// <summary>«۱۲ روز مانده» — تا رفتن به سطل، یا در سطل تا حذفِ کامل.</summary>
    public string DaysLeft { get; }

    /// <summary>پیامِ خودم — حبابش سمتِ دیگر و رنگش تاکیدی است.</summary>
    public bool Mine { get; }
    public bool NotMine => !Mine;

    /// <summary>هنوز نرفته. با رفتن، خودش خاموش می‌شود.</summary>
    [ObservableProperty] private bool _isPending;

    /// <summary>پاک شده — جایش می‌ماند، متنش نه.</summary>
    [ObservableProperty] private bool _deleted;

    /// <summary>عکس، از فایلِ همین کامپیوتر.</summary>
    [ObservableProperty] private Bitmap? _image;

    /// <summary>رسانه روی سرور منقضی شده و نسخه‌ای هم این‌جا نبود.</summary>
    [ObservableProperty] private bool _gone;

    public bool IsText => Kind == "text" && !Deleted && Text.Length > 0;
    public bool IsImage => Kind == "image" && !Deleted && !Gone;
    public bool IsMedia => (Kind == "video" || Kind == "audio") && !Deleted && !Gone;
    public bool CanDelete => Id.Length > 0 && !Deleted && !Trashed;
    public string MediaLabel => Kind == "video" ? "🎥 ویدیو — باز کردن" : Kind == "audio" ? "🎤 پیامِ صوتی — پخش" : "";

    partial void OnDeletedChanged(bool v) => Changed();
    partial void OnGoneChanged(bool v) => Changed();

    private void Changed()
    {
        foreach (var n in new[] { nameof(IsText), nameof(IsImage), nameof(IsMedia), nameof(CanDelete) })
            OnPropertyChanged(n);
    }
}

/// <summary>یک خطِ «حساب‌های این نفر» در ستونِ مشخصات.</summary>
public sealed record ChatInfoLine(string Label, string Value, string ColorKey = "Pump.Text")
{
    //  ⚠️ رنگ با کلاس، نه با مبدلِ کلید: مبدل رنگ را یک بار در تمِ همان لحظه
    //  می‌گیرد و با عوض شدنِ تم رنگِ تمِ قبلی می‌ماند (عدد در تمِ تیره گم می‌شد).
    public bool IsAccent => ColorKey == "Pump.Accent";
    public bool IsMuted => ColorKey == "Pump.Muted";
}

/// <summary>یک دفتر (واحدِ تیل / واحدِ پول) با چهار عددش.</summary>
public sealed record ChatInfoBook(string Title, IReadOnlyList<ChatInfoLine> Lines);

/// <summary>یک گفت‌وگو.</summary>
public sealed partial class ChatThreadViewModel : ObservableObject
{
    public ChatThreadViewModel(string id, string title, ChatKind kind, string? acct = null)
    {
        Id = id; _title = title; Kind = kind; Acct = acct;
    }

    public string Id { get; }
    public ChatKind Kind { get; }

    /// <summary>شناسهٔ حسابِ کیو‌آر (‎d12‎، ‎c3‎) — فقط برای مشتری.</summary>
    public string? Acct { get; }

    public bool IsCustomer => Kind == ChatKind.Customer;
    public bool IsGroup => Kind == ChatKind.Group;
    public bool IsSupportDesk => Kind == ChatKind.Support;

    /// <summary>عکس و ویدیو و صدا — فقط گفت‌وگوی مشتری روی سرورِ حساب درِ رسانه دارد.</summary>
    public bool CanAttach => IsCustomer;

    [ObservableProperty] private string _title;
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    [ObservableProperty] private int _unread;
    [ObservableProperty] private string _preview = "";
    [ObservableProperty] private string _previewTime = "";
    [ObservableProperty] private bool _blocked;
    [ObservableProperty] private long _lastAt;

    /// <summary>نامی که خودِ مشتری در صفحهٔ کیو‌آر نوشته — زیرنویس، نه عنوان.</summary>
    [ObservableProperty] private string _who = "";

    public bool HasUnread => Unread > 0;

    public string Avatar => Kind switch
    {
        ChatKind.Group => "👥",
        ChatKind.Support => "🛟",
        _ => (Title ?? "").TrimStart() is { Length: > 0 } t ? t[..1] : "؟",
    };

    /// <summary>رنگِ دایرهٔ کنارِ نام — از پالتِ تم، ثابت برای هر گفت‌وگو.</summary>
    public string AvatarKey => Kind switch
    {
        ChatKind.Group => "Pump.Accent",
        ChatKind.Support => "Pump.Info",
        _ => new[] { "Pump.Ok", "Pump.Warn", "Pump.Info", "Pump.Danger", "Pump.Accent" }
                 [(int)((uint)StableHash(Id) % 5)],
    };

    public string Subtitle => Kind switch
    {
        ChatKind.Group => "کارمندان · مدیر · میرزا",
        ChatKind.Support => "پشتیبانیِ برنامه — مشکل را با ما بگویید",
        _ => (Blocked ? "🚫 بلاک شده · " : "") + (Who.Length > 0 ? Who + " · " : "") + "مشتریِ کیو‌آر",
    };

    public string BlockText => Blocked ? "رفعِ بلاک" : "🚫 بلاک";
    public bool HasPreview => !string.IsNullOrWhiteSpace(Preview);

    partial void OnUnreadChanged(int v) => OnPropertyChanged(nameof(HasUnread));
    partial void OnPreviewChanged(string v) => OnPropertyChanged(nameof(HasPreview));
    partial void OnTitleChanged(string v) => OnPropertyChanged(nameof(Avatar));
    partial void OnWhoChanged(string v) => OnPropertyChanged(nameof(Subtitle));
    partial void OnBlockedChanged(bool v) { OnPropertyChanged(nameof(Subtitle)); OnPropertyChanged(nameof(BlockText)); }

    private static int StableHash(string s)
    {
        unchecked
        {
            var h = 17;
            foreach (var ch in s) h = h * 31 + ch;
            return h;
        }
    }
}

/// <summary>
/// ══ پیام‌رسانِ تمام‌صفحه — فهرست · گفت‌وگو · مشخصات ═══════════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «چت روم… وقتی میری توش تمام‌صفحه بشه…
/// یک طرف من با کاربرهایی که حساب دارن و یک طرف همون یارو که بهم پیام داده و
/// پایینش اطلاعاتِ یارو نوشته، حساب‌هاش… و گروپ چت برای کارمندان و مدیر و
/// میرزا… و یک بخشِ کوچک با ساپورت که میرزا اگه مشکلی بود با من در تماس
/// بشه… و این‌ها روی سرور فضایی رو نگیره، توی برنامه باشن.»
///
/// سه نوع گفت‌وگو (‎ChatKind‎)، سه در، و <b>یک</b> جای ماندگاری:
///
///   • <b>گروهِ کارکنان</b> — ‎StationChat‎ (پوشهٔ همین پمپ روی سرورِ خانگی).
///   • <b>پشتیبانیِ برنامه</b> — ‎CloudLink.Support*‎ (رشتهٔ همین پمپ).
///   • <b>مشتری‌های کیو‌آر</b> — ‎CloudLink.Chat*‎؛ هر مشتری فقط گفت‌وگوی خودش
///     را می‌بیند (رمزِ ‎k‎ی همان یک حساب).
///
/// ⛔ هر چه می‌رسد همان لحظه در ‎ChatStore‎ می‌نشیند (رسانه هم) و صفحه فقط از
/// همان می‌خواند — پس پاک شدنِ ۱۵‌روزهٔ سرور هیچ پیامی را از این کامپیوتر
/// نمی‌برد، و ماندگاریِ ۱۵ + ۱۵ روزه فقط یک جا تصمیم گرفته می‌شود
/// (‎ChatStore.Decide‎).
/// </summary>
public sealed partial class ChatSectionViewModel : SectionViewModel
{
    public const string GroupId = "group";
    public const string SupportId = "support";

    /// <summary>هر چند وقت، وقتی خودِ پیام‌رسان باز است.</summary>
    public static readonly TimeSpan CloudEvery = TimeSpan.FromSeconds(6);

    /// <summary>وقتی پیام‌رسان باز نیست: فقط برای شمارهٔ نخواندهٔ سربرگ، هر دقیقه.</summary>
    public static readonly TimeSpan CloudEveryIdle = TimeSpan.FromSeconds(60);

    private readonly AppHost _host;
    private readonly CancellationTokenSource _life = new();
    private readonly SupportState _support = new();
    private readonly StationChat _group;
    private readonly SemaphoreSlim _tick = new(1, 1);
    private readonly SemaphoreSlim _wake = new(0, 1);
    private ChatStore? _store;
    private CloudLink? _cloud;
    private DateTime _lastThreads = DateTime.MinValue;
    private WaveRecorder? _rec;

    public ChatSectionViewModel(AppHost host) : base("chat", "chat", "پیام‌رسان")
    {
        _host = host;
        _group = new StationChat(AppSettings.Load);

        // ⛔ تمام‌صفحه: سربرگ، نوارِ عددها و نوارِ بخش‌ها پنهان می‌شوند
        // (‎MainViewModel.IsChromeVisible‎) — همان راهِ صفحهٔ حسابِ قرض‌دار.
        IsPageOpen = true;

        Threads.Add(new ChatThreadViewModel(GroupId, "گروهِ کارکنان", ChatKind.Group));
        Threads.Add(new ChatThreadViewModel(SupportId, "پشتیبانیِ برنامه", ChatKind.Support));
        Current = Threads[0];

        host.LedgerSwitched += () => Dispatcher.UIThread.Post(OpenStore);
        OpenStore();
        _ = LoopAsync();
    }

    public ObservableCollection<ChatThreadViewModel> Threads { get; } = new();

    /// <summary>فهرستِ دیده‌شده — پس از جست‌وجو.</summary>
    public ObservableCollection<ChatThreadViewModel> Shown { get; } = new();

    [ObservableProperty] private ChatThreadViewModel? _current;
    [ObservableProperty] private string _draft = "";
    [ObservableProperty] private string _search = "";

    /// <summary>حالِ اتصال — همان چیزی که کاربر باید ببیند، نه پنهان بماند.</summary>
    [ObservableProperty] private string _state = "";

    /// <summary>همهٔ پیام‌های نخوانده — روی دکمهٔ «پشتیبانی» در سربرگ.</summary>
    [ObservableProperty] private int _supportUnread;

    [ObservableProperty] private bool _recording;

    /// <summary>نمای سطلِ زبالهٔ همین گفت‌وگو.</summary>
    [ObservableProperty] private bool _showTrash;
    [ObservableProperty] private int _trashCount;

    // ── ستونِ مشخصات ──
    [ObservableProperty] private string _infoTitle = "";
    [ObservableProperty] private string _infoKind = "";
    [ObservableProperty] private string _infoPhone = "";
    [ObservableProperty] private string _infoNote = "";
    public ObservableCollection<ChatInfoBook> InfoBooks { get; } = new();
    public ObservableCollection<ChatInfoLine> InfoAccounts { get; } = new();
    public ObservableCollection<ChatInfoLine> Members { get; } = new();
    public ObservableCollection<ChatMessageViewModel> SharedImages { get; } = new();
    [ObservableProperty] private int _imageCount;
    [ObservableProperty] private int _videoCount;
    [ObservableProperty] private int _audioCount;

    public bool CanRecord => WaveRecorder.Available;
    public string RecordText => Recording ? "⏹ پایان و فرستادن" : "🎤";
    partial void OnRecordingChanged(bool v) => OnPropertyChanged(nameof(RecordText));

    public bool HasSupportUnread => SupportUnread > 0;
    partial void OnSupportUnreadChanged(int v) => OnPropertyChanged(nameof(HasSupportUnread));

    public bool HasCurrent => Current is not null;
    public bool HasInfoBooks => InfoBooks.Count > 0;
    public bool HasInfoAccounts => InfoAccounts.Count > 0;
    public bool HasMembers => Members.Count > 0;
    public bool HasSharedImages => SharedImages.Count > 0;
    public bool HasInfoPhone => InfoPhone.Length > 0;
    public bool IsEmpty => Current is { } c && c.Messages.Count == 0;
    public string TrashText => ShowTrash ? "‹ برگشت به گفت‌وگو" : "🗑 سطلِ زباله (" + Shamsi.Money(TrashCount) + ")";
    public string EmptyText => ShowTrash
        ? "سطل خالی است."
        : Current?.Kind switch
        {
            ChatKind.Group => "هنوز پیامی در گروه نیست. پیامِ شما را همهٔ کارمندان، مدیر و میرزای همین پمپ می‌بینند — مشتری‌ها راهی به این گروه ندارند.",
            ChatKind.Support => "سلام. هر مشکلی یا سؤالی دربارهٔ برنامهٔ پمپ دارید همین‌جا بنویسید؛ جوابِ پشتیبانی همین‌جا می‌آید.",
            _ => "هنوز پیامی نیست.",
        };

    /// <summary>قاعدهٔ ماندگاری، همان‌طور که کاربر باید بداند.</summary>
    public string RetentionText =>
        $"پیام‌ها {Shamsi.Money(ChatStore.ActiveDays)} روز در گفت‌وگو می‌مانند، بعد به سطل می‌روند و "
        + $"{Shamsi.Money(ChatStore.TrashDays)} روزِ دیگر حذفِ کامل می‌شوند. همه روی همین کامپیوتر نگه داشته می‌شوند.";

    partial void OnShowTrashChanged(bool v)
    {
        OnPropertyChanged(nameof(TrashText));
        OnPropertyChanged(nameof(EmptyText));
        if (Current is { } th) Reload(th);
    }

    partial void OnTrashCountChanged(int v) => OnPropertyChanged(nameof(TrashText));
    partial void OnInfoPhoneChanged(string v) => OnPropertyChanged(nameof(HasInfoPhone));
    partial void OnSearchChanged(string v) => RebuildShown();

    partial void OnCurrentChanged(ChatThreadViewModel? v)
    {
        OnPropertyChanged(nameof(HasCurrent));
        OnPropertyChanged(nameof(EmptyText));
        ShowTrash = false;
        if (v is null) return;
        Reload(v);
        _ = FillInfoAsync(v);
        MarkSeen(v);
    }

    /// <summary>دفتر (کلِ پیام‌ها و رسانه) کنارِ دفترِ حساب‌های همین حساب.</summary>
    private void OpenStore()
    {
        try
        {
            var dir = Path.GetDirectoryName(_host.Db.DbPath);
            if (string.IsNullOrEmpty(dir)) return;
            _store = new ChatStore(dir);
            _store.Sweep(Now);
        }
        catch { _store = null; State = "دفترِ پیام‌ها باز نشد"; return; }

        //  حسابِ دیگر ⇒ گفت‌وگوهای مشتریِ حسابِ قبلی از فهرست می‌روند
        foreach (var t in Threads.Where(t => t.IsCustomer).ToList()) Threads.Remove(t);
        foreach (var id in _store.Threads().Where(x => x.StartsWith("acct:", StringComparison.Ordinal)))
            BindCustomer(id[5..]);
        foreach (var t in Threads) Reload(t, onlySummary: !ReferenceEquals(t, Current));
        RebuildShown();
        UpdateUnread();
    }

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private string Me => _host.Session.UserName is { Length: > 0 } n ? n : "میرزا";

    private string MyRole => _host.Session.Role switch
    {
        UserRole.Admin => "admin",
        UserRole.Staff => "staff",
        _ => "mirza",
    };

    /// <summary>سرورِ حساب — فقط وقتی این کامپیوتر به پمپ بند است.</summary>
    private CloudLink? Cloud
    {
        get
        {
            var file = AppSettings.Load();
            if (string.IsNullOrWhiteSpace(file.CloudDeviceToken)) return null;
            return _cloud ??= new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });
        }
    }

    private string OwnerName
    {
        get
        {
            try { var n = HomeLink.StationName(_host); return n.Length > 0 ? n : "پمپ"; }
            catch { return "پمپ"; }
        }
    }

    public override void OnDayChanged()
    {
        try { _store?.Sweep(Now); } catch { }
        if (Current is { } th) Reload(th);
    }

    // ══ برگشت و جست‌وجو ═════════════════════════════════════════════════════════

    /// <summary>‹ برگشت — پیام‌رسان تمام‌صفحه است و بی این دکمه راهِ بیرون نداشت.</summary>
    [RelayCommand]
    private async Task BackAsync()
    {
        if (_host.GoHome is { } go) await go();
    }

    [RelayCommand]
    private void ToggleTrash() => ShowTrash = !ShowTrash;

    private void RebuildShown()
    {
        var q = (Search ?? "").Trim();
        var list = Threads
            .Where(t => q.Length == 0 || t.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                                      || t.Who.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Kind == ChatKind.Customer ? 1 : 0)
            .ThenBy(t => t.Kind == ChatKind.Support ? 1 : 0)
            .ThenByDescending(t => t.LastAt)
            .ToList();
        //  ⚠️ فقط اگر واقعاً عوض شد — ساختنِ دوبارهٔ فهرست انتخاب را می‌پراند
        if (list.SequenceEqual(Shown)) return;
        Shown.Clear();
        foreach (var t in list) Shown.Add(t);
    }

    // ══ فرستادن ═════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = (Draft ?? "").Trim();
        if (text.Length == 0 || Current is not { } th || _store is null) return;
        if (ShowTrash) ShowTrash = false;
        Draft = "";

        switch (th.Kind)
        {
            case ChatKind.Group: await SendGroupAsync(th, text); break;
            case ChatKind.Support: await SendSupportDeskAsync(th, text); break;
            default: await SendCustomerAsync(th, text); break;
        }
    }

    private async Task SendGroupAsync(ChatThreadViewModel th, string text)
    {
        var cid = Guid.NewGuid().ToString("N");
        var key = "grp:cid:" + cid;
        _store!.Upsert(new ChatRow(key, GroupId, 0, Me, text, "text", null, true, Now, Pending: true));
        _store.SetMeta("pending." + cid, MyRole);
        Reload(th);

        var (ok, msg, why) = await _group.PostAsync(cid, Me, MyRole, text, _life.Token);
        if (ok && msg is not null)
        {
            _store.Rekey(key, GroupRow(msg, mine: true));
            State = "";
        }
        else State = "در صف — " + why;
        Reload(th);
    }

    /// <summary>پیام‌های گروه که هنوز نرفته‌اند — با هر دور دوباره امتحان می‌شوند.</summary>
    private async Task FlushGroupAsync(CancellationToken ct)
    {
        if (_store is null) return;
        foreach (var r in _store.Rows(GroupId).Where(r => r.Pending && r.Key.StartsWith("grp:cid:", StringComparison.Ordinal)))
        {
            var cid = r.Key["grp:cid:".Length..];
            var role = _store.Meta("pending." + cid, MyRole);
            var (ok, msg, _) = await _group.PostAsync(cid, r.Sender, role, r.Text, ct);
            if (!ok || msg is null) return;
            _store.Rekey(r.Key, GroupRow(msg, mine: true));
        }
    }

    private static ChatRow GroupRow(GroupMessage m, bool mine) =>
        new("grp:" + m.Seq, GroupId, m.Seq, m.From, m.Text, "text", null, mine, m.At > 0 ? m.At : Now,
            Anchor: m.At > 0 ? m.At : Now) { };

    private async Task SendSupportDeskAsync(ChatThreadViewModel th, string text)
    {
        var cloud = Cloud;
        if (cloud is null)
        {
            State = "پشتیبانی وقتی کار می‌کند که این کامپیوتر به حسابِ پمپ وصل باشد (پروفایل)";
            _host.Toast(State, ToastKind.Warn);
            return;
        }
        var key = "sup:pend:" + Guid.NewGuid().ToString("N");
        _store!.Upsert(new ChatRow(key, SupportId, 0, Me, text, "text", null, true, Now, Pending: true));
        Reload(th);
        var r = await cloud.SupportSendAsync(text, _life.Token);
        if (!r.Ok) { State = "نرفت: " + r.Why; _host.Toast("پیام به پشتیبانی نرفت: " + r.Why, ToastKind.Error); return; }
        _store.Delete(key);
        State = "";
        await PollSupportDeskAsync(cloud, _life.Token);
        Reload(th);
    }

    private async Task SendCustomerAsync(ChatThreadViewModel th, string text, string kind = "", string? mediaId = null)
    {
        var cloud = Cloud;
        if (cloud is null || th.Acct is null) { State = "برنامه به سرورِ حساب وصل نیست"; return; }
        State = "در حالِ فرستادن…";
        var (ok, msg, why) = await cloud.ChatSendAsync(th.Acct, OwnerName, text, kind, mediaId, _life.Token);
        if (!ok || msg is null) { State = "نرفت: " + why; _host.Toast("پیام فرستاده نشد: " + why, ToastKind.Error); return; }
        State = "";
        _support.Merge(new[] { msg });
        Keep(msg);
        Reload(th);
    }

    /// <summary>📎 عکس یا ویدیو یا صدا از روی دیسک — فقط برای مشتری.</summary>
    [RelayCommand]
    private Task AttachAsync() => CrashGuard.RunAsync("فرستادنِ فایل", async () =>
    {
        if (Current is not { IsCustomer: true } th) return;
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
        //  ⛔ نسخهٔ خودمان همان لحظه این‌جا می‌نشیند — سرور ۱۵ روز بعد پاکش می‌کند
        try { _store?.SaveMedia(mediaId, mime, bytes); } catch { }
        await SendCustomerAsync(th, "", kind, mediaId);
    }

    [RelayCommand]
    private Task RecordAsync() => CrashGuard.RunAsync("ضبطِ صدا", async () =>
    {
        if (!CanRecord || Current is not { IsCustomer: true } th) return;
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

    /// <summary>🗑 پاک کردنِ پیامِ مشتری — جایش می‌ماند، متنش نه (هر دو طرف می‌بینند).</summary>
    [RelayCommand]
    private Task DeleteMessageAsync(ChatMessageViewModel? m) => CrashGuard.RunAsync("پاک کردنِ پیام", async () =>
    {
        if (m is null || !m.CanDelete || Current is not { IsCustomer: true } th) return;
        var cloud = Cloud;
        if (cloud is null) return;
        var r = await cloud.ChatDeleteAsync(m.Id, _life.Token);
        if (!r.Ok) { _host.Toast("پاک نشد: " + r.Why, ToastKind.Error); return; }
        _support.MarkDeleted(m.Id);
        if (_store?.Rows(th.Id).FirstOrDefault(x => x.Key == m.Key) is { } row)
            _store.Upsert(row with { Deleted = true, Text = "", MediaId = null });
        Reload(th);
    });

    /// <summary>↩ برگرداندن از سطل — از همین لحظه ۱۵ روزِ دیگر.</summary>
    [RelayCommand]
    private void Restore(ChatMessageViewModel? m)
    {
        if (m is null || _store is null || Current is not { } th) return;
        if (_store.Restore(m.Key, Now)) _host.Toast("پیام برگشت", ToastKind.Info);
        Reload(th);
    }

    [RelayCommand]
    private Task ToggleBlockAsync() => CrashGuard.RunAsync("بلاک", async () =>
    {
        if (Current is not { IsCustomer: true, Acct: { } acct } th) return;
        var cloud = Cloud;
        if (cloud is null) return;
        var r = await cloud.ChatBlockAsync(acct, !th.Blocked, _life.Token);
        if (!r.Ok) { _host.Toast("نشد: " + r.Why, ToastKind.Error); return; }
        th.Blocked = !th.Blocked;
        _support.Get(acct).Blocked = th.Blocked;
        _host.Toast(th.Blocked ? "مشتری بلاک شد" : "بلاک برداشته شد", ToastKind.Info);
    });

    /// <summary>باز کردنِ حسابِ همین مشتری در بخشِ خودش.</summary>
    [RelayCommand]
    private Task OpenAccountAsync() => CrashGuard.RunAsync("باز کردنِ حساب", async () =>
    {
        if (Current is not { IsCustomer: true, Acct: { Length: > 1 } acct }) return;
        if (!long.TryParse(acct[1..], out var id)) return;
        if (acct[0] == 'd')
        {
            var owner = await _host.Debtors.OwnerOfAccountAsync(id, _life.Token);
            if (owner is null || _host.GoSection is not { } go) return;
            await go("debt");
            //  همان راهی که «قرض‌های دسته‌جمعی» حساب را باز می‌کند
            if (_host.FindSection?.Invoke("debt") is DebtSectionViewModel debt) await debt.OpenPersonAsync(owner.Value);
        }
        else if (_host.GoSection is { } go) await go("noinv");
    });

    /// <summary>▶ ویدیو یا صدا — از فایلِ همین کامپیوتر، با برنامهٔ خودِ ویندوز.</summary>
    [RelayCommand]
    private Task OpenMediaAsync(ChatMessageViewModel? m) => CrashGuard.RunAsync("باز کردنِ رسانه", async () =>
    {
        if (m is null || !SafeMediaId(m.MediaId)) return;
        var path = await EnsureMediaAsync(m.MediaId!);
        if (path is null) { m.Gone = true; _host.Toast("این رسانه روی سرور منقضی شده و نسخه‌ای این‌جا نیست", ToastKind.Warn); return; }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { _host.Toast("برنامه‌ای برای باز کردنِ این فایل نیست", ToastKind.Error); }
    });

    // ══ گرفتن ═══════════════════════════════════════════════════════════════════

    private async Task LoopAsync()
    {
        //  ⚠️ صفحهٔ اول بی رقیب بیاید — همان قاعدهٔ ‎StationPublisher.FirstDelay‎
        try { await Task.Delay(TimeSpan.FromSeconds(8), _life.Token); } catch { return; }
        while (!_life.IsCancellationRequested)
        {
            try { await PollCloudAsync(_life.Token); }
            catch (OperationCanceledException) { return; }
            catch { /* نرسید — دورِ بعد */ }
            try { await _wake.WaitAsync(IsActive ? CloudEvery : CloudEveryIdle, _life.Token); }
            catch { return; }
        }
    }

    /// <summary>با باز شدنِ پیام‌رسان همان لحظه بپرس — نه شصت ثانیه بعد.</summary>
    public override Task OnActivatedAsync()
    {
        if (_wake.CurrentCount == 0) _wake.Release();
        if (Current is { } th) MarkSeen(th);
        return Task.CompletedTask;
    }

    /// <summary>یک دور: گروه، پشتیبانی و صندوقِ مشتری‌ها.</summary>
    public async Task PollCloudAsync(CancellationToken ct = default)
    {
        if (!await _tick.WaitAsync(0, ct)) return;
        try
        {
            var notes = new List<string>();
            if (_group.Ready) notes.Add(await PollGroupAsync(ct));
            if (Cloud is { } cloud)
            {
                await PollSupportDeskAsync(cloud, ct);
                await PollCustomersAsync(cloud, ct);
            }
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var t in Threads) Reload(t, onlySummary: !ReferenceEquals(t, Current));
                RebuildShown();
                UpdateUnread();
                var bad = notes.FirstOrDefault(n => n.Length > 0);
                if (bad is not null) State = bad;
                else if (State.StartsWith("در صف", StringComparison.Ordinal) || State.StartsWith("گروه:", StringComparison.Ordinal)) State = "";
            });
        }
        finally { _tick.Release(); }
    }

    private async Task<string> PollGroupAsync(CancellationToken ct)
    {
        if (_store is null) return "";
        await FlushGroupAsync(ct);
        var since = _store.MetaLong("group.since");
        var (ok, msgs, why, last) = await _group.FetchAsync(since, ct);
        if (!ok) return "گروه: " + why;
        //  پوشهٔ پمپ روی سرور از نو ساخته شد ⇒ شماره‌ها از صفر؛ بی این، هیچ
        //  پیامِ تازه‌ای دیگر هیچ‌وقت نمی‌رسید.
        if (last < since)
        {
            since = 0;
            (ok, msgs, why, _) = await _group.FetchAsync(0, ct);
            if (!ok) return "گروه: " + why;
        }
        var fresh = new List<GroupMessage>();
        foreach (var m in msgs)
        {
            //  پیامی که همین کامپیوتر فرستاده و هنوز «درراه» مانده بود
            var pendingKey = "grp:cid:" + m.Cid;
            var mine = m.Cid.Length > 0 && _store.Meta("pending." + m.Cid).Length > 0;
            if (mine) _store.Delete(pendingKey);
            var existed = _store.Rows(GroupId).Any(r => r.Key == "grp:" + m.Seq);
            _store.Upsert(GroupRow(m, mine));
            if (!existed && !mine) fresh.Add(m);
            if (m.Seq > since) since = m.Seq;
        }
        _store.SetMeta("group.since", since.ToString());
        if (fresh.Count > 0)
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!(IsActive && Current?.IsGroup == true))
                    foreach (var m in fresh.TakeLast(3))
                        _host.Toast("👥 " + m.From + ": " + Clip(m.Text), ToastKind.Info);
            });
        return "";
    }

    private async Task PollSupportDeskAsync(CloudLink cloud, CancellationToken ct)
    {
        if (_store is null) return;
        var after = _store.MetaLong("support.after");
        var (ok, msgs, _, _) = await cloud.SupportThreadAsync(after, ct);
        if (!ok) return;
        var fresh = new List<CloudChatMessage>();
        foreach (var m in msgs)
        {
            var key = "sup:" + m.Id;
            var existed = _store.Rows(SupportId).Any(r => r.Key == key);
            var mine = m.From == "o";
            if (mine)
                foreach (var p in _store.Rows(SupportId).Where(r => r.Pending && r.Text == m.Text))
                    _store.Delete(p.Key);
            _store.Upsert(new ChatRow(key, SupportId, m.Seq,
                mine ? (m.Name.Length > 0 ? m.Name : Me) : "پشتیبانیِ برنامه",
                m.Text, "text", null, mine, m.At, Anchor: m.At));
            if (!existed && !mine) fresh.Add(m);
            if (m.Seq > after) after = m.Seq;
        }
        _store.SetMeta("support.after", after.ToString());
        if (fresh.Count > 0)
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!(IsActive && Current?.IsSupportDesk == true))
                    _host.Toast("🛟 پشتیبانی: " + Clip(fresh[^1].Text), ToastKind.Info);
            });
    }

    private async Task PollCustomersAsync(CloudLink cloud, CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastThreads > TimeSpan.FromMinutes(1))
        {
            var (ok, threads, _) = await cloud.ChatThreadsAsync(ct);
            if (ok)
            {
                _lastThreads = DateTime.UtcNow;
                _support.ApplyThreads(threads);
                foreach (var t in threads) Keep(t.Last);
            }
        }

        var after = Math.Max(_support.LastSeq, _store?.MetaLong("cloud.lastSeq") ?? 0);
        var (ok2, msgs, _) = await cloud.ChatInboxAsync(after, ct);
        if (!ok2) return;
        var fresh = _support.Merge(msgs);
        foreach (var m in msgs) Keep(m);
        _store?.SetMeta("cloud.lastSeq", Math.Max(after, _support.LastSeq).ToString());

        //  ⛔ رسانه همان لحظه این‌جا می‌نشیند — سرور فقط ۱۵ روز نگهش می‌دارد
        foreach (var m in msgs.Where(x => !x.Deleted && SafeMediaId(x.MediaId)))
            await EnsureMediaAsync(m.MediaId!);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var t in _support.Threads) BindCustomer(t.Acct);
            foreach (var m in fresh)
            {
                var th = BindCustomer(m.Acct);
                if (ReferenceEquals(th, Current) && IsActive) MarkSeen(th);
                else _host.Toast("💬 " + (m.Name.Length > 0 ? m.Name : "مشتری") + ": " + Preview(m), ToastKind.Info);
            }
        });
    }

    /// <summary>پیامِ مشتری ⇒ دفترِ همین کامپیوتر.</summary>
    private void Keep(CloudChatMessage? m)
    {
        if (m is null || _store is null || m.Acct.Length == 0) return;
        var mine = !m.FromCustomer;
        var sender = m.FromCustomer ? (m.Name.Length > 0 ? m.Name : "مشتری") : (m.Name.Length > 0 ? m.Name : OwnerName);
        _store.Upsert(new ChatRow("cloud:" + m.Id, "acct:" + m.Acct, m.Seq, sender, m.Deleted ? "" : m.Text,
            m.Kind, m.Deleted ? null : m.MediaId, mine, m.At > 0 ? m.At : Now, m.Deleted, Anchor: m.At > 0 ? m.At : Now));
    }

    /// <summary>فایلِ محلیِ رسانه — اگر نیست، یک بار از سرور گرفته و نگه داشته می‌شود.</summary>
    private async Task<string?> EnsureMediaAsync(string mediaId)
    {
        if (_store is null) return null;
        if (_store.MediaPath(mediaId) is { } have) return have;
        if (Cloud is not { } cloud) return null;
        var got = await cloud.ChatMediaAsync(mediaId, _life.Token);
        if (got is null) return null;
        try { return _store.SaveMedia(mediaId, got.Value.Mime, got.Value.Bytes); }
        catch { return null; }
    }

    // ══ نشاندن روی صفحه ═══════════════════════════════════════════════════════

    private ChatThreadViewModel BindCustomer(string acct)
    {
        var id = "acct:" + acct;
        var th = Threads.FirstOrDefault(x => x.Id == id);
        if (th is null)
        {
            var name = _support.TryGet(acct, out var t) ? t.Name : "";
            th = new ChatThreadViewModel(id, TitleOf(acct, name), ChatKind.Customer, acct) { Who = name };
            if (_store is not null && long.TryParse(_store.Meta("seen." + acct), out var seen))
                _support.Get(acct).OwnerSeenSeq = Math.Max(_support.Get(acct).OwnerSeenSeq, seen);
            Threads.Add(th);
        }
        if (_support.TryGet(acct, out var st))
        {
            th.Who = st.Name;
            th.Blocked = st.Blocked;
            th.Title = TitleOf(acct, st.Name);
        }
        WantName(acct);
        return th;
    }

    /// <summary>
    /// پیام‌های یک گفت‌وگو را با دفترِ این کامپیوتر یکی می‌کند — فقط تفاوت‌ها.
    /// ‎onlySummary‎: فقط آخرین پیام و نخوانده (گفت‌وگویی که جلوی چشم نیست).
    /// </summary>
    private void Reload(ChatThreadViewModel th, bool onlySummary = false)
    {
        if (_store is null) return;
        List<ChatRow> rows;
        try { rows = _store.Rows(th.Id, trash: !onlySummary && ShowTrash && ReferenceEquals(th, Current)); }
        catch { return; }

        var live = !onlySummary && ShowTrash && ReferenceEquals(th, Current) ? _store.Rows(th.Id) : rows;
        if (live.Count > 0)
        {
            var last = live[^1];
            th.Preview = last.Deleted ? "🚫 پاک شد" : PreviewOf(last.Kind, last.Text);
            th.PreviewTime = TimeOf(last.At);
            th.LastAt = last.At;
        }

        th.Unread = ReferenceEquals(th, Current) && IsActive ? 0 : UnreadOf(th, live);
        if (onlySummary) return;

        TrashCount = _store.TrashCount(th.Id);
        var now = Now;
        var want = rows.Select(r => r.Key).ToList();
        //  کاملاً دیگر (سطل ⇄ گفت‌وگو) ⇒ از نو؛ وگرنه فقط اضافه و علامت
        if (th.Messages.Count > 0 && !th.Messages.Select(m => m.Key).Any(want.Contains)) th.Messages.Clear();
        var byKey = th.Messages.ToDictionary(m => m.Key, m => m);
        foreach (var gone in th.Messages.Where(m => !want.Contains(m.Key)).ToList()) th.Messages.Remove(gone);

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (byKey.TryGetValue(r.Key, out var vm) && vm.Deleted == r.Deleted && vm.IsPending == r.Pending
                && vm.Text == r.Text)
                continue;
            var bubble = new ChatMessageViewModel(r.Sender, r.Deleted ? "" : r.Text, TimeOf(r.At), r.Mine, r.Pending,
                id: r.Key.StartsWith("cloud:", StringComparison.Ordinal) ? r.Key[6..] : "",
                kind: r.Kind, mediaId: r.MediaId, deleted: r.Deleted, key: r.Key,
                role: th.IsGroup ? RoleOf(r) : "", trashed: r.TrashedAt > 0,
                daysLeft: Shamsi.Money(ChatStore.DaysLeft(r.Anchor, r.TrashedAt, now)) + " روز مانده");
            if (vm is not null)
            {
                var at = th.Messages.IndexOf(vm);
                th.Messages[at] = bubble;
            }
            else
            {
                //  جای درستش به ترتیبِ زمان
                var at = th.Messages.Count;
                for (var j = 0; j < th.Messages.Count; j++)
                    if (want.IndexOf(th.Messages[j].Key) > i) { at = j; break; }
                th.Messages.Insert(at, bubble);
            }
            if (bubble.IsImage) _ = LoadImageAsync(bubble);
        }
        OnPropertyChanged(nameof(IsEmpty));
        if (ReferenceEquals(th, Current)) CountMedia(th);
    }

    private string RoleOf(ChatRow r)
    {
        if (r.Mine) return new GroupMessage(0, "", "", MyRole, "x", 0).RoleText;
        return "";
    }

    private int UnreadOf(ChatThreadViewModel th, List<ChatRow> rows)
    {
        if (th.IsCustomer && th.Acct is { } acct && _support.TryGet(acct, out var t)) return t.Unread;
        if (_store is null) return 0;
        var seen = _store.MetaLong("seen." + th.Id);
        return rows.Count(r => !r.Mine && !r.Deleted && r.At > seen);
    }

    private void MarkSeen(ChatThreadViewModel th)
    {
        th.Unread = 0;
        if (_store is null) { UpdateUnread(); return; }
        if (th.IsCustomer && th.Acct is { } acct && _support.TryGet(acct, out var t))
        {
            var last = t.LastSeq;
            _support.Seen(acct);
            _store.SetMeta("seen." + acct, t.OwnerSeenSeq.ToString());
            if (Cloud is { } cloud && last > 0) _ = cloud.ChatSeenAsync(acct, last, _life.Token);
        }
        else
        {
            _store.SetMeta("seen." + th.Id, Now.ToString());
            if (th.IsSupportDesk && Cloud is { } cloud) _ = cloud.SupportSeenAsync(_life.Token);
        }
        UpdateUnread();
    }

    private void UpdateUnread() => SupportUnread = Threads.Sum(t => t.Unread);

    private void CountMedia(ChatThreadViewModel th)
    {
        ImageCount = th.Messages.Count(m => m.Kind == "image" && !m.Deleted);
        VideoCount = th.Messages.Count(m => m.Kind == "video" && !m.Deleted);
        AudioCount = th.Messages.Count(m => m.Kind == "audio" && !m.Deleted);
        SharedImages.Clear();
        foreach (var m in th.Messages.Where(m => m.IsImage).Reverse().Take(6)) SharedImages.Add(m);
        OnPropertyChanged(nameof(HasSharedImages));
        if (th.IsGroup)
        {
            Members.Clear();
            foreach (var g in th.Messages.Where(m => !m.Deleted).GroupBy(m => m.Sender).Take(12))
                Members.Add(new ChatInfoLine(g.Key, "آخرین پیام " + g.Last().Time));
            OnPropertyChanged(nameof(HasMembers));
        }
    }

    private async Task LoadImageAsync(ChatMessageViewModel m)
    {
        try
        {
            if (!SafeMediaId(m.MediaId)) return;
            var path = await EnsureMediaAsync(m.MediaId!);
            if (path is null) { await Dispatcher.UIThread.InvokeAsync(() => m.Gone = true); return; }
            var bmp = await Task.Run(() =>
            {
                using var fs = File.OpenRead(path);
                return Bitmap.DecodeToWidth(fs, 480);
            });
            await Dispatcher.UIThread.InvokeAsync(() => m.Image = bmp);
        }
        catch { /* عکسِ خراب — حباب بی‌عکس می‌ماند */ }
    }

    // ══ ستونِ مشخصات ═══════════════════════════════════════════════════════════

    private async Task FillInfoAsync(ChatThreadViewModel th)
    {
        InfoBooks.Clear(); InfoAccounts.Clear(); Members.Clear();
        InfoPhone = "";
        InfoTitle = th.Title;
        switch (th.Kind)
        {
            case ChatKind.Group:
                InfoKind = "گروهِ کارکنانِ همین پمپ";
                InfoNote = _group.Ready
                    ? "فقط کارمندان، مدیر و میرزای همین پمپ؛ مشتری‌ها و پمپ‌های دیگر هیچ راهی به این گروه ندارند. کارمندان از «اپِ کارمندان» روی گوشی می‌نویسند."
                    : "گروه وقتی کار می‌کند که این پمپ به سرورِ خانگی وصل باشد (چراغِ سربرگ).";
                break;
            case ChatKind.Support:
                InfoKind = "پشتیبانیِ برنامهٔ پمپ";
                InfoNote = Cloud is null
                    ? "برای نوشتن به پشتیبانی، این کامپیوتر باید به حسابِ پمپ وصل باشد (پروفایل). پشتیبانی هیچ‌وقت پشتِ اشتراک نمی‌رود."
                    : "مشکل یا پرسشی دربارهٔ برنامه دارید؟ همین‌جا بنویسید؛ جواب همین‌جا می‌آید. پشتیبانی هیچ‌وقت پشتِ اشتراک نمی‌رود.";
                break;
            default:
                InfoKind = th.Acct?.StartsWith("c", StringComparison.Ordinal) == true ? "شرکتِ تیل · مشتریِ کیو‌آر" : "قرض‌دار · مشتریِ کیو‌آر";
                InfoNote = "این مشتری فقط گفت‌وگوی خودش را می‌بیند.";
                await FillAccountAsync(th);
                break;
        }
        OnPropertyChanged(nameof(HasInfoBooks));
        OnPropertyChanged(nameof(HasInfoAccounts));
        OnPropertyChanged(nameof(HasMembers));
        CountMedia(th);
    }

    /// <summary>
    /// حساب‌های همین نفر — همان عددهایی که کیو‌آرش نشان می‌دهد (‎AcctSnapshots‎)،
    /// نه محاسبهٔ تازه. ⚠️ فقط خواندن؛ همه پشتِ ‎try‎ — مشخصات رفاه است، پیام اصل.
    /// </summary>
    private async Task FillAccountAsync(ChatThreadViewModel th)
    {
        try
        {
            if (th.Acct is not { Length: > 1 } acct || !long.TryParse(acct[1..], out var id)) return;
            AcctSnapshot? snap = null;
            if (acct[0] == 'd')
            {
                var owner = await _host.Debtors.OwnerOfAccountAsync(id, _life.Token);
                if (owner is null) return;
                var person = await _host.Debtors.LoadFullAsync(owner.Value, _life.Token);
                if (person is null) return;
                var a = person.AllAccounts().FirstOrDefault(x => x.Id == id);
                if (a is null) return;
                snap = AcctSnapshots.ForDebtAccount(person.Name, a.IsMain ? null : a.Name, a, _host.Debt);
                if (!ReferenceEquals(Current, th)) return;
                InfoPhone = person.Phone ?? "";
                foreach (var other in person.AllAccounts().Where(x => x.Id > 0))
                    InfoAccounts.Add(new ChatInfoLine(
                        other.IsMain ? "حسابِ اصلی" : (other.Name ?? "حسابِ فرعی"),
                        other.Id == id ? "همین کیو‌آر" : (other.Mode.IsMoney() ? "واحدِ پول" : "واحدِ تیل"),
                        other.Id == id ? "Pump.Accent" : "Pump.Muted"));
            }
            else if (acct[0] == 'c')
            {
                var co = await _host.Companies.LoadAsync(id, _life.Token);
                if (co is null) return;
                snap = AcctSnapshots.ForCompany(co, _host.Company);
            }
            if (snap is null || !ReferenceEquals(Current, th)) return;
            foreach (var b in snap.Books)
                InfoBooks.Add(new ChatInfoBook(b.Title + " (" + b.Unit + ")",
                    b.Summary.Where(s => s.Length >= 2)
                             .Select(s => new ChatInfoLine(s[0], s[1], s[0].Contains("الباقی") ? "Pump.Accent" : "Pump.Text"))
                             .ToList()));
        }
        catch { /* دفتر در دسترس نبود — مشخصات خالی می‌ماند */ }
        finally
        {
            OnPropertyChanged(nameof(HasInfoBooks));
            OnPropertyChanged(nameof(HasInfoAccounts));
        }
    }

    // ══ نامِ حسابِ هر کیو‌آر ═════════════════════════════════════════════════════

    private readonly Dictionary<string, string> _acctNames = new();
    private readonly HashSet<string> _naming = new();

    private string TitleOf(string acct, string who)
    {
        if (_acctNames.TryGetValue(acct, out var n) && n.Length > 0) return n;
        return who.Length > 0 ? who : "مشتری · " + acct;
    }

    private void WantName(string? acct)
    {
        if (string.IsNullOrEmpty(acct)) return;
        if (_acctNames.ContainsKey(acct) || !_naming.Add(acct)) return;
        _ = ResolveNameAsync(acct);
    }

    /// <summary>‎d12‎ ⇒ نامِ حسابِ قرض‌دار · ‎c3‎ ⇒ نامِ شرکت.</summary>
    private async Task ResolveNameAsync(string acct)
    {
        try
        {
            var label = "";
            if (acct.Length > 1 && long.TryParse(acct[1..], out var id) && id > 0)
            {
                if (acct[0] == 'd') label = await _host.Debtors.AccountLabelAsync(id, _life.Token);
                else if (acct[0] == 'c') label = (await _host.Companies.LoadAsync(id, _life.Token))?.Name ?? "";
            }
            if (string.IsNullOrWhiteSpace(label)) { _naming.Remove(acct); return; }
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _acctNames[acct] = label.Trim();
                foreach (var th in Threads.Where(x => x.Acct == acct)) th.Title = label.Trim();
                if (Current?.Acct == acct) InfoTitle = label.Trim();
                RebuildShown();
            });
        }
        catch { _naming.Remove(acct); }
    }

    // ══ کمکی ═════════════════════════════════════════════════════════════════════

    private static string Clip(string s) => s.Length > 60 ? s[..60] + "…" : s;

    private static string Preview(CloudChatMessage m) => m.Deleted ? "🚫 پاک شد" : PreviewOf(m.Kind, m.Text);

    private static string PreviewOf(string kind, string text) => kind switch
    {
        "image" => "📷 عکس", "video" => "🎥 ویدیو", "audio" => "🎤 پیامِ صوتی", _ => text,
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

    /// <summary>
    /// ⛔ شناسهٔ رسانه از سرور (و از پیامِ مشتری) می‌آید و در نامِ فایل و
    /// مسیرِ درخواست می‌نشیند؛ پس فقط حرف و رقم و «_» و «-».
    /// </summary>
    public static bool SafeMediaId(string? id) => ChatStore.SafeId(id);
}
