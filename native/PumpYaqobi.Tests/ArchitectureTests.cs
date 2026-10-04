using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ج۵ — ویومدل بی DbContext (آزمونِ معماری با بازتاب) ═══════════════════
///
/// ⛔ هیچ نوعی زیرِ <c>PumpYaqobi.App.ViewModels</c> — با ماشین‌های حالتِ async و
/// لامبداهایش — به <c>PumpDbContext</c> یا <c>PumpDbFactory</c> دست نمی‌زند: نه
/// سازنده‌ای با آن‌ها، نه فیلدی، نه هیچ فراخوانیِ <c>Create()</c>. ویومدل فقط
/// سرویس‌های <c>AppHost</c> را صدا می‌زند.
///
/// ⚠️ این گشتنِ متن نیست: IL ِ هر متد خوانده و هر عضوِ صدا‌زده‌شده با
/// <see cref="Module.ResolveMember(int, Type[], Type[])"/> پیدا می‌شود — پس نامِ
/// کامل، <c>using</c>، <c>var</c> و نامِ مستعار هیچ‌کدام دورش نمی‌زنند.
/// </summary>
public class ArchitectureTests
{
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value);

    private static readonly string[] Forbidden =
    {
        "PumpYaqobi.Persistence.PumpDbContext",
        "PumpYaqobi.Services.Data.PumpDbFactory",
    };

    /// <summary>هر عضوی که IL ِ این متد به آن اشاره می‌کند.</summary>
    private static IEnumerable<MemberInfo> Touched(MethodBase m)
    {
        byte[]? il;
        try { il = m.GetMethodBody()?.GetILAsByteArray(); } catch { yield break; }
        if (il is null) yield break;
        Type[]? ta = m.DeclaringType is { IsGenericType: true } dt ? dt.GetGenericArguments() : null;
        Type[]? ma = m.IsGenericMethod ? m.GetGenericArguments() : null;
        var i = 0;
        while (i < il.Length)
        {
            short v = il[i] == 0xFE && i + 1 < il.Length ? (short)(0xFE00 | il[i + 1]) : il[i];
            if (!Codes.TryGetValue(v, out var op)) yield break;
            i += op.Size;
            int size = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4,
            };
            if (op.OperandType is OperandType.InlineMethod or OperandType.InlineField
                or OperandType.InlineType or OperandType.InlineTok)
            {
                MemberInfo? mi = null;
                try { mi = m.Module.ResolveMember(BitConverter.ToInt32(il, i), ta, ma); } catch { }
                if (mi is not null) yield return mi;
            }
            i += size;
        }
    }

    private static string? Hit(MemberInfo mi)
    {
        var t = mi as Type ?? mi.DeclaringType;
        if (mi is FieldInfo f) t = f.FieldType.FullName is { } ft && Forbidden.Contains(ft) ? f.FieldType : f.DeclaringType;
        if (mi is MethodInfo mm && Forbidden.Contains(mm.ReturnType.FullName)) return mm.ReturnType.FullName;
        return t?.FullName is { } n && Forbidden.Contains(n) ? n : null;
    }

    [Fact]
    public void Viewmodel_BeDbContext_DastNemizanad()
    {
        var app = typeof(PumpYaqobi.App.Services.AppHost).Assembly;
        var vms = app.GetTypes().Where(t => t.Namespace?.StartsWith("PumpYaqobi.App.ViewModels", StringComparison.Ordinal) == true).ToList();
        Assert.True(vms.Count > 50, "ویومدل‌ها پیدا نشدند — آزمون توخالی است");

        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                                 | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var bad = new List<string>();
        foreach (var t in vms)
        {
            foreach (var f in t.GetFields(All))
                if (Forbidden.Contains(f.FieldType.FullName)) bad.Add($"{t.FullName}.{f.Name} (فیلد)");
            foreach (var c in t.GetConstructors(All))
                if (c.GetParameters().Any(p => Forbidden.Contains(p.ParameterType.FullName)))
                    bad.Add($"{t.FullName} (سازنده)");
            foreach (var m in t.GetMethods(All).Cast<MethodBase>().Concat(t.GetConstructors(All)))
                foreach (var mi in Touched(m))
                    if (Hit(mi) is { } n) bad.Add($"{t.FullName}.{m.Name} ⇒ {n}");
        }
        Assert.True(bad.Count == 0, "ویومدل به دیتابیس دست زد:\n" + string.Join("\n", bad.Distinct()));
    }

    /// <summary>سنجه دندان دارد: همین اسکن، روی سرویسی که واقعاً دیتابیس می‌زند، پیدا می‌کند.</summary>
    [Fact]
    public void Eskan_DandanDarad()
    {
        var m = typeof(PumpYaqobi.Services.Data.LedgerParityService).GetMethod(nameof(PumpYaqobi.Services.Data.LedgerParityService.CheckDatesAsync))!;
        var sm = m.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()!.StateMachineType;
        var touched = sm.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SelectMany(Touched).ToList();
        Assert.Contains(touched, x => Hit(x) is not null);
    }
}
