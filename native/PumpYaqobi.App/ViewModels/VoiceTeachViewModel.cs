using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels;

/// <summary>یک حساب در فهرستِ «آموزش صدا» — یک نام و صداهای ثبت‌شده‌اش.</summary>
public sealed partial class VoiceTeachRowViewModel : ObservableObject
{
    private readonly VoiceTeachViewModel _owner;

    public VoiceTeachRowViewModel(VoiceTeachViewModel owner, string key, string name, int count)
    { _owner = owner; Key = key; Name = name; _count = count; }

    public string Key { get; }
    public string Name { get; }

    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _recording;

    /// <summary>«✅ ۲ صدا» یا «ثبت نشده» — همان نوشتهٔ ‎vxRenderTeach‎.</summary>
    public string CountText => Count > 0 ? "✅ " + Shamsi.Money(Count) + " صدا" : "ثبت نشده";

    public bool HasVoice => Count > 0;

    partial void OnCountChanged(int value)
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasVoice));
    }

    /// <summary>«🎤» یا «⏺» وقتی در حالِ ضبط است.</summary>
    public string MicText => Recording ? "⏺" : "🎤";

    partial void OnRecordingChanged(bool value) => OnPropertyChanged(nameof(MicText));

    [RelayCommand]
    private Task Teach() => _owner.TeachAsync(this);

    [RelayCommand]
    private Task Forget() => _owner.ForgetAsync(this);
}

/// <summary>
/// ══ 🎓 آموزش صدا — بندِ ۲۴ ══════════════════════════════════════════════════
/// رونوشتِ ‎vxOpenTeach‎ · ‎vxRenderTeach‎ · ‎vxTeachOne‎ · ‎vxForgetOne‎ ·
/// ‎_vxRows‎.
///
/// تا حالا نیتیو فقط «یادگیری از زدنِ نام» را داشت: باید یک‌بار صدا می‌گفتید،
/// نشناخته می‌ماند، بعد خودتان روی نام می‌زدید تا یاد بگیرد. برای صد قرض‌دار
/// یعنی صد بارِ نشناختن. این پنجره همان کار را **یکجا** می‌کند: فهرستِ همهٔ
/// حساب‌ها، جلوی هرکدام یک دکمهٔ 🎤.
///
/// ⚠️ زیرحساب‌ها هم ردیفِ خودشان را دارند. صدای «موترِ دومِ کریم» باید همان
/// زیرحساب را باز کند، نه حسابِ اصلی را — همان کاری که ‎_vxRows‎ در نسخهٔ وب
/// می‌کرد و در نیتیو نبود.
/// </summary>
public sealed partial class VoiceTeachViewModel : ObservableObject
{
    private readonly AppHost _host;
    private List<VoiceTeachRowViewModel> _all = new();

    public VoiceTeachViewModel(AppHost host) => _host = host;

    public ObservableCollection<VoiceTeachRowViewModel> Rows { get; } = new();

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _busy;

    public bool IsEmpty => Rows.Count == 0;

    partial void OnSearchChanged(string value) => ApplyFilter();

    /// <summary>‎_vxRows()‎ — هر شخص و هر زیرحسابش، یک ردیف.</summary>
    public async Task LoadAsync()
    {
        var built = new List<VoiceTeachRowViewModel>();

        foreach (var noInvoice in new[] { false, true })
        {
            var people = await _host.Debtors.ListAsync(noInvoice);
            var accounts = await _host.Debtors.AccountsByDebtorAsync(noInvoice);

            foreach (var p in people)
            {
                var name = string.IsNullOrWhiteSpace(p.Name) ? "بی‌نام" : p.Name!;
                built.Add(new VoiceTeachRowViewModel(this, VoiceKeys.Of(p.Id), name,
                    await _host.Voice.CountAsync(VoiceKeys.Of(p.Id))));

                if (!accounts.TryGetValue(p.Id, out var list)) continue;
                foreach (var a in list)
                {
                    // حسابِ اصلی همان ردیفِ بالاست؛ فقط زیرحساب‌ها ردیفِ جدا دارند.
                    if (a.MainOfDebtorId is not null) continue;
                    var key = VoiceKeys.Of(p.Id, a.Id);
                    built.Add(new VoiceTeachRowViewModel(this, key,
                        name + " — " + (string.IsNullOrWhiteSpace(a.Name) ? "حساب فرعی" : a.Name!),
                        await _host.Voice.CountAsync(key)));
                }
            }
        }

        _all = built;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = Search.Trim();
        Rows.Clear();
        foreach (var r in _all)
            if (q.Length == 0 || r.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                Rows.Add(r);
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>‎vxTeachOne‎ — یک‌بار نام را بگویید، همین‌جا ثبت می‌شود.</summary>
    public async Task TeachAsync(VoiceTeachRowViewModel row)
    {
        if (Busy) return;
        Busy = true;
        row.Recording = true;
        Status = "🎤 نام «" + row.Name + "» را بگویید…";
        try
        {
            var pcm = await new MicRecorder().RecordAsync();
            if (pcm is null)
            {
                Status = "🎤 مایکروفون باز نشد — دسترسیِ ویندوز را بررسی کنید";
                return;
            }

            var feat = VoiceEngine.Features(pcm);
            if (feat is null)
            {
                Status = "🎤 چیزی شنیده نشد — دوباره و بلندتر بگویید";
                return;
            }

            await _host.Voice.EnrollAsync(row.Key, row.Name, feat);
            row.Count = await _host.Voice.CountAsync(row.Key);
            Status = "✅ صدای «" + row.Name + "» ثبت شد";
        }
        finally
        {
            row.Recording = false;
            Busy = false;
        }
    }

    /// <summary>‎vxForgetOne‎ — همهٔ صداهای این نام.</summary>
    public async Task ForgetAsync(VoiceTeachRowViewModel row)
    {
        await _host.Voice.ForgetAsync(row.Key);
        row.Count = 0;
        Status = "🗑️ صداهای «" + row.Name + "» پاک شد";
    }
}
