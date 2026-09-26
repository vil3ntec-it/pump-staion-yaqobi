using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PumpYaqobi.Domain;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Persistence;

/// <summary>
/// ══ دفترِ تغییرات — یک نقطه، و هیچ دری غیر از آن ═════════════════════════
///
/// بندِ ۲۰٫۱ و بندِ ۱ی پرامپتِ ۲۲: «همهٔ writeها فقط از طریقِ یک
/// <c>Repository</c> (یک نقطه) که op می‌نویسد.»
///
/// ── چرا این‌جا و نه در سرویس‌ها ─────────────────────────────────────────
/// برنامه از پیش سی‌وچند سرویسِ داده دارد (<c>DebtorService</c>،
/// <c>LedgerService</c>، …) و همه‌شان سرِ آخر به یک جا می‌رسند:
/// <see cref="PumpDbContext.SaveChangesAsync"/>. پس نقطهٔ واحد <b>همان‌جا</b>
/// است، نه یک لایهٔ تازه‌ٔ موازی. اگر فردا سرویسِ سی‌وسومی نوشته شود،
/// بی این‌که کسی چیزی یادش بماند opهایش نوشته می‌شوند — و این دقیقاً
/// همان چیزی است که قاعده می‌خواهد.
///
/// ⚠️ <b>op در همان تراکنشِ خودِ داده می‌نشیند.</b> ردیف‌های
/// <see cref="SyncOp"/> پیش از <c>base.SaveChanges()</c> به همان
/// <c>ChangeTracker</c> اضافه می‌شوند، پس یا هر دو می‌نشینند یا هیچ‌کدام.
/// برق که وسطِ کار برود، دفتر و داده از هم جلو نمی‌افتند.
///
/// ⚠️ <b>نوشتنِ خام (<c>ExecuteSql</c>) op نمی‌سازد</b> — و این هم عمدی
/// است: تنها جایی که برنامه خام می‌نویسد، اعمالِ opهای <b>رسیده از سرور</b>
/// است (<c>SyncStore.ApplyIncoming</c>). اگر آن‌ها op می‌ساختند، هر تغییرِ
/// دستگاهِ دیگر دوباره به سرور برمی‌گشت — حلقهٔ بی‌پایانِ پژواک.
/// </summary>
public static class OpLog
{
    /// <summary>
    /// نوشتنِ op روشن است؟
    ///
    /// ⚠️ پیش‌فرض <b>روشن</b> است و باید بماند: تغییری که پیش از ورودِ
    /// کاربر به حساب داده شده هم باید یک روز به سرور برسد. خاموشی فقط
    /// برای مهاجرت و بازگردانیِ پشتیبان است، که خودشان دفتر را از نو
    /// می‌چینند.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// نسخهٔ schemaی این برنامه — همان عددی که سرور در
    /// <c>sync-v1-migrations.js</c> برای بخشِ <c>pump</c> دارد.
    ///
    /// ⛔ هر جدول یا فیلدِ تازه = یک پله بالاتر این‌جا <b>و</b> یک پله
    /// آن‌جا، با همان شماره. جلو بردنِ یک‌طرفهٔ این عدد یعنی ۴۲۶ و ایستادنِ
    /// کلِ همگام‌سازی.
    /// </summary>
    public const int SchemaVersion = 1;

