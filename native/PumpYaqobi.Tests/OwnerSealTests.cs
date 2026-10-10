using System.Text.Json;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Security;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ⛔ «حساب‌ها»ی گوشی فقط با رمزِ برنامه (شورای آمادگیِ عرضه، ۱۴۰۵/۰۷/۲۳):
/// عکسی که به سرورِ خانگی و سرورِ حساب می‌رود دفترِ مالی را خام ندارد و
/// هشِ رمزِ مدیر را هم نه. سمتِ گوشی: ‎tools/check-kar-seal.mjs‎ با همان بردار.
/// </summary>
public class OwnerSealTests
{
    //  بردارِ مستقل (پایتون، کتابخانهٔ cryptography) — همان که آزمونِ گوشی هم باز می‌کند
    private const string VecGate = "pbkdf2$sha256$1000$AAECAwQFBgcICQoLDA0ODw==$9kbGdxNIWtcaudfqvWxuNeyCRN/oP4fBgleqMnWKmLs=";
    private const string VecIv = "BwcHBwcHBwcHBwcH";
    private const string VecCt = "HnBsr7v+hoHuc/3mWsltjg1igb+iB0cYsNWoK/noOXUN3rD5CRZkEEbFq4bi+eMTw4gIcf6DP3hjRXk78kpdsxzVF/FUO3XstTvSrkAe+iv9CDxcvUlJ94tXuUKvEc2h";

    private static Dictionary<string, object?> Snap(string gate) => new()
    {
        ["v"] = 1,
        ["gate"] = gate,
        ["debtors"] = new List<object?> { new Dictionary<string, object?> { ["name"] = "کریم" } },
        ["tank"] = new Dictionary<string, object?> { ["petrol"] = 10 },
        ["alerts"] = new List<object?>(),
        ["sections"] = new Dictionary<string, object?> { ["safe"] = new Dictionary<string, object?> { ["t"] = "گاوصندوق" } },
        ["banner"] = new List<object?> { new List<object?> { "مفاد", "120", "x" } },
    };

    [Fact]
    public void DaftareMali_KhamBiroonNemiravad_VaHashRamzHam()
    {
        var hash = PasswordHasher.Hash("رمزِ مدیر");
        var snap = Snap(hash);
        var phone = OwnerSeal.ForPhones(snap, hash);
        //  بی گریزِ یونیکد، وگرنه «نبودنِ گاوصندوق» همیشه راست بود
        var json = JsonSerializer.Serialize(phone, new JsonSerializerOptions
        { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains("کریم", json);   // همان سنجه نوشتهٔ فارسیِ خام را می‌بیند

        Assert.False(phone.ContainsKey("sections"));
        Assert.False(phone.ContainsKey("banner"));
        Assert.DoesNotContain("گاوصندوق", json);
        Assert.DoesNotContain(hash.Split('$')[4], json);
        Assert.Equal(4, ((string)phone["gate"]!).Split('$').Length);
        //  کارمند همچنان قرض‌دار و مخزن و هشدار را دارد
        Assert.True(phone.ContainsKey("debtors") && phone.ContainsKey("tank") && phone.ContainsKey("alerts"));
        //  ورودی دست نخورد (هشِ «چیزی عوض شد؟» از روی همان است)
        Assert.True(snap.ContainsKey("sections") && snap.ContainsKey("banner") && (string)snap["gate"]! == hash);
    }

    [Fact]
    public void BaRamzeDorost_BazMishavad_BaRamzeGhalat_Na()
    {
        var hash = PasswordHasher.Hash("رمزِ مدیر");
        var phone = OwnerSeal.ForPhones(Snap(hash), hash);
        var seal = (Dictionary<string, object?>)phone["seal"]!;
        var iv = (string)seal["iv"]!; var ct = (string)seal["ct"]!;

        Assert.True(OwnerSeal.TryParse(hash, out _, out var dk));
        var plain = OwnerSeal.Open(dk, iv, ct);
        Assert.NotNull(plain);
        using (var doc = JsonDocument.Parse(plain!))
        {
            Assert.Equal("گاوصندوق", doc.RootElement.GetProperty("sections").GetProperty("safe").GetProperty("t").GetString());
            Assert.Equal("مفاد", doc.RootElement.GetProperty("banner")[0][0].GetString());
        }

        Assert.True(OwnerSeal.TryParse(PasswordHasher.Hash("رمزِ دیگر"), out _, out var wrong));
        Assert.Null(OwnerSeal.Open(wrong, iv, ct));
    }

    [Fact]
    public void BiRamzeBarname_BakhsheMali_AslanNemiravad()
    {
        foreach (var none in new[] { null, "", "متنِ ساده" })
        {
            var phone = OwnerSeal.ForPhones(Snap(none ?? ""), none);
            Assert.False(phone.ContainsKey("sections"));
            Assert.False(phone.ContainsKey("banner"));
            Assert.False(phone.ContainsKey("seal"));
            Assert.False(phone.ContainsKey("gate"));
        }
    }

    [Fact]
    public void BordareMostaghel_BaHamanAlgorithm_BazMishavad()
    {
        Assert.True(OwnerSeal.TryParse(VecGate, out var kdf, out var dk));
        Assert.Equal("pbkdf2$sha256$1000$AAECAwQFBgcICQoLDA0ODw==", kdf);
        var plain = OwnerSeal.Open(dk, VecIv, VecCt);
        Assert.NotNull(plain);
        using var doc = JsonDocument.Parse(plain!);
        Assert.Equal("گاوصندوق", doc.RootElement.GetProperty("sections").GetProperty("safe").GetProperty("t").GetString());
    }

    [Fact]
    public void HarDoDarePublisher_MohrShodeMifrestand()
    {
        var src = File.ReadAllText(Path.Combine(Native(), "PumpYaqobi.Shell", "Services", "StationPublisher.cs"));
        Assert.Contains("_sync.SetAsync(path, OwnerSeal.ForPhones(snap,", src);
        Assert.Contains("OwnerSeal.ForPhones(StationSnapshot.ForCloud(snap),", src);
        Assert.DoesNotContain("_sync.SetAsync(path, snap, ct)", src);
    }

    private static string Native()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        return d!.FullName;
    }
}
