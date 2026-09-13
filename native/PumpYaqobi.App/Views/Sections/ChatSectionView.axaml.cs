using Avalonia.Controls;

namespace PumpYaqobi.App.Views.Sections;

/// <summary>
/// ⚠️ ‎InitializeComponent()‎ و نه ‎AvaloniaXamlLoader.Load(this)‎ — فیلدهای
/// ‎x:Name‎ را فقط اولی پر می‌کند. توضیحِ کامل در ‎DialogWindow‎.
/// </summary>
public partial class ChatSectionView : UserControl
{
    public ChatSectionView() => InitializeComponent();
}
