# چک‌لیستِ صدموردیِ تحویل — وضعِ برنامهٔ نیتیو (نسخه 3.1.43)

هر بند با **شاهد** آمده: آزمونِ واحد (`PumpYaqobi.Tests`)، سنجشِ رفتاری
(`dotnet run --project PumpYaqobi.UiTests -- <نام>`) یا خودِ کد. حدس نیست.

نشانه‌ها: ✅ دارد و سنجیده شده · ⚠️ دارد ولی با قید · ➖ برای برنامهٔ نصبیِ
ویندوز موضوعیت ندارد · 🔧 نداشت و در همین نسخه درست شد.

## ۱. عملکرد اصلی (۱–۱۰)

| # | وضع | شاهد |
|---|---|---|
| 1 | ✅ | `verify` ده رفتارِ روزمره را واقعاً انجام می‌دهد؛ `years` هر بخش و زیربخش را با پنج سال داده باز می‌کند |
| 2 | ✅ | `DesktopUxTests` و `SubSectionTests`: هر دکمهٔ نوار و زیربخش فرمان دارد (دکمه‌های مرده‌ی قبلی برداشته شدند) |
| 3 | ✅ | `NavOrderTests`: بیست بخش، همه ساخته و در `shots` عکس‌دار |
| 4 | ✅ | `warm`: جابه‌جایی بینِ همهٔ بخش‌ها بی لودینگِ دوباره و بی برگشت به قفل |
| 5 | ✅ | `SubSectionTests` و `CompanyArchiveTests`: «‹ برگشت» از زیربخش و صفحه‌های رویی |
| 6 | ✅ | `PersistenceTests.Data_SurvivesCloseAndReopen`؛ ذخیرهٔ خودکارِ هر خانه پس از ۳۵۰ms (`RowViewModel.DelayedSaveAsync`) |
| 7 | ✅ | `PersistenceTests.EveryTable_RoundTrips`: همهٔ جدول‌ها نوشته و خوانده می‌شوند |
| 8 | ✅ | `GridBehaviourTests`، `verify` (ویرایشِ خانه، ثبت در حساب‌ها) |
| 9 | ✅ | حذف نرم + سطلِ زباله: `PersistenceTests.SoftDelete_HidesRowButKeepsIt`، `BackupTrashTests` |
| 10 | ✅ | فرمان‌های `async` غیرِ هم‌زمان‌اند (فقط سه ناوبری عمداً `AllowConcurrentExecutions`)؛ `HashOf` انتشارِ تکراری را رد می‌کند |

## ۲. فرم‌ها و ورودی‌ها (۱۱–۲۰)

| # | وضع | شاهد |
|---|---|---|
| 11 | ✅ | خرید، میله‌زنی، شرکت، شخص، دوربین: کادرِ خالی با توست رد می‌شود (`StorageSectionViewModel` خط ۳۶۳، `DebtSectionViewModel` خط ۵۶۵ …) |
| 12 | ✅ | `Shamsi.Num` هر متنِ غیرعددی را صفر می‌کند؛ `KeyboardAndZeroTests` |
| 13 | ⚠️ | ردیف‌های دفتری منفی می‌پذیرند چون معنی دارد (برگشت/اصلاح، مثلِ سایت)؛ فرم‌های خرید/میله‌زنی فقط مثبت |
| 14 | ✅ | خانه‌های عددی فقط عدد می‌خوانند (`CellDirectionTests`، `KeyboardAndZeroTests`) |
| 15 | ⚠️ | سقفِ عددی نیست — سایت هم ندارد؛ `decimal` سرریز نمی‌کند |
| 16 | ✅ | `Shamsi.Key`: سال ۱۰۰۰–۹۹۹۹، ماه ۱–۱۲، روز ۱–۳۱، وگرنه صفر؛ `DocDatesTests` |
| 17 | ⚠️ | تلفن متنِ آزاد است (شماره‌های افغانستان شکل‌های مختلف دارند) |
| 18 | ➖ | ایمیل فقط از ورودِ گوگل می‌آید، تایپ نمی‌شود |
| 19 | ✅ | توست‌ها به فارسی و با علت (`ToastService`) |
| 20 | ✅ | با خطای ثبت، فرم پاک نمی‌شود — `return` پیش از پاک کردن (`StorageSectionViewModel.BuyAsync`) |

