using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PumpYaqobi.App.ViewModels;

// ══ پرسشِ درونِ پنجره — نه پنجرهٔ جدای ویندوز (۱۴۰۵/۰۸/۰۲) ══════════════════════
//
//  گزارشِ صاحب ریپو: «با ایکسِ ویندوز می‌خواستم ببندم، بسته نمی‌شد و برنامه هم کار
//  نمی‌کرد؛ صدا از ویندوز می‌آمد؛ انگار یک صفحهٔ جدا پشتِ صفحهٔ برنامه بود که اول
//  اون باید بسته می‌شد، و بعد اون هم نبود.»
//
//  ⛔ ریشه: «پیش از بستن، بکاپ؟» و «نسخهٔ تازه آماده است» پنجرهٔ جدای ویندوز بودند
//  (‎ShowDialog‎). تا آن پنجره باز است ویندوز پنجرهٔ برنامه را **غیرفعال** می‌کند: هر
//  کلیک و هر ✕ فقط «دینگ» است (سنجهٔ ‎closex‎: ‎SC_CLOSE‎ِ پنجرهٔ غیرفعال اصلاً به
//  ‎Closing‎ نمی‌رسد). پنجره‌ای که دیده نشود — بیرون از صفحه، روی نمایشگرِ دیگر، یا
//  کشیده‌نشده — یعنی برنامه‌ای که نه کار می‌کند و نه بسته می‌شود.
//  پس این دو پرسش حالا **داخلِ خودِ پنجرهٔ برنامه**اند: نه غیرفعالش می‌کنند، نه
//  می‌توانند پشت یا بیرون بیفتند، و ✕ همیشه به برنامه می‌رسد.
//
//  ⚠️ یک پرسش در هر لحظه: پرسشِ تازه پرسشِ پیشین را «نه» جواب می‌دهد (✕ وسطِ پیشنهادِ
//  «نسخهٔ تازه» یعنی «بعداً» و رفتن به «پیش از بستن»).
public sealed partial class MainViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAskingBlocking), nameof(IsAskingCard))]
    private string _askTitle = "";

    [ObservableProperty] private string _askText = "";
    [ObservableProperty] private string _askOkText = "";
    [ObservableProperty] private string _askNoText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAskingBlocking), nameof(IsAskingCard))]
    private bool _askBlocks;

    /// <summary>پرسشِ تمام‌پنجره (پیش از بستن) — جای کارِ دیگری نیست تا جواب بیاید.</summary>
    public bool IsAskingBlocking => AskTitle.Length > 0 && AskBlocks;

    /// <summary>کارتِ گوشهٔ پنجره (نسخهٔ تازه) — کار ادامه دارد، فوکوس دزدیده نمی‌شود.</summary>
    public bool IsAskingCard => AskTitle.Length > 0 && !AskBlocks;

    private TaskCompletionSource<bool>? _ask;

    /// <summary>
    /// بپرس و منتظرِ جواب بمان — ‎true‎ فقط با دکمهٔ «بله»ی خودِ کاربر.
    /// </summary>
    public Task<bool> AskConfirmAsync(string title, string text, string ok, string no, bool blocks)
    {
        //  ⚠️ کارتِ «نسخهٔ تازه» جای «پیش از بستن»ِ باز را نمی‌گیرد — برنامه دارد بسته می‌شود
        if (!blocks && IsAskingBlocking) return Task.FromResult(false);
        AnswerAsk(false);
        var t = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ask = t;
        AskText = text;
        AskOkText = ok;
        AskNoText = no;
        AskBlocks = blocks;
        AskTitle = title;
        return t.Task;
    }

    /// <summary>پرسشِ باز را جواب بده (اگر هست) و ببندش.</summary>
    public void AnswerAsk(bool yes)
    {
        var t = _ask;
        _ask = null;
        AskTitle = "";
        t?.TrySetResult(yes);
    }

    [RelayCommand] private void AskAccept() => AnswerAsk(true);
    [RelayCommand] private void AskDecline() => AnswerAsk(false);
}
