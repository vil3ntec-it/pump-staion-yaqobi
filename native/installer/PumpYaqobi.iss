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
; صفحهٔ «انتخابِ پوشه» باز است — خواستهٔ صاحب ریپو: «بشه انتخاب کرد که کجا
; فایل‌ها رو ببرم بزارم موقع نصب». پیش‌فرض همان بالاست و اگر جای دیگری
; انتخاب شود، کدِ پایینِ فایل نتیجه‌اش را می‌گوید.
DisableDirPage=no
; نصبِ دوباره/به‌روزرسانی به همان پوشه‌ای می‌رود که کاربر بارِ اول انتخاب کرد
UsePreviousAppDir=yes

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
fa.WizardSelectDir=پوشهٔ نصب
fa.SelectDirDesc=[name] کجا نصب شود؟
fa.SelectDirLabel3=[name] در پوشهٔ زیر نصب می‌شود.
fa.SelectDirBrowseLabel=برای ادامه «Next» را بزنید. برای انتخابِ پوشهٔ دیگر «Browse» را بزنید.
fa.DiskSpaceGBLabel=دستِ‌کم [gb] گیگابایت جای خالی لازم است.
fa.DiskSpaceMBLabel=دستِ‌کم [mb] مگابایت جای خالی لازم است.
fa.ButtonBrowse=&انتخابِ پوشه
fa.DirNotEmpty=پوشهٔ «%1» خالی نیست. باز هم همان‌جا نصب شود؟
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
  P := Lowercase(Path);
  Result := (Pos(Lowercase(ExpandConstant('{commonpf}')), P) = 1)
         or (Pos(Lowercase(ExpandConstant('{commonpf32}')), P) = 1)
         or (Pos(Lowercase(ExpandConstant('{win}')), P) = 1);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and NeedsAdminFolder(WizardDirValue) then
    MsgBox('این پوشه اجازهٔ مدیر می‌خواهد.' + #13#10 + #13#10 +
           'برنامه همان‌جا نصب می‌شود و کار می‌کند، ولی هر بار که خودش را' + #13#10 +
           'به‌روز می‌کند ویندوز اجازهٔ مدیر می‌پرسد.' + #13#10 + #13#10 +
           'اگر می‌خواهید به‌روزرسانی بی‌پرسش انجام شود، پوشهٔ پیشنهادی را' + #13#10 +
           'نگه دارید.', mbInformation, MB_OK);
end;
