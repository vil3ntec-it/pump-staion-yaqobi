using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ «📝 یادداشت این بخش» ═══════════════════════════════════════════════════
///
/// همتای ‎.sec-note-box‎ی نسخهٔ وب — کادری که در سایت پایینِ چهارده بخش هست و
/// در برنامهٔ نیتیو هیچ‌جا نبود. گزارشِ صاحب ریپو: «تو سایت بخش نوت هم داشت،
/// آن چه شد؟»
///
/// سه تکه، دقیقاً مثلِ سایت:
///   • کادرِ نوشتن (‎#secnote-&lt;key&gt;‎) که پیش‌نویسش با هر حرف ذخیره می‌شود
///     (‎saveNoteDraft‎) تا با بسته شدنِ برنامه نپرد؛
///   • «📨 فرستادن به صندوق نوت‌ها» (‎sendNoteToBox‎)؛
///   • «📋 لیست نوت‌ها ‎&lt;شمار&gt;‎» (‎openNotes‎) که باز و بسته می‌شود.
/// </summary>
public sealed partial class SectionNotesViewModel : ObservableObject
{
    private readonly SectionNoteService _svc;
    private readonly Action<string, bool>? _toast;

    /// <summary>کلیدِ بخش — همان کلیدی که سایت به کار می‌برد.</summary>
    public string Key { get; }

    public SectionNotesViewModel(string key, SectionNoteService svc,
                                 Action<string, bool>? toast = null)
    { Key = key; _svc = svc; _toast = toast; }

    public ObservableCollection<SectionNote> Items { get; } = new();

    /// <summary>
    /// اندازهٔ نوشتهٔ کادرهای یادداشت — مشترکِ همهٔ بخش‌ها، جدا از ‎A−/A+‎ِ
    /// خودِ بخش. همتای ‎adjNoteFont‎ی سایت؛ توضیحش آن‌جاست.
    /// </summary>
    public NoteFontViewModel Font => NoteFontViewModel.Instance;

    private bool _loading;

    /// <summary>متنِ داخلِ کادر. ⚠️ هر حرف ذخیره می‌شود، مثلِ ‎saveNoteDraft‎.</summary>
    [ObservableProperty] private string _draft = "";

    /// ⚠️ ‎_loading‎ لازم است: وقتی خودمان پیش‌نویسِ خوانده‌شده را روی ‎Draft‎
    /// می‌نشانیم، این هم صدا می‌خورد و همان چیزی را که تازه از دیتابیس آمده
    /// دوباره می‌نویسد.
    partial void OnDraftChanged(string v)
    {
        if (_loading) return;
        _ = _svc.SaveDraftAsync(Key, v);
    }

    [ObservableProperty] private bool _isListOpen;
    [ObservableProperty] private int _count;

    partial void OnIsListOpenChanged(bool v) => OnPropertyChanged(nameof(ListToggleText));
    partial void OnCountChanged(int v) => OnPropertyChanged(nameof(ListToggleText));

    public string ListToggleText =>
        "📋 لیست نوت‌ها" + (Count > 0 ? " (" + Shamsi.Money(Count) + ")" : "");

    /// <summary>پیش‌نویس و شمارِ نوت‌ها را از دیتابیس بردار.</summary>
    public async Task LoadAsync()
    {
        // ⚠️ بی این پرچم، نشستنِ پیش‌نویسِ خوانده‌شده روی ‎Draft‎ خودش یک
        // ذخیرهٔ بی‌مورد راه می‌انداخت و همان چیزی را که تازه خوانده بودیم
        // دوباره می‌نوشت.
        var d = await _svc.GetDraftAsync(Key);
        if (d != Draft) { _loading = true; Draft = d; _loading = false; }
        Count = await _svc.CountAsync(Key);
        if (IsListOpen) await RefreshListAsync();
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var n = await _svc.SendAsync(Key, Draft, Shamsi.Today());
        if (n is null) { _toast?.Invoke("اول یادداشت را بنویسید", false); return; }

        _loading = true; Draft = ""; _loading = false;
        Count = await _svc.CountAsync(Key);
        if (IsListOpen) await RefreshListAsync();
        _toast?.Invoke("📨 در صندوق نوت‌ها ثبت شد", true);
    }

    [RelayCommand]
    private async Task ToggleListAsync()
    {
        IsListOpen = !IsListOpen;
        if (IsListOpen) await RefreshListAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(SectionNote? n)
    {
        if (n is null) return;
        await _svc.DeleteAsync(n.Id);
        Items.Remove(n);
        Count = await _svc.CountAsync(Key);
    }

    private async Task RefreshListAsync()
    {
        Items.Clear();
        foreach (var n in await _svc.ListAsync(Key)) Items.Add(n);
    }
}