## ۳. دیتابیس (۲۱–۳۰)

| # | وضع | شاهد |
|---|---|---|
| 21 | ✅ | `PersistenceTests.EveryTable_RoundTrips` |
| 22 | ✅ | `LegacyId` یکتا (`UNIQUE`)؛ `MigrationTests` |
| 23 | ✅ | کلیدِ خارجی روشن (`PRAGMA foreign_keys=ON`)؛ `PersistenceTests.Database_IsCreatedWithTheExpectedIndexes` |
| 24 | ✅ | حذفِ قرض‌دار حساب‌ها و ردیف‌هایش را با خودش به سطل می‌برد و با هم برمی‌گردند (`BackupTrashTests.RestoringADebtorBringsBackItsAccountsAndRows`) |
| 25 | ✅ | شناسه‌ها `AUTOINCREMENT`؛ `LegacyId` برای مهاجرت |
| 26 | ✅ | `PersistenceTests.Timestamps_AreStampedAutomatically` |
| 27 | ✅ | `PersistenceTests.Transaction_RollsBackEverythingOnFailure`؛ واردکردنِ بکاپ در یک تراکنش |
| 28 | ✅ | همان ۲۷ + اعتبارسنجیِ فرم‌ها |
| 29 | ✅ | SQLite با WAL؛ `Data_SurvivesCloseAndReopen` |
| 30 | ✅ | `BackupTrashTests.RestoreBringsTheDatabaseBackExactly` و `TheSafetyCopyHoldsTheStateFromBeforeTheRestore`؛ عکسِ روزانهٔ خودکار |

## ۴. امنیت (۳۱–۴۰)

| # | وضع | شاهد |
|---|---|---|
| 31 | ✅ | صفحهٔ قفل پیش از هر چیز؛ `warm` ثابت می‌کند هیچ داده‌ای پیش از رمز خوانده نمی‌شود |
| 32 | ✅ | سه نقش (مدیر/کارمند/بیننده): `SecurityTests.Viewer_CanOnlyLook`، `Staff_CanEditButNotDeleteOrSeeProfit` |
| 33 | ✅ | اجازه در **لایهٔ سرویس** است نه دکمه (`PermissionService.Require`)؛ سرورِ ابر هم جدا می‌سنجد (`shop/test/pump-*.test.js`) |
| 34 | ✅ | `ManagerOnly`؛ `BackupTrashTests.StaffCannotRestore`، `StaffCannotEmptyTheTrash` |
| 35 | ✅ | رمزها فقط هَش‌شده (`SecurityTests.PasswordIsNeverStoredAsPlaintext`)؛ نشانیِ ابر عمداً در کد قفل است (راز نیست، قفل است) |
| 36 | ✅ | ورودِ گوگل با PKCE و بی `client_secret`؛ `CloudAddressLockTests` |
| 37 | ✅ | توکنِ دستگاه/حساب برای هر درخواستِ ابر؛ `LicenseGuardTests` (امضای ES256، TOFU) |
| 38 | ✅ | سرورِ ابر و خانگی ورودی را جدا می‌سنجند (`server/test/stations.mjs`، `shop/test/pump-chat.test.js`) |
| 39 | ✅ | `SignOut` ⇒ `Locked`؛ `warm` سناریوی ۵ |
| 40 | ✅ | `crash.log` فقط نوع و متنِ استثنا را می‌نویسد؛ رمز جایی چاپ نمی‌شود؛ سه رمزِ غلط در را می‌بندد (`ThreeWrongTries_LockTheDoor`) |

## ۵. اینترنت و آفلاین (۴۱–۵۰)

