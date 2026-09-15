namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ حالتِ چتِ پشتیبانی — بی صفحه، بی شبکه ═══════════════════════════════════
///
/// تنها جایی که تصمیم می‌گیرد «این پیام تازه است؟ مالِ کدام گفت‌وگو؟ چند
/// نخوانده داریم؟». ویومدل فقط همین را نشان می‌دهد و ابر فقط همین را
/// پر می‌کند — پس همین یک کلاس جدا آزموده می‌شود (‎SupportChatTests‎).
///
/// ⚠️ ترتیب و یکتاییِ پیام با ‎Seq‎ی ابر است، نه با زمان: دو پیامِ هم‌زمان،
/// یا پیامی که دو بار می‌رسد (یک‌بار جوابِ فرستادن، یک‌بار صندوق) نباید
/// دو تا بشوند.
/// </summary>
public sealed class SupportState
{
    /// <summary>یک گفت‌وگو — به شناسهٔ حساب (‎d12‎، ‎c9‎).</summary>
    public sealed class Thread
    {
        public Thread(string acct) { Acct = acct; }
        public string Acct { get; }
        public string Name { get; set; } = "";
        public bool Blocked { get; set; }
        public long OwnerSeenSeq { get; set; }
        public List<CloudChatMessage> Messages { get; } = new();

        /// <summary>پیام‌های مشتری که بعد از آخرین «خوانده شد» آمده‌اند و پاک نشده‌اند.</summary>
        public int Unread => Messages.Count(m => m.FromCustomer && !m.Deleted && m.Seq > OwnerSeenSeq);

        /// <summary>بلندترین ‎Seq‎ی این گفت‌وگو — برای «خوانده شد».</summary>
        public long LastSeq => Messages.Count == 0 ? 0 : Messages[^1].Seq;
    }

    private readonly Dictionary<string, Thread> _threads = new(StringComparer.Ordinal);

    /// <summary>بلندترین ‎Seq‎ی دیده‌شده — صندوق از این به بعد پرسیده می‌شود.</summary>
    public long LastSeq { get; private set; }

    public IReadOnlyCollection<Thread> Threads => _threads.Values;

    public Thread Get(string acct)
    {
        if (!_threads.TryGetValue(acct, out var t)) _threads[acct] = t = new Thread(acct);
        return t;
    }

    public bool TryGet(string acct, out Thread t) => _threads.TryGetValue(acct, out t!);

    /// <summary>فهرستِ گفت‌وگوها از ابر: نام، بلاک و «تا کجا خوانده‌ام».</summary>
    public void ApplyThreads(IEnumerable<CloudChatThread> list)
    {
        foreach (var c in list)
        {
            var t = Get(c.Acct);
            if (c.Name.Length > 0) t.Name = c.Name;
            t.Blocked = c.Blocked;
            if (c.Last is { } last) Merge(new[] { last });
            // نخوانده‌های ابر همان پیام‌های بعد از ‎owner_seen_seq‎اند؛ اگر ابر
            // می‌گوید صفر، یعنی تا آخرین پیام خوانده شده.
            if (c.Unread == 0 && t.LastSeq > t.OwnerSeenSeq) t.OwnerSeenSeq = t.LastSeq;
        }
    }

    /// <summary>
    /// پیام‌های تازه را در گفت‌وگوهایشان می‌نشاند. خروجی: فقط آن‌هایی که
    /// **واقعاً تازه** بودند و از مشتری آمده‌اند — برای خبر دادن.
    /// پیامِ تکراری با همان شناسه جایگزین می‌شود (مثلاً «پاک شد»).
    /// </summary>
    public List<CloudChatMessage> Merge(IEnumerable<CloudChatMessage> incoming)
    {
        var fresh = new List<CloudChatMessage>();
        foreach (var m in incoming)
        {
            if (m.Acct.Length == 0) continue;
            var t = Get(m.Acct);
            var i = t.Messages.FindIndex(x => x.Id == m.Id);
            if (i >= 0) t.Messages[i] = m;
            else
            {
                t.Messages.Add(m);
                if (m.FromCustomer && !m.Deleted) fresh.Add(m);
                if (m.FromCustomer && m.Name.Length > 0) t.Name = m.Name;
            }
            if (m.Seq > LastSeq) LastSeq = m.Seq;
        }
        foreach (var t in _threads.Values) t.Messages.Sort((a, b) => a.Seq.CompareTo(b.Seq));
        return fresh;
    }

    /// <summary>«تا این‌جا خواندم» — ‎Unread‎ی همان گفت‌وگو صفر می‌شود.</summary>
    public void Seen(string acct)
    {
        if (_threads.TryGetValue(acct, out var t)) t.OwnerSeenSeq = Math.Max(t.OwnerSeenSeq, t.LastSeq);
    }

    /// <summary>پاک‌شده‌ها متنشان می‌رود ولی جایشان می‌ماند.</summary>
    public void MarkDeleted(string id)
    {
        foreach (var t in _threads.Values)
        {
            var i = t.Messages.FindIndex(x => x.Id == id);
            if (i >= 0) t.Messages[i] = t.Messages[i] with { Deleted = true, Text = "", MediaId = null };
        }
    }

    public int TotalUnread => _threads.Values.Sum(t => t.Unread);
}
