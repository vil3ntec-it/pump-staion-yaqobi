package top.yaqobipump.app;

import android.Manifest;
import android.annotation.SuppressLint;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.app.DownloadManager;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.webkit.CookieManager;
import android.webkit.JavascriptInterface;
import android.webkit.MimeTypeMap;
import android.webkit.URLUtil;
import android.webkit.PermissionRequest;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;

import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.io.Writer;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

/**
 * پوستهٔ اندرویدِ برنامهٔ پمپ.
 *
 * خودِ برنامه (index.html و دارایی‌هایش) داخلِ همین فایلِ نصب است و از پوشهٔ
 * assets بار می‌شود — پس بدونِ اینترنت هم کامل کار می‌کند و هیچ نوارِ آدرس یا
 * سربرگِ مرورگری بالای صفحه دیده نمی‌شود.
 *
 * آپدیتِ خودکار (بدونِ نصبِ دوباره)
 * ---------------------------------
 * وقتی سایت نسخهٔ تازه‌ای منتشر کند، خودِ برنامه آن را می‌گیرد و با پلِ
 * {@code PumpAndroid} در پوشهٔ خصوصیِ برنامه می‌نویسد؛ از دفعهٔ بعد همان نسخهٔ
 * تازه بالا می‌آید. نسخهٔ همراهِ نصب همیشه سرِ جایش می‌ماند، پس اگر آپدیت خراب
 * باشد برنامه خودش به نسخهٔ سالم برمی‌گردد و کاربر هیچ‌وقت مجبور نیست برنامه را
 * پاک و دوباره نصب کند:
 *   • هر بار که نسخهٔ آپدیت‌شده بالا می‌آید، شمارندهٔ «تلاش» یکی زیاد می‌شود.
 *   • اگر برنامه سالم بالا بیاید، خودش {@code updateOk()} را صدا می‌زند و
 *     شمارنده صفر می‌شود.
 *   • اگر دو بار پشتِ سرِ هم بالا نیاید (شمارنده به ۲ برسد)، فایلِ آپدیت پاک
 *     می‌شود و نسخهٔ همراهِ نصب بار می‌شود.
 */
public class MainActivity extends Activity {

  private static final String ASSET_URL = "file:///android_asset/www/index.html";
  private static final String PREFS = "pump-shell";
  private static final String KEY_TRIES = "updTries";
  private static final String KEY_VERSION = "updVersion";
  private static final int MAX_TRIES = 2;
  private static final int MIC_REQ = 4711;
  private static final int NOTIF_REQ = 4712;

  private WebView web;
  private boolean usingUpdate = false;

  // ── لودینگِ واقعی ────────────────────────────────────────────────────────
  // نوارش همان درصدی است که WebView واقعاً بار کرده، و با «آماده»ی خودِ برنامه
  // برداشته می‌شود — نه با یک تایمر. پیش از این هیچ لودینگی نبود و کاربر چند
  // ثانیه یک صفحهٔ خالیِ تیره می‌دید.
  private View splash;
  private ProgressBar splashBar;
  private TextView splashStage;
  private boolean splashGone = false;
  private static final int SPLASH_MAX_MS = 30000;

  @SuppressLint("SetJavaScriptEnabled")
  @Override
  protected void onCreate(Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);

