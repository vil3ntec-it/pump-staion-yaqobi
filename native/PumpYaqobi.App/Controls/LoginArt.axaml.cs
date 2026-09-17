using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ آدمک‌های صفحهٔ ورود — برداری، با تمِ خودِ برنامه ════════════════════════
/// شرحِ کامل بالای <c>LoginArt.axaml</c>. هیچ کدِ C#ی ندارد و نباید بگیرد:
/// یک نقشهٔ برداریِ ساکن است، پس نه فایلی می‌خواند، نه نخی می‌گیرد، نه
/// انیمیشنی دارد.
/// </summary>
public partial class LoginArt : UserControl
{
    public LoginArt() => AvaloniaXamlLoader.Load(this);
}