    /// <summary>
    /// جدول‌هایی که مالِ <b>همین</b> کامپیوترند و هیچ‌وقت به سرور نمی‌روند.
    ///
    /// ⚠️ همان چهارتایی که فهرستِ سفیدِ سرور هم ندارد
    /// (<c>lib/sync-v1.js</c>): رمزِ محلی، تنظیمات، سطلِ زباله و دفترِ
    /// کارهای همین دستگاه. فرستادنشان یعنی رمزِ یک پمپ روی کامپیوترِ
    /// دیگری بنشیند.
    /// </summary>
    public static readonly IReadOnlySet<string> Local = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(AppUser), nameof(Setting), nameof(TrashItem), nameof(AuditEntry),
    };

    /// <summary>ستون‌هایی که هیچ‌وقت در <c>fields</c> نمی‌روند.</summary>
    private static readonly IReadOnlySet<string> Skip = new HashSet<string>(StringComparer.Ordinal)
    {
        "Id", nameof(EntityBase.SyncUid),
    };

    /// <summary>
    /// ══ ترتیبِ پدر پیش از فرزند ═══════════════════════════════════════
    ///
    /// رتبهٔ هر جدول: جدولی که به هیچ جدولِ دیگری کلیدِ خارجی ندارد صفر،
    /// بقیه یکی بیشتر از بزرگ‌ترین پدرشان. «بارِ اول» و opهای یک ذخیره با
    /// همین ترتیب می‌روند، پس روی کامپیوترِ دیگر پدر همیشه پیش از فرزند
    /// می‌رسد. ⛔ تا ۱۴۰۵/۰۷/۱۴ ترتیب الفبایی بود: «DebtAccount» پیش از
    /// «Debtor» و «DebtRow» پیش از هر دو — و کامپیوترِ دوم با «FOREIGN KEY
    /// constraint failed» هیچ حسابِ قرض‌داری نمی‌گرفت (سنجهٔ `tensync`).
    /// </summary>
    public static IReadOnlyDictionary<string, int> Rank(Microsoft.EntityFrameworkCore.Metadata.IModel model)
    {
        lock (RankLock)
        {
            if (_rankFor == model && _rank is not null) return _rank;
            var types = model.GetEntityTypes().Where(e => typeof(EntityBase).IsAssignableFrom(e.ClrType)).ToList();
            var rank = new Dictionary<string, int>(StringComparer.Ordinal);
            var busy = new HashSet<string>(StringComparer.Ordinal);
            int Of(Microsoft.EntityFrameworkCore.Metadata.IEntityType e)
            {
                var name = e.ClrType.Name;
                if (rank.TryGetValue(name, out var r)) return r;
                if (!busy.Add(name)) return 0;   // حلقه — همان‌جا بشکن
                var best = 0;
                foreach (var fk in e.GetForeignKeys())
                {
                    var parent = fk.PrincipalEntityType;
                    if (parent == e || !typeof(EntityBase).IsAssignableFrom(parent.ClrType)) continue;
                    best = Math.Max(best, Of(parent) + 1);
                }
                foreach (var soft in SoftParents)
                {
                    if (soft.Entity != name || soft.Parent == name) continue;
                    var parent = types.FirstOrDefault(x => x.ClrType.Name == soft.Parent);
                    if (parent is not null) best = Math.Max(best, Of(parent) + 1);
                }
                busy.Remove(name);
                return rank[name] = best;
            }
            foreach (var e in types) Of(e);
            _rankFor = model;
            return _rank = rank;
        }
    }

    /// <summary>
    /// ══ پیوندهایی که کلیدِ خارجیِ اعلام‌شده ندارند ════════════════════════
    ///
    /// این ستون‌ها <c>Id</c>ِ ردیفِ جدولِ دیگری را نگه می‌دارند ولی در مدلِ EF
    /// کلیدِ خارجی نیستند (پس دیتابیس هم نمی‌سنجدشان). روی کامپیوترِ دیگر
    /// همان عدد به ردیفِ <b>دیگری</b> اشاره می‌کرد — بی هیچ خطایی. پس مثلِ
    /// کلیدِ خارجی ترجمه می‌شوند (<c>SyncStore.DataTables</c>).
    /// ⛔ ستونِ تازه‌ای که شمارهٔ ردیفِ جدولِ دیگری را نگه دارد، این‌جا هم.
    /// </summary>
    public static readonly IReadOnlyList<(string Entity, string Property, string Parent)> SoftParents = new[]
    {
        (nameof(Expense), nameof(Expense.SalaryStaffId), nameof(StaffMember)),
        (nameof(RasidEntry), nameof(RasidEntry.InvoiceId), nameof(Invoice)),
        (nameof(DebtRow), nameof(DebtRow.InvoiceId), nameof(Invoice)),
        (nameof(DebtTableArchive), nameof(DebtTableArchive.AccountId), nameof(DebtAccount)),
        (nameof(CompanyTableArchive), nameof(CompanyTableArchive.CompanyId), nameof(TilCompany)),
        (nameof(Invoice), nameof(Invoice.DebtAccountId), nameof(DebtAccount)),
        (nameof(StaffShortage), nameof(StaffShortage.StaffId), nameof(StaffMember)),
        //  مرزِ بازهٔ خریدهای آرشیوشده — شمارهٔ خرید، نه شمارش
        (nameof(TilCompany), nameof(TilCompany.PurchaseCheckpointPetrol), nameof(FuelPurchase)),
        (nameof(TilCompany), nameof(TilCompany.PurchaseCheckpointDiesel), nameof(FuelPurchase)),
        (nameof(CompanyTableArchive), nameof(CompanyTableArchive.PurchasesAfter), nameof(FuelPurchase)),
        (nameof(CompanyTableArchive), nameof(CompanyTableArchive.PurchasesBefore), nameof(FuelPurchase)),
    };

    private static readonly object RankLock = new();
    private static Microsoft.EntityFrameworkCore.Metadata.IModel? _rankFor;
    private static Dictionary<string, int>? _rank;

    /// <summary>این موجودیت به سرور می‌رود؟</summary>
    public static bool Tracked(EntityEntry entry) => !Local.Contains(entry.Metadata.ClrType.Name);

    /// <summary>
    /// از یک ردیفِ در حالِ ذخیره، یک op می‌سازد — یا <c>null</c> اگر چیزی
    /// برای گفتن نباشد (ویرایشی که هیچ فیلدِ فرستادنی‌ای عوض نکرده).
    /// </summary>
    /// <param name="entry">ردیفِ ردیابی‌شده.</param>
    /// <param name="state">حالِ <b>پیش از</b> حذفِ نرم — وگرنه حذف «ویرایش» دیده می‌شود.</param>
    /// <param name="nowMs">زمانِ دستگاه.</param>
    public static SyncOp? Build(EntityEntry entry, EntityState state, long nowMs)
    {
        var table = entry.Metadata.ClrType.Name;
        var uid = (entry.Entity as EntityBase)?.SyncUid ?? "";
        if (uid.Length == 0) return null;

        var type = state switch
        {
            EntityState.Added => "insert",
            EntityState.Deleted => "delete",
            _ => "update",
        };

        var fields = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        if (type != "delete")
        {
            foreach (var p in entry.Properties)
            {
                var name = p.Metadata.Name;
                if (Skip.Contains(name)) continue;
                //  ⚠️ ویرایش فقط فیلدهای **عوض‌شده** را می‌برد — همان چیزی که
                //  «تغییرِ یک حرف در یک اسم = یک op با یک فیلد» را ممکن می‌کند.
                if (type == "update" && !p.IsModified) continue;
                fields[name] = Plain(p.CurrentValue);
            }
            //  ویرایشی که چیزی عوض نکرده، op هم ندارد
            if (type == "update" && fields.Count == 0) return null;
        }

        var json = JsonSerializer.Serialize(fields);
        return new SyncOp
        {
            OpId = Ulid.New(nowMs),
            TableName = table,
            RowUid = uid,
            OpType = type,
            FieldsJson = json,
            Hash = HashOf(table, uid, type, json),
            ClientTs = nowMs,
            SchemaVersion = SchemaVersion,
            Synced = false,
        };
    }

    /// <summary>
    /// اثرِ انگشتِ محلیِ یک op.
    ///
    /// ⚠️ این <b>همان</b> چیزی نیست که سرور با <c>hashOf</c> می‌سازد و
    /// عمداً هم فرستاده نمی‌شود: سرور روی JSONِ جاوااسکریپت هش می‌گیرد و
    /// شکلِ عددِ اعشاری در دو زبان مو‌به‌مو یکی نیست. هشِ ناجور یعنی
    /// <c>hash_mismatch</c> و ردِ کلِ op — یعنی دادهٔ گم‌شده به‌جای یک
    /// سنجشِ اختیاری. این‌جا فقط برای سلامتِ خودِ دفتر نگه داشته می‌شود.
    /// </summary>
    public static string HashOf(string table, string rowUid, string type, string fieldsJson) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{table}|{rowUid}|{type}|{fieldsJson}"))).ToLowerInvariant();

    /// <summary>
    /// مقدارِ یک ستون، به شکلی که هم JSON بپذیرد و هم معنایش گم نشود.
    ///
    /// ⚠️ <b>مبلغ رشته می‌رود، نه عدد.</b> همان قاعدهٔ همیشگیِ این برنامه:
    /// پول با <c>double</c> نگه داشته نمی‌شود، و JSONِ شناور روی اعشارِ
    /// بلند گرد می‌کند. خودِ ستون هم در SQLite <c>TEXT</c> است.
    /// </summary>
    public static object? Plain(object? value)
    {
        if (value is null) return null;
        if (value is string s) return s;
        if (value is bool b) return b;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is short sh) return (int)sh;
        if (value is byte by) return (int)by;
        if (value is decimal d) return d.ToString(CultureInfo.InvariantCulture);
        if (value is double db) return db;
        if (value is float f) return (double)f;
        if (value is DateTime dt) return dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        if (value is DateTimeOffset dto) return dto.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        if (value is Guid g) return g.ToString("N");
        if (value is byte[] raw) return Convert.ToBase64String(raw);
        if (value is Enum e) return Convert.ToInt32(e, CultureInfo.InvariantCulture);
        return value.ToString();
    }
}
