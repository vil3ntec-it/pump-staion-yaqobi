using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ چتِ پشتیبانی — حالتِ بی‌صفحه ═══════════════════════════════════════════
/// «اسمش برای من دیده بشه، پیام‌ها پاک بشن، بلاک، و نخوانده‌ها» — همه از
/// ‎SupportState‎ می‌آیند، پس همین‌جا قفل می‌شوند.
/// </summary>
public class SupportChatTests
{
    private static CloudChatMessage M(string id, long seq, string acct, string from, string text,
                                      string name = "", string kind = "text", string? media = null, bool deleted = false)
        => new(id, seq, acct, from, name, kind, text, media, 1700000000000 + seq, deleted);

    [Fact]
    public void FreshCustomerMessagesAreReportedOnceAndNamedAfterTheCustomer()
    {
        var s = new SupportState();
        var fresh = s.Merge(new[]
        {
            M("m1", 1, "d7", "c", "سلام", "هارون"),
            M("m2", 2, "d7", "o", "بفرمایید", "پمپ"),
            M("m3", 3, "c9", "c", "قیمت؟", "شرکتِ الف"),
        });
        Assert.Equal(new[] { "m1", "m3" }, fresh.Select(m => m.Id));
        Assert.Equal(3, s.LastSeq);
        Assert.Equal("هارون", s.Get("d7").Name);
        Assert.Equal(1, s.Get("d7").Unread);          // فقط پیامِ مشتری، نه جوابِ خودم
        Assert.Equal(2, s.TotalUnread);

        // همان پیام دوباره (صندوق بعد از جوابِ فرستادن) ⇒ تازه نیست
        Assert.Empty(s.Merge(new[] { M("m1", 1, "d7", "c", "سلام", "هارون") }));
        Assert.Equal(2, s.Get("d7").Messages.Count);
    }

    [Fact]
    public void SeenClearsUnreadAndDeletedKeepsItsPlace()
    {
        var s = new SupportState();
        s.Merge(new[] { M("m1", 1, "d7", "c", "سلام"), M("m2", 2, "d7", "c", "هستید؟") });
        Assert.Equal(2, s.Get("d7").Unread);
        s.Seen("d7");
        Assert.Equal(0, s.Get("d7").Unread);

        s.Merge(new[] { M("m3", 3, "d7", "c", "اشتباه") });
        Assert.Equal(1, s.Get("d7").Unread);
        s.MarkDeleted("m3");
        var t = s.Get("d7");
        Assert.Equal(3, t.Messages.Count);
        Assert.True(t.Messages[^1].Deleted);
        Assert.Equal("", t.Messages[^1].Text);
        Assert.Equal(0, t.Unread);                    // پاک‌شده شمرده نمی‌شود
    }

    [Fact]
    public void ThreadListFromTheCloudSetsNameBlockAndSeen()
    {
        var s = new SupportState();
        s.Merge(new[] { M("m1", 5, "d7", "c", "x", "هارون") });
        s.ApplyThreads(new[]
        {
            new CloudChatThread("d7", "محمد هارون", Blocked: true, Unread: 0, UpdatedAt: 1, Last: null),
            new CloudChatThread("c2", "شرکت", Blocked: false, Unread: 1, UpdatedAt: 1,
                                Last: M("m9", 9, "c2", "c", "سلام", "شرکت")),
        });
        Assert.True(s.Get("d7").Blocked);
        Assert.Equal("محمد هارون", s.Get("d7").Name);
        Assert.Equal(0, s.Get("d7").Unread);          // ابر گفت خوانده شده
        Assert.Equal(1, s.Get("c2").Unread);
        Assert.Equal(9, s.LastSeq);
    }

    [Fact]
    public void MessagesAreOrderedBySeqNotArrival()
    {
        var s = new SupportState();
        s.Merge(new[] { M("b", 2, "d7", "o", "دوم"), M("a", 1, "d7", "c", "اول") });
        Assert.Equal(new[] { "a", "b" }, s.Get("d7").Messages.Select(m => m.Id));
    }

    [Fact]
    public void CloudJsonIsReadTheWayTheServerWritesIt()
    {
        var json = JsonDocument.Parse("""
            {"messages":[{"id":"msg1","seq":4,"acct":"d7","from":"c","name":"هارون","kind":"image",
                          "text":"","mediaId":"med1","at":1700000000000,"deleted":false},
                         {"id":"msg2","seq":5,"from":"o","kind":"text","text":"باشد","at":1,"deleted":true}]}
            """).RootElement;
        var list = CloudChatMessage.ParseList(json, "d7");
        Assert.Equal(2, list.Count);
        Assert.Equal("image", list[0].Kind);
        Assert.Equal("med1", list[0].MediaId);
        Assert.True(list[0].FromCustomer);
        Assert.Equal("d7", list[1].Acct);            // بی acct ⇒ همان گفت‌وگو
        Assert.True(list[1].Deleted);
        Assert.Null(list[1].MediaId);
    }

    [Fact]
    public void TheVoiceNoteIsARealWavFile()
    {
        var pcm = new byte[3200];
        var wav = WaveRecorder.Wav(pcm);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(16000, BitConverter.ToInt32(wav, 24));       // نرخ
        Assert.Equal(pcm.Length, BitConverter.ToInt32(wav, 40)); // اندازهٔ داده
        Assert.Equal(44 + pcm.Length, wav.Length);
    }
}
