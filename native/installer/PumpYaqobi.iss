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
#define AppName "پمپ یعقوبی"
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

; نصب برای همین کاربر — بی اجازهٔ مدیر
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={localappdata}\Programs\{#InstallFolder}
DefaultGroupName={#AppName}
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
; ⛔ هر دو بار داخلِ فایل‌اند؛ فقط یکی می‌نشیند (WantX64 / WantX86).
Source: "{#SourceDir64}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: WantX64
Source: "{#SourceDir86}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: WantX86

[InstallDelete]
; عوض شدنِ معماری: پوشهٔ VLCِ معماریِ دیگر برداشته می‌شود (فایل‌های دیگر
; هم‌نام‌اند و روی هم نوشته می‌شوند). ⛔ هیچ چیزِ دیگری از {app} پاک نمی‌شود —
; کاربر می‌تواند پوشهٔ دلخواه انتخاب کرده باشد.
Type: filesandordirs; Name: "{app}\libvlc\win-x64"; Check: WantX86
Type: filesandordirs; Name: "{app}\libvlc\win-x86"; Check: WantX64

[Registry]
; کدام معماری نشسته — تا به‌روزرسانیِ بی‌صدا همان را نگه دارد
Root: HKCU; Subkey: "{#ArchKey}"; ValueType: string; ValueName: "Arch"; ValueData: "x64"; Flags: uninsdeletekey; Check: WantX64
Root: HKCU; Subkey: "{#ArchKey}"; ValueType: string; ValueName: "Arch"; ValueData: "x86"; Flags: uninsdeletekey; Check: WantX86

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
  P := Lowercase(Path);
  Result := (Pos(Lowercase(ExpandConstant('{commonpf}')), P) = 1)
         or (Pos(Lowercase(ExpandConstant('{commonpf32}')), P) = 1)
         or (Pos(Lowercase(ExpandConstant('{win}')), P) = 1);
end;

// ── «۳۲ یا ۶۴بیتی؟» — یک تصمیم، یک جا ──────────────────────────────────────
//  PickedArch تنها جای تصمیم است و هر Check از همین می‌خواند:
//    نصبِ دستی  ⇒ صفحهٔ انتخاب (پیش‌فرضش همان قاعدهٔ بی‌صدا)
//    نصبِ بی‌صدا ⇒ /ARCH=  ⇒ رجیستریِ نصبِ پیشین ⇒ ویندوز (IsWin64)
//  و روی ویندوزِ ۳۲بیتی هر چه باشد، ۳۲بیتی — ۶۴ آن‌جا اجرا نمی‌شود.
var
  ArchPage: TInputOptionWizardPage;

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
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  //  فقط به‌روزرسانیِ بی‌صدا نمی‌پرسد؛ هر نصبِ دستی — تازه یا روی موجود — می‌پرسد.
  Result := (PageID = ArchPage.ID) and WizardSilent;
end;

// ── «از نو و خالی» هنگامِ حذف — کنار گذاشتن، نه پاک کردن ─────────────────
//  پیش‌فرضِ پرسش «نه» است (MB_DEFBUTTON2)، و حذفِ بی‌صدا (به‌روزرسانی یا
//  اسکریپت) اصلاً نمی‌پرسد و دست نمی‌زند. «بله» پوشهٔ داده را با نامِ
//  تاریخ‌دار کنار می‌گذارد؛ برگرداندنش یعنی برگرداندنِ همان نام.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Data, Aside: String;
begin
  if (CurUninstallStep = usPostUninstall) and (not UninstallSilent) then
  begin
    Data := ExpandConstant('{userappdata}\PumpYaqobi');
    if DirExists(Data) then
      if MsgBox('برنامه حذف شد. اطلاعاتِ این کامپیوتر (دفترِ حساب‌ها، رمز و تنظیمات) هنوز سرِ جایش است.' + #13#10 + #13#10 +
                'اگر نصبِ بعدی باید از نو و خالی باشد، «بله» را بزنید:' + #13#10 +
                'پوشهٔ اطلاعات پاک نمی‌شود — با نامِ تاریخ‌دار کنار گذاشته می‌شود و هر وقت خواستید برمی‌گردد.' + #13#10 + #13#10 +
                'اگر می‌خواهید دوباره نصب کنید و همه‌چیز همان باشد، «نه» را بزنید.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        Aside := Data + '-kenar-' + GetDateTimeString('yyyymmdd-hhnnss', '-', '-');
        if RenameFile(Data, Aside) then
          MsgBox('اطلاعات کنار گذاشته شد:' + #13#10 + Aside + #13#10 + #13#10 +
                 'نصبِ بعدی از نو و خالی شروع می‌شود.', mbInformation, MB_OK)
        else
          MsgBox('پوشهٔ اطلاعات کنار گذاشته نشد — شاید برنامه هنوز باز است.' + #13#10 +
                 'برنامه را ببندید و پوشهٔ زیر را خودتان تغییرِ نام دهید:' + #13#10 + Data, mbError, MB_OK);
      end;
  end;
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