    web = new WebView(this);
    FrameLayout root = new FrameLayout(this);
    root.addView(web, new FrameLayout.LayoutParams(
        ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
    root.addView(buildSplash(), new FrameLayout.LayoutParams(
        ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
    setContentView(root);

    WebSettings s = web.getSettings();
    s.setJavaScriptEnabled(true);
    s.setDomStorageEnabled(true);          // localStorage
    s.setDatabaseEnabled(true);            // IndexedDB
    s.setAllowFileAccess(true);
    s.setAllowContentAccess(true);
    s.setMediaPlaybackRequiresUserGesture(false);
    s.setUseWideViewPort(true);
    s.setLoadWithOverviewMode(true);
    s.setSupportZoom(false);
    s.setBuiltInZoomControls(false);
    s.setCacheMode(WebSettings.LOAD_DEFAULT);
    tuneForSpeed(s);

    web.setWebViewClient(new WebViewClient());
    web.addJavascriptInterface(new Bridge(), "PumpAndroid");
    setupDownloads();

    // اجازهٔ مایکروفون برای ضبطِ پیام صوتی و جستجوی صوتی
    web.setWebChromeClient(new WebChromeClient() {
      @Override
      public void onPermissionRequest(final PermissionRequest request) {
        runOnUiThread(() -> request.grant(request.getResources()));
      }
      /** درصدِ واقعیِ بار شدنِ صفحه — تا ۸۵٪ِ نوار، بقیه‌اش دستِ خودِ برنامه است */
      @Override
      public void onProgressChanged(WebView v, int p) {
        bootProgress(Math.min(85, (p * 85) / 100), p < 100 ? "خواندنِ فایلِ برنامه…" : "راه‌اندازیِ برنامه…");
      }
    });

    requestNeededPermissions();

    web.loadUrl(pickStartUrl());
    // اگر نسخهٔ بالا‌آمده آن‌قدر کهنه باشد که «آماده» را خبر ندهد، کسی پشتِ
    // صفحهٔ لودینگ گیر نکند
    web.postDelayed(this::hideSplash, SPLASH_MAX_MS);
  }

  /**
   * صفحهٔ لودینگ — بدونِ فایلِ layout ساخته می‌شود تا هیچ وابستگیِ تازه‌ای به
   * پروژه اضافه نشود. نوارش «تعیین‌شده» است، نه چرخانِ بی‌پایان: عددش همان
   * چیزی است که واقعاً بار شده.
   */
  private View buildSplash() {
    LinearLayout box = new LinearLayout(this);
    box.setOrientation(LinearLayout.VERTICAL);
    box.setGravity(Gravity.CENTER);
    box.setBackgroundColor(0xFF0B0F17);
    box.setClickable(true);              // لمس به صفحهٔ زیرش نرسد
    int pad = (int) (24 * getResources().getDisplayMetrics().density);
    box.setPadding(pad, pad, pad, pad);

    TextView title = new TextView(this);
    title.setText("پمپ یعقوبی");
    title.setTextColor(0xFFFFB300);
    title.setTextSize(22);
    title.setGravity(Gravity.CENTER);
    box.addView(title);

    TextView sub = new TextView(this);
    sub.setText("نرم‌افزار مدیریت پمپ بنزین");
    sub.setTextColor(0xFF9FB0C7);
    sub.setTextSize(12);
    sub.setGravity(Gravity.CENTER);
    sub.setPadding(0, (int) (6 * getResources().getDisplayMetrics().density), 0, 0);
    box.addView(sub);

    splashBar = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
    splashBar.setMax(100);
    splashBar.setProgress(0);
    LinearLayout.LayoutParams bp = new LinearLayout.LayoutParams(
        (int) (220 * getResources().getDisplayMetrics().density),
        ViewGroup.LayoutParams.WRAP_CONTENT);
    bp.topMargin = (int) (18 * getResources().getDisplayMetrics().density);
    splashBar.setLayoutParams(bp);
    box.addView(splashBar);

    splashStage = new TextView(this);
    splashStage.setText("آماده‌سازی…");
    splashStage.setTextColor(0xFF7F90A8);
    splashStage.setTextSize(11);
    splashStage.setGravity(Gravity.CENTER);
    splashStage.setPadding(0, (int) (10 * getResources().getDisplayMetrics().density), 0, 0);
    box.addView(splashStage);

    splash = box;
    return box;
  }

  /** درصد هرگز عقب نمی‌رود — هر عدد یک کارِ واقعیِ انجام‌شده است */
  private void bootProgress(final int pct, final String stage) {
    runOnUiThread(() -> {
      if (splashGone || splashBar == null) return;
      if (pct > splashBar.getProgress()) splashBar.setProgress(pct);
      if (stage != null && splashStage != null) splashStage.setText(stage);
    });
  }

  private void hideSplash() {
    runOnUiThread(() -> {
      if (splashGone || splash == null) return;
      splashGone = true;
      if (splashBar != null) splashBar.setProgress(100);
      splash.animate().alpha(0f).setDuration(160).withEndAction(() -> {
        try {
          ViewGroup parent = (ViewGroup) splash.getParent();
          if (parent != null) parent.removeView(splash);
        } catch (Throwable ignored) { }
        splash = null; splashBar = null; splashStage = null;
      }).start();
    });
  }

  /**
   * سرعت — چیزهایی که فقط «برنامهٔ نصب‌شده» می‌تواند داشته باشد.
   *
   * خواستهٔ صاحب ریپو: «در مرورگر هر محدودیتی هست باشد، ولی در اپ یک ذره هم
   * محدودیت نبینم.» مرورگرِ گوشی برای ده‌ها تبِ ناشناس محتاط است؛ این‌جا فقط
   * یک برنامه هست و همهٔ فایل‌هایش روی خودِ گوشی:
   *   • setOffscreenPreRaster: چیزی که همین حالا زیرِ لبهٔ صفحه است، از پیش
   *     کشیده می‌شود — اسکرولِ جدول‌های بلند دیگر «تکه‌تکه» بالا نمی‌آید.
   *   • RenderPriority.HIGH: رندرِ صفحه اولویت می‌گیرد، نه کارهای پس‌زمینه.
   *   • OVER_SCROLL_NEVER: کشِ سبزِ ته‌ی اسکرول (و کارِ رسمِ آن) حذف می‌شود؛
   *     خودِ برنامه هم چنین حسی ندارد.
   *   • پس‌زمینهٔ تیره: بینِ باز شدن و آمدنِ صفحه، آن فلاشِ سفید دیده نشود.
   * (largeHeap و hardwareAccelerated در AndroidManifest روشن‌اند.)
   */
  private void tuneForSpeed(WebSettings s) {
    try { s.setRenderPriority(WebSettings.RenderPriority.HIGH); } catch (Throwable ignored) { }
    try { if (Build.VERSION.SDK_INT >= 23) s.setOffscreenPreRaster(true); } catch (Throwable ignored) { }
    try { s.setLoadsImagesAutomatically(true); } catch (Throwable ignored) { }
    try { s.setBlockNetworkImage(false); } catch (Throwable ignored) { }
    try { web.setOverScrollMode(View.OVER_SCROLL_NEVER); } catch (Throwable ignored) { }
    try { web.setBackgroundColor(0xFF0B0F17); } catch (Throwable ignored) { }
  }

  /**
   * دانلودِ فایل از داخلِ خودِ برنامه.
   *
   * تا حالا WebView هیچ DownloadListener نداشت. نتیجه‌اش این بود که زدنِ دکمهٔ
   * دانلود (مثلاً فایلِ نصبِ تازه در صفحهٔ «دریافت برنامه») هیچ کاری نمی‌کرد:
   * نوارِ پیشرفتِ خودِ صفحه تا آخر پر می‌شد و بعد هیچ فایلی ذخیره نمی‌شد —
   * دقیقاً همان «تا آخرین مگابایت می‌رود و همان‌جا می‌ماند».
   *
   * حالا دانلود به DownloadManagerِ خودِ اندروید سپرده می‌شود: فایل در پوشهٔ
   * Downloads می‌نشیند، نوارِ پیشرفت در سایهٔ اعلان‌ها دیده می‌شود، و با تمام
   * شدنش می‌شود همان‌جا بازش کرد و نصبش کرد.
   */
  private void setupDownloads() {
    web.setDownloadListener((url, userAgent, contentDisposition, mimeType, contentLength) -> {
      // blob:/data: را DownloadManager نمی‌فهمد؛ خودِ صفحه دیگر از آن‌ها استفاده
      // نمی‌کند، ولی اگر نسخهٔ کهنه‌ای از صفحه بالا آمده بود، به‌جای سکوت به
      // مرورگر می‌سپاریم تا کاربر بی‌جواب نماند.
      if (url == null || url.startsWith("blob:") || url.startsWith("data:")) {
        openOutside(url);
        return;
      }
      try {
        String name = URLUtil.guessFileName(url, contentDisposition, mimeType);
        DownloadManager.Request req = new DownloadManager.Request(Uri.parse(url));
        req.setMimeType(resolveMime(mimeType, name));
        req.addRequestHeader("User-Agent", userAgent);
        try {
          String cookie = CookieManager.getInstance().getCookie(url);
          if (cookie != null) req.addRequestHeader("Cookie", cookie);
        } catch (Exception ignored) {}
        req.setTitle(name);
        req.setDescription("پمپ یعقوبی");
        req.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED);
        req.setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS, name);
        DownloadManager dm = (DownloadManager) getSystemService(Context.DOWNLOAD_SERVICE);
        if (dm == null) { openOutside(url); return; }
        dm.enqueue(req);
        toast("دانلود شروع شد — در پوشهٔ Downloads ذخیره می‌شود");
      } catch (Exception e) {
        openOutside(url);        // هر مشکلی پیش آمد، دستِ کم مرورگر بگیردش
      }
    });
  }

  /**
   * نوعِ فایل. برای فایلِ نصب باید دقیقاً نوعِ apk باشد، وگرنه اندروید فایلِ
   * دانلودشده را «ناشناخته» می‌بیند و با زدنش نصب‌کننده باز نمی‌شود.
   */
  private String resolveMime(String mimeType, String name) {
    String n = (name == null) ? "" : name.toLowerCase();
    if (n.endsWith(".apk")) return "application/vnd.android.package-archive";
    if (mimeType != null && !mimeType.isEmpty() && !"application/octet-stream".equals(mimeType)) return mimeType;
    try {
      String ext = MimeTypeMap.getFileExtensionFromUrl(n);
      String guess = MimeTypeMap.getSingleton().getMimeTypeFromExtension(ext);
      if (guess != null && !guess.isEmpty()) return guess;
    } catch (Exception ignored) {}
    return "application/octet-stream";
  }

  /** بازکردنِ یک نشانی با مرورگرِ خودِ گوشی (وقتی خودمان نمی‌توانیم بگیریمش) */
  private void openOutside(String url) {
    try {
      if (url == null) return;
      startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(url)));
    } catch (Exception e) {
      toast("دانلود نشد — این صفحه را در مرورگر باز کنید");
    }
  }

  private void toast(String msg) {
    try {
      runOnUiThread(() -> android.widget.Toast.makeText(this, msg, android.widget.Toast.LENGTH_LONG).show());
    } catch (Exception ignored) {}
  }

  /**
   * اجازه‌های لازم. «دریافت پیام حتی وقتی برنامه بسته است» همیشه فعال است و
   * داخلِ برنامه هیچ کلیدِ خاموش/روشنی ندارد؛ مدیریتش فقط از تنظیماتِ خودِ
   * اندروید (App Info › Notifications) انجام می‌شود — پس همان اجازه را یک بار
   * از سیستم می‌گیریم.
   */
  private void requestNeededPermissions() {
    if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M) return;
    List<String> want = new ArrayList<>();
    if (checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
      want.add(Manifest.permission.RECORD_AUDIO);
    }
    if (Build.VERSION.SDK_INT >= 33
        && checkSelfPermission("android.permission.POST_NOTIFICATIONS") != PackageManager.PERMISSION_GRANTED) {
      want.add("android.permission.POST_NOTIFICATIONS");
    }
    if (!want.isEmpty()) requestPermissions(want.toArray(new String[0]), MIC_REQ);
  }

  // ── مسیرهای آپدیت ─────────────────────────────────────────────────────────
  private File updateDir()  { return new File(getFilesDir(), "update"); }
  private File updateFile() { return new File(updateDir(), "index.html"); }
  private File stagingFile() { return new File(updateDir(), "index.part"); }
  private SharedPreferences prefs() { return getSharedPreferences(PREFS, Context.MODE_PRIVATE); }

  /** نسخهٔ برنامه‌ای که همراهِ خودِ فایلِ نصب آمده (همان APP_VERSIONِ index.html) */
  private String bundledVersion() {
    try {
      String v = getPackageManager().getPackageInfo(getPackageName(), 0).versionName;
      return v == null ? "" : v;
    } catch (Throwable t) {
      return "";
    }
  }

  /** ۱ اگر a تازه‌تر از b باشد — همان مقایسه‌ای که خودِ برنامه می‌کند */
  private static int verCmp(String a, String b) {
    String[] pa = (a == null ? "" : a).split("\\.");
    String[] pb = (b == null ? "" : b).split("\\.");
    int n = Math.max(pa.length, pb.length);
    for (int i = 0; i < n; i++) {
      int x = 0, y = 0;
      try { if (i < pa.length) x = Integer.parseInt(pa[i].trim()); } catch (Throwable ignored) { }
      try { if (i < pb.length) y = Integer.parseInt(pb[i].trim()); } catch (Throwable ignored) { }
      if (x != y) return x > y ? 1 : -1;
    }
    return 0;
  }

  /**
   * اگر آپدیتِ سالمی هست همان بار شود، وگرنه نسخهٔ همراهِ نصب.
   * فایلِ خیلی کوچک = ناقص، پس دور انداخته می‌شود.
   *
   * ⚠️ نسخهٔ ذخیره‌شده باید از نسخهٔ همراهِ نصب «تازه‌تر» باشد، وگرنه دور
   * انداخته می‌شود. بدونِ این شرط، وقتی کاربر فایلِ نصبِ تازه را نصب می‌کرد،
   * همان آپدیتِ کهنه‌ای که قبلاً در حافظهٔ خصوصی نشسته بود باز هم برنده می‌شد و
   * برنامه نسخهٔ قدیمی را نشان می‌داد — یعنی «هرچه آپدیت می‌دهم به اپ نمی‌رسد».
   * چون android:allowBackup روشن است، آن فایل حتی از پاک‌کردن و نصبِ دوباره هم
   * جان سالم به در می‌برد، پس نصبِ دوباره هم درستش نمی‌کرد.
   * اگر نسخهٔ فایلِ ذخیره‌شده معلوم نباشد (تنظیماتِ پاک‌شده یا ساختِ قدیمی) هم
   * دور انداخته می‌شود: نسخهٔ همراهِ نصب همیشه سالم است و اگر سایت تازه‌تر باشد،
   * خودِ برنامه دوباره می‌گیردش.
   */
  private String pickStartUrl() {
    File f = updateFile();
    try {
      if (f.exists() && f.length() > 200000) {
        String stored = prefs().getString(KEY_VERSION, "");
        if (stored.isEmpty() || verCmp(stored, bundledVersion()) <= 0) {
          dropUpdate();
          return ASSET_URL;
        }
        int tries = prefs().getInt(KEY_TRIES, 0);
        if (tries >= MAX_TRIES) {
          dropUpdate();                      // دو بار بالا نیامد → برگشت به نسخهٔ سالم
        } else {
          prefs().edit().putInt(KEY_TRIES, tries + 1).apply();
          usingUpdate = true;
          return "file://" + f.getAbsolutePath();
        }
      } else if (f.exists()) {
        dropUpdate();
      }
    } catch (Throwable t) {
      dropUpdate();
    }
    return ASSET_URL;
  }

  private void dropUpdate() {
    try {
      File f = updateFile();
      if (f.exists()) f.delete();
      File p = stagingFile();
      if (p.exists()) p.delete();
    } catch (Throwable ignored) { }
    try { prefs().edit().putInt(KEY_TRIES, 0).remove(KEY_VERSION).apply(); } catch (Throwable ignored) { }
    usingUpdate = false;
  }

  /**
   * پلِ بینِ خودِ برنامه (جاوااسکریپت) و پوستهٔ اندروید.
   * نسخهٔ تازه تکه‌تکه فرستاده می‌شود تا هیچ‌وقت به سقفِ حجمِ یک فراخوانی نخورد.
   */
  private class Bridge {
    private Writer out;

    @JavascriptInterface
    public String platform() { return "android"; }

    /** مرحله‌های واقعیِ بالا آمدن — خودِ برنامه در همان لحظه صدایشان می‌زند */
    @JavascriptInterface
    public void boot(String step) {
      if (step == null) return;
      switch (step) {
        case "script": bootProgress(88, "اجرای برنامه…"); break;
        case "data":   bootProgress(92, "خواندنِ دفترها…"); break;
        case "render": bootProgress(97, "چیدنِ بخش‌ها…"); break;
        case "ready":  hideSplash(); break;
        default: break;
      }
    }

    /** {usingUpdate, version, tries} — برنامه می‌داند الان کدام نسخه بالا آمده */
    @JavascriptInterface
    public String info() {
      String v = prefs().getString(KEY_VERSION, "");
      return "{\"usingUpdate\":" + (usingUpdate ? "true" : "false")
          + ",\"version\":\"" + v.replace("\"", "") + "\"}";
    }

    /** برنامه سالم بالا آمد — شمارندهٔ تلاش صفر می‌شود */
    @JavascriptInterface
    public void updateOk() {
      try { prefs().edit().putInt(KEY_TRIES, 0).apply(); } catch (Throwable ignored) { }
    }

    /** شروعِ نوشتنِ نسخهٔ تازه */
    @JavascriptInterface
    public synchronized boolean updateBegin() {
      try {
        closeQuietly();
        File dir = updateDir();
        if (!dir.exists() && !dir.mkdirs()) return false;
        File part = stagingFile();
        if (part.exists() && !part.delete()) return false;
        out = new OutputStreamWriter(new FileOutputStream(part), StandardCharsets.UTF_8);
        return true;
      } catch (Throwable t) {
        closeQuietly();
        return false;
      }
    }

    /** یک تکه از نسخهٔ تازه */
    @JavascriptInterface
    public synchronized boolean updateChunk(String part) {
      try {
        if (out == null || part == null) return false;
        out.write(part);
        return true;
      } catch (Throwable t) {
        abortInternal();
        return false;
      }
    }

    /** پایانِ نوشتن — فایل فقط وقتی جای نسخهٔ فعلی می‌نشیند که کامل و سالم باشد */
    @JavascriptInterface
    public synchronized boolean updateCommit(String version) {
      try {
        if (out == null) return false;
        out.flush();
        closeQuietly();
        File part = stagingFile(), dest = updateFile();
        if (!part.exists() || part.length() < 200000) { part.delete(); return false; }
        if (dest.exists() && !dest.delete()) { part.delete(); return false; }
        if (!part.renameTo(dest)) { part.delete(); return false; }
        prefs().edit().putInt(KEY_TRIES, 0)
            .putString(KEY_VERSION, version == null ? "" : version).apply();
        return true;
      } catch (Throwable t) {
        abortInternal();
        return false;
      }
    }

    /** لغوِ نوشتن (مثلاً اینترنت وسطِ کار قطع شد) */
    @JavascriptInterface
    public synchronized void updateAbort() { abortInternal(); }

    /** برگشتِ دستی به نسخه‌ای که همراهِ نصب آمده */
    @JavascriptInterface
    public void updateRollback() {
      dropUpdate();
      restart();
    }

    /** بالا آوردنِ دوبارهٔ برنامه تا نسخهٔ تازه سوار شود */
    @JavascriptInterface
    public void restart() {
      runOnUiThread(() -> {
        try {
          Intent i = new Intent(MainActivity.this, MainActivity.class);
          i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
          startActivity(i);
          finish();
        } catch (Throwable ignored) { }
      });
    }

    private void abortInternal() {
      closeQuietly();
      try { stagingFile().delete(); } catch (Throwable ignored) { }
    }

    private void closeQuietly() {
      try { if (out != null) out.close(); } catch (Throwable ignored) { }
      out = null;
    }
  }

  /** دکمهٔ برگشتِ گوشی، داخلِ خودِ برنامه عقب برود، نه اینکه برنامه بسته شود. */
  @Override
  public void onBackPressed() {
    // اگر صفحه‌ای تمام‌صفحه (فاکتور/چاپ/گزارش) باز باشد، خودِ برنامه آن را
    // می‌بندد؛ فقط وقتی چیزی برای بستن نبود، از تاریخچهٔ WebView عقب می‌رویم.
    if (web != null) {
      web.evaluateJavascript(
          "(function(){try{return window.pumpHandleBack&&window.pumpHandleBack()?'1':'0'}catch(e){return '0'}})()",
          value -> {
            if (value != null && value.contains("1")) return;
            if (web.canGoBack()) web.goBack();
            else finish();
          });
      return;
    }
    super.onBackPressed();
  }

  @Override
  protected void onDestroy() {
    try { if (web != null) web.destroy(); } catch (Throwable ignored) { }
    super.onDestroy();
  }
}
