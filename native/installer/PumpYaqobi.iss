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

; ── دو معماری، یک اسکریپت ──────────────────────────────────────────────────
;  خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «۳۲ بیت و ۶۴ بیت، چون کامپیوتر خیلی نسخه
;  قدیمی است.» («۸۶ بیت» وجود ندارد — x86 همان ۳۲بیتی است.)
;
;  ⛔ AppIdِ ۶۴بیتی **یک حرف هم عوض نشد**. عوض شدنش یعنی هر نصبی که همین
;     حالا دستِ مشتری است برای ویندوز یک برنامهٔ «غریبه» می‌شود: نصبِ تازه
;     رویش نمی‌نشیند، پوشهٔ قبلی پیدا نمی‌شود، و کاربر دو ردیف در «برنامه‌ها
;     و قابلیت‌ها» می‌بیند.
;  ⛔ و ۳۲بیتی AppIdِ **جدا** و پوشهٔ **جدا** دارد: Inno فایل‌های کهنه را پاک
;     نمی‌کند، پس نشستنِ ۳۲بیتی روی نصبِ ۶۴بیتی یعنی یک پوشه با بارِ بومیِ هر
;     دو معماری و ~۱۰۰ مگابایت آشغالِ بی‌مصرف.
#ifndef Arch
  #define Arch "x64"
#endif
#if Arch == "x86"
  #define AppName "پمپ یعقوبی (۳۲بیتی)"
  #define AppGuid "{{1D5B7C42-9E38-4A61-B0F7-2C83A6D41E95}"
  #define InstallFolder "PumpYaqobi-32"
  #define OutName "PumpYaqobi-Setup-x86"
#else
  #define AppName "پمپ یعقوبی"
  #define AppGuid "{{8E86F349-343C-4FFB-983E-BBDDC5390081}"
  #define InstallFolder "PumpYaqobi"
  #define OutName "PumpYaqobi-Setup"
#endif
#define AppExe  "PumpYaqobi.exe"
; کلیدِ «برنامه‌ها و قابلیت‌ها»ی نسخهٔ ۳۲بیتی — تا نصابِ ۶۴ بفهمد کدام از قبل هست
#define X86UninstallKey "Software\Microsoft\Windows\CurrentVersion\Uninstall\{1D5B7C42-9E38-4A61-B0F7-2C83A6D41E95}_is1"

; ── «۳۲ یا ۶۴بیتی؟» — پرسشِ نصبِ تازه (۱۴۰۵/۰۷/۱۴) ─────────────────────────
;  خواستهٔ صاحب ریپو: «موقعِ نصبِ برنامه بگه کدوم رو می‌خوای، ۳۲ بیت یا ۶۴
;  بیت — توی جایی که تازه روی کامپیوتر نصب می‌کنی این مرحله هم باشه.»
;  فقط نصابِ اصلی (۶۴بیتی، همان فایلی که همه می‌گیرند) می‌پرسد، و فقط وقتی
;  ساختِ CI هشِ نصابِ ۳۲بیتیِ **همین** انتشار را داده (OtherSha). انتخابِ ۳۲
;  ⇒ همان فایل از همان انتشار گرفته، هشش سنجیده و اجرا می‌شود.
;  ⛔ بارِ ۳۲بیتی داخلِ این فایل نیست (۷۸ مگابایت بیشتر برای هر نصب و هر
;     به‌روزرسانیِ بی‌صدا)؛ و ⛔ فایلی که هشش جور نباشد هرگز اجرا نمی‌شود.
#ifdef OtherSha
  #define OtherShaOr OtherSha
#else
  #define OtherShaOr ""
#endif
#ifndef ReleaseBase
  #define ReleaseBase "https://github.com/vil3ntec-it/pump-staion-yaqobi/releases/download"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
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

// ── فایلِ ۶۴بیتی روی ویندوزِ ۳۲بیتی ────────────────────────────────────────
//  خودِ نصاب ۳۲بیتی است، پس روی ویندوزِ ۳۲بیتی **باز می‌شود** و بی این
//  نگهبان، باری را می‌نشاند که هرگز اجرا نمی‌شود: کاربر آیکون را می‌زند و
//  ویندوز فقط می‌گوید «این برنامه روی این کامپیوتر اجرا نمی‌شود» — بی این‌که
//  بگوید چه باید بکند. پس همان اول، با راهِ حل، گفته می‌شود.
//  ⚠️ `IsWin64` در همهٔ نسخه‌های Inno 6 هست؛ `ArchitecturesAllowed` املایش
//     بینِ ۶٫۲ و ۶٫۳ عوض شد و به نسخهٔ رانر بند می‌شد.
function InitializeSetup(): Boolean;
begin
  Result := True;
#if Arch != "x86"
  //  با پرسشِ «۳۲ یا ۶۴»، ویندوزِ ۳۲بیتی دیگر بن‌بست نیست: همان صفحه فقط
  //  ۳۲بیتی را پیشنهاد می‌کند. بی‌صدا (به‌روزرسانی) همان نگهبانِ قدیم.
  if (not IsWin64) and (WizardSilent or ('{#OtherShaOr}' = '')) then
  begin
    MsgBox('این فایل برای ویندوزِ ۶۴بیتی ساخته شده و ویندوزِ این کامپیوتر ۳۲بیتی است.' + #13#10 + #13#10 +
           'فایلِ «PumpYaqobi-Setup-x86.exe» را از همان صفحهٔ دانلود بگیرید —' + #13#10 +
           'همین برنامه است، برای ویندوزِ ۳۲بیتی.', mbError, MB_OK);
    Result := False;
  end;
