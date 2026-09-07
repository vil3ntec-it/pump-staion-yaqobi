using PumpYaqobi.Application.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// جدا کردنِ فریم‌های MJPEG — همان چیزی که تصویرِ زندهٔ دوربین از آن می‌آید.
/// جریانِ واقعی تکه‌تکه و بی‌نظم می‌رسد، پس آزمون هم تکه‌تکه می‌دهد.
/// </summary>
public class MjpegSplitterTests
{
    private static byte[] Jpeg(int payload)
    {
        var body = new byte[payload];
        for (var i = 0; i < payload; i++) body[i] = (byte)(i % 251);
        // ⚠️ هیچ ‎FF D9‎ی زودرسی داخلِ بدنه نباشد، وگرنه آزمون خودش را گول می‌زند.
        for (var i = 1; i < payload; i++) if (body[i - 1] == 0xFF && body[i] == 0xD9) body[i] = 0x00;
        var all = new byte[payload + 4];
        all[0] = 0xFF; all[1] = 0xD8;
        Array.Copy(body, 0, all, 2, payload);
        all[^2] = 0xFF; all[^1] = 0xD9;
        return all;
    }

    private static List<byte[]> FeedAll(MjpegSplitter s, byte[] data, int chunk)
    {
        var got = new List<byte[]>();
        for (var i = 0; i < data.Length; i += chunk)
        {
            var n = Math.Min(chunk, data.Length - i);
            var buf = new byte[n];
            Array.Copy(data, i, buf, 0, n);
            got.AddRange(s.Feed(buf, n));
        }
        return got;
    }

    [Fact]
    public void OneFrame_ComesOutWhole_HoweverItIsChopped()
    {
        var frame = Jpeg(500);
        foreach (var chunk in new[] { 1, 7, 64, 4096 })
        {
            var got = FeedAll(new MjpegSplitter(), frame, chunk);
            Assert.Single(got);
            Assert.Equal(frame, got[0]);
        }
    }

    /// <summary>
    /// جریانِ واقعی: هدرِ ‎--boundary‎ بینِ فریم‌ها. جداکننده عمداً هدر را
    /// نمی‌خواند و فقط نشانه‌های خودِ JPEG را دنبال می‌کند.
    /// </summary>
    [Fact]
    public void HeadersBetweenFrames_AreThrownAway()
    {
        var a = Jpeg(300);
        var b = Jpeg(400);
        var header = System.Text.Encoding.ASCII.GetBytes(
            "\r\n--myboundary\r\nContent-Type: image/jpeg\r\nContent-Length: 400\r\n\r\n");

        var stream = new List<byte>();
        stream.AddRange(System.Text.Encoding.ASCII.GetBytes("--myboundary\r\n\r\n"));
        stream.AddRange(a);
        stream.AddRange(header);
        stream.AddRange(b);

        var got = FeedAll(new MjpegSplitter(), stream.ToArray(), 33);
        Assert.Equal(2, got.Count);
        Assert.Equal(a, got[0]);
        Assert.Equal(b, got[1]);
    }

    /// <summary>
    /// لینکی که اصلاً MJPEG نیست (مثلاً صفحهٔ HTML) هیچ‌وقت ‎FF D9‎ نمی‌دهد.
    /// بی سقفِ اندازه، بافر تا تمام شدنِ حافظه بزرگ می‌شد.
    /// </summary>
    [Fact]
    public void GarbageStream_NeverGrowsWithoutBound()
    {
        var s = new MjpegSplitter();
        var junk = new byte[64 * 1024];
        junk[0] = 0xFF; junk[1] = 0xD8;                  // آغازِ دروغین، بی پایان
        for (var i = 2; i < junk.Length; i++) junk[i] = 0x41;

        for (var round = 0; round < 300; round++)        // ~۱۹ مگابایت
            Assert.Empty(s.Feed(junk, junk.Length));

        Assert.True(s.Buffered <= MjpegSplitter.MaxFrameBytes,
                    "بافر از سقف گذشت: " + s.Buffered);
    }

    [Fact]
    public void Reset_DropsTheHalfFrame()
    {
        var s = new MjpegSplitter();
        var half = Jpeg(200)[..100];
        Assert.Empty(s.Feed(half, half.Length));
        Assert.True(s.Buffered > 0);
        s.Reset();
        Assert.Equal(0, s.Buffered);
    }
}
