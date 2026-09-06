# فهرستِ کاملِ امکانات — «پمپ یعقوبی»
## مرحلهٔ ۱ و ۲: Audit و Feature Inventory

این سند **حدسی نیست**. همه‌اش با اسکریپت از خودِ `index.html` بیرون کشیده شده
(نسخه ۲.۹.۴۲۹ · ۶۳٬۱۴۷ خط · ۴ مگابایت) و مبنای بازنویسیِ Native است.

قاعدهٔ بندِ ۳۴: «هیچ قابلیت را از روی حدس حذف نکن. اگر قابلیتی در HTML وجود
دارد، باید در Native وجود داشته باشد.» — این سند سیاههٔ همان قابلیت‌هاست.

---

## اعداد کلی

| | تعداد |
|---|---|
| بخش‌های برنامه (`<div class="section">`) | **42** |
| توابع سراسری جاوااسکریپت | **1506** |
| کلیدهای ریشهٔ دیتابیس | **32** |
| توابع PDF/چاپ | **44** |
| مودال/دیالوگ | **36** |
| جدول‌های ایستا در HTML | **40** |

⚠️ این ۱٬۵۰۶ تابع مقیاسِ واقعیِ کار را نشان می‌دهد. بازنویسیِ Native یعنی
بازتولیدِ رفتارِ همهٔ این‌ها — نه ترجمهٔ خط‌به‌خط، بلکه همان نتیجه با معماریِ درست.

---

## ۱) بخش‌ها

shifts                 ⛽ پارچهٔ پطرول
shifts-diesel          🟤 پارچهٔ دیزل
storage                🛢️ مخزن پطرول
storage-diesel         🟤 مخزن دیزل
tanker                 🚚 تخلیهٔ تانکر
tankdip                📏 میله‌زنی مخزن
debt                   👥 قرض‌داران
debtrasid              🧾 رسید قرض‌داران
debtsum                📊 دسته‌جمعی (تیل)
debtsummoney           💵 دسته‌جمعی (پول)
oldloans               ⏰ قرض‌های کهنه (تیل)
oldloansmoney          💵 قرض‌های کهنه (پول)
priceloss              📉 زیان افزایش قیمت
plperson               📉 جزئیات زیان قرض‌دار
chakana                🧾 چکنه
noinv                  🏭 شرکت‌های تیل
expenses               💸 مصارف
rasid                  🧾 فاکتورهای رسید
profit                 📈 مفاد / ضرر
plsource               📊 ضرر و مفاد از کجا آمده؟
extraincome            ➕ درآمد اضافی
monthreport            📅 گزارش ماهانه
ratehist               📈 تاریخچهٔ نرخ
safe                   🏦 گاوصندوق
amanat                 🛢️ تیل امانت
sarrafi                💱 صرافی
waraq                  📝 ورق روزانه
zodiac                 
attendance             🧑‍💼 حاضری و معاش
staffshort             👷 کمبودی کارمندان
cameras                
invoices               🧾 فاکتورها
invrate                
history                🕘 تاریخچه‌ها
historyview            
settings               ⚙️ تنظیمات
serverservices         
notifications          
alerts                 
datamgmt               
appearance             
passwords              

---

## ۲) ساختار داده (ریشهٔ `DB`)

password
viewerPassword
profitPassword
adminUnlocked
shifts
reports
fuelEntries
debtPersons
noinvPersons
tilCompanies
sarrafiRows
syncCode
sarrafiRasidLabel
sarrafiAlbaqiLabel
expenses
chakanaRows
safeEntries
amanatAccounts
amanatBatches
amanatSettings
waraqEntries
lowStockInterval
lowStockThreshold
lowStockPhone
stationName
stationAddress
stationPhone
unionRatePetrol
unionRateDiesel
rateAnnounceUpPct
rateAnnounceDownPct
trash

---

## ۳) توابعِ PDF و چاپ

