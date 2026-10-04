// ============================================================================
//  جستجوی سریع در سایت‌ها، دامنه‌ها، مشتری‌ها، سرورها، رمزها + فعالیت‌ها
// ============================================================================
import { db } from '../db.js';
import { recent } from '../activity.js';

export function register(router) {
  router.get('/api/search', ({ query }) => {
    const q = String(query.q || '').trim();
    if (!q) return { websites: [], domains: [], clients: [], servers: [], credentials: [] };
    const like = `%${q}%`;
    return {
      websites: db
        .prepare(
          `SELECT id, name, url, status FROM websites
           WHERE name LIKE ? OR url LIKE ? OR server_ip LIKE ? OR cms LIKE ? LIMIT 25`
        )
        .all(like, like, like, like),
      domains: db
        .prepare('SELECT id, domain_name, registrar, expiry_date FROM domains WHERE domain_name LIKE ? OR registrar LIKE ? LIMIT 25')
        .all(like, like),
      clients: db
        .prepare('SELECT id, name, email, phone FROM clients WHERE name LIKE ? OR email LIKE ? OR phone LIKE ? LIMIT 25')
        .all(like, like, like),
      servers: db
        .prepare('SELECT id, name, provider, ip FROM servers WHERE name LIKE ? OR ip LIKE ? OR provider LIKE ? LIMIT 25')
        .all(like, like, like),
      // فقط عنوان و نام‌کاربری قابل جستجوست — مقدار محرمانه هرگز.
      credentials: db
        .prepare('SELECT id, title, type, username, website_id FROM credentials WHERE title LIKE ? OR username LIKE ? LIMIT 25')
        .all(like, like),
    };
  });

  router.get('/api/activity', ({ query }) => recent(query.limit || 100));
}
