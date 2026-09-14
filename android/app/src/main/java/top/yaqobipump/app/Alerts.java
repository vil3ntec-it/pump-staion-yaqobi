package top.yaqobipump.app;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.job.JobInfo;
import android.app.job.JobScheduler;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Build;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * ══ خبر دادن، حتی وقتی برنامه بسته است ══════════════════════════════════════
 *
 * خواستهٔ صریحِ صاحب ریپو: «برای کارمندان وقتی که یک قرض‌دار اضافه برد یا کم
 * مانده بود از حسابش، برنامه یک پیام بدهد — حتی اگر گوشی خاموش یا حتی اگر توی
 * برنامه نبود هم پیام برود تا بفهمد.»
 *
 * ══ چه‌طور، بی هیچ سرویسِ بیرونی ═══════════════════════════════════════════
 *
 * ⚠️ هیچ سرویسِ پیام‌رسانِ بیرونی‌ای در کار نیست و نباید بیاید — قاعدهٔ همیشگیِ
 * این پروژه. پس گوشی خودش هر چند دقیقه یک‌بار از سرورِ خانگیِ خودِ پمپ
 * می‌پرسد و اگر خبرِ تازه‌ای بود، خودش روی همان گوشی اعلان می‌سازد:
 *
 *     JobScheduler (هر ۱۵ دقیقه، حتی بعد از خاموش/روشن شدنِ گوشی)
 *         ⇒ GET /api/stations/&lt;کد&gt;/live?token=&lt;رمزِ فقط‌خواندنی&gt;
 *             ⇒ alerts[]  ⇒ اعلانِ گوشی
 *
 * ⚠️ «حتی اگر گوشی خاموش باشد» شدنی نیست — گوشیِ خاموش هیچ برنامه‌ای را اجرا
 * نمی‌کند. ولی ‎setPersisted(true)‎ یعنی همین که روشن شد، بی این‌که کسی برنامه
 * را باز کند، دوباره می‌پرسد و خبرهای عقب‌مانده را همان لحظه می‌دهد.
 *
 * ⚠️ فهرستِ خبرها این‌جا ساخته نمی‌شود: ‎StationSnapshot.Alerts‎ در خودِ برنامهٔ
 * کامپیوتر می‌سازدش و داخلِ همان عکسِ زنده می‌گذارد. اگر این‌جا قاعده‌ای جدا
 * نوشته می‌شد، روزی کارتِ قرض‌دار روی کامپیوتر سرخ می‌بود و گوشی ساکت.
 *
 * ⚠️ رمزی که به کار می‌رود <b>فقط‌خواندنی</b> است (همانی که در کیو‌آرِ کارمند
 * می‌نشیند) — گوشی هیچ‌وقت چیزی نمی‌نویسد.
 *
 * ⚠️ و چرا ‎JobScheduler‎ی خودِ اندروید و نه کتابخانه: این پروژه عمداً هیچ
 * وابستگیِ گریدلی ندارد (androidx یک‌بار ساخت را شکست). ‎JobScheduler‎ و
 * ‎HttpURLConnection‎ و ‎org.json‎ هر سه خودِ اندروید‌اند.
 */
public final class Alerts {

  private Alerts() { }

  public static final String PREFS = "pumpAlerts";
  private static final String K_SERVER = "server";
  private static final String K_TOKEN = "token";
  private static final String K_STATION = "station";
  private static final String K_SEEN = "seen";

  public static final String CHANNEL = "pump-alerts";
  private static final int JOB_ID = 7021;

  /** هر ۱۵ دقیقه — کمترین دوره‌ای که خودِ اندروید برای کارِ دوره‌ای می‌پذیرد. */
  private static final long EVERY_MS = 15L * 60L * 1000L;

  /**
   * چند کلیدِ خبرداده‌شده نگه داشته شود.
   *
   * ⚠️ بی سقف، این فهرست با هر ماه بزرگ‌تر می‌شد و روزی خواندنش خودش کند
   * می‌شد. با سقف، کهنه‌ترین‌ها می‌افتند — و بدترین اتفاقی که می‌افتد این است
   * که خبرِ خیلی قدیمی یک‌بار دیگر داده شود، نه این‌که خبری گم شود.
   */
  private static final int SEEN_CAP = 400;

