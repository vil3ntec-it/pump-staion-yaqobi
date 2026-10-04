using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ «نشانم بده» — برجسته کردنِ یک کادر (شورا، ث۱) ══════════════════════════
/// روی کادرِ هدف در XAML: <c>c:Spot.Id="tank-capacity"</c>. <see cref="Show"/>
/// نخستین کادرِ **دیدنیِ** همان شناسه را پیدا می‌کند، به دید می‌آورد، فوکوس
/// می‌دهد و چهار ثانیه کلاسِ <c>spot</c> (لبهٔ پررنگِ تاکید) را رویش می‌گذارد.
///
/// ⚠️ اگر هدف خودش کادرِ تایپ یا دکمه نیست (مثلاً کارتِ روزِ پارچه)، نخستین
/// کادرِ تایپِ داخلش برجسته می‌شود — همان جایی که کاربر باید بنویسد.
/// ⛔ هیچ چیزی نمی‌نویسد و هیچ فرمانی نمی‌زند؛ فقط نشان می‌دهد.
/// </summary>
public static class Spot
{
    public static readonly AttachedProperty<string?> IdProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Id", typeof(Spot));

    public static string? GetId(Control c) => c.GetValue(IdProperty);
    public static void SetId(Control c, string? v) => c.SetValue(IdProperty, v);

    /// <summary>پنجرهٔ اصلی — جایی که «نشانم بده» در آن می‌گردد.</summary>
    public static Visual? Root { get; set; }

    /// <summary>آخرین کادری که برجسته شد — فقط برای سنجه‌ها.</summary>
    public static Control? Last { get; private set; }

    public static Control? Find(Visual root, string id) =>
        root.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(c => GetId(c) == id && c.IsEffectivelyVisible);

    /// <summary>پیدا کن، به دید بیاور و برجسته کن. نبود ⇒ ‎null‎.</summary>
    public static Control? Show(Visual root, string id)
    {
        var host = Find(root, id);
        if (host is null) return null;

        Control target = host is TextBox or Button
            ? host
            : host.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsEffectivelyVisible) ?? host;

        target.BringIntoView();
        target.Focus();
        target.Classes.Add("spot");
        Last = target;
        DispatcherTimer.RunOnce(() => target.Classes.Remove("spot"), TimeSpan.FromSeconds(4));
        return target;
    }
}
