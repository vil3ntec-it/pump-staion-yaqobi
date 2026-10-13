using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ۳۲بیتی: کارهایی که در فضای ۲ گیگی «کمبودِ حافظه» می‌شدند (۱۴۰۵/۰۷/۲۱) ══════
/// صاحب ریپو: «روی کامپیوترِ ۳۲ بیت هم تست کن.» رفتارِ واقعی را ‎x86-check.yml‎ در
/// فرآیندِ ۳۲بیتی می‌سنجد؛ این‌جا قفلِ سورس تا ریشه‌ها برنگردند.
/// </summary>
public class X86MemoryTests
{
    private static string Root()
    {
        var d = AppContext.BaseDirectory;
        while (d is not null && !File.Exists(Path.Combine(d, "PumpYaqobi.sln"))) d = Path.GetDirectoryName(d);
        return d!;
    }

    private static string Src(params string[] rel) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(rel).ToArray()));

    /// <summary>پشتیبانِ رمزشده: سقفِ کوچک‌تر در ۳۲بیتی و بازکردن بی کپیِ دومِ کلِ فایل.</summary>
    [Fact]
    public void Poshtiban_Dar32Bit_JaMishavad()
    {
        var s = Src("PumpYaqobi.Services", "Data", "SyncBackup.cs");
        Assert.Contains("Environment.Is64BitProcess ? 256L * 1024 * 1024 : 64L * 1024 * 1024", s);
        Assert.DoesNotContain("raw.AsSpan(Magic.Length + 28).ToArray()", s);
        Assert.Contains("aes.Decrypt(raw.AsSpan(Magic.Length, 12), cipher, raw.AsSpan(Magic.Length + 12, 16), plain);", s);
    }

    /// <summary>چاپ: ورقِ ساخته‌نشده بی‌صدا جا نمی‌افتد و کپیِ اضافه نیست؛ ۳۲بیتی تا ۳۰۰ dpi.</summary>
    [Fact]
    public void Chap_VaraghBiSedaJaNemiOftad()
    {
        var p = Src("PumpYaqobi.App", "Printing", "Printers.cs");
        Assert.DoesNotContain("if (decoded is null) continue;", p);
        Assert.DoesNotContain("if (bgra is null) continue;", p);
        Assert.DoesNotContain(": decoded.Copy();", p);
        Assert.Contains("src.ColorType == SKColorType.Bgra8888 ? null : src.Copy(SKColorType.Bgra8888)", p);
        var d = Src("PumpYaqobi.App", "Printing", "DocumentPreview.cs");
        Assert.DoesNotContain("Math.Clamp(Setup.Dpi, 72, 400)", d);
    }

    /// <summary>پیوستِ گفت‌وگو: اندازه پیش از خواندنِ کلِ فایل.</summary>
    [Fact]
    public void Peyvast_AndazeAvalSanjideMishavad()
    {
        var c = Src("PumpYaqobi.App", "ViewModels", "Sections", "ChatSectionViewModel.cs");
        var at = c.IndexOf("if (size > MaxAttachBytes)", StringComparison.Ordinal);
        var read = c.IndexOf("File.ReadAllBytesAsync(path, _life.Token)", StringComparison.Ordinal);
        Assert.True(at > 0 && read > at, "اندازه باید پیش از ReadAllBytesAsync سنجیده شود");
        Assert.Contains("Environment.Is64BitProcess ? 200L * 1024 * 1024 : 50L * 1024 * 1024", c);
    }
}
