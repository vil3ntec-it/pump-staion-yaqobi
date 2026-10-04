namespace PumpYaqobi.Domain.Entities;

/// <summary>
/// ══ شورا، ب۳ — «این ستون شمارهٔ ردیفِ جدولِ دیگری است» ══════════════════
///
/// برای ستونی که <c>Id</c>ِ ردیفِ جدولِ دیگری را نگه می‌دارد ولی در مدلِ EF
/// کلیدِ خارجی نیست. همگام‌سازی آن را مثلِ کلیدِ خارجی ترجمه می‌کند
/// (شناسهٔ سراسریِ پدر کنارش می‌رود) — وگرنه روی کامپیوترِ دیگر همان عدد به
/// ردیفِ <b>دیگری</b> اشاره می‌کرد، بی هیچ خطایی.
/// ⛔ فهرستِ دستیِ دیگری نیست: <c>OpLog.SoftParents</c> همین را می‌خواند.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SyncParentAttribute : Attribute
{
    public SyncParentAttribute(Type parent) => Parent = parent;
    public Type Parent { get; }
}

/// <summary>
/// ستونی که نامش به «Id» ختم می‌شود ولی شمارهٔ ردیفِ هیچ جدولی نیست (مثلاً
/// شناسهٔ سرور یا شمارهٔ بیرونی). آزمونِ ب۳ هر ستونِ «…Id»ِ عددی را که نه
/// کلیدِ خارجی است، نه <see cref="SyncParentAttribute"/> و نه این، سرخ می‌کند.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class NotAParentAttribute : Attribute { }
