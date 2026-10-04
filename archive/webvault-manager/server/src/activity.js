// ============================================================================
//  WebVault Manager — ثبت فعالیت‌ها (Audit Log)
// ============================================================================
import { db } from './db.js';

export function log(action, entityType = null, entityId = null, detail = null) {
  try {
    db.prepare(
      'INSERT INTO activity_log (action, entity_type, entity_id, detail) VALUES (?, ?, ?, ?)'
    ).run(action, entityType, entityId, detail);
  } catch {
    /* ثبت لاگ نباید مسیر اصلی را بشکند */
  }
}

export function recent(limit = 100) {
  return db
    .prepare('SELECT * FROM activity_log ORDER BY id DESC LIMIT ?')
    .all(Math.min(Number(limit) || 100, 500));
}
