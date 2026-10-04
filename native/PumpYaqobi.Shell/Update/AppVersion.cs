namespace PumpYaqobi.App.Update;

/// <summary>
/// نسخهٔ همین ساخت — از خودِ اسمبلیِ <b>برنامه</b> خوانده می‌شود، نه دستی.
/// ⛔ شورا ج۵: از <see cref="Services.AppIdentity"/>، نه <c>typeof(AppVersion).Assembly</c> —
/// این فایل حالا در پوسته است و نسخهٔ پوسته را می‌خواند.
/// </summary>
public static class AppVersion
{
    public static string Current
    {
        get
        {
            var v = Services.AppIdentity.Assembly.GetName().Version;
            return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}