| # | وضع | شاهد |
|---|---|---|
| 41 | ✅ | همه‌چیز روی SQLiteِ محلی؛ `CloudLink` هرگز استثنا نمی‌دهد |
| 42 | ✅ | جز چتِ پشتیبانی و انتشارِ زنده، هیچ کاری اینترنت نمی‌خواهد |
| 43 | ✅ | همان ۲۹ |
| 44 | ✅ | نوشتن اول محلی است، بعد انتشار؛ قطعِ شبکه فقط انتشار را عقب می‌اندازد (`AcctLivePublisher.Pending`) |
| 45 | ✅ | `StationPublisher` هر ۲۰ ثانیه دوباره می‌کوشد؛ `AcctLiveTests` |
| 46 | ✅ | اثرِ انگشتِ عکس (`HashOf`) — عکسِ تکراری نمی‌رود |
| 47 | ➖ | هم‌گام‌سازی یک‌طرفه است: فقط برنامه می‌نویسد، گوشی‌ها فقط می‌خوانند/درخواست می‌گذارند؛ تضاد ممکن نیست |
| 48 | ⚠️ | خطای انتشار عمداً بی‌صداست (سرورِ خاموش خطا نیست)؛ وضعِ اتصال در «حسابِ من» دیده می‌شود |
| 49 | ✅ | همان ۴۶ + گیتِ `PumpDbContext.Version` |
| 50 | ✅ | `check-kar-bot.mjs` (۶۲ سنجه) همان عکس را از دیدِ گوشی می‌خواند |

## ۶. محاسبات و حسابداری (۵۱–۶۰)

| # | وضع | شاهد |
|---|---|---|
| 51–54 | ✅ | هجده آزمونِ «برابری با سایت» (`*ParityTests`) روی داده‌های طلاییِ خودِ سایت (`golden-*.json`) |
| 55 | ✅ | همه `decimal`؛ گردکردن مطابقِ سایت (`GoldenParityTests`)؛ `SUM` به SQLite داده نمی‌شود |
| 56 | ✅ | `StorageParityTests` (فروش از ورق، میله‌زنی) |
| 57 | ✅ | `PurchaseCompanyParityTests` (خرید ⇒ مخزن و شرکت) |
| 58 | ✅ | `ProfitLossParityTests`، `PriceLossTests` با تاریخچهٔ واقعیِ نرخ |
| 59 | ✅ | هر ذخیره `Version` را بالا می‌برد و نوار/داشبورد/انتشار همان را می‌بینند |
| 60 | ✅ | نمایش از همان `decimal`ِ ذخیره‌شده ساخته می‌شود (`Shamsi.Money`)، نه از متنِ خانه |

## ۷. تاریخ، گزارش و اطلاعات (۶۱–۷۰)

| # | وضع | شاهد |
|---|---|---|
| 61 | ✅ | `Shamsi.Today` (تقویمِ خودِ دات‌نت)؛ `DocDatesTests` سه تقویم |
| 62 | 🔧 | تاریخِ سربرگ و «مفاد/مصارفِ امروز» فقط یک بار خوانده می‌شد؛ حالا تیکِ ساعت نیمه‌شب را می‌گیرد (`MainViewModel.DayChanged`) و نوار با روز هم کلید می‌خورد |
| 63–64 | ✅ | همان ۶۲؛ کشوی ماه/سال از خودِ ردیف‌ها ساخته می‌شود (`HistorySectionViewModel`) |
| 65 | ✅ | ورقِ روزانه و پارچه (`WaraqParityTests`، `ShiftParityTests`) |
| 66 | ➖ | گزارشِ هفتگی در سایت هم نیست |
| 67 | ✅ | «گزارشِ پایانِ ماه» (`MonthReportService`، `ToolsParityTests`) |
| 68 | ✅ | فیلترِ ماه در دفترها و تاریخچه (`HistoryTests`، `ledgerperf`) |
| 69 | ✅ | جست‌وجوی نام/شماره (`verify` بندِ ۶)، جست‌وجوی خرید (`CompanyArchiveTests`) |
| 70 | ✅ | `PdfTests`، `ReportSuiteTests`: عددِ PDF همان عددِ صفحه |

## ۸. رابط کاربری (۷۱–۸۰)

