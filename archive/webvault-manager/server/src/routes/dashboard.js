// ============================================================================
//  داشبورد اصلی — آمار، دامنه‌های نزدیک انقضا، بکاپ‌ها، هشدارهای امنیتی
// ============================================================================
import { db } from '../db.js';
import { recent } from '../activity.js';

export function register(router) {
  router.get('/api/dashboard', () => {
    const count = (sql, ...a) => db.prepare(sql).get(...a).n;

    const websites = {
      total: count('SELECT COUNT(*) n FROM websites'),
      active: count("SELECT COUNT(*) n FROM websites WHERE status = 'active'"),
      sold: count("SELECT COUNT(*) n FROM websites WHERE status = 'sold'"),
      developing: count("SELECT COUNT(*) n FROM websites WHERE status = 'developing'"),
      archived: count("SELECT COUNT(*) n FROM websites WHERE status = 'archived'"),
    };

    const counts = {
      domains: count('SELECT COUNT(*) n FROM domains'),
      servers: count('SELECT COUNT(*) n FROM servers'),
      clients: count('SELECT COUNT(*) n FROM clients'),
      credentials: count('SELECT COUNT(*) n FROM credentials'),
    };

    // دامنه‌های نزدیک انقضا (تا ۳۰ روز) یا منقضی‌شده
    const expiring = db
      .prepare(
        `SELECT id, domain_name, expiry_date,
                CAST(julianday(expiry_date) - julianday('now') AS INTEGER) AS days
         FROM domains
         WHERE expiry_date IS NOT NULL AND expiry_date != ''
           AND julianday(expiry_date) - julianday('now') <= 30
         ORDER BY expiry_date`
      )
      .all();

    // آخرین بکاپ‌ها
    const recentBackups = db
      .prepare(
        `SELECT b.id, b.backup_date, b.type, b.status, b.location, w.name AS website_name
         FROM backups b LEFT JOIN websites w ON w.id = b.website_id
         ORDER BY b.backup_date DESC, b.id DESC LIMIT 8`
      )
      .all();

    // درآمد فروش
    const revenue = db
      .prepare("SELECT COALESCE(SUM(sale_price),0) s FROM websites WHERE status = 'sold'")
      .get().s;

    // هشدارهای امنیتی
    const alerts = [];
    for (const d of expiring) {
      if (d.days < 0) alerts.push({ level: 'danger', text: `دامنهٔ ${d.domain_name} منقضی شده است` });
      else if (d.days <= 14) alerts.push({ level: 'warning', text: `دامنهٔ ${d.domain_name} تا ${d.days} روز دیگر منقضی می‌شود` });
    }
    // سایت‌های فعال بدون هیچ بکاپ
    const noBackup = db
      .prepare(
        `SELECT w.name FROM websites w
         WHERE w.status = 'active'
           AND NOT EXISTS (SELECT 1 FROM backups b WHERE b.website_id = w.id)`
      )
      .all();
    for (const w of noBackup) alerts.push({ level: 'warning', text: `سایت «${w.name}» هیچ بکاپی ندارد` });

    // مشتری‌های با پرداخت ناتمام
    const unpaid = db
      .prepare("SELECT COUNT(*) n FROM clients WHERE payment_status != 'paid'")
      .get().n;
    if (unpaid > 0) alerts.push({ level: 'info', text: `${unpaid} مشتری پرداخت ناتمام دارند` });

    return {
      websites, counts, revenue,
      expiringDomains: expiring,
      recentBackups,
      alerts,
      recentActivity: recent(10),
    };
  });
}
