package top.yaqobipump.app;

import android.app.job.JobParameters;
import android.app.job.JobService;

/**
 * کارِ دوره‌ایِ خبر دادن — بدنه‌اش در {@link Alerts#runOnce}.
 *
 * ⚠️ ‎onStartJob‎ روی نخِ اصلی صدا زده می‌شود و شبکه آن‌جا ممنوع است، پس کار
 * به یک نخِ جدا می‌رود و ‎jobFinished‎ سرِ تمام شدنش زده می‌شود. بی این،
 * اندروید برنامه را با ‎NetworkOnMainThreadException‎ می‌بندد.
 */
public class AlertJob extends JobService {

  @Override
  public boolean onStartJob(final JobParameters params) {
    new Thread(new Runnable() {
      @Override public void run() {
        try { Alerts.runOnce(getApplicationContext()); }
        catch (Throwable ignored) { }
        // ‎false‎ یعنی «دوباره امتحان نکن» — دورِ بعدی خودش پانزده دقیقهٔ دیگر
        // می‌آید و تلاشِ فوری فقط باتری می‌سوزاند.
        jobFinished(params, false);
      }
    }, "pump-alerts").start();
    return true;   // کار هنوز تمام نشده
  }

  @Override
  public boolean onStopJob(JobParameters params) {
    return true;   // اندروید کار را قطع کرد ⇒ دوباره بچینش
  }
}