| # | وضع | شاهد |
|---|---|---|
| 71 | ✅ | `LayoutMetricsTests`، `VisualQualityTests`، عکس‌های `shots` |
| 72–73 | ➖ | برنامهٔ نصبیِ ویندوز است؛ کمینهٔ پنجره ۱۰۰۰×۶۴۰ و همه‌چیز می‌شکند و می‌لغزد. گوشی/تبلت اپِ جداگانهٔ `kar/` را دارند |
| 74 | ✅ | همهٔ سنجش‌ها روی ۱۴۴۰×۹۰۰ |
| 75 | ✅ | `FlowDirection=RightToLeft` روی پنجره؛ `CellDirectionTests` |
| 76 | ✅ | قلمِ وزیرمتن داخلِ برنامه (`Pump.Font`)؛ `SectionFontTests` |
| 77–78 | ✅ | دو تم: `look` و `themeflip` هر بخش را در هر دو می‌سنجند |
| 79 | ✅ | پردهٔ لودینگ (`warm`)، رشدِ تدریجیِ جدول بی صفحهٔ خالی (`vanish`) |
| 80 | ✅ | متنِ حالتِ خالی در هر بخش، توستِ خطا، پرده |

## ۹. چاپ و فایل (۸۱–۹۰)

| # | وضع | شاهد |
|---|---|---|
| 81–84 | ✅ | `PrintPageTests`، `PageSetupTests` (ورق، حاشیه، مقیاس، بازهٔ ورق) |
| 85–86 | ✅ | `PdfLookTests`: قلمِ فارسی داخلِ PDF، عددها همان‌جا |
| 87–88 | ✅ | `PdfTests`، `ReportSuiteTests` برای همهٔ گزارش‌ها |
| 89 | ✅ | نامِ فایل با بخش و تاریخ (`PdfShortcutTests`) |
| 90 | ✅ | PDF استاندارد (QuestPDF)، بی وابستگی به قلمِ نصب‌شده |

## ۱۰. تست نهایی و تحویل (۹۱–۱۰۰)

| # | وضع | شاهد |
|---|---|---|
| 91 | ✅ | `CrashGuard` هر استثنای نگرفته را در `crash.log` می‌نویسد و برنامه را زنده نگه می‌دارد |
| 92 | ✅ | `CloudLink`/`HomeSync` خطای شبکه را به `CloudResult` برمی‌گردانند؛ `StationLinkTests` |
| 93 | ✅ | `years`: پنج سال داده، ۶۴ هزار ردیف |
| 94 | ✅ | سه نقش؛ چند کاربرِ هم‌زمان روی یک کامپیوتر موضوعیت ندارد |
| 95 | ✅ | فرمان‌های `AsyncRelayCommand` تا پایانِ اجرا خاموش‌اند |
| 96 | ✅ | `warm` (خروج/ورود)، `PersistenceTests` (بستن/باز کردن) |
| 97 | ✅ | `BackupTrashTests.RestoreBringsTheDatabaseBackExactly` |
| 98 | ✅ | برنامه هیچ دادهٔ نمونه‌ای نمی‌سازد؛ دادهٔ آزمون فقط در `UiTests`/`Tests` و در پوشهٔ موقت |
| 99 | ✅ | همان کامیتی که `dotnet test` و سنجش‌ها را گذرانده، `build-native.yml` منتشر می‌کند |
| 100 | ✅ | `verify` + `warm` + `years` مسیرِ کامل: باز شدن ⇒ رمز ⇒ هر بخش ⇒ ثبت ⇒ ویرایش ⇒ حذف ⇒ چاپ ⇒ خروج |

## سرعت (خواستهٔ جدا)

| سنجش | عدد |
|---|---|
| تا صفحهٔ رمز (`startup`، دیتابیسِ خالی) | ۳٫۵ ثانیه در headless؛ روی ویندوز با ReadyToRun کمتر |
| بی‌کاری ۳ ثانیه، ده ثانیه پس از ورود | ۰ چیدمان، ۴۰ms CPU (۱٫۳٪ یک هسته)؛ سه ثانیهٔ اولِ پس از ورود JITِ پس‌زمینه است و با ReadyToRun روی ویندوز کوتاه‌تر |
| باز کردنِ هر بخش پس از ورود (`warm`) | زیرِ ۱۴۰ms |
| با پنج سال داده (`years`) | هر بخش و زیربخش زیرِ ۱٫۲ ثانیه |
