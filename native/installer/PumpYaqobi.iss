; ═══════════════════════════════════════════════════════════════════════════
;  نصب‌کنندهٔ «پمپ یعقوبی» — Inno Setup
; ═══════════════════════════════════════════════════════════════════════════
;
;  تا امروز انتشار فقط یک زیپ بود: کاربر باید خودش جایی بازش می‌کرد و
;  ‎PumpYaqobi.exe‎ را پیدا می‌کرد. نه آیکونی روی دسکتاپ می‌آمد، نه در فهرستِ
;  برنامه‌ها پیدا می‌شد، نه در «برنامه‌ها و قابلیت‌ها» بود که بشود حذفش کرد.
;  گلایهٔ صاحب ریپو دقیقاً همین بود: «نصب هم نمی‌شد».
;
;  ── چرا نصبِ «برای همین کاربر» و نه Program Files ─────────────────────────
;  برنامه خودش را به‌روز می‌کند: فایل‌های تازه را می‌گیرد و جای کهنه‌ها
;  می‌گذارد. داخلِ ‎Program Files‎ این کار هر بار اجازهٔ مدیر می‌خواهد و
;  به‌روزرسانیِ خودکار عملاً می‌میرد. پس مثلِ کروم، وی‌اس‌کد و اسلک، در
;  ‎%LocalAppData%\Programs‎ نصب می‌شود: بی اجازهٔ مدیر نصب می‌شود و
;  به‌روزرسانی هم بی دردسر کار می‌کند.
;
;  ── دادهٔ کاربر ────────────────────────────────────────────────────────────
;  دیتابیس در ‎%AppData%\PumpYaqobi\pump.db‎ است — بیرونِ پوشهٔ نصب. پس
;  به‌روزرسانی و حتی حذفِ برنامه هیچ دست به حساب‌ها نمی‌زند. حذف‌کننده هم
;  عمداً آن پوشه را پاک نمی‌کند (پایینِ فایل، توضیحِ UninstallDelete).
; ═══════════════════════════════════════════════════════════════════════════

#define AppName "پمپ یعقوبی"
#define AppExe  "PumpYaqobi.exe"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

[Setup]
AppId={{8E86F349-343C-4FFB-983E-BBDDC5390081}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
VersionInfoVersion={#AppVersion}

; نصب برای همین کاربر — بی اجازهٔ مدیر
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={localappdata}\Programs\PumpYaqobi
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; کاربر پوشهٔ نصب را انتخاب نکند: جایش مهم است (به‌روزرسانیِ خودکار)
DisableDirPage=yes

OutputDir=..\..\rel
OutputBaseFilename=PumpYaqobi-Setup
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

[Languages]
Name: "fa"; MessagesFile: "compiler:Default.isl"

; ── نوشته‌های فارسی ────────────────────────────────────────────────────────
; Inno ترجمهٔ رسمیِ فارسی ندارد، پس همان چند جمله‌ای که کاربر واقعاً می‌بیند
; این‌جا بازنویسی می‌شود. بقیه انگلیسیِ پیش‌فرض می‌ماند.
[Messages]
fa.SetupAppTitle=نصبِ {#AppName}
fa.SetupWindowTitle=نصبِ {#AppName}
fa.WelcomeLabel1=نصبِ [name]
fa.WelcomeLabel2=[name/ver] روی این کامپیوتر نصب می‌شود.%n%nحساب‌های شما جدا از برنامه نگه داشته می‌شوند، پس به‌روزرسانی و حتی حذفِ برنامه به آن‌ها دست نمی‌زند.
fa.ClickNext=برای ادامه «Next» را بزنید.
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

[CustomMessages]
fa.CreateDesktopIcon=ساختنِ آیکون روی دسکتاپ
fa.LaunchProgram=باز کردنِ {#AppName}

[Tasks]
; تیک‌خورده به‌صورت پیش‌فرض — خواستهٔ صاحب ریپو این بود که «روی دسکتاپ بیاید»
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "میان‌برها:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExe}"
Name: "{group}\حذفِ {#AppName}";          Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";        Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent

; ── هیچ [UninstallDelete] ای این‌جا نیست، و عمدی است ────────────────────────
; حساب‌های کاربر در ‎%AppData%\PumpYaqobi‎ است. حذفِ برنامه نباید به آن دست
; بزند. اگر روزی کسی وسوسه شد این‌جا خطی بنویسد که آن پوشه را پاک کند —
; ننویسد: کلِ دفترِ حساب‌ها با یک «حذفِ برنامه» می‌رود.
