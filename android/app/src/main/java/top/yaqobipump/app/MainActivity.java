package top.yaqobipump.app;

import android.Manifest;
import android.annotation.SuppressLint;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;
import android.webkit.JavascriptInterface;
import android.webkit.PermissionRequest;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.app.Activity;

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

  @SuppressLint("SetJavaScriptEnabled")
  @Override
  protected void onCreate(Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);

    web = new WebView(this);
    setContentView(web);

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

    web.setWebViewClient(new WebViewClient());
    web.addJavascriptInterface(new Bridge(), "PumpAndroid");

    // اجازهٔ مایکروفون برای ضبطِ پیام صوتی و جستجوی صوتی
    web.setWebChromeClient(new WebChromeClient() {
      @Override
      public void onPermissionRequest(final PermissionRequest request) {
        runOnUiThread(() -> request.grant(request.getResources()));
      }
    });

    requestNeededPermissions();

    web.loadUrl(pickStartUrl());
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

  /**
   * اگر آپدیتِ سالمی هست همان بار شود، وگرنه نسخهٔ همراهِ نصب.
   * فایلِ خیلی کوچک = ناقص، پس دور انداخته می‌شود.
   */
  private String pickStartUrl() {
    File f = updateFile();
    try {
      if (f.exists() && f.length() > 200000) {
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
