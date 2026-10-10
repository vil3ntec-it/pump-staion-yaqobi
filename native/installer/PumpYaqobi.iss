; ═══════════════════════════════════════════════════════════════════════════
;  نصب‌کنندهٔ «پمپ یعقوبی» — Inno Setup
; ═══════════════════════════════════════════════════════════════════════════
;
;  تا امروز انتشار فقط یک زیپ بود: کاربر باید خودش جایی بازش می‌کرد و
;  ‎PumpYaqobi.exe‎ را پیدا می‌کرد. نه آیکونی روی دسکتاپ می‌آمد، نه در فهرستِ
;  برنامه‌ها پیدا می‌شد، نه در «برنامه‌ها و قابلیت‌ها» بود که بشود حذفش کرد.
;  گلایهٔ صاحب ریپو دقیقاً همین بود: «نصب هم نمی‌شد».
;
;  ── پوشهٔ نصب ──────────────────────────────────────────────────────────────
;  کاربر خودش انتخاب می‌کند فایل‌ها کجا بروند (صفحهٔ «انتخابِ پوشه»)، ولی
;  پیشنهادِ پیش‌فرض ‎%LocalAppData%\Programs\PumpYaqobi‎ است — همان‌جایی که
;  کروم و وی‌اس‌کد و اسلک هم می‌نشینند.
;
;  چرا این پیش‌فرض: برنامه خودش را به‌روز می‌کند و فایل‌هایش را جابه‌جا
;  می‌کند. داخلِ ‎Program Files‎ این کار هر بار اجازهٔ مدیر می‌خواهد. پس اگر
;  کاربر چنان جایی را انتخاب کند، هم همین‌جا (کدِ پایینِ فایل) به او گفته
;  می‌شود و هم خودِ برنامه هنگامِ به‌روزرسانی اجازهٔ مدیر می‌گیرد به‌جای آنکه
;  بی‌صدا شکست بخورد.
;
;  ── دادهٔ کاربر ────────────────────────────────────────────────────────────
;  دیتابیس در ‎%AppData%\PumpYaqobi\pump.db‎ است — بیرونِ پوشهٔ نصب. پس
;  به‌روزرسانی و حتی حذفِ برنامه هیچ دست به حساب‌ها نمی‌زند. حذف‌کننده هم
;  عمداً آن پوشه را پاک نمی‌کند (پایینِ فایل، توضیحِ UninstallDelete).
; ═══════════════════════════════════════════════════════════════════════════

; ── دو معماری، یک فایل ─────────────────────────────────────────────────────
;  خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «۳۲ و ۶۴ توی یک فایل باشن، انتخابی باشه و
;  کار هم کنه — حدس نباشه.» («۸۶ بیت» وجود ندارد — x86 همان ۳۲بیتی است.)
;
;  پس یک نصاب، دو بار: بارِ ۶۴بیتی (SourceDir64) و بارِ ۳۲بیتی (SourceDir86)
;  هر دو داخلِ همین فایل‌اند و صفحهٔ «۳۲ یا ۶۴؟» می‌گوید کدام نصب شود.
;  فایل‌هایی که Check‌شان رد شود اصلاً روی دیسک نمی‌نشینند.
;
;  ⛔ AppIdِ همیشگی **یک حرف هم عوض نشد**. عوض شدنش یعنی هر نصبی که همین
;     حالا دستِ مشتری است برای ویندوز یک برنامهٔ «غریبه» می‌شود: نصبِ تازه
;     رویش نمی‌نشیند، پوشهٔ قبلی پیدا نمی‌شود، و کاربر دو ردیف در «برنامه‌ها
;     و قابلیت‌ها» می‌بیند. هر دو معماری همین یک AppId و همین یک پوشه را
;     دارند — دو نسخه کنارِ هم نمی‌نشینند، یکی جای دیگری می‌نشیند.
;  ⛔ عوض کردنِ معماری روی نصبِ موجود بارِ کهنه را پاک می‌کند
;     ([InstallDelete]، فقط پوشهٔ VLCِ معماریِ دیگر — بقیهٔ فایل‌ها هم‌نام‌اند
;     و رویشان نوشته می‌شود). بی این، ~۱۰۰ مگابایت آشغال می‌مانْد.
;  ⛔ نصبِ بی‌صدا (به‌روزرسانیِ خودِ برنامه) معماری را از ‎/ARCH=x86|x64‎
;     می‌گیرد، وگرنه از همان چیزی که بارِ پیش نصب شده (رجیستری)، وگرنه از
;     ویندوز. یعنی به‌روزرسانی هیچ‌وقت معماری را عوض نمی‌کند.
;  ⛔ روی ویندوزِ ۳۲بیتی همیشه ۳۲بیتی — گزینهٔ ۶۴ بسته است و بی‌صدا هم ۶۴
;     نمی‌نشیند، چون آن فایل آن‌جا اجرا نمی‌شود.
; نامِ برنامه در ویندوز (۱۴۰۵/۰۷/۱۵، خواستهٔ صاحب ریپو: «اسمش پمپ یعقوبی نباشد، پمپ
; بنزین خالی»). ⚠️ فقط نامِ دیدنی عوض شد: AppGuid، PumpYaqobi.exe و پوشه‌ها همان‌اند،
; پس به‌روزرسانی همان نصب را پیدا می‌کند و دفتر دست نمی‌خورد.
#define AppName "پمپ بنزین"
#define OldName "پمپ یعقوبی"
#define AppGuid "{{8E86F349-343C-4FFB-983E-BBDDC5390081}"
#define InstallFolder "PumpYaqobi"
#define OutName "PumpYaqobi-Setup"
#define AppExe  "PumpYaqobi.exe"
#define ArchKey "Software\PumpYaqobi"
#ifndef SourceDir64
  #define SourceDir64 "..\publish\win-x64"
#endif
#ifndef SourceDir86
  #define SourceDir86 "..\publish\win-x86"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={#AppGuid}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