_amAccPdfBlock
_amPdfShell
_kbPdfAction
_mirzaPrintQR
_pumpDocPrint
_pumpDocPrintLegacy
_qrPrintPDF
buildPdf
doPrint
exportPdf
invPdf
invPdfList
invPrint
pdfChakana
pdfCompany
pdfCompanyById
pdfCompanyHistory
pdfCompanyPurchases
pdfDebtRasid
pdfDebtSummary
pdfDieselShifts
pdfExpenses
pdfMembershipList
pdfMonthReport
pdfOldLoans
pdfParchaHistory
pdfPerson
pdfPersonById
pdfPersonCurrentAcct
pdfPersonHistory
pdfShifts
pdfSingleReport
pdfStackedAcct
pdfStorage
printAmanat
printAmanatAccount
printDieselShifts
printSafe
printSarrafi
printShifts
printWaraq
printWaraqById
pumpPrintDoc
pumpSavePdfDoc

---

## ۴) مودال‌ها و دیالوگ‌ها

acctCardModal
acctImportModal
addCompanyModal
addExpModal
addPersonModal
addPurchaseModal
addSafeBulkModal
addSafeModal
amanatAccModal
amanatToolsModal
camAddModal
camScanModal
cmpSearchModal
companyArchiveModal
companyModal
companyModalTitle
companyPurchasesModal
gfxUltraConfirmModal
invDetailModal
lowStockModal
managerContactModal
membershipListModal
notesModal
ntfModal
parchaHistoryModal
personArchiveModal
personModal
personModalTitle
pl-pw-modal
pumpHistoryModal
qrModal
transferModal
waraqDateModal
waraqModal
waraqModalTitle
xlsImportModal

---

## ۵) جدول‌ها و ستون‌هایشان

[xls-tbl]
   # | تاریخ شروع | نام قرض‌دار | مدت عضویت
[tbl]
   # | نام قرض‌دار | جمله بردگی | جمله رسید | الباقی | چند وقت قرض‌دار
[tbl]
   # | نام قرض‌دار | جمله بردگی | جمله رسید | الباقی | چند وقت قرض‌دار
[tbl rasid-tbl]
   # | تاریخ | به حساب | نام | نمبر حواله | مقدار تیل | فی لیتر | بردگی | رسید | الباقی | 
[tbl]
   # | تاریخ | مقدار | فی عمده | نرخ بازار | پول فروشنده | درآمد اضافی | 
[xls-tbl]
   # | تاریخ | نوع | نام | مبلغ (افغانی) | یادداشت | حذف
[xls-tbl]
   # | تاریخ | توضیحات | مبلغ | واحد | فی | دالر | رسید به صرافی | بردگی پمپ بنزین ($) | الباقی ($) | حذف
[xls-tbl]
   تاریخ | کارمند | شیفت | پایه | نوع | شروع | ختم
[xls-tbl]
   تاریخ | شیفت | پایه | شروع | ختم
[xls-tbl]
   تاریخ | شیفت | پایه | شروع | ختم
[tbl]
   پایه | تاریخ | نام | نوع | شروع | ختم | فی لیتر | مقدار (لیتر) | مبلغ فروش | جمله قرض | 
[tbl]
   # | نام | نوع تیل | مقدار تیل | مبلغ | نوع | واحد
[tbl]
   # | نام | نوع تیل | مقدار تیل | مبلغ | نوع | واحد
[xls-tbl]
   # | تاریخ | نام | حواله | نوع تیل | مقدار تیل | فی لیتر | مقدار بردگی | رسید | رسید تیل | الباقی | حذف
[xls-tbl]
   # | تاریخ | نام | خرید (کیلو) | قیمت تن ($) | کل ($) | نرخ | کل (افغانی) | رسید | الباقی | حذف
[xls-tbl]
   # | عنوان | مبلغ (افغانی) | یادداشت | حذف
[ro-tbl sa-tbl]
   # | تاریخ | بردگی | رسید | الباقی
[ro-tbl]
   # | تاریخ | نام | شماره حواله | نوع تیل | مقدار | فی لیتر | بردگی | پول | الباقی
[dash-recent]
   '
    + 'تاریخ | وقت | سوخت | مبلغ | مقدار | 
