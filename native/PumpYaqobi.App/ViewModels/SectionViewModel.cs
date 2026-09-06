using CommunityToolkit.Mvvm.ComponentModel;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// پایهٔ هر بخشِ برنامه. یک نمونه برای هر بخش ساخته می‌شود و تا پایانِ اجرا
/// زنده می‌ماند — پس رفت‌وبرگشت بین بخش‌ها هیچ چیزی را دوباره بار نمی‌کند
/// (بندِ ۲۹ خواسته: «تغییر Section نباید کل صفحه را reload کند»).
/// </summary>
public abstract partial class SectionViewModel : ObservableObject
{
    protected SectionViewModel(string id, string iconKey, string title)
    {
        Id = id; IconKey = iconKey; Title = title;
    }

    public string Id { get; }
    /// <summary>کلیدِ آیکون در <c>Icons.axaml</c> — معمولاً همان شناسهٔ بخش.</summary>
    public string IconKey { get; }
    public string Title { get; }

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;

    /// <summary>بارِ اولِ داده — فقط یک‌بار، همان لحظه‌ای که کاربر واقعاً وارد بخش شد.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded) return;
        IsBusy = true;
        try { await LoadAsync(); IsLoaded = true; }
        finally { IsBusy = false; }
    }

    protected virtual Task LoadAsync() => Task.CompletedTask;

    /// <summary>
    /// هر بار که کاربر واردِ بخش می‌شود — نه فقط بارِ اول. بخش‌هایی که فقط
    /// «خلاصه»ی دادهٔ بخش‌های دیگرند (مثلِ داشبورد) باید این‌جا خودشان را تازه
    /// کنند، وگرنه عددهایشان روی عکسِ لحظهٔ ورودِ اولِ برنامه می‌ماند.
    /// </summary>
    public virtual Task OnActivatedAsync() => Task.CompletedTask;

    /// <summary>بارگیریِ دوباره (پس از واردکردن بکاپ یا هم‌گام‌سازی).</summary>
    public virtual async Task ReloadAsync()
    {
        IsLoaded = false;
        await EnsureLoadedAsync();
    }
}