#endif
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



// ── «۳۲ یا ۶۴بیتی؟» — در هر نصبِ دستی ────────────────────────────────────────
//  خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «۳۲ و ۶۴ بیت هم موقعِ مراحلِ نصب سؤال
//  بشه.» تا ۳.۱.۱۸۵ فقط نصبِ تازه می‌پرسید و کسی که روی برنامهٔ موجود نصب
//  می‌کرد هیچ‌وقت نمی‌دیدش.
//  ⛔ فقط نصبِ بی‌صدا (به‌روزرسانیِ خودِ برنامه) این صفحه را نمی‌بیند.
//  ⛔ و پیش‌فرض هیچ‌وقت برنامهٔ دوم کنارِ اولی نمی‌نشاند: اگر همین کامپیوتر
//     نسخهٔ ۳۲بیتی دارد و ۶۴ نه، ۳۲ برگزیده است؛ وگرنه آن‌چه ویندوز می‌خواهد
//     (IsWin64). روی ویندوزِ ۳۲بیتی گزینهٔ ۶۴ بسته است.
#if Arch != "x86"
#ifdef OtherSha
var
  ArchPage: TInputOptionWizardPage;
  DownloadPage: TDownloadWizardPage;
  HandedOff: Boolean;

procedure InitializeWizard();
begin
  ArchPage := CreateInputOptionPage(wpWelcome,
    'نسخهٔ ۳۲ یا ۶۴بیتی',
    'کدام را روی این کامپیوتر نصب کنیم؟',
    'هر دو همان برنامه‌اند با همان دفتر و حساب‌ها؛ فقط برای بیتیِ ویندوز فرق دارند.' + #13#10 +
    'نمی‌دانید؟ همان گزینهٔ انتخاب‌شده را نگه دارید — از روی ویندوزِ همین کامپیوتر انتخاب شده.',
    True, False);
  ArchPage.Add('۶۴بیتی — ویندوزِ امروزی (پیشنهادی)');
  ArchPage.Add('۳۲بیتی — ویندوزِ قدیمیِ ۳۲بیتی');
  if IsWin64 and (WizardForm.PrevAppDir = '') and
     (RegKeyExists(HKCU, '{#X86UninstallKey}') or RegKeyExists(HKLM, '{#X86UninstallKey}')) then
    ArchPage.SelectedValueIndex := 1
  else if IsWin64 then
    ArchPage.SelectedValueIndex := 0
  else
  begin
    ArchPage.SelectedValueIndex := 1;
    ArchPage.CheckListBox.ItemEnabled[0] := False;
  end;
  DownloadPage := CreateDownloadPage('گرفتنِ نسخهٔ ۳۲بیتی',
    'نصابِ ۳۲بیتیِ همین نسخه گرفته می‌شود…', nil);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = ArchPage.ID then
    Result := WizardSilent;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if HandedOff then Confirm := False;
end;

function RunOther(): Boolean;
var
  Code: Integer;
begin
  Result := False;
  DownloadPage.Clear;
  DownloadPage.Add('{#ReleaseBase}/v{#AppVersion}/PumpYaqobi-Setup-x86.exe',
                   'PumpYaqobi-Setup-x86.exe', '{#OtherSha}');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
      Result := True;
    except
      MsgBox('نصابِ ۳۲بیتی گرفته نشد — اینترنت نیست یا فایل ناقص رسید.' + #13#10 + #13#10 +
             'فایلِ «PumpYaqobi-Setup-x86.exe» را از همان صفحهٔ دانلود بگیرید و بزنید،' + #13#10 +
             'یا این‌جا ۶۴بیتی را انتخاب کنید.', mbError, MB_OK);
    end;
  finally
    DownloadPage.Hide;
  end;
  if not Result then exit;
  //  منتظر می‌مانیم تا تمام شود: {tmp} با بسته شدنِ همین نصاب پاک می‌شود.
  WizardForm.Hide;
  Exec(ExpandConstant('{tmp}\PumpYaqobi-Setup-x86.exe'), '', '', SW_SHOW, ewWaitUntilTerminated, Code);
  HandedOff := True;
  WizardForm.Close;
end;
#endif
#endif

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
#if Arch != "x86"
#ifdef OtherSha
  if (CurPageID = ArchPage.ID) and (ArchPage.SelectedValueIndex = 1) then
  begin
    RunOther();
    Result := False;
    exit;
  end;
#endif
#endif
  if (CurPageID = wpSelectDir) and NeedsAdminFolder(WizardDirValue) then
    MsgBox('این پوشه اجازهٔ مدیر می‌خواهد.' + #13#10 + #13#10 +
           'برنامه همان‌جا نصب می‌شود و کار می‌کند، ولی هر بار که خودش را' + #13#10 +
           'به‌روز می‌کند ویندوز اجازهٔ مدیر می‌پرسد.' + #13#10 + #13#10 +
           'اگر می‌خواهید به‌روزرسانی بی‌پرسش انجام شود، پوشهٔ پیشنهادی را' + #13#10 +
           'نگه دارید.', mbInformation, MB_OK);
end;