[xls-tbl]
   # | تاریخ | نام | حواله | نوع تیل | مقدار تیل | فی لیتر | مقدار بردگی | رسید | الباقی | حذف
[pl-loss-tbl]
   '
    + '# | شماره فاکتور | تاریخ | مشتری | نوع تیل | لیتر | فیِ فاکتورروزِ ثبت | نرخ اتحادیهروزِ تایید | تفاوت هر لیتر | تفاوت کلافغانی | وضعیت
[pl-sub-tbl]
   '
        + '# | تاریخ برداشت | مقدار باقی‌مانده | نرخ برداشت | تاریخ رسید | نرخ اتحادیه روز رسید | زیان | وضعیت | مبلغ ثبت‌شده | ارزش با نرخ رسید | نوع | نوع تیل
[pl-sub-tbl]
   '
          + 'شماره | تاریخ | مقدار | نرخ ثبت فاکتور | نرخ اتحادیه روز تایید | تفاوت هر لیتر | ضرر این فاکتور | وضعیت | مبلغ فاکتور | نوع تیل
[pl-loss-tbl]
   '
        + '# | قرض‌دار | تیل باقی‌مانده | مبلغ ثبت‌شده | ارزش بر اساس نرخ رسید | زیان افزایش قیمت | زیان نرخ فاکتور(ثبت ← تایی | تعداد فاکتور | تعداد برداشت تکی | جزئیات
[xls-tbl]
   # | تاریخ | نام | حواله | نوع تیل | مقدار تیل | فی لیتر | مقدار بردگی | رسید | رسید تیل | الباقی | حذف
[tbl]
   # | تاریخ | نام | مبلغ (افغانی) | یادداشت | حذف
[tbl]
   شماره | تاریخ | نام | مقدار تیل | فی | مقدار بردگی | رسید | الباقی | حذف
[tbl]
   تاریخ | نام قرض‌دار | توضیحات | مبلغ رسید | 
[xls-tbl]
   # | تاریخ | نام | خرید(کیلو) | قیمت تن($) | کل($) | نرخ | کل(افغانی) | رسید | الباقی | حذف
[pl-loss-tbl pl-brk-tbl]
   منبع | مفاد (+) | ضرر (−) | سهم
[tbl safe-tbl]
   # | تاریخ | نوع | نام | مبلغ | یادداشت | حذف
[am-guide]
   گرما | ضریب | تبخیر در ماه | تبخیر در سال | انبساط گرمایی
[am-guide]
   مدت زمان | کمبودی | فیصدیِ سربه‌سر | فیصدیِ پیشنهادی | با فیصدیِ شما
[xls-tbl am-tbl2]
   # | شناسه | 🛢️ تیل (لیتر) | 🌡️ شرایطِ نگهداری | تیلِ بخار (لیتر) | ٪ فیصدی‌ها | سهمِ من (لیتر) | الباقیِ طرف (لیتر) | 📏 اندازه‌گیری | حذف | تاریخ | نام | به حسابهٔ | رسید تیل | برده شده | مدت زمان | درجه گرما | فیصدیِ من | فیصدیِ بخار | فیصدیِ لازم | سهمیهٔ من | به من می‌رسد | موجودی واقعی | اختلاف
[am-guide]
   '
    + 'اگر رویِ‌هم این‌ق | بخار | سربه‌سر | پیشنهاد | با فیصدیِ شما
[ro-tbl]
   # | تاریخ | نام | نوع تیل | تن | $/تن | کل $ | نرخ | کل افغانی | رسید | الباقی
[ro-tbl]
   # | تاریخ | نام | تن | $/تن | کل $ | نرخ | کل افغانی | رسید | الباقی
[ro-tbl]
   # | تاریخ | نام | تیل (لیتر) | بردگی | رسید | الباقی
[att-tbl]
   تاریخ | آمدن | رفتن | ساعت | 
[tbl]
   کارمند | شیفت | 🔴 کمبودیِ مانده | 🟢 اضافیِ مانده | عمل