VersionInfoVersion={#AppVersion}
; پروانهٔ اختصاصی — همان LICENSEِ ریشهٔ مخزن
AppCopyright=© VILL3N — All rights reserved.
VersionInfoCopyright=© VILL3N — All rights reserved.

; نصب برای همین کاربر — بی اجازهٔ مدیر
PrivilegesRequired=lowest
; ⛔ «dialog» نه (۱۴۰۵/۰۷/۱۵): پنجرهٔ انگلیسیِ «برای همه یا فقط من؟» اولِ کار
; گیج می‌کرد و «برای همه» یک نصبِ دومِ جدا (HKLM) کنارِ نصبِ کاربری می‌ساخت.
; نصبِ همه‌کاربره هنوز با ‎/ALLUSERS‎ شدنی است.
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={localappdata}\Programs\{#InstallFolder}
DefaultGroupName={#AppName}
; ⚠️ گروهِ پیشین «پمپ یعقوبی» بود — نامِ تازه، و گروهِ کهنه پایین در [InstallDelete] می‌رود
UsePreviousGroup=no
DisableProgramGroupPage=yes
; صفحهٔ «انتخابِ پوشه» باز است — خواستهٔ صاحب ریپو: «بشه انتخاب کرد که کجا
; فایل‌ها رو ببرم بزارم موقع نصب». پیش‌فرض همان بالاست و اگر جای دیگری
; انتخاب شود، کدِ پایینِ فایل نتیجه‌اش را می‌گوید.
DisableDirPage=no
; نصبِ دوباره/به‌روزرسانی به همان پوشه‌ای می‌رود که کاربر بارِ اول انتخاب کرد
UsePreviousAppDir=yes

OutputDir=..\..\rel
OutputBaseFilename={#OutName}
SetupIconFile=..\PumpYaqobi.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; در Inno 6 صفحهٔ خوش‌آمد پیش‌فرض خاموش است — روشنش می‌کنیم
; وگرنه متنِ «حساب‌های شما جدا نگه داشته می‌شوند» هرگز دیده نمی‌شد.
DisableWelcomePage=no
; اگر برنامه باز باشد، خودش می‌بندد و بعد از نصب باز می‌کند — لازمهٔ
; به‌روزرسانیِ خودکار، وگرنه فایلِ در حالِ اجرا قفل است و نصب شکست می‌خورد.
CloseApplications=yes
RestartApplications=yes
; پسوندهای ‎.pumpyaqobi‎ و ‎.pumpkey‎ (پایین، [Registry]) — ویندوز باید همان لحظه بفهمد
ChangesAssociations=yes

[Languages]
Name: "fa"; MessagesFile: "compiler:Default.isl"

; ── نوشته‌های فارسی ────────────────────────────────────────────────────────
; Inno ترجمهٔ رسمیِ فارسی ندارد، پس همان چند جمله‌ای که کاربر واقعاً می‌بیند
; این‌جا بازنویسی می‌شود. بقیه انگلیسیِ پیش‌فرض می‌ماند.
[Messages]
fa.SetupAppTitle=نصبِ {#AppName}
fa.SetupWindowTitle=نصبِ {#AppName}
fa.WelcomeLabel1=نصبِ [name]
fa.WelcomeLabel2=[name/ver] روی این کامپیوتر نصب می‌شود.%n%nحساب‌های شما در پوشهٔ «data» داخلِ همین پوشهٔ برنامه نگه داشته می‌شوند — به‌روزرسانی و حذفِ برنامه به آن‌ها دست نمی‌زند، و با عوض کردنِ ویندوز هم از بین نمی‌روند (برنامه را روی درایوی غیر از C نصب کنید).
fa.ClickNext=برای ادامه «Next» را بزنید.
fa.WizardSelectDir=پوشهٔ نصب
fa.SelectDirDesc=[name] کجا نصب شود؟
fa.SelectDirLabel3=[name] در پوشهٔ زیر نصب می‌شود. حساب‌ها هم داخلِ همین پوشه (data) می‌مانند — درایوی غیر از C (مثلاً D) بهتر است تا با عوض کردنِ ویندوز از بین نروند.
fa.SelectDirBrowseLabel=برای ادامه «Next» را بزنید. برای انتخابِ پوشهٔ دیگر «Browse» را بزنید.
fa.DiskSpaceGBLabel=دستِ‌کم [gb] گیگابایت جای خالی لازم است.
fa.DiskSpaceMBLabel=دستِ‌کم [mb] مگابایت جای خالی لازم است.
fa.ButtonBrowse=&انتخابِ پوشه
fa.DirNotEmpty=پوشهٔ «%1» خالی نیست. باز هم همان‌جا نصب شود؟
; پیام‌های خودِ Inno برای پوشهٔ نادرست — پیش از NextButtonClick می‌آیند (سنجهٔ ویزارد دید که انگلیسی بودند)
fa.InvalidDrive=درایوی که انتخاب کردید روی این کامپیوتر پیدا نشد یا در دسترس نیست. درایو یا پوشهٔ دیگری انتخاب کنید.
fa.InvalidPath=نشانیِ کامل با حرفِ درایو بنویسید، مثلاً:%n%nD:\PumpYaqobi
fa.DiskSpaceWarningTitle=جای خالی کم است
fa.DiskSpaceWarning=برای نصب دستِ‌کم %1 کیلوبایت جای خالی لازم است ولی این درایو فقط %2 کیلوبایت دارد.%n%nباز هم ادامه شود؟
fa.ButtonNext=&بعدی
fa.ButtonBack=&قبلی
fa.ButtonInstall=&نصب
fa.ButtonCancel=انصراف
fa.ButtonFinish=&پایان
fa.SelectTasksLabel2=کدام کارها انجام شود؟
fa.ReadyLabel1=آمادهٔ نصب است.
fa.FinishedHeadingLabel=نصب تمام شد
fa.FinishedLabel=[name] روی کامپیوتر نصب شد. با آیکونِ دسکتاپ یا از فهرستِ برنامه‌ها بازش کنید.
fa.RunEntryExec=باز کردنِ %1
; ⚠️ همان پیامِ «The source file is corrupted»ِ دو عکسِ صاحب ریپو (۱۴۰۵/۰۷/۱۵).
; با مُهرِ سلامت، کپیِ خراب پیش از نصب گرفته می‌شود؛ این برای جایی است که فایل
; سالم بود ولی خواندنش از دیسک/فلشِ همین کامپیوتر وسطِ کار خراب درآمد.
fa.SourceIsCorrupted=فایلِ نصب هنگامِ خواندن خراب درآمد. «Cancel»/«انصراف» را بزنید، فایل را روی خودِ همین کامپیوتر (مثلاً دسکتاپ) کپی کنید و از همان‌جا اجرا کنید — نه از روی فلش. اگر باز همین شد، دوباره دانلودش کنید.

[CustomMessages]
fa.CreateDesktopIcon=ساختنِ آیکون روی دسکتاپ
fa.LaunchProgram=باز کردنِ {#AppName}

[Tasks]
; تیک‌خورده به‌صورت پیش‌فرض — خواستهٔ صاحب ریپو این بود که «روی دسکتاپ بیاید»
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "میان‌برها:"

[Dirs]
; ── جای همهٔ اطلاعات: ‎{app}\data‎ (۱۴۰۵/۰۷/۱۵) ─────────────────────────────
; «هرگز توی درایو سی نره هیچ اطلاعاتی؛ همه باید توی همون فولدرِ اپ بیان.»
; اجازهٔ نوشتن برای کاربر — حتی اگر برنامه در Program Files نصب شده باشد — و
; ⛔ uninsneveruninstall: حذفِ برنامه این پوشه را هرگز برنمی‌دارد.
Name: "{app}\data"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
; ⛔ هر دو بار داخلِ فایل‌اند؛ فقط یکی می‌نشیند (WantX64 / WantX86).
Source: "{#SourceDir64}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: WantX64
Source: "{#SourceDir86}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: WantX86
; آیکونِ میان‌برها — فایلِ جدا، نه آیکونِ داخلِ exe: ویندوز آیکونِ هر مسیر را کَش
; می‌کند و exeِ هم‌مسیر آیکونِ کهنه را نشان می‌داد (عکسِ صاحب ریپو، ۱۴۰۵/۰۷/۱۵).
Source: "..\PumpYaqobi.App\Assets\app.ico"; DestDir: "{app}"; DestName: "PumpYaqobi.ico"; Flags: ignoreversion
; ممیزیِ عرضه (۱۴۰۵/۰۷/۲۰): مجوزِ کتابخانه‌ها همراهِ برنامه — LGPLِ LibVLC و بقیه
; (ساخته از tools/third-party-notices.py؛ آزمون: ThirdPartyNoticesTests).
Source: "THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
; نامِ کهنهٔ برنامه (۱۴۰۵/۰۷/۱۵): میان‌برهای «پمپ یعقوبی» برداشته می‌شوند تا کنارِ
; «پمپ بنزین» دو آیکون نماند. ⛔ فقط همین میان‌برها — هیچ فایلِ داده‌ای.
Type: files; Name: "{autodesktop}\{#OldName}.lnk"
Type: filesandordirs; Name: "{autoprograms}\{#OldName}"
; عوض شدنِ معماری: پوشهٔ VLCِ معماریِ دیگر برداشته می‌شود (فایل‌های دیگر
; هم‌نام‌اند و روی هم نوشته می‌شوند). ⛔ هیچ چیزِ دیگری از {app} پاک نمی‌شود —
; کاربر می‌تواند پوشهٔ دلخواه انتخاب کرده باشد.
Type: filesandordirs; Name: "{app}\libvlc\win-x64"; Check: WantX86
Type: filesandordirs; Name: "{app}\libvlc\win-x86"; Check: WantX64
; و سه فایلِ ریزِ ویژهٔ هر معماری که همتای هم‌نام ندارند (۱۴۰۵/۰۷/۱۵ — از
; مقایسهٔ فهرستِ دو ساختِ واقعیِ v3.1.201). برنامه آن‌ها را بار نمی‌کند (در
; deps.jsonِ معماریِ دیگر نیستند)، ولی جا ماندنشان یعنی پوشهٔ ناپاک.
Type: files; Name: "{app}\libgcc_s_dw2-1.dll"; Check: WantX64
Type: files; Name: "{app}\Microsoft.DiaSymReader.Native.x86.dll"; Check: WantX64
Type: files; Name: "{app}\mscordaccore_x86_x86_*.dll"; Check: WantX64
Type: files; Name: "{app}\libgcc_s_seh-1.dll"; Check: WantX86
Type: files; Name: "{app}\Microsoft.DiaSymReader.Native.amd64.dll"; Check: WantX86
Type: files; Name: "{app}\mscordaccore_amd64_amd64_*.dll"; Check: WantX86

[Registry]
; کدام معماری نشسته — تا به‌روزرسانیِ بی‌صدا همان را نگه دارد
Root: HKCU; Subkey: "{#ArchKey}"; ValueType: string; ValueName: "Arch"; ValueData: "x64"; Flags: uninsdeletekey; Check: WantX64
Root: HKCU; Subkey: "{#ArchKey}"; ValueType: string; ValueName: "Arch"; ValueData: "x86"; Flags: uninsdeletekey; Check: WantX86

; ── دوبار-کلیک روی فایل‌های خودِ برنامه (۱۴۰۵/۰۷/۱۵) ─────────────────────────
; «چرا فایلِ برنامه رو که گرفتم و می‌خوام باز کنم، برنامهٔ من پیشنهاد نمی‌شه؟»
;   .pumpyaqobi ⇒ «فایلِ کاملِ برنامه»   .pumpkey ⇒ کدِ اشتراکِ آفلاین
; برنامه خودش پیش از هر کاری می‌سنجد و می‌پرسد (Services/OpenRequest.cs).
; ⚠️ انتخابِ کهنهٔ ویندوز (UserChoice) برداشته می‌شود — فقط برای همین دو پسوندِ
; خودمان؛ ساختنش را ویندوز اجازه نمی‌دهد، برداشتنش را می‌دهد.
Root: HKA; Subkey: "Software\Classes\.pumpyaqobi"; ValueType: string; ValueName: ""; ValueData: "PumpYaqobi.Full"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.pumpyaqobi\OpenWithProgids"; ValueType: string; ValueName: "PumpYaqobi.Full"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\PumpYaqobi.Full"; ValueType: string; ValueName: ""; ValueData: "فایلِ کاملِ {#AppName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\PumpYaqobi.Full\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\PumpYaqobi.ico"
Root: HKA; Subkey: "Software\Classes\PumpYaqobi.Full\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\.pumpkey"; ValueType: string; ValueName: ""; ValueData: "PumpYaqobi.Key"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.pumpkey\OpenWithProgids"; ValueType: string; ValueName: "PumpYaqobi.Key"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\PumpYaqobi.Key"; ValueType: string; ValueName: ""; ValueData: "کدِ اشتراکِ {#AppName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\PumpYaqobi.Key\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\PumpYaqobi.ico"
Root: HKA; Subkey: "Software\Classes\PumpYaqobi.Key\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""
; ── بکاپ‌های پوشهٔ «backups» (۱۴۰۵/۰۷/۱۸) ── «بکاپ‌ها آیکون ندارند و باز نمی‌شوند»
;   .pyq ⇒ پشتیبانِ رمزشدهٔ خودِ برنامه — آیکون و دوبار-کلیک ⇒ «مشاهدهٔ بکاپ» (فقط دیدن)
;   .db  ⇒ فقط در «Open with»؛ پیش‌فرضِ .db فقط وقتی گرفته می‌شود که هیچ برنامهٔ دیگری
;          صاحبش نیست (NoDbOwner) — فایلِ .dbِ برنامه‌های دیگر دست نمی‌خورد.
Root: HKA; Subkey: "Software\Classes\.pyq"; ValueType: string; ValueName: ""; ValueData: "PumpYaqobi.Backup"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.pyq\OpenWithProgids"; ValueType: string; ValueName: "PumpYaqobi.Backup"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.db\OpenWithProgids"; ValueType: string; ValueName: "PumpYaqobi.Backup"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.db"; ValueType: string; ValueName: ""; ValueData: "PumpYaqobi.Backup"; Flags: uninsdeletevalue; Check: NoDbOwner
Root: HKA; Subkey: "Software\Classes\PumpYaqobi.Backup"; ValueType: string; ValueName: ""; ValueData: "بکاپِ {#AppName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\PumpYaqobi.Backup\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\PumpYaqobi.ico"
Root: HKA; Subkey: "Software\Classes\PumpYaqobi.Backup\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".pyq"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".db"; ValueData: ""
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pyq\UserChoice"; ValueType: none; Flags: deletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".pumpyaqobi"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".pumpkey"; ValueData: ""
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pumpyaqobi\UserChoice"; ValueType: none; Flags: deletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pumpkey\UserChoice"; ValueType: none; Flags: deletekey

[Icons]
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExe}"; IconFilename: "{app}\PumpYaqobi.ico"
Name: "{group}\حذفِ {#AppName}";          Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";        Filename: "{app}\{#AppExe}"; IconFilename: "{app}\PumpYaqobi.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent
; ⛔ به‌روزرسانیِ درون‌برنامه (بی‌صدا، با ‎/RELAUNCH=1‎) ⇒ برنامه دوباره باز می‌شود (۱۴۰۵/۰۷/۱۸).
; خطِ بالا ‎skipifsilent‎ است و ‎RestartApplications‎ فقط برنامه‌ای را برمی‌گرداند که خودش
; در Restart Manager ثبت کرده — برنامهٔ ما خودش بسته می‌شود و ثبت نکرده. پس روی هر
; کامپیوتری که به‌روزرسانی از راهِ نصاب رفت، برنامه بسته می‌ماند.
; ‎runasoriginaluser‎: نصابِ بالابرده برنامه را با کاربرِ خودش باز کند، نه با مدیر.
Filename: "{app}\{#AppExe}"; Parameters: "--after-update"; Flags: nowait skipifnotsilent runasoriginaluser; Check: WantRelaunch

; ── هیچ [UninstallDelete] ای این‌جا نیست، و عمدی است ────────────────────────
; حساب‌های کاربر در ‎{app}\data‎ است (و در نصب‌های کهنه ‎%AppData%\PumpYaqobi‎). حذفِ برنامه نباید به آن دست
; بزند. اگر روزی کسی وسوسه شد این‌جا خطی بنویسد که آن پوشه را پاک کند —
; ننویسد: کلِ دفترِ حساب‌ها با یک «حذفِ برنامه» می‌رود.
;
; ⚠️ «از نو و خالی» (۱۴۰۵/۰۷/۱۳، صاحب ریپو: «کسی که از سر نصب کند رمز دارد یا
; اطلاعات داخلش هست»): حذف یک **پرسش** دارد، پیش‌فرضش «نه». «بله» پوشه را
; **کنار می‌گذارد** (نامِ تاریخ‌دار)، پاک نمی‌کند — پس نصبِ بعدی خالی است و
; هیچ داده‌ای گم نمی‌شود. پایینِ ‎[Code]‎: ‎CurUninstallStepChanged‎.

; ── هشدارِ پوشه‌ای که به‌روزرسانی را سخت می‌کند ─────────────────────────────
; کاربر آزاد است هرجا بخواهد نصب کند، ولی باید بداند چه می‌شود: داخلِ
; ‎Program Files‎ هر به‌روزرسانی اجازهٔ مدیر می‌خواهد. جلویش گرفته نمی‌شود —
; فقط گفته می‌شود. (خودِ برنامه هم در چنان پوشه‌ای اجازهٔ مدیر می‌گیرد،
; به‌جای آنکه بی‌صدا شکست بخورد.)
[Code]
function NeedsAdminFolder(Path: String): Boolean;
var
  P: String;
begin
  P := AddBackslash(Lowercase(Path));
  //  ⚠️ نصاب ۳۲بیتی است، پس {commonpf} همان «Program Files (x86)» است و
  //  «C:\Program Files»ِ ویندوزِ ۶۴ را نمی‌گرفت — سنجهٔ ویزارد (۱۴۰۵/۰۷/۱۵) دید
  //  که آن‌جا بی هیچ هشداری گذشت. {commonpf64} فقط روی ویندوزِ ۶۴ معنا دارد.
  Result := (Pos(AddBackslash(Lowercase(ExpandConstant('{commonpf}'))), P) = 1)
         or (Pos(AddBackslash(Lowercase(ExpandConstant('{commonpf32}'))), P) = 1)
         or (Pos(AddBackslash(Lowercase(ExpandConstant('{win}'))), P) = 1);
  if IsWin64 and (not Result) then
    Result := Pos(AddBackslash(Lowercase(ExpandConstant('{commonpf64}'))), P) = 1;
end;

// ── نصبِ دوباره = به‌روزرسانی · نسخهٔ کهنه‌تر پذیرفته نمی‌شود ─────────────
//  خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «برای کسایی که نت ندارن… نصاب ببیند
//  برنامه توی کامپیوتر است یا نه؛ اگر بود همان را آپدیت کند، اگر نبود خودش
//  نصب شود… و مدل‌های قدیمی را قبول نکند و جدیدها را قبول کند.»
//
//  ReadInstalled تنها جای «چه نسخه‌ای کجا نصب است» است و از ثبتِ حذفِ همان
//  AppId می‌خواند (DisplayVersion و مسیرِ نصب) — نصبِ کاربری (HKCU) و نصبِ
//  مدیر (HKLM، هر دو نمای ۳۲ و ۶۴) هر دو.
//    نصب نیست       ⇒ نصبِ تازه، مثلِ همیشه
//    کهنه‌تر نصب است ⇒ همان پوشه به‌روز می‌شود (صفحهٔ پوشه پرسیده نمی‌شود)
//    همین نسخه      ⇒ همان پوشه دوباره نوشته می‌شود (تعمیر)
//    تازه‌تر نصب است ⇒ ⛔ هیچ کاری نمی‌شود و گفته می‌شود چرا
//  ⚠️ دفتر و تنظیمات در ‎%AppData%\PumpYaqobi‎ است و نصب هیچ‌وقت به آن دست نمی‌زند.
var
  InstalledVer: String;
  InstalledDir: String;

function UninstKey(): String;
begin
  Result := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + ExpandConstant('{#AppGuid}') + '_is1';
end;

function ReadFrom(Root: Integer; var Ver, Dir: String): Boolean;
begin
  Result := RegQueryStringValue(Root, UninstKey(), 'DisplayVersion', Ver) and (Trim(Ver) <> '');
  if Result then
    if not RegQueryStringValue(Root, UninstKey(), 'Inno Setup: App Path', Dir) then Dir := '';
end;

procedure ReadInstalled();
begin
  InstalledVer := '';
  InstalledDir := '';
  if not ReadFrom(HKCU, InstalledVer, InstalledDir) then
    if not ReadFrom(HKLM, InstalledVer, InstalledDir) then
      if IsWin64 then
        if not ReadFrom(HKLM64, InstalledVer, InstalledDir) then
          InstalledVer := '';
  InstalledVer := Trim(InstalledVer);
end;

//  «3.1.200» ⇒ یک تکهٔ عددی و بقیه. تکهٔ ناخوانا صفر است.
function NextPart(var S: String): Integer;
var
  P: Integer;
  T: String;
begin
  P := Pos('.', S);
  if P = 0 then
  begin
    T := S;
    S := '';
  end
  else
  begin
    T := Copy(S, 1, P - 1);
    S := Copy(S, P + 1, Length(S));
  end;
  Result := StrToIntDef(Trim(T), 0);
end;

//  ‎-1‎ ⇒ A کهنه‌تر · ‎0‎ ⇒ برابر · ‎1‎ ⇒ A تازه‌تر (چهار تکه، مثلِ نسخهٔ ویندوز)
function CompareVer(A, B: String): Integer;
var
  I, X, Y: Integer;
begin
  Result := 0;
  for I := 1 to 4 do
  begin
    X := NextPart(A);
    Y := NextPart(B);
    if X > Y then
    begin
      Result := 1;
      Exit;
    end;
    if X < Y then
    begin
      Result := -1;
      Exit;
    end;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  ReadInstalled();
  if (InstalledVer <> '') and (CompareVer('{#AppVersion}', InstalledVer) < 0) then
  begin
    //  ⛔ کهنه روی تازه نمی‌نشیند — نه با پرسش، نه بی‌صدا. کدِ بیرون آمدن
    //  ناصفر است تا نصبِ خودکار هم بفهمد که انجام نشد.
    SuppressibleMsgBox('روی این کامپیوتر نسخهٔ تازه‌ترِ برنامه نصب است.' + #13#10 + #13#10 +
                       'نصب‌شده: ' + InstalledVer + #13#10 +
                       'این فایلِ نصب: {#AppVersion}' + #13#10 + #13#10 +
                       'فایلِ نصبِ کهنه‌تر پذیرفته نمی‌شود و هیچ چیزی عوض نشد.' + #13#10 +
                       'برای به‌روزرسانی، فایلِ نصبِ نسخهٔ تازه‌تر را بگیرید.',
                       mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

// ── «۳۲ یا ۶۴بیتی؟» — یک تصمیم، یک جا ──────────────────────────────────────
//  PickedArch تنها جای تصمیم است و هر Check از همین می‌خواند:
//    نصبِ دستی  ⇒ صفحهٔ انتخاب (پیش‌فرضش همان قاعدهٔ بی‌صدا)
//    نصبِ بی‌صدا ⇒ /ARCH=  ⇒ رجیستریِ نصبِ پیشین ⇒ ویندوز (IsWin64)
//  و روی ویندوزِ ۳۲بیتی هر چه باشد، ۳۲بیتی — ۶۴ آن‌جا اجرا نمی‌شود.
var
  ArchPage: TInputOptionWizardPage;
  FreshPage: TInputOptionWizardPage;

// ── «نصبِ خالی» (۱۴۰۵/۰۷/۱۸) ────────────────────────────────────────────────
//  خواستهٔ صاحب ریپو: «وقتی نسخهٔ جدید را می‌دهی بی رمز و اطلاعات باشد.» خودِ
//  فایلِ نصب هیچ اطلاعاتی ندارد؛ آن‌چه دیده می‌شد اطلاعاتِ همین کامپیوتر بود
//  که برنامه عمداً پیدا و کپی می‌کند (DataHome: ‎{app}\data‎ ⇐ جای آخرِ
//  ثبت‌شده در رجیستری ⇐ ‎%AppData%\PumpYaqobi‎). پس «خالی» یعنی **همهٔ** آن
//  جاها کنار گذاشته شوند، وگرنه برنامه سرِ اولین اجرا همان را برمی‌گرداند.
//  ⛔ هیچ چیزی پاک نمی‌شود — فقط نامِ تاریخ‌دار. پیش‌فرض «نگه دار» است، و
//  به‌روزرسانیِ بی‌صدا این را هرگز نمی‌بیند (فقط ‎/FRESH=1‎ی آزمون).
function DataCandidates(): TArrayOfString;
var
  R: String;
  N: Integer;
begin
  SetArrayLength(Result, 4);
  N := 0;
  if InstalledDir <> '' then begin Result[N] := AddBackslash(InstalledDir) + 'data'; N := N + 1; end;
  Result[N] := ExpandConstant('{userappdata}\PumpYaqobi'); N := N + 1;
  if RegQueryStringValue(HKCU, 'Software\PumpYaqobi', 'DataDir', R) and (Trim(R) <> '') then
  begin Result[N] := R; N := N + 1; end;
  SetArrayLength(Result, N);
end;

function AnyOldData(): Boolean;
var
  D: TArrayOfString;
  I: Integer;
begin
  Result := False;
  D := DataCandidates();
  for I := 0 to GetArrayLength(D) - 1 do
    if FileExists(AddBackslash(D[I]) + 'pump.db') or FileExists(AddBackslash(D[I]) + 'settings.json') then
      Result := True;
end;

function FreshChosen(): Boolean;
begin
  //  ⛔ برنامه نصب است ⇒ فقط به‌روزرسانی، هرگز «خالی» — حتی با ‎/FRESH=1‎
  //  (خواستهٔ صاحب ریپو: «اگه حسابی توی کامپیوتر نصب بود فقط برنامه رو
  //  بروز رسانی کنه و کار اشتباهی سر نخوره»).
  if InstalledVer <> '' then
    Result := False
  else if WizardSilent then
    Result := Trim(ExpandConstant('{param:FRESH|}')) = '1'
  else
    Result := (FreshPage <> nil) and (FreshPage.SelectedValueIndex = 1);
end;

function DefaultArch(): String;
var
  P, R: String;
begin
  P := Lowercase(Trim(ExpandConstant('{param:ARCH|}')));
  if (P = 'x86') or (P = 'x64') then
    Result := P
  else if RegQueryStringValue(HKCU, '{#ArchKey}', 'Arch', R) and ((R = 'x86') or (R = 'x64')) then
    Result := R
  else if IsWin64 then
    Result := 'x64'
  else
    Result := 'x86';
  if not IsWin64 then Result := 'x86';
end;

function PickedArch(): String;
begin
  if (ArchPage <> nil) and (not WizardSilent) then
  begin
    if ArchPage.SelectedValueIndex = 1 then Result := 'x86' else Result := 'x64';
    if not IsWin64 then Result := 'x86';
  end
  else
    Result := DefaultArch();
end;

function HasSwitch(const S: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), S) = 0 then Result := True;
end;

// فقط به‌روزرسانیِ درون‌برنامه (بی‌صدا): ‎/RELAUNCH=1‎ از ۳.۱.۲۳۱ به بعد.
//  ⛔ و نسخه‌های پیش از آن (۱۴۰۵/۰۷/۲۰): آن‌ها ‎/RELAUNCH‎ نمی‌فرستادند ولی
//  همیشه ‎/SILENT /RESTARTAPPLICATIONS‎ می‌فرستادند — پس هر کامپیوتری که از
//  نسخهٔ کهنه به‌روز می‌شد، برنامه‌اش بسته می‌ماند. نصبِ بی‌صدای دیگر (آزمون‌ها،
//  ‎/FRESH‎) هیچ‌کدام را ندارد و برنامه را باز نمی‌کند.
// ⛔ پیش‌فرضِ .db فقط وقتی هیچ برنامهٔ دیگری صاحبش نیست (۱۴۰۵/۰۷/۱۸)
function NoDbOwner(): Boolean;
var
  V: String;
begin
  Result := True;
  if RegQueryStringValue(HKCR, '.db', '', V) then
    Result := (V = '') or (V = 'PumpYaqobi.Backup');
end;

function WantRelaunch(): Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
  if not Result then
    Result := WizardSilent and HasSwitch('/RESTARTAPPLICATIONS');
end;

function WantX64(): Boolean;
begin
  Result := PickedArch() = 'x64';
end;

function WantX86(): Boolean;
begin
  Result := PickedArch() = 'x86';
end;

procedure InitializeWizard();
begin
  ArchPage := CreateInputOptionPage(wpWelcome,
    'نسخهٔ ۳۲ یا ۶۴بیتی',
    'کدام را روی این کامپیوتر نصب کنیم؟',
    'هر دو داخلِ همین فایل‌اند و همان برنامه‌اند با همان دفتر و حساب‌ها؛ فقط برای بیتیِ ویندوز فرق دارند.' + #13#10 +
    'نمی‌دانید؟ همان گزینهٔ انتخاب‌شده را نگه دارید — از روی ویندوزِ همین کامپیوتر انتخاب شده.',
    True, False);
  ArchPage.Add('۶۴بیتی — ویندوزِ امروزی (پیشنهادی)');
  ArchPage.Add('۳۲بیتی — ویندوزِ قدیمیِ ۳۲بیتی');
  if DefaultArch() = 'x86' then
    ArchPage.SelectedValueIndex := 1
  else
    ArchPage.SelectedValueIndex := 0;
  if not IsWin64 then
    ArchPage.CheckListBox.ItemEnabled[0] := False;

  //  فقط وقتی برنامه نصب نیست ولی اطلاعاتش مانده (حذف شده، یا پوشه جابه‌جا شده)
  if (InstalledVer = '') and AnyOldData() then
  begin
    FreshPage := CreateInputOptionPage(ArchPage.ID,
      'اطلاعاتِ قبلیِ همین کامپیوتر',
      'روی این کامپیوتر از قبل حساب، رمز و تنظیمات هست. با آن‌ها چه کنیم؟',
      'خودِ فایلِ نصب هیچ اطلاعاتی ندارد؛ این‌ها مالِ نصبِ قبلیِ همین کامپیوترند.' + #13#10 +
      '«نصبِ خالی» هیچ چیزی را پاک نمی‌کند — پوشهٔ اطلاعات با نامِ تاریخ‌دار کنار گذاشته می‌شود و هر وقت خواستید برمی‌گردد.',
      True, False);
    FreshPage.Add('نگه داشتنِ حساب‌ها، رمز و تنظیمات (پیشنهادی)');
    FreshPage.Add('نصبِ خالی — بی رمز و بی اطلاعات (اطلاعاتِ قبلی کنار گذاشته می‌شود)');
    FreshPage.SelectedValueIndex := 0;
  end;

  //  نصب از قبل هست ⇒ خوش‌آمد همان را بگوید، نه «نصب می‌شود»
  if InstalledVer <> '' then
  begin
    if CompareVer('{#AppVersion}', InstalledVer) = 0 then
      WizardForm.WelcomeLabel2.Caption :=
        'همین نسخه ({#AppVersion}) روی این کامپیوتر نصب است و دوباره روی همان نصب می‌شود (تعمیر).' + #13#10 + #13#10 +
        'حساب‌ها، تم و تنظیماتِ شما دست نمی‌خورند.'
    else
      WizardForm.WelcomeLabel2.Caption :=
        'برنامه روی این کامپیوتر نصب است (نسخهٔ ' + InstalledVer + ') و همان به نسخهٔ {#AppVersion} به‌روز می‌شود.' + #13#10 + #13#10 +
        'حساب‌ها، تم و تنظیماتِ شما دست نمی‌خورند.';
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  //  فقط به‌روزرسانیِ بی‌صدا نمی‌پرسد؛ هر نصبِ دستی — تازه یا روی موجود — می‌پرسد.
  Result := (PageID = ArchPage.ID) and WizardSilent;
  if (FreshPage <> nil) and (PageID = FreshPage.ID) and WizardSilent then Result := True;
  //  ⛔ صفحهٔ پوشه در هیچ نصبِ دستی رد نمی‌شود (۱۴۰۵/۰۷/۱۵، صاحب ریپو: «نصاب
  //  انتخابِ فولدر نداشت که بگم کجا یا توی کدوم درایو نصب بشه… ارور داد»).
  //  تا دیروز روی نصبِ موجود رد می‌شد و اگر پوشهٔ پیشین دیگر نوشتنی نبود
  //  (Program Files، درایوِ رفته) نصب همان‌جا می‌شکست و راهِ عوض کردنی نبود.
  //  پیش‌فرضش همان پوشهٔ پیشین است (UsePreviousAppDir)، پس «بعدی» یعنی همان.
end;

//  نصب از قبل هست ⇒ صفحهٔ پوشه همین را بگوید، نه فقط «کجا نصب شود؟»
procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpSelectDir) and (InstalledDir <> '') then
    WizardForm.SelectDirLabel.Caption :=
      'برنامه الان در این پوشه نصب است و همان‌جا به‌روز می‌شود:' + #13#10 + InstalledDir + #13#10 + #13#10 +
      'اگر درایو یا پوشهٔ دیگری می‌خواهید، «انتخابِ پوشه» را بزنید.';
end;

function SameDir(A, B: String): Boolean;
begin
  Result := CompareText(RemoveBackslashUnlessRoot(Trim(A)), RemoveBackslashUnlessRoot(Trim(B))) = 0;
end;

// ── «از نو و خالی» هنگامِ حذف — کنار گذاشتن، نه پاک کردن ─────────────────
//  پیش‌فرضِ پرسش «نه» است (MB_DEFBUTTON2)، و حذفِ بی‌صدا (به‌روزرسانی یا
//  اسکریپت) اصلاً نمی‌پرسد و دست نمی‌زند. «بله» پوشهٔ داده را با نامِ
//  تاریخ‌دار کنار می‌گذارد؛ برگرداندنش یعنی برگرداندنِ همان نام.
//  ⚠️ از ۱۴۰۵/۰۷/۱۵ اطلاعات در ‎{app}\data‎ است؛ نصب‌های کهنه هنوز جای قبلی
//  (‎%AppData%\PumpYaqobi‎) را هم دارند — هر دو کنار گذاشته می‌شوند.
function SetAside(Data: String): Boolean;
var
  Aside: String;
begin
  Result := True;
  if not DirExists(Data) then Exit;
  Aside := Data + '-kenar-' + GetDateTimeString('yyyymmdd-hhnnss', '-', '-');
  Result := RenameFile(Data, Aside);
  if not Result then
    MsgBox('پوشهٔ اطلاعات کنار گذاشته نشد — شاید برنامه هنوز باز است.' + #13#10 +
           'برنامه را ببندید و پوشهٔ زیر را خودتان تغییرِ نام دهید:' + #13#10 + Data, mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  InApp, Legacy: String;
begin
  if (CurUninstallStep = usPostUninstall) and (not UninstallSilent) then
  begin
    InApp := ExpandConstant('{app}\data');
    Legacy := ExpandConstant('{userappdata}\PumpYaqobi');
    if DirExists(InApp) or DirExists(Legacy) then
      if MsgBox('برنامه حذف شد. اطلاعاتِ این کامپیوتر (دفترِ حساب‌ها، رمز و تنظیمات) هنوز سرِ جایش است.' + #13#10 + #13#10 +
                'اگر نصبِ بعدی باید از نو و خالی باشد، «بله» را بزنید:' + #13#10 +
                'پوشهٔ اطلاعات پاک نمی‌شود — با نامِ تاریخ‌دار کنار گذاشته می‌شود و هر وقت خواستید برمی‌گردد.' + #13#10 + #13#10 +
                'اگر می‌خواهید دوباره نصب کنید و همه‌چیز همان باشد، «نه» را بزنید.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        if SetAside(InApp) and SetAside(Legacy) then
          MsgBox('اطلاعات کنار گذاشته شد. نصبِ بعدی از نو و خالی شروع می‌شود.', mbInformation, MB_OK);
  end;
end;

// ── پس از نصب: اطلاعاتِ پوشهٔ قبلی، و آیکونِ تازه (۱۴۰۵/۰۷/۱۵) ─────────────
//  ۱) نصب در پوشهٔ دیگر ⇒ ‎<قبلی>\data‎ به ‎{app}\data‎ **کپی** می‌شود (robocopy
//     بی ‎/MIR‎ و بی ‎/MOV‎ — هیچ چیزی پاک یا جابه‌جا نمی‌شود)، فقط اگر پوشهٔ
//     تازه هنوز دفتر ندارد. نشد؟ خودِ برنامه سرِ اولین اجرا همین را می‌کند.
//  ۲) ویندوز آیکونِ هر فایل را کَش می‌کند و میان‌برِ تازه آیکونِ کهنه را
//     نشان می‌داد — ‎ie4uinit‎ همان کَش را تازه می‌کند (ویندوز ۱۰/۱۱: ‎-show‎،
//     ویندوز ۷/۸: ‎-ClearIconCache‎). هر دو بی‌خطرند؛ نبودنشان هم.
function HasLedger(Dir: String): Boolean;
begin
  Result := FileExists(AddBackslash(Dir) + 'pump.db') or FileExists(AddBackslash(Dir) + 'settings.json');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  OldDir, NewDir, Tool: String;
  Code, I: Integer;
  D: TArrayOfString;
begin
  if (CurStep = ssInstall) and FreshChosen() then
  begin
    D := DataCandidates();
    if not SetAside(ExpandConstant('{app}\data')) then Log('PYFRESH app data not set aside');
    for I := 0 to GetArrayLength(D) - 1 do
      if not SameDir(D[I], ExpandConstant('{app}\data')) then
        if not SetAside(D[I]) then Log('PYFRESH not set aside: ' + D[I]);
    RegDeleteValue(HKCU, 'Software\PumpYaqobi', 'DataDir');
    Log('PYFRESH set aside');
    Exit;
  end;
  if CurStep <> ssPostInstall then Exit;

  NewDir := ExpandConstant('{app}\data');
  if (not FreshChosen()) and (InstalledDir <> '') and (not SameDir(InstalledDir, ExpandConstant('{app}'))) then
  begin
    OldDir := AddBackslash(InstalledDir) + 'data';
    if HasLedger(OldDir) and (not HasLedger(NewDir)) then
    begin
      Exec(ExpandConstant('{sys}\robocopy.exe'), '"' + OldDir + '" "' + NewDir + '" /E /R:2 /W:1 /NFL /NDL /NJH /NJS',
           '', SW_HIDE, ewWaitUntilTerminated, Code);
      Log('PYDATA copy ' + OldDir + ' => ' + NewDir + ' code=' + IntToStr(Code));
    end;
  end;

  Tool := ExpandConstant('{sys}\ie4uinit.exe');
  if FileExists(Tool) then
  begin
    Exec(Tool, '-show', '', SW_HIDE, ewWaitUntilTerminated, Code);
    Exec(Tool, '-ClearIconCache', '', SW_HIDE, ewWaitUntilTerminated, Code);
  end;
end;



// ── فایلِ نصب پیش از نصب خودش را می‌سنجد (۱۴۰۵/۰۷/۱۵) ───────────────────────
//  روی هر دو کامپیوترِ صاحب ریپو نصاب وسطِ «Extracting files» گفت «The source
//  file is corrupted»: بایت‌های همان کپی که اجرا شد با آن‌چه ساخته شد یکی نبودند
//  (فایلِ روی گیت‌هاب سالم است — هشش با SHA256SUMS.txt می‌خورد). Inno این را
//  فقط وقتی می‌فهمد که به همان تکهٔ خراب برسد: نیمه‌راه، با پیامِ انگلیسی.
//
//  ساختِ CI پس از ISCC یک مُهرِ ۸۰ بایتی ته فایل می‌گذارد (native/installer/seal.ps1):
//    PYSEAL01 + هشِ ۶۴ نویسه‌ای + PYSEAL01
//  و این‌جا، با زدنِ «نصب» و پیش از نوشتنِ هر فایلی، همان هش دوباره ساخته می‌شود.
//  ⛔ شکلِ هش مو‌به‌مو همان seal.ps1 است: تکه‌های ۴ مگابایتی ⇒ ‎SHA-256‎ِ هر تکه
//  (hexِ کوچک) ⇒ همه پشتِ هم ⇒ ‎SHA-256‎ِ همان. (Pascal Script هشِ جریانی ندارد.)
//  ⚠️ بی مُهر (ساختِ محلی) ⇒ هیچ سنجشی، مثلِ پیش. و نصبِ بی‌صدا (به‌روزرسانیِ
//  درون‌برنامه) این را نمی‌زند: آن‌جا UpdateService هشِ کلِ فایل را با
//  SHA256SUMS.txt سنجیده است.
const
  SealChunk = 4194304;
  SealLen = 80;

var
  SealBroken: Boolean;

//  0 مُهر ندارد · 1 سالم · 2 خراب · 3 خوانده نشد
function SealState(P: TOutputProgressWizardPage; var Detail: String): Integer;
var
  F: TFileStream;
  Total, Left: Int64;
  N, Done, Parts: Integer;
  Buf, Tail, Hexes: AnsiString;
  Want, Got: String;
begin
  Result := 3;
  Detail := '';
  F := nil;
  try
    //  $40 = fmShareDenyNone — خودِ Setup هم همین فایل را باز نگه داشته است
    F := TFileStream.Create(ExpandConstant('{srcexe}'), $40);
    Total := F.Size;
    Result := 0;
    if Total > SealLen then
    begin
      SetLength(Tail, SealLen);
      F.Seek(Total - SealLen, 0);
      F.ReadBuffer(Tail, SealLen);
      if (Copy(Tail, 1, 8) = 'PYSEAL01') and (Copy(Tail, 73, 8) = 'PYSEAL01') then
      begin
        Want := Lowercase(Copy(Tail, 9, 64));
        Left := Total - SealLen;
        Parts := (Left + SealChunk - 1) div SealChunk;
        Done := 0;
        Hexes := '';
        F.Seek(0, 0);
        Result := 3;
        while Left > 0 do
        begin
          if Left > SealChunk then N := SealChunk else N := Left;
          SetLength(Buf, N);
          F.ReadBuffer(Buf, N);
          Hexes := Hexes + Lowercase(GetSHA256OfString(Buf));
          Left := Left - N;
          Done := Done + 1;
          if P <> nil then P.SetProgress(Done, Parts);
        end;
        Buf := '';
        Got := Lowercase(GetSHA256OfString(Hexes));
        if Got = Want then Result := 1 else Result := 2;
      end;
    end;
  except
    Detail := GetExceptionMessage;
    Result := 3;
  end;
  if F <> nil then F.Free;
end;

function SealOk(): Boolean;
var
  P: TOutputProgressWizardPage;
  State: Integer;
  Detail: String;
begin
  P := CreateOutputProgressPage('سنجشِ فایلِ نصب',
    'پیش از نصب، سالم بودنِ خودِ همین فایل سنجیده می‌شود…');
  P.SetText('فایلِ نصب خوانده و با مُهرِ سلامتش سنجیده می‌شود.', '');
  P.SetProgress(0, 1);
  P.Show;
  try
    State := SealState(P, Detail);
  finally
    P.Hide;
  end;
  //  ⚠️ نوشتهٔ لاگ لاتین است تا سنجهٔ CI با هر کدگذاریِ لاگ پیدایش کند
  Log('PYSEAL state=' + IntToStr(State) + ' ' + Detail);
  Result := (State = 0) or (State = 1);
  if Result then Exit;

  SealBroken := True;
  if State = 2 then
    MsgBox('این فایلِ نصب خراب است و نصب انجام نمی‌شود.' + #13#10 + #13#10 +
           'فایل پس از ساخته شدن عوض شده — معمولاً هنگامِ دانلود یا کپی روی فلش.' + #13#10 +
           'هیچ چیزی روی این کامپیوتر نوشته نشد و حساب‌های شما دست نخوردند.' + #13#10 + #13#10 +
           'راهِ درست:' + #13#10 +
           '۱) فایل را دوباره از لینکِ دانلود بگیرید (اگر دانلودکننده دارید، بی آن).' + #13#10 +
           '۲) آن را روی خودِ همین کامپیوتر بگذارید (مثلاً دسکتاپ) و از همان‌جا اجرا کنید،' + #13#10 +
           '   نه مستقیم از روی فلش.', mbError, MB_OK)
  else
    MsgBox('این فایلِ نصب خوانده نشد و نصب انجام نمی‌شود:' + #13#10 + Detail + #13#10 + #13#10 +
           'فایل را روی خودِ همین کامپیوتر کپی کنید (مثلاً دسکتاپ) و از همان‌جا اجرا کنید؛' + #13#10 +
           'اگر باز همین شد، دوباره دانلودش کنید.', mbError, MB_OK);
  //  بی پرسشِ «بیرون بروم؟» (CancelButtonClick پایین) — ماندن فایده‌ای ندارد
  WizardForm.Close;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if SealBroken then Confirm := False;
end;

// ── پوشه‌ای که نصب در آن نمی‌نشیند، همین‌جا گفته می‌شود — نه وسطِ نصب ─────
//  ⛔ هر کدام کاربر را روی همین صفحه نگه می‌دارد تا جای دیگری انتخاب کند؛
//  هیچ‌کدام نصب را نمی‌بندد. و نصبِ بی‌صدا این صفحه را نمی‌بیند (/DIR).
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Dir, Drive: String;
begin
  Result := True;
  if (CurPageID = wpReady) and (not WizardSilent) then
  begin
    Result := SealOk();
    Exit;
  end;
  if (FreshPage <> nil) and (CurPageID = FreshPage.ID) and (not WizardSilent) and (FreshPage.SelectedValueIndex = 1) then
  begin
    Result := MsgBox('نصبِ خالی: حساب‌ها، رمز و تنظیماتِ این کامپیوتر در برنامه دیده نمی‌شوند.' + #13#10 +
                     'پاک نمی‌شوند — با نامِ تاریخ‌دار کنار گذاشته می‌شوند.' + #13#10 + #13#10 +
                     'همین را می‌خواهید؟', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
    Exit;
  end;
  if CurPageID <> wpSelectDir then Exit;
  //  ⛔ نصبِ بی‌صدا (به‌روزرسانی، اسکریپت) هیچ پنجره‌ای نمی‌بیند: ‎MsgBox‎ با
  //  ‎/SUPPRESSMSGBOXES‎ پنهان نمی‌شود و نصاب تا ابد منتظرِ کلیک می‌ماند
  //  (سنجهٔ ‎installer-check‎ با نصبِ بی‌صدا در پوشهٔ دیگر گرفتش، ۱۴۰۵/۰۷/۱۵).
  if WizardSilent then Exit;
  Dir := WizardDirValue;

  //  درایوی که نیست (فلشِ جداشده، درایوِ شبکهٔ قطع) ⇒ «نمی‌توان پوشه ساخت»ِ وسطِ نصب
  Drive := ExtractFileDrive(Dir);
  if (Drive = '') or (not DirExists(AddBackslash(Drive))) then
  begin
    MsgBox('درایوِ «' + Drive + '» روی این کامپیوتر پیدا نشد.' + #13#10 + #13#10 +
           'درایو یا پوشهٔ دیگری انتخاب کنید.', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  if NeedsAdminFolder(Dir) then
  begin
    if not IsAdmin then
    begin
      //  نصب بی اجازهٔ مدیر اجرا شده ⇒ این‌جا نوشتنی نیست و نصب با «دسترسی رد
      //  شد» می‌شکست. همین‌جا گفته می‌شود و صفحه می‌ماند.
      MsgBox('این پوشه بی اجازهٔ مدیرِ ویندوز نوشتنی نیست و نصب در آن انجام نمی‌شود:' + #13#10 + Dir + #13#10 + #13#10 +
             'پوشهٔ دیگری انتخاب کنید — مثلاً پوشهٔ پیشنهادی، یا پوشه‌ای در درایوِ D.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    MsgBox('این پوشه اجازهٔ مدیر می‌خواهد.' + #13#10 + #13#10 +
           'برنامه همان‌جا نصب می‌شود و کار می‌کند، ولی هر بار که خودش را' + #13#10 +
           'به‌روز می‌کند ویندوز اجازهٔ مدیر می‌پرسد.' + #13#10 + #13#10 +
           'اگر می‌خواهید به‌روزرسانی بی‌پرسش انجام شود، پوشهٔ پیشنهادی را' + #13#10 +
           'نگه دارید.', mbInformation, MB_OK);
  end;

  //  نصب از قبل جای دیگری است ⇒ آن‌جا می‌ماند؛ گفته می‌شود تا دو نسخه بی‌خبر نماند.
  //  ⚠️ حساب‌ها در ‎<پوشهٔ قبلی>\data‎اند و پس از نصب به پوشهٔ تازه **کپی** می‌شوند
  //  (CurStepChanged پایین) — پوشهٔ قبلی دست نمی‌خورد.
  if (InstalledDir <> '') and DirExists(InstalledDir) and (not SameDir(InstalledDir, Dir)) then
    if MsgBox('برنامه الان در این پوشه نصب است:' + #13#10 + InstalledDir + #13#10 + #13#10 +
              'حساب‌ها و تنظیمات به پوشهٔ تازه کپی می‌شوند و پوشهٔ قبلی دست نمی‌خورد.' + #13#10 + #13#10 +
              'در پوشهٔ تازه نصب شود؟', mbConfirmation, MB_YESNO) <> IDYES then
      Result := False;
end;