  // ══════════════════════════════════════════════════════════════════════
  //  تنظیمات
  // ══════════════════════════════════════════════════════════════════════

  public static SharedPreferences prefs(Context c) {
    return c.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
  }

  /**
   * نشانی و رمز و کدِ پمپ را نگه می‌دارد و کارِ دوره‌ای را می‌چیند.
   *
   * ⚠️ این را خودِ صفحهٔ ‎kar/‎ صدا می‌زند، چون تنظیمات آن‌جا زندگی می‌کند
   * (‎localStorage‎) و جاوا نمی‌تواند بخواندش. پس هر بار که صفحه بالا می‌آید،
   * جاوا تازه‌ترین تنظیمات را می‌گیرد.
   */
  public static void setup(Context c, String server, String token, String station) {
    String s = server == null ? "" : server.trim();
    String t = token == null ? "" : token.trim();
    String st = station == null || station.trim().isEmpty() ? "pump1" : station.trim();

    prefs(c).edit().putString(K_SERVER, s).putString(K_TOKEN, t).putString(K_STATION, st).apply();
    if (s.isEmpty()) cancel(c); else schedule(c);
  }

  public static void schedule(Context c) {
    JobScheduler js = (JobScheduler) c.getSystemService(Context.JOB_SCHEDULER_SERVICE);
    if (js == null) return;

    JobInfo.Builder b = new JobInfo.Builder(JOB_ID, new ComponentName(c, AlertJob.class))
        .setRequiredNetworkType(JobInfo.NETWORK_TYPE_ANY)
        // ⚠️ بی این، با هر بار خاموش/روشن شدنِ گوشی کار پاک می‌شد و تا وقتی
        // کسی برنامه را باز نمی‌کرد هیچ خبری نمی‌آمد — یعنی دقیقاً همان چیزی
        // که نباید بشود.
        .setPersisted(true)
        .setPeriodic(EVERY_MS);
    try { js.schedule(b.build()); } catch (Exception ignored) { }
  }

  public static void cancel(Context c) {
    JobScheduler js = (JobScheduler) c.getSystemService(Context.JOB_SCHEDULER_SERVICE);
    if (js != null) js.cancel(JOB_ID);
  }

  // ══════════════════════════════════════════════════════════════════════
  //  یک دور: بپرس، تازه‌ها را جدا کن، خبر بده
  // ══════════════════════════════════════════════════════════════════════

  /** @return شمارِ خبرهای تازه‌ای که اعلان شدند. */
  public static int runOnce(Context c) {
    SharedPreferences p = prefs(c);
    String server = p.getString(K_SERVER, "");
    if (server.isEmpty()) return 0;

    JSONArray alerts = fetch(server, p.getString(K_TOKEN, ""), p.getString(K_STATION, "pump1"));
    if (alerts == null) return 0;

    Set<String> seen = new HashSet<>(p.getStringSet(K_SEEN, new HashSet<String>()));
    List<String[]> fresh = new ArrayList<>();
    List<String> keysNow = new ArrayList<>();

    for (int i = 0; i < alerts.length(); i++) {
      JSONObject a = alerts.optJSONObject(i);
      if (a == null) continue;
      String key = a.optString("k", "");
      String text = a.optString("t", "");
      if (key.isEmpty() || text.isEmpty()) continue;
      keysNow.add(key);
      if (seen.add(key)) fresh.add(new String[] { key, text, a.optString("s", "low") });
    }

    // ⚠️ کلیدهایی که دیگر در فهرست نیستند فراموش می‌شوند: حسابی که تسویه شد و
    // بعد دوباره خراب شد باید دوباره خبر بدهد، نه این‌که برای همیشه ساکت بماند.
    seen.retainAll(new HashSet<>(keysNow));
    while (seen.size() > SEEN_CAP) seen.remove(seen.iterator().next());
    p.edit().putStringSet(K_SEEN, seen).apply();

    for (String[] f : fresh) notify(c, f[0], f[1], "out".equals(f[2]));
    return fresh.size();
  }

