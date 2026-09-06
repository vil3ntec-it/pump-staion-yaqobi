using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.App;

/// <summary>
/// هر ViewModel به Viewِ هم‌نامش وصل می‌شود (…ViewModels.XViewModel ← …Views.XView).
/// نمونهٔ Viewِ ساخته‌شده در خودِ ViewModel کش می‌شود، پس برگشتن به یک بخش
/// دوباره‌سازی ندارد — نه چیدمانِ جدول از دست می‌رود نه جای اسکرول.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private readonly Dictionary<object, Control> _cache = new();

    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "" };
        if (_cache.TryGetValue(data, out var cached)) return cached;

        var name = data.GetType().FullName!
            .Replace("ViewModels", "Views")
            .Replace("ViewModel", "View");
        var type = Type.GetType(name) ?? data.GetType().Assembly.GetType(name);
        Control view = type is not null
            ? (Control)Activator.CreateInstance(type)!
            : new Views.PlaceholderView();
        view.DataContext = data;
        _cache[data] = view;
        return view;
    }

    public bool Match(object? data) => data is SectionViewModel;
}
