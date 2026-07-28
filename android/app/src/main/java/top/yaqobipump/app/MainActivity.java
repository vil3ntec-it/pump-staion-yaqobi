package top.yaqobipump.app;

import android.Manifest;
import android.annotation.SuppressLint;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;
import android.webkit.PermissionRequest;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;

/**
 * پوستهٔ اندرویدِ برنامهٔ پمپ.
 *
 * خودِ برنامه (index.html و دارایی‌هایش) داخلِ همین فایلِ نصب است و از پوشهٔ
 * assets بار می‌شود — پس بدونِ اینترنت هم کامل کار می‌کند و هیچ نوارِ آدرس یا
 * سربرگِ مرورگری بالای صفحه دیده نمی‌شود.
 */
public class MainActivity extends AppCompatActivity {

  private WebView web;
  private static final int MIC_REQ = 4711;

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

    // اجازهٔ مایکروفون برای ضبطِ پیام صوتی و جستجوی صوتی
    web.setWebChromeClient(new WebChromeClient() {
      @Override
      public void onPermissionRequest(final PermissionRequest request) {
        runOnUiThread(() -> request.grant(request.getResources()));
      }
    });

    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M
        && checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
      ActivityCompat.requestPermissions(this, new String[]{ Manifest.permission.RECORD_AUDIO }, MIC_REQ);
    }

    // برنامه از داخلِ خودِ فایلِ نصب بار می‌شود — نه از اینترنت
    web.loadUrl("file:///android_asset/www/index.html");
  }

  @Override
  public void onRequestPermissionsResult(int req, @NonNull String[] p, @NonNull int[] r) {
    super.onRequestPermissionsResult(req, p, r);
  }

  /** دکمهٔ برگشتِ گوشی، داخلِ خودِ برنامه عقب برود، نه اینکه برنامه بسته شود. */
  @Override
  public void onBackPressed() {
    if (web != null && web.canGoBack()) web.goBack();
    else super.onBackPressed();
  }
}