  /**
   * ‎GET /api/stations/&lt;کد&gt;/live?token=…‎ ⇒ ‎live.alerts‎
   *
   * ⚠️ فقط همین درِ HTTP: سرورِ به‌روزنشده فقط وب‌سوکت دارد و کارِ پس‌زمینه
   * نمی‌تواند وب‌سوکت بزند بی این‌که کتابخانه‌ای اضافه شود. خودِ اپ با هر دو
   * در کار می‌کند؛ فقط خبرِ پس‌زمینه سرورِ به‌روز می‌خواهد.
   */
  static JSONArray fetch(String server, String token, String station) {
    HttpURLConnection conn = null;
    try {
      String base = httpBase(server);
      String u = base + "/api/stations/" + Uri.encode(station) + "/live"
               + (token.isEmpty() ? "" : "?token=" + Uri.encode(token));

      conn = (HttpURLConnection) new URL(u).openConnection();
      conn.setRequestMethod("GET");
      conn.setConnectTimeout(12000);
      conn.setReadTimeout(12000);
      conn.setRequestProperty("Accept", "application/json");
      if (conn.getResponseCode() != 200) return null;

      JSONObject body = new JSONObject(readAll(conn.getInputStream()));
      JSONObject live = body.optJSONObject("live");
      if (live == null) return null;
      return live.optJSONArray("alerts");
    } catch (Exception e) {
      return null;   // بی‌شبکه، سرورِ خواب، رمزِ غلط — دورِ بعد دوباره
    } finally {
      if (conn != null) conn.disconnect();
    }
  }

  /** نشانی هر شکلی نوشته شده باشد ⇒ ‎http/https‎ی درست، بی ‎/‎ی ته. */
  static String httpBase(String server) {
    String b = server.trim().replaceAll("/+$", "");
    if (b.startsWith("wss://")) b = "https://" + b.substring(6);
    else if (b.startsWith("ws://")) b = "http://" + b.substring(5);
    else if (!b.startsWith("http://") && !b.startsWith("https://")) b = "https://" + b;
    return b;
  }

  private static String readAll(InputStream in) throws Exception {
    ByteArrayOutputStream out = new ByteArrayOutputStream();
    byte[] buf = new byte[8192];
    int n;
    while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
    return new String(out.toByteArray(), StandardCharsets.UTF_8);
  }

  // ══════════════════════════════════════════════════════════════════════
  //  اعلان
  // ══════════════════════════════════════════════════════════════════════

  public static void ensureChannel(Context c) {
    if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return;
    NotificationManager nm = (NotificationManager) c.getSystemService(Context.NOTIFICATION_SERVICE);
    if (nm == null || nm.getNotificationChannel(CHANNEL) != null) return;

    NotificationChannel ch = new NotificationChannel(
        CHANNEL, "هشدارِ قرض‌داران و مخزن", NotificationManager.IMPORTANCE_HIGH);
    ch.setDescription("وقتی کسی اضافه برد یا حسابش کم مانده، یا مخزن ته کشید.");
    ch.enableVibration(true);
    nm.createNotificationChannel(ch);
  }

  static void notify(Context c, String key, String text, boolean urgent) {
    ensureChannel(c);
    NotificationManager nm = (NotificationManager) c.getSystemService(Context.NOTIFICATION_SERVICE);
    if (nm == null) return;

    PendingIntent open = PendingIntent.getActivity(
        c, 0, new Intent(c, MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP),
        PendingIntent.FLAG_UPDATE_CURRENT
            | (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M ? PendingIntent.FLAG_IMMUTABLE : 0));

    Notification.Builder b = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
        ? new Notification.Builder(c, CHANNEL)
        : new Notification.Builder(c);

    b.setSmallIcon(android.R.drawable.stat_sys_warning)
     .setContentTitle(urgent ? "⛔ اضافه نده" : "⚠️ کم مانده")
     .setContentText(text)
     .setStyle(new Notification.BigTextStyle().bigText(text))
     .setAutoCancel(true)
     .setContentIntent(open);

    // ⚠️ شناسه از خودِ کلید می‌آید، نه از یک شمارنده: خبرِ یک نفر خبرِ نفرِ
    // دیگر را از روی صفحه پاک نمی‌کند، و خبرِ تکراری هم روی هم تلنبار نمی‌شود.
    nm.notify(Math.abs(key.hashCode()), b.build());
  }
}
