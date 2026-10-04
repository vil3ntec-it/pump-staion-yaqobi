using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، پ۲ — یکپارچگیِ فایل‌های برنامه ═══════════════════════════════════
/// امضا مو‌به‌مو همان کارِ ‎installer/integrity-sign.ps1‎ است (نام⇥هش، مرتبِ
/// ترتیبی، ECDSA P-256 / SHA-256). هر دست‌کاری ⇒ ‎Tampered‎ ⇒ شش بخشِ اشتراکی
/// بسته؛ پشتیبانی و دفتر هرگز. ⚠️ ‎Integrity.TestResult‎ سراسری است، پس این
/// کلاس تنها می‌دود.
/// </summary>
[Collection(nameof(IntegrityTests))]
public class IntegrityTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pump-int-").FullName;
    private readonly ECDsa _ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private string Key => Convert.ToBase64String(_ec.ExportSubjectPublicKeyInfo());

    public void Dispose()
    {
        Integrity.TestResult = null;
        Entitlements.Unlocked = Entitlements.UnlockedForNow;
        try { Directory.Delete(_dir, true); } catch { }
    }

    private void Put(string name, string body) => File.WriteAllText(Path.Combine(_dir, name), body);

    private void Sign()
    {
        var files = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(_dir))
        {
            var n = Path.GetFileName(f);
            if (Integrity.Covered(n)) files[n] = Integrity.HashOf(f);
        }
        var sig = _ec.SignData(Encoding.UTF8.GetBytes(Integrity.Canonical(files)), HashAlgorithmName.SHA256);
        Put(Integrity.ManifestName, JsonSerializer.Serialize(new { v = 1, files, sig = Convert.ToBase64String(sig) }));
    }

    private void App()
    {
        Put("PumpYaqobi.App.dll", "app");
        Put("PumpYaqobi.Services.dll", "services");
        Put("PumpYaqobi.deps.json", "{}");
        Put("PumpYaqobi.exe", "exe");
    }

    [Fact]
    public void Salem_Ok_Va_BiKelid_HichSanjeshiNist()
    {
        App(); Sign();
        Assert.Equal(IntegrityState.Ok, Integrity.Verify(_dir, Key).State);
        Assert.Equal(IntegrityState.NotSigned, Integrity.Verify(_dir, "").State);
        //  فایلِ اضافه (باقی‌ماندهٔ نسخهٔ قدیمی، آیکونِ نصاب) مشتری را نمی‌بندد
        Put("PumpYaqobi.Old.dll", "old");
        Put("PumpYaqobi.ico", "ico");
        Assert.Equal(IntegrityState.Ok, Integrity.Verify(_dir, Key).State);
    }

    [Theory]
    [InlineData("tamper")]
    [InlineData("missing")]
    [InlineData("manifest")]
    [InlineData("sig")]
    [InlineData("otherkey")]
    [InlineData("deps")]
    [InlineData("empty")]
    public void HarDastkari_Tampered(string how)
    {
        App(); Sign();
        var key = Key;
        switch (how)
        {
            case "tamper": File.AppendAllText(Path.Combine(_dir, "PumpYaqobi.Services.dll"), "\0"); break;
            case "missing": File.Delete(Path.Combine(_dir, "PumpYaqobi.App.dll")); break;
            case "manifest": File.Delete(Path.Combine(_dir, Integrity.ManifestName)); break;
            case "deps": Put("PumpYaqobi.deps.json", "{\"x\":1}"); break;
            case "sig":
                var m = SrcText.Read(Path.Combine(_dir, Integrity.ManifestName));
                Put(Integrity.ManifestName, m.Replace("\"services\"", "\"x\""));
                //  هشِ تازهٔ فایلِ عوض‌شده در فهرست — بی امضای تازه
                var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(m)!;
                var files = doc["files"].Deserialize<Dictionary<string, string>>()!;
                File.AppendAllText(Path.Combine(_dir, "PumpYaqobi.App.dll"), "x");
                files["PumpYaqobi.App.dll"] = Integrity.HashOf(Path.Combine(_dir, "PumpYaqobi.App.dll"));
                Put(Integrity.ManifestName, JsonSerializer.Serialize(new { v = 1, files, sig = doc["sig"].GetString() }));
                break;
            case "otherkey":
                using (var other = ECDsa.Create(ECCurve.NamedCurves.nistP256))
                    key = Convert.ToBase64String(other.ExportSubjectPublicKeyInfo());
                break;
            case "empty":
                var sig0 = _ec.SignData(Encoding.UTF8.GetBytes(""), HashAlgorithmName.SHA256);
                Put(Integrity.ManifestName, JsonSerializer.Serialize(new { v = 1, files = new { }, sig = Convert.ToBase64String(sig0) }));
                break;
        }
        var r = Integrity.Verify(_dir, key);
        Assert.True(r.Tampered, how + ": " + r.State);
        Assert.False(string.IsNullOrEmpty(r.Why));
    }

    [Fact]
    public void Dastkhorde_ShishBakhsh_Baste_DaftarVaPoshtibani_Baz()
    {
        //  ⛔ حتی با «همه باز» (قفلِ موقت) فایلِ دست‌خورده شش بخش را نمی‌گشاید
        Entitlements.Unlocked = true;
        Assert.True(Entitlements.Allows(Entitlements.Dashboard));
        Integrity.TestResult = new IntegrityResult(IntegrityState.Tampered, "دست خورده: PumpYaqobi.Services.dll");
        foreach (var f in Entitlements.Paid)
        {
            Assert.False(Entitlements.Allows(f), f);
            Assert.Contains("دست خورده", Entitlements.Why(f));
        }
        Assert.True(Entitlements.Allows(Entitlements.Support));
        Assert.True(Entitlements.Allows("ledger"));
        Integrity.TestResult = new IntegrityResult(IntegrityState.Ok, "");
        Assert.True(Entitlements.Allows(Entitlements.Dashboard));
    }
}

[CollectionDefinition(nameof(IntegrityTests), DisableParallelization = true)]
public class IntegrityTestsCollection { }
